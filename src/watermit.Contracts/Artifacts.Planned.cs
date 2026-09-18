using System;
using System.Collections.Generic;

namespace Watermit.Contracts;

// ────────────────────────────────────────────────────────────────────────────
// PLANNED artifact contracts — the shared "language" the parallel modules code
// against. Shapes are skeletal but frozen enough for independent development +
// fixture-based testing. Each is the output of one stage / segment.
// ────────────────────────────────────────────────────────────────────────────

// ── Segment 1 (Extraction) output: ClaimFacts ───────────────────────────────
public sealed record Chamber(
    string Name, double? FloorSf = null, double? WallsSf = null, double? Height = null,
    double? CubicFeet = null, double? FloorAffectedPct = null);

public sealed record LineItem(
    int Number, string? Code, string Description,
    double? Qty = null, string? Unit = null, decimal? Amount = null);

/// <summary>Equipment billed as unit-days (e.g. air movers, dehumidifiers, air scrubbers).</summary>
public sealed record EquipmentItem(
    string Kind, string? Model = null, double? UnitDays = null, double? UnitRate = null, string? Chamber = null);

public sealed record DryLogEntry(
    DateOnly Date, string? Chamber = null, double? RhPct = null, double? Gpp = null, double? MoisturePct = null);

public sealed record ClaimFacts(
    IReadOnlyList<Chamber> Chambers,
    IReadOnlyList<LineItem> LineItems,
    IReadOnlyList<EquipmentItem> Equipment,
    IReadOnlyList<DryLogEntry> DryLog);

// ── Phase C output: LossClassification (or gate → NeedsHumanReview) ──────────
public sealed record LossClassification(
    int? Category = null, double? CategoryConfidence = null,
    int? WaterClass = null, double? WaterClassConfidence = null,
    SourceRef? Source = null);

// ── Phase D output: ReconciliationResult ────────────────────────────────────
public sealed record ReconciliationResult(
    IReadOnlyList<Flag> MathChecks,
    IReadOnlyList<Flag> Duplicates,
    IReadOnlyList<Flag> CrossDocMismatches);

// ── Segment 2 (Audit) output: FindingSet ─────────────────────────────────────
/// <summary>
/// One WMAG audit finding. Mirrors the legacy gwmag_analysis finding: <see cref="Issue"/> is the
/// vendor-facing text (never names a norm/standard); <see cref="Evidence"/> is the internal
/// Source→Data→Standard→Comparison analysis; <see cref="RuleRefs"/> are the norm/rule citations
/// (e.g. "Industry Norm #17", "Rule L.19"); <see cref="Severity"/> is Critical/Major/Minor;
/// <see cref="ErrorType"/> is one of the 19 vendor flags. <see cref="DollarImpact"/> (recommended
/// reduction) is our structured enhancement over the legacy's free-text dollars.
/// </summary>
public sealed record Finding(
    string RuleId,
    string ErrorType,
    string Severity,
    string Issue,
    string Evidence,
    decimal DollarImpact = 0m,
    IReadOnlyList<string>? RuleRefs = null,
    bool ReviewRecommended = false,
    string Source = "ai",
    /// <summary>"reduction" (a dollar cut to the bill) or "observation" (informational — underbilling,
    /// equipment oversizing, price-list note — carries no reduction). Keeps the headline reductions clean.</summary>
    string Kind = "reduction");

/// <summary>
/// One norm the auditor actually weighed — the transparency ledger entry. <see cref="Verdict"/> is
/// fired | cleared | exception | insufficient (trigger met+violation / trigger not met / trigger met but
/// an exception applies / not enough evidence to decide). <see cref="Rationale"/> is the model's one-line
/// reason; <see cref="Group"/> is the flag-group that evaluated it. Name/ErrorType are joined from the
/// catalog for display. Present only on audits run with per-norm transparency enabled.
/// </summary>
public sealed record NormEvaluation(
    string NormId,
    string Verdict,
    string Rationale,
    string? Name = null,
    string? ErrorType = null,
    string? Group = null);

/// <summary>
/// Stage E output: the audit findings plus the AI summary, the derived vendor-error checkbox grid, and
/// (when transparency is enabled) the per-norm evaluation ledger showing every norm the auditor judged.
/// </summary>
public sealed record FindingSet(
    IReadOnlyList<Finding> Findings,
    string? SummaryText = null,
    IReadOnlyDictionary<string, bool>? VendorCheckboxFlags = null,
    IReadOnlyList<NormEvaluation>? Evaluations = null);

// NOTE: Segment 3's output is the ComparativeEstimate + EstimateComparison (see Comparison.cs);
// the FindingSet above supplies the per-line ScopeDifferenceReason. The MAR (see Mar.cs) tracks
// the negotiation rounds. There is no separate "audit report" artifact — the comparison IS the report.

// ── Settlement closeout: SettlementRecord — the CMS "ESTIMATE details" fields (Step 9) ───────
public sealed record Deduction(string RuleId, string Reason, decimal Amount);

/// <summary>
/// Captured once settlement concludes. Mirrors the exact fields the WMS records in CMS:
/// whether an ACOR (Agreed Cost Of Repair) was obtained, the original vendor invoice amount,
/// the TPA comparative amount, and the ACOR / Non-ACOR split.
/// </summary>
public sealed record SettlementRecord(
    string CaseId,
    bool WasAcorObtained,
    decimal OriginalVendorInvoiceAmount,
    decimal ComparativeEstimateAmount,
    decimal? AcorAmount,           // agreed portion (when an ACOR was obtained)
    decimal? NonAcorAmount,        // disputed remainder (when applicable)
    IReadOnlyList<Deduction> Deductions,
    DateTimeOffset SettledUtc);
