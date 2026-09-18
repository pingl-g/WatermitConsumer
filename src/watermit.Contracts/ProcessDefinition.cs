using System.Collections.Generic;

namespace Watermit.Contracts;

/// <summary>
/// The ordered stage sequence for one ProcessType — the declarative pipeline the runner /
/// background job iterates. Each entry is a <see cref="IPipelineStage.StageId"/>; the job calls
/// the runner for each id in order, stopping when a stage raises the human-review gate.
/// This is the contract that answers "for water-mit, which stages run and in what order?" —
/// it does not require every stage to be implemented yet (unimplemented ids simply throw when hit).
/// </summary>
public sealed record ProcessDefinition(
    string ProcessTypeId,
    string Version,
    IReadOnlyList<string> Stages);

/// <summary>
/// The v1 WaterMitigation pipeline: classify the package (A1) → extract the estimate header
/// (A23) → extract claim facts / vendor lines (B) → classify the loss (C) → run the WMAG audit
/// that derives the expected figures (E) → assemble the TPA comparative estimate + the
/// apple-to-apple comparison (F). The WMS then reviews (NeedsReview), sends to the vendor, and
/// negotiates. Settlement is a post-negotiation human closeout, not an automated stage.
/// </summary>
public static class WaterMitigationProcess
{
    public static readonly ProcessDefinition Definition = new(
        ProcessTypeId: WaterMitigationTaxonomy.ProcessTypeId,
        Version: WaterMitigationTaxonomy.Version,
        // BMD (per-document Claude markdown) feeds A23 (header) and B (ClaimFacts); DEHU (deterministic
        // chambers/equipment sizing) feeds the WMAG audit (E) as its top-authority input.
        Stages: new[] { "A1", "BMD", "A23", "B", "C", "DEHU", "E", "F" });

    /// <summary>
    /// The four attachments AllMutual sends with every new claim; all must be present before
    /// "Review Estimate" runs (else the case goes to NeedsAttention). Taxonomy codes:
    /// FNOL, Vendor's Original Invoice (VendorEstimate), Dry Logs (DryLogs), Photo Report (VendorImages).
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredCategories = new[]
    {
        WaterMitigationTaxonomy.Fnol,
        WaterMitigationTaxonomy.VendorEstimate,
        WaterMitigationTaxonomy.DryLogs,
        WaterMitigationTaxonomy.VendorImages
    };
}
