using System;
using System.Collections.Generic;

namespace Watermit.Contracts;

// ────────────────────────────────────────────────────────────────────────────
// MAR — Mitigation Analysis Report ("MAR WATER MIT WS"). Two jobs in the manual
// process, both replicated here:
//   1. the equipment Calculation Tool output (type & size of equipment by room), and
//   2. the running log of Comps & Revisions across negotiation rounds with the vendor.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>One equipment recommendation for a room, from the Calculation Tool (Calc engine).</summary>
public sealed record EquipmentPlanItem(
    string Chamber,
    string Kind,               // AirMover, Dehumidifier, AirScrubber, …
    int Recommended,           // count the guidelines call for
    string? SizeOrClass,       // e.g. dehu class / capacity band
    string Basis);             // the norm/calc that produced it

/// <summary>One negotiation round: what TPA asked, what the vendor came back with.</summary>
public sealed record NegotiationRound(
    int RoundNo,
    DateTimeOffset SentUtc,
    decimal ComparativeAmount,
    decimal? VendorResponseAmount = null,
    DateTimeOffset? VendorResponseUtc = null,
    string? Notes = null);

/// <summary>The MAR worksheet: equipment plan + the comps/revisions tracker.</summary>
public sealed record Mar(
    string CaseId,
    IReadOnlyList<EquipmentPlanItem> EquipmentPlan,
    IReadOnlyList<NegotiationRound> Rounds,
    decimal CurrentComparativeAmount);
