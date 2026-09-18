using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using Watermit.Contracts;
using Watermit.Pipeline.Abstractions;

namespace Watermit.Persistence.Tables;

/// <summary>
/// Azure Table intake queue behind <see cref="IClaimWorkSource"/>. One row per claim
/// (PartitionKey = {Tenant}, RowKey = {CaseId}). A client App Service writes Pending rows (see
/// <see cref="EnqueueAsync"/>, the seed/producer side); the worker App Service polls and claims them.
/// A "claim" is an ETag If-Match update (optimistic concurrency), so two worker instances can poll the
/// same table and only one wins a given row — no message lock to lose on a big/slow claim. A crashed
/// worker's lease expires (LeasedUntil &lt; now) and the row becomes claimable again; because each
/// pipeline stage persists its artifact, a reclaimed run resumes from the last completed stage.
/// </summary>
public sealed class TableClaimWorkSource : IClaimWorkSource, IClaimWorkEnqueuer, IClaimStatusReader, IClaimWorkList
{
    private readonly TableClient _table;
    private readonly object _gate = new();
    private Task? _ensure;

    public TableClaimWorkSource(TableServiceClient service, IOptions<StorageOptions> options)
    {
        _table = service.GetTableClient(options.Value.ClaimWorkTable);
    }

    public string Name => $"AzureTable({_table.Name})";

    // Create the table once per instance (cached task; CreateIfNotExists is idempotent).
    private Task EnsureTableAsync()
    {
        lock (_gate) return _ensure ??= _table.CreateIfNotExistsAsync();
    }

    public async Task<IReadOnlyList<ClaimWork>> GetClaimableAsync(int max, CancellationToken ct = default)
    {
        await EnsureTableAsync();
        var now = DateTimeOffset.UtcNow;
        var results = new List<ClaimWork>();

        // Fetch the non-terminal rows; decide claimability client-side (Pending, or an expired lease).
        const string filter =
            "Status eq 'Pending' or Status eq 'Claimed' or Status eq 'Processing'";
        await foreach (var e in _table.QueryAsync<ClaimWorkEntity>(filter, cancellationToken: ct))
        {
            var claimable = e.Status == nameof(ClaimStatus.Pending)
                            || (e.LeasedUntil is { } until && until < now);
            if (!claimable) continue;
            results.Add(ToWork(e));
            if (results.Count >= max) break;
        }
        return results;
    }

    public async Task<ClaimWork?> TryClaimAsync(ClaimWork work, string owner, TimeSpan lease, CancellationToken ct = default)
    {
        await EnsureTableAsync();
        var now = DateTimeOffset.UtcNow;
        var e = FromWork(work);
        e.Status = nameof(ClaimStatus.Claimed);
        e.LeaseOwner = owner;
        e.LeasedUntil = now.Add(lease);
        e.Attempts += 1;
        e.UpdatedUtc = now;
        try
        {
            var resp = await _table.UpdateEntityAsync(e, new ETag(work.ConcurrencyToken), TableUpdateMode.Replace, ct);
            return ToWork(e, resp.Headers.ETag);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            return null; // another worker claimed it first
        }
    }

    public async Task<ClaimWork?> RenewAsync(ClaimWork work, TimeSpan lease, CancellationToken ct = default)
    {
        await EnsureTableAsync();
        var now = DateTimeOffset.UtcNow;
        var e = FromWork(work);
        e.Status = nameof(ClaimStatus.Processing);
        e.LeasedUntil = now.Add(lease);
        e.UpdatedUtc = now;
        try
        {
            var resp = await _table.UpdateEntityAsync(e, new ETag(work.ConcurrencyToken), TableUpdateMode.Replace, ct);
            return ToWork(e, resp.Headers.ETag);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            return null; // lease lost — the row was reclaimed elsewhere
        }
    }

