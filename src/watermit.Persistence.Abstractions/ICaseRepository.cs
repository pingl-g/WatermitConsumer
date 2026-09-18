using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Watermit.Domain;

namespace Watermit.Persistence;

/// <summary>
/// Persistence-neutral access to Cases. Implemented today by the Azure Tables provider
/// (watermit.Persistence.Tables); an EF Core / Azure SQL provider can implement the same
/// interface with no change to callers. Methods take/return domain models only — no
/// storage-specific concepts (PartitionKey, ETag, etc.) leak through.
/// </summary>
public interface ICaseRepository
{
    /// <summary>Create backing structures (tables/schema) if needed. Call once at startup.</summary>
    Task InitAsync(CancellationToken ct = default);

    Task CreateCaseAsync(CaseHeader header, CancellationToken ct = default);

    Task AddDocumentAsync(string tenantId, string caseId, CaseDocument doc, CancellationToken ct = default);

    /// <summary>Load the whole Case (header + documents). Null if not found.</summary>
    Task<CaseAggregate?> GetCaseAsync(string tenantId, string caseId, CancellationToken ct = default);

    /// <summary>Update just the Case status (drives the background-processing lifecycle).</summary>
    Task SetStatusAsync(string tenantId, string caseId, Watermit.Contracts.CaseStatus status, CancellationToken ct = default);

    /// <summary>
    /// Mark the case Submitted and start the 72h SLA clock: sets Status=Submitted plus
    /// ReceivedUtc / DueUtc in one update.
    /// </summary>
    Task MarkSubmittedAsync(
        string tenantId, string caseId,
        System.DateTimeOffset receivedUtc, System.DateTimeOffset dueUtc, CancellationToken ct = default);

    /// <summary>
    /// Record the WMS decision on the comparison review: sets Status (e.g. SentToVendor) plus the
    /// decision trail (who / when / notes). When the decision is SentToVendor, also stamps
    /// ComparativeSentUtc (the CMS 'Inspection Completed' milestone).
    /// </summary>
    Task SetReviewDecisionAsync(
        string tenantId, string caseId, Watermit.Contracts.CaseStatus decision,
        string? reviewedBy, string? notes, System.DateTimeOffset whenUtc, CancellationToken ct = default);

    /// <summary>
    /// Persist the settlement closeout (the CMS ESTIMATE details) and move the case to Settled
    /// (stamping SettledUtc from the record) in one operation.
    /// </summary>
    Task SaveSettlementAsync(string tenantId, string caseId, CaseSettlement settlement, CancellationToken ct = default);
}

/// <summary>
/// Persistence-neutral file storage. Files always live in blob storage regardless of whether
/// metadata is kept in Tables or SQL, so this interface is provider-independent.
/// </summary>
public interface IBlobStore
{
    Task InitAsync(CancellationToken ct = default);

    Task<string> UploadOriginalAsync(
        string tenantId, string caseId, string docId, string fileName,
        Stream content, string contentType, CancellationToken ct = default);
}
