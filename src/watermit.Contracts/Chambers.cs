using System.Collections.Generic;

namespace Watermit.Contracts;

// ────────────────────────────────────────────────────────────────────────────
// Enriched chamber / equipment extraction — parity with the legacy get_chambers
// sub-task outputs (BUILDING_FACTORS + ROOMS_PARSING + EQUIPMENT_PARSING). This
// is the input the CalculationEngine (sizing/validation) consumes; it is richer
// than ClaimFacts (per-room L×W×H, %-affected, physical equipment counts, AHAM/
// CFM ratings, building factors). JSON is snake_case to match the legacy schemas.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Building-wide drying multipliers (legacy _GLOBAL_FACTORS_SCHEMA). Enum-ish fields are the
/// legacy string values: build_out_density ∈ very_open|fairly_open|average|very_dense;
/// building_construction ∈ standard|high_end; weather_conditions ∈ favorable|neutral|unfavorable;
/// building_envelope ∈ tight|moderate|loose.</summary>
public sealed record BuildingFactors(
    int ClassOfWater,
    int WaterCategory,
    string BuildOutDensity,
    string BuildingConstruction,
    bool HvacUsed,
    string WeatherConditions,
    string BuildingEnvelope);

/// <summary>Per-room factor override — only fields that clearly differ from the building default.</summary>
public sealed record RoomFactorOverride(
    string RoomName,
    int? ClassOfWater = null,
    int? WaterCategory = null,
    string? BuildOutDensity = null,
    string? Justification = null);

/// <summary>A subroom rolled up under a parent room.</summary>
public sealed record SubRoom(
    string RoomName,
    double Sf,
    double Length = 0,
    double Width = 0,
    double Height = 0,
    double Offset = 0,
    double PercentAffected = 0,
    double AffectedSf = 0);

/// <summary>One parsed room (legacy ROOMS_PARSING item). Dimensions in feet; SF is floor square footage.</summary>
public sealed record ExtractedRoom(
    string RoomId,
    string RoomName,
    string Level,
    double Length,
    double Width,
    double Height,
    double Sf,
    double Offset,
    double PercentAffected,
    double AffectedSf,
    string? PercentAffectedBasis,
    bool IsSubroom,
    string? ParentRoom,
    string? ChamberSource,
    bool DimensionsEstimated,
    IReadOnlyList<SubRoom> Subrooms);

/// <summary>A chamber (drying zone) and the rooms grouped into it.</summary>
public sealed record IdentifiedChamber(
    string ChamberName,
    IReadOnlyList<string> RoomsIncluded,
    string? Reasoning);

/// <summary>One physical equipment unit the vendor used (NOT billing unit-days).
/// equipment_type ∈ Dehumidifier|Air Mover|Air Filtration Device; dehu_type ∈ LGR|Desiccant|Unknown.</summary>
public sealed record UsedEquipment(
    string EquipmentType,
    string? Model,
    double Quantity,
    double AhamRating,
    double CfmRating,
    string? DehuType,
    string? SerialNumber);

/// <summary>Equipment grouped by room (legacy EQUIPMENT_PARSING item).</summary>
public sealed record RoomEquipment(
    string RoomId,
    string RoomName,
    IReadOnlyList<UsedEquipment> Equipment);

/// <summary>
/// Stage output: the structured chamber/equipment extraction that feeds the deterministic
/// CalculationEngine (equipment sizing/validation). Assembling rooms into chambers, resolving
/// per-chamber factors, and aggregating equipment counts is the CalculationEngine's job.
/// </summary>
public sealed record ChamberExtraction(
    string CaseId,
    BuildingFactors GlobalFactors,
    IReadOnlyList<RoomFactorOverride> RoomOverrides,
    IReadOnlyList<ExtractedRoom> Rooms,
    IReadOnlyList<IdentifiedChamber> ChambersIdentified,
    IReadOnlyList<RoomEquipment> RoomEquipment,
    decimal? VendorInvoiceTotal);
