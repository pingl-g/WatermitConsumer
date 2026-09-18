using System;
using System.Collections.Generic;

namespace Watermit.Contracts;

// ────────────────────────────────────────────────────────────────────────────
// A-1 output artifact: PackageManifest — what's in the submission and where.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>One classified document (or segment) within the package.</summary>
public sealed record ManifestDocument(
    string DocId,
    string? Category,          // taxonomy code (e.g. "VendorEstimate"); null if unclassified
    int? PageStart,
    int? PageEnd,
    double? Confidence,
    CategorySource? Source);

/// <summary>
/// A-1 output: the classified/segmented view of a claim package. This is the contract
/// A-2/A-3 and all of Phase B consume to know which pages hold what.
/// </summary>
public sealed record PackageManifest(
    string CaseId,
    IReadOnlyList<ManifestDocument> Documents,
    IReadOnlyDictionary<string, bool> Presence,   // per taxonomy code
    IReadOnlyList<Flag> Flags);

// ────────────────────────────────────────────────────────────────────────────
// A-2/A-3 output artifact: EstimateHeader — header + totals from the estimate.
// ────────────────────────────────────────────────────────────────────────────

public sealed record VendorInfo(string? Name = null, string? Address = null, string? Phone = null, string? TaxId = null);

public sealed record EstimatorInfo(string? Name = null, string? Position = null, string? Email = null, string? Phone = null);

public sealed record EstimateIdentifiers(string? VendorJobNumber = null, string? CarrierClaimNumber = null);

public sealed record EstimateDates(
    DateOnly? DateOfLoss = null,
    DateOnly? DateEntered = null,
    DateOnly? DateAssigned = null,
    DateOnly? EstimateDate = null);

public sealed record EstimateTotals(
    decimal? LineItemTotal = null,
    decimal? MaterialSalesTax = null,
    decimal? Rcv = null,
    decimal? Acv = null,
    decimal? NetClaim = null,
    decimal? OverheadAndProfit = null);

/// <summary>
/// A-2/A-3 output: the vendor estimate's own header + totals, extracted and normalized.
/// Feeds Norm #27 (price list), Segment 4 (baseline = NetClaim), and Phase D reconcile.
/// </summary>
public sealed record EstimateHeader(
    VendorInfo? Vendor = null,
    EstimatorInfo? Estimator = null,
    string? InsuredName = null,
    string? PropertyAddress = null,
    string? EstimateType = null,
    EstimateIdentifiers? Identifiers = null,
    EstimateDates? Dates = null,
    string? PriceListCode = null,
    EstimateTotals? Totals = null);