    public async Task CompleteAsync(ClaimWork work, ClaimStatus terminal, string? error = null, CancellationToken ct = default)
    {
        await EnsureTableAsync();
        var e = FromWork(work);
        e.Status = terminal.ToString();
        e.LeaseOwner = null;
        e.LeasedUntil = null;
        e.LastError = error;
        e.UpdatedUtc = DateTimeOffset.UtcNow;
        try
        {
            await _table.UpdateEntityAsync(e, new ETag(work.ConcurrencyToken), TableUpdateMode.Replace, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // Lease lost mid-run; another worker now owns the row and will publish its own terminal state.
        }
    }

    /// <summary>List rows for a dashboard — newest first (by UpdatedUtc). See <see cref="IClaimWorkList"/>.</summary>
    public async Task<IReadOnlyList<ClaimWorkRow>> ListAsync(int max = 200, CancellationToken ct = default)
    {
        await EnsureTableAsync();
        var rows = new List<ClaimWorkRow>();
        await foreach (var e in _table.QueryAsync<ClaimWorkEntity>(cancellationToken: ct))
        {
            rows.Add(ToWork(e).Row);
            if (rows.Count >= 2000) break; // safety cap before the client-side sort
        }
        return rows
            .OrderByDescending(r => r.UpdatedUtc ?? r.CreatedUtc ?? DateTimeOffset.MinValue)
            .Take(max)
            .ToList();
    }

    /// <summary>Client status-poll seam: point-read one claim's row (null if none). See <see cref="IClaimStatusReader"/>.</summary>
    public async Task<ClaimWorkRow?> GetAsync(string tenant, string caseId, CancellationToken ct = default)
    {
        await EnsureTableAsync();
        try
        {
            var resp = await _table.GetEntityAsync<ClaimWorkEntity>(tenant, caseId, cancellationToken: ct);
            return ToWork(resp.Value).Row;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Producer side (seed / test today; the client-side dispatcher formalises this later): write or reset
    /// a Pending row for a claim whose files are already in blob. Upsert so a re-seed is harmless.
    /// </summary>
    public async Task EnqueueAsync(ClaimWorkRow row, CancellationToken ct = default)
    {
        await EnsureTableAsync();
        var now = DateTimeOffset.UtcNow;
        var e = new ClaimWorkEntity
        {
            PartitionKey = row.Tenant,
            RowKey = row.CaseId,
            Status = nameof(ClaimStatus.Pending),
            DocumentsJson = JsonSerializer.Serialize(row.Documents),
            ClaimNumber = row.ClaimNumber,
            Attempts = 0,
            CreatedUtc = row.CreatedUtc ?? now,
            UpdatedUtc = now,
        };
        await _table.UpsertEntityAsync(e, TableUpdateMode.Replace, ct);
    }

    private static ClaimWork ToWork(ClaimWorkEntity e, ETag? newTag = null)
    {
        var row = new ClaimWorkRow(
            e.PartitionKey,
            e.RowKey,
            Enum.TryParse<ClaimStatus>(e.Status, out var s) ? s : ClaimStatus.Pending,
            Deserialize(e.DocumentsJson),
            e.ClaimNumber,
            e.LeaseOwner,
            e.LeasedUntil,
            e.Attempts,
            e.LastError,
            e.CreatedUtc,
            e.UpdatedUtc);
        return new ClaimWork(row, (newTag ?? e.ETag).ToString());
    }

    private static ClaimWorkEntity FromWork(ClaimWork work)
    {
        var r = work.Row;
        return new ClaimWorkEntity
        {
            PartitionKey = r.Tenant,
            RowKey = r.CaseId,
            Status = r.Status.ToString(),
            DocumentsJson = JsonSerializer.Serialize(r.Documents),
            ClaimNumber = r.ClaimNumber,
            LeaseOwner = r.LeaseOwner,
            LeasedUntil = r.LeasedUntil,
            Attempts = r.Attempts,
            LastError = r.LastError,
            CreatedUtc = r.CreatedUtc,
            UpdatedUtc = r.UpdatedUtc,
            ETag = new ETag(work.ConcurrencyToken),
        };
    }

    private static IReadOnlyList<string> Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

    /// <summary>Table row shape. Arrays aren't a Table type, so Documents is stored as a JSON string.</summary>
    internal sealed class ClaimWorkEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty; // Tenant
        public string RowKey { get; set; } = string.Empty;       // CaseId
        public string Status { get; set; } = nameof(ClaimStatus.Pending);
        public string? DocumentsJson { get; set; }
        public string? ClaimNumber { get; set; }
        public string? LeaseOwner { get; set; }
        public DateTimeOffset? LeasedUntil { get; set; }
        public int Attempts { get; set; }
        public string? LastError { get; set; }
        public DateTimeOffset? CreatedUtc { get; set; }
        public DateTimeOffset? UpdatedUtc { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
