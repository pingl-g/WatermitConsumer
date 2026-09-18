namespace Watermit.Contracts;

/// <summary>
/// Lifecycle status of a Case (one water-mitigation invoice review), following the
/// TPA↔carrier process: intake → generate comparative → WMS review → negotiate with vendor → settle.
/// </summary>
public enum CaseStatus
{
    Draft,          // created, the 4 required documents being attached
    NeedsAttention, // intake problem (e.g. one of the 4 required documents missing)
    Submitted,      // "Review Estimate" pressed; queued for the pipeline (72h SLA clock starts)
    Processing,     // a background job is generating the comparative estimate
    NeedsReview,    // comparative + comparison ready; the WMS reviews it internally
    SentToVendor,   // comparative emailed to the vendor with scope differences; awaiting revision
    InNegotiation,  // one or more comp/revision rounds with the vendor are in progress
    Settled,        // settlement concluded; ACOR details captured
    Failed          // pipeline errored
}

/// <summary>How a submission entered the platform. Web is the only channel wired in the first increment.</summary>
public enum IngestChannel
{
    Web,
    Email,
    Api,
    Sftp
}

/// <summary>Where a document's category value came from. Recorded per document for the classifier feedback loop.</summary>
public enum CategorySource
{
    /// <summary>Assigned by the AUTO classifier, not yet confirmed by a human.</summary>
    Auto,
    /// <summary>A human accepted the AUTO suggestion unchanged.</summary>
    UserConfirmed,
    /// <summary>A human changed the category away from the AUTO suggestion.</summary>
    UserOverride
}
