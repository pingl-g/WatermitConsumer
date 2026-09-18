using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Watermit.Contracts;
using Watermit.Persistence;
using Watermit.Pipeline.Abstractions;

namespace Watermit.Persistence.Tables;

/// <summary>
/// The client-side <b>push</b> seam: an <see cref="IPipelineDispatcher"/> that hands a case off to the
/// worker by writing a Pending row to the intake table (rather than running anything in-process). This
/// is what lets the MVC / REST front-ends live in their own App Services — <c>Submit</c> returns
/// immediately and the worker App Service picks the row up on its next poll. The document names come
/// from the case record (authoritative), so the row is self-describing.
/// </summary>
public sealed class TableEnqueueDispatcher : IPipelineDispatcher
{
    private readonly ICaseRepository _cases;
    private readonly IClaimWorkEnqueuer _enqueuer;
    private readonly ILogger<TableEnqueueDispatcher> _log;

    public TableEnqueueDispatcher(
        ICaseRepository cases, IClaimWorkEnqueuer enqueuer, ILogger<TableEnqueueDispatcher> log)
    {
        _cases = cases;
        _enqueuer = enqueuer;
        _log = log;
    }

    public async Task<string> EnqueueAsync(CaseRef caseRef, CancellationToken ct = default)
    {
        var aggregate = await _cases.GetCaseAsync(caseRef.TenantId, caseRef.CaseId, ct);
        // Blob names under {tenant}/{caseId}/input match how BlobStore wrote them: "{docId}-{fileName}".
        var documents = aggregate is null
            ? new List<string>()
            : aggregate.Documents.OrderBy(d => d.Ordinal).Select(d => $"{d.DocId}-{d.FileName}").ToList();

        await _enqueuer.EnqueueAsync(
            new ClaimWorkRow(caseRef.TenantId, caseRef.CaseId, ClaimStatus.Pending, documents,
                aggregate?.Header.Number),
            ct);

        _log.LogInformation(
            "Enqueued {Tenant}/{Case} ({Docs} docs) to the intake table for the worker.",
            caseRef.TenantId, caseRef.CaseId, documents.Count);
        return caseRef.CaseId; // tracking id the caller can poll status by
    }
}
