using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Watermit.Contracts;
using Watermit.Pipeline.Abstractions;

namespace Watermit.Persistence.Tables;

/// <summary>
/// Local-dev stand-in for <see cref="IClaimWorkSource"/>: reads pending claims from a static
/// <c>Extraction:WorkSource:Claims</c> list in configuration instead of a table. There is no real
/// concurrency here — <see cref="TryClaimAsync"/> always wins and processed case ids are remembered
/// in-memory so a long-running poll doesn't reprocess them. Swap <see cref="TableClaimWorkSource"/> in
/// (via <c>Extraction:WorkSource:Provider=Table</c>) with no change to the worker loop.
/// </summary>
public sealed class AppSettingsClaimWorkSource : IClaimWorkSource
{
    private readonly IConfiguration _cfg;
    private readonly HashSet<string> _done = new(StringComparer.OrdinalIgnoreCase);

    public AppSettingsClaimWorkSource(IConfiguration cfg) => _cfg = cfg;

    public string Name => "AppSettings(Extraction:WorkSource:Claims)";

    public Task<IReadOnlyList<ClaimWork>> GetClaimableAsync(int max, CancellationToken ct = default)
    {
        var rows = _cfg.GetSection("Extraction:WorkSource:Claims").Get<List<ClaimWorkConfigRow>>() ?? new();
        var list = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.CaseId) && !_done.Contains(r.CaseId!))
            .Take(max)
            .Select(r => new ClaimWork(
                new ClaimWorkRow(
                    string.IsNullOrWhiteSpace(r.Tenant) ? "genpact" : r.Tenant!,
                    r.CaseId!,
                    ClaimStatus.Pending,
                    r.Documents ?? new List<string>(),
                    r.ClaimNumber),
                ConcurrencyToken: "appsettings")) // opaque; no optimistic concurrency in the stand-in
            .ToList();
        return Task.FromResult<IReadOnlyList<ClaimWork>>(list);
    }

    public Task<ClaimWork?> TryClaimAsync(ClaimWork work, string owner, TimeSpan lease, CancellationToken ct = default) =>
        Task.FromResult<ClaimWork?>(work with
        {
            Row = work.Row with { Status = ClaimStatus.Claimed, LeaseOwner = owner, LeasedUntil = DateTimeOffset.UtcNow.Add(lease) }
        });

    public Task<ClaimWork?> RenewAsync(ClaimWork work, TimeSpan lease, CancellationToken ct = default) =>
        Task.FromResult<ClaimWork?>(work with
        {
            Row = work.Row with { Status = ClaimStatus.Processing, LeasedUntil = DateTimeOffset.UtcNow.Add(lease) }
        });

    public Task CompleteAsync(ClaimWork work, ClaimStatus terminal, string? error = null, CancellationToken ct = default)
    {
        _done.Add(work.Row.CaseId);
        return Task.CompletedTask;
    }

    // Mutable shape for configuration binding (positional records bind poorly from config sections).
    private sealed class ClaimWorkConfigRow
    {
        public string? Tenant { get; set; }
        public string? CaseId { get; set; }
        public List<string>? Documents { get; set; }
        public string? ClaimNumber { get; set; }
    }
}
