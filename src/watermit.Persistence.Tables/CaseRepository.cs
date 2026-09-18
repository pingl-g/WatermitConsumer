using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using Watermit.Contracts;
using Watermit.Domain;
using Watermit.Persistence;

namespace Watermit.Persistence.Tables;

/// <summary>Azure Table Storage implementation of <see cref="ICaseRepository"/>.</summary>
public sealed class CaseRepository : ICaseRepository
{
    private readonly TableClient _table;

    public CaseRepository(TableServiceClient serviceClient, IOptions<StorageOptions> options)
    {
        _table = serviceClient.GetTableClient(options.Value.CasesTable);
    }

    public Task InitAsync(CancellationToken ct = default) =>
        _table.CreateIfNotExistsAsync(ct);

    public async Task CreateCaseAsync(CaseHeader header, CancellationToken ct = default)
    {
        var entity = CaseHeaderEntity.FromModel(header);
        await _table.AddEntityAsync(entity, ct);
    }

    public async Task AddDocumentAsync(string tenantId, string caseId, CaseDocument doc, CancellationToken ct = default)
    {
        var entity = CaseDocumentEntity.FromModel(tenantId, caseId, doc);
        await _table.AddEntityAsync(entity, ct);
    }

    public async Task<CaseAggregate?> GetCaseAsync(string tenantId, string caseId, CancellationToken ct = default)
    {
        var partition = CaseKeys.Partition(tenantId, caseId);

        var headerResp = await _table.GetEntityIfExistsAsync<CaseHeaderEntity>(
            partition, CaseKeys.HeaderRowKey, cancellationToken: ct);
        if (!headerResp.HasValue || headerResp.Value is null)
            return null;

        var documents = new List<CaseDocument>();
        // Whole-partition query; the header row deserializes into a doc entity with a
        // non-"DOC~" RowKey, so we skip it by RowKey prefix.
        await foreach (var e in _table.QueryAsync<CaseDocumentEntity>(
                           x => x.PartitionKey == partition, cancellationToken: ct))
        {
            if (!CaseKeys.IsDocumentRowKey(e.RowKey)) continue;
            documents.Add(e.ToModel());
        }

        documents.Sort((a, b) => a.Ordinal.CompareTo(b.Ordinal));

        var settlementResp = await _table.GetEntityIfExistsAsync<CaseSettlementEntity>(
            partition, CaseKeys.SettlementRowKey, cancellationToken: ct);
        var settlement = settlementResp.HasValue && settlementResp.Value is not null
            ? settlementResp.Value.ToModel()
            : null;

        return new CaseAggregate
        {
            Header = headerResp.Value.ToModel(),
            Documents = documents,
            Settlement = settlement
        };
    }

    public Task SetStatusAsync(string tenantId, string caseId, CaseStatus status, CancellationToken ct = default)
    {
        // Merge-update only the Status property on the CASE row.
        var patch = new TableEntity(CaseKeys.Partition(tenantId, caseId), CaseKeys.HeaderRowKey)
        {
            ["Status"] = status.ToString()
        };
        return _table.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge, ct);
    }

    public Task MarkSubmittedAsync(
        string tenantId, string caseId,
        System.DateTimeOffset receivedUtc, System.DateTimeOffset dueUtc, CancellationToken ct = default)
    {
        var patch = new TableEntity(CaseKeys.Partition(tenantId, caseId), CaseKeys.HeaderRowKey)
        {
            ["Status"] = CaseStatus.Submitted.ToString(),
            ["ReceivedUtc"] = receivedUtc,
            ["DueUtc"] = dueUtc
        };
        return _table.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge, ct);
    }

    public Task SetReviewDecisionAsync(
        string tenantId, string caseId, CaseStatus decision,
        string? reviewedBy, string? notes, System.DateTimeOffset whenUtc, CancellationToken ct = default)
    {
        // Merge-update Status + the decision trail on the CASE row in one call.
        var patch = new TableEntity(CaseKeys.Partition(tenantId, caseId), CaseKeys.HeaderRowKey)
        {
            ["Status"] = decision.ToString(),
            ["ReviewedBy"] = reviewedBy,
            ["ReviewedUtc"] = whenUtc,
            ["ReviewNotes"] = notes
        };
        // Sending the comparative to the vendor is the CMS 'Inspection Completed' milestone.
        if (decision == CaseStatus.SentToVendor)
            patch["ComparativeSentUtc"] = whenUtc;

        return _table.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge, ct);
    }

    public async Task SaveSettlementAsync(
        string tenantId, string caseId, CaseSettlement settlement, CancellationToken ct = default)
    {
        // Upsert the SETTLEMENT row, then move the header to Settled + stamp SettledUtc.
        var entity = CaseSettlementEntity.FromModel(tenantId, caseId, settlement);
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);

        var patch = new TableEntity(CaseKeys.Partition(tenantId, caseId), CaseKeys.HeaderRowKey)
        {
            ["Status"] = CaseStatus.Settled.ToString(),
            ["SettledUtc"] = settlement.SettledUtc
        };
        await _table.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge, ct);
    }
}
