using System.Collections.Generic;

namespace Watermit.Contracts;

/// <summary>
/// One unit of extraction work: a claim plus the file names uploaded for it. The frontend records this
/// in the intake table (an appsettings list stands in for now); the worker polls for pending rows and
/// runs the pipeline for each. The files themselves live in blob at {tenant}/{caseId}/input/{name}.
/// </summary>
/// <param name="Tenant">Org id, e.g. "genpact".</param>
/// <param name="CaseId">The claim id (also the blob folder under the container).</param>
/// <param name="Documents">The uploaded file names, each present at {tenant}/{caseId}/input/{name}.</param>
/// <param name="ClaimNumber">Optional carrier claim number (from the FNOL), for correlation.</param>
public sealed record ClaimExtractionRequest(
    string Tenant,
    string CaseId,
    IReadOnlyList<string> Documents,
    string? ClaimNumber = null);
