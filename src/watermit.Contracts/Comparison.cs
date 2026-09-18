using System;
using System.Collections.Generic;

namespace Watermit.Contracts;

// ────────────────────────────────────────────────────────────────────────────
// The apple-to-apple comparison — the heart of the review.
//
//   Vendor side : the vendor's submitted line items / equipment (extracted into
//                 ClaimFacts by Segment 1) — "what they billed".
//   TPA  side   : ComparativeEstimate — INDEPENDENTLY GENERATED from the physical
//                 evidence (chambers, dry-log, category/class) by the Calculation +
//                 Norm engines applying TPA guidelines — "what it should cost".
//
// Both use the same LineItem / EquipmentItem shapes (see Artifacts.Planned.cs) so the
// two estimates line up directly. EstimateComparison is the diff shown to the WMS and
// emailed to the vendor.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Segment 3 output. The TPA's own estimate, generated from evidence + guidelines (never
/// from the vendor's numbers). Same line/equipment shape as the vendor's for direct comparison.
/// </summary>
public sealed record ComparativeEstimate(
    IReadOnlyList<LineItem> Lines,
    IReadOnlyList<EquipmentItem> Equipment,
    decimal Total,
    string BasisNotes);        // which norms/calcs produced these figures

/// <summary>One vendor line paired with its comparative counterpart and the reason they differ.</summary>
public sealed record LineComparison(
    string Description,
    string? Chamber,
    decimal? VendorAmount,
    decimal? ComparativeAmount,
    decimal Delta,             // VendorAmount - ComparativeAmount (the reduction being asked for)
    string ScopeDifferenceReason);

/// <summary>
/// The deliverable / negotiation basis: vendor estimate vs TPA comparative, line by line,
/// with a narrative summary that becomes the body of the email to the vendor.
/// </summary>
public sealed record EstimateComparison(
    string CaseId,
    decimal VendorTotal,
    decimal ComparativeTotal,
    decimal TotalReduction,    // VendorTotal - ComparativeTotal
    IReadOnlyList<LineComparison> Lines,
    string NarrativeSummary,
    DateTimeOffset GeneratedUtc);
