using System.Collections.Generic;

namespace Watermit.Contracts;

/// <summary>
/// The document categories for the WaterMitigation process (v1). These are seeded into the
/// Taxonomies config table in Step 2; the constants here are for validation and referencing
/// in code. Other processes define their own taxonomies — nothing here is baked into storage.
/// </summary>
public static class WaterMitigationTaxonomy
{
    public const string ProcessTypeId = "WaterMitigation";
    public const string Version = "v1";

    public const string DryLogs = "DryLogs";
    public const string VendorEstimate = "VendorEstimate";
    public const string VendorImages = "VendorImages";
    public const string ThirdPartyInvoices = "ThirdPartyInvoices";
    public const string FieldInspectionImages = "FieldInspectionImages";
    public const string RebuildEstimate = "RebuildEstimate";
    public const string Fnol = "FNOL";

    public static readonly IReadOnlyList<string> All = new[]
    {
        DryLogs, VendorEstimate, VendorImages, ThirdPartyInvoices,
        FieldInspectionImages, RebuildEstimate, Fnol
    };

    private static readonly HashSet<string> Set = new(All);

    public static bool IsValid(string? code) => code is not null && Set.Contains(code);
}
