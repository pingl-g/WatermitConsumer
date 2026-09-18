using System;
using System.Collections.Generic;

namespace Watermit.Contracts;

/// <summary>
/// Lifecycle of one claim in the intake queue — the status column front-ends poll to follow a claim
/// across the process boundary (the worker runs in its own App Service; each client in its own).
/// Terminal states are <see cref="Completed"/> / <see cref="NeedsReview"/> / <see cref="Failed"/>.
/// </summary>
public enum ClaimStatus
{
    /// <summary>A front-end recorded the row; awaiting a worker.</summary>
    Pending,
    /// <summary>A worker leased it (LeaseOwner/LeasedUntil set); pipeline not started yet.</summary>
    Claimed,
    /// <summary>Pipeline running; the lease is renewed by a heartbeat while it runs.</summary>
    Processing,
    /// <summary>Pipeline finished; WMS review required (the audit always routes here).</summary>
    NeedsReview,
    /// <summary>Finished with no review outstanding.</summary>
    Completed,
    /// <summary>Gave up after retries — see <see cref="ClaimWorkRow.LastError"/> / Attempts.</summary>
    Failed,
}

/// <summary>
/// One row of the intake table — the durable contract between a client App Service (which writes a
/// Pending row + uploads the files) and the worker App Service (which polls, claims via ETag, and runs
/// the pipeline). PartitionKey = <see cref="Tenant"/>, RowKey = <see cref="CaseId"/>. The files
/// themselves live in blob at <c>{tenant}/{caseId}/input/{name}</c>; this row only names them. This is a
/// pure contract — the Azure Table mapping (and its ETag) lives in the persistence layer, so every
/// host (worker, MVC, REST API) can reference this shape without an Azure dependency.
/// </summary>
/// <param name="Tenant">Org id, e.g. "genpact" (PartitionKey).</param>
/// <param name="CaseId">Claim id, also the blob folder under the container (RowKey).</param>
/// <param name="Status">Where this claim is in its lifecycle.</param>
/// <param name="Documents">Uploaded file names, each present at {tenant}/{caseId}/input/{name}.</param>
/// <param name="ClaimNumber">Optional carrier claim number (from the FNOL), for correlation.</param>
/// <param name="LeaseOwner">Worker instance currently holding the row (Claimed/Processing), else null.</param>
/// <param name="LeasedUntil">When the current lease expires; past-due => the row is claimable again.</param>
/// <param name="Attempts">How many times a worker has claimed this row (for poison detection).</param>
/// <param name="LastError">Message from the last failed attempt, when Status = Failed.</param>
/// <param name="CreatedUtc">When the row was first enqueued.</param>
/// <param name="UpdatedUtc">When the row last changed status/lease.</param>
public sealed record ClaimWorkRow(
    string Tenant,
    string CaseId,
    ClaimStatus Status,
    IReadOnlyList<string> Documents,
    string? ClaimNumber = null,
    string? LeaseOwner = null,
    DateTimeOffset? LeasedUntil = null,
    int Attempts = 0,
    string? LastError = null,
    DateTimeOffset? CreatedUtc = null,
    DateTimeOffset? UpdatedUtc = null);
