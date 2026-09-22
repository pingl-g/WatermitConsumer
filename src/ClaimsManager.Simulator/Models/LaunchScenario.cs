using System.Text.Json;

namespace ClaimsManager.Simulator.Models;

/// <summary>One canned hand-off to POST at SampleClient.Web, good or bad.</summary>
/// <param name="Key">Stable id used by the picker.</param>
/// <param name="Name">Label shown on the scenario button.</param>
/// <param name="Expected">What SampleClient.Web should do with it — the assertion, in prose.</param>
/// <param name="ClaimId">Goes in the route: /Claim/{claimId}/attachments.
/// SQL bigint on the Claims Manager side, so long rather than int.</param>
/// <param name="Json">The literal "payload" form-field value (kept as a string so the
/// malformed case can be genuinely malformed).</param>
public sealed record LaunchScenario(
    string Key, string Name, string Expected, long ClaimId, string Json);

public static class Scenarios
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    /// <summary>The payload exactly as the integration spec documents it.</summary>
    public const string SpecClaimNumber = "060034262-1";

    public static IReadOnlyList<LaunchScenario> All { get; } = Build();

    public static LaunchScenario Default => All[0];

    public static LaunchScenario? Find(string? key) =>
        All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<LaunchScenario> Build()
    {
        var spec = Payload(123, SpecClaimNumber, new[]
        {
            File("tst2/OutboundDocuments/bulk update check status.png",
                 "bulk update check status.png", 56643, "2026-09-10T02:05:00-04:00",
                 "Tina Smitch", "Supporting File", ""),
            File("tst2/OutboundDocuments/InventoryReplacementReview_SK0063_0819072542.pdf",
                 "InventoryReplacementReview_SK0063_0819072542.pdf", 238384, "2026-09-12T22:02:00-04:00",
                 "Alex Cooper", "Other", "Testing Only"),
        });

        return new List<LaunchScenario>
        {
            new("spec", "Spec example (2 files)",
                "Grid renders 2 rows, newest first, pager reads \"2 items in 1 page\".",
                123, spec),

            new("paging", "11 files (paging)",
                "Pager reads \"11 items in 3 pages\"; select-all ticks only the 5 on the current page.",
                123, ManyFiles()),

            new("bigint", "Bigint claimId",
                "Binds and renders normally. 8000000000123 overflows int, so an int route "
                + "constraint or parameter would 404 here.",
                8_000_000_000_123, Payload(8_000_000_000_123, SpecClaimNumber, new[]
                {
                    File("tst2/OutboundDocuments/FNOL_MORRONE.pdf", "FNOL_MORRONE.pdf",
                         102400, "2026-09-10T02:05:00-04:00", "Tina Smitch", "FNOL", ""),
                })),

            new("mismatch", "claimId mismatch",
                "Red error: route claimId wins, no grid. Payload says 123, route says 999.",
                999, spec),

            new("malformed", "Malformed JSON",
                "Red error naming the parse failure — must not throw a 500.",
                123, "{\"claimId\":123,\"claimNumber\":\"060034262-1\",\"files\":[{\"filename\":"),

            new("empty", "Empty files array",
                "\"No attachments on this claim.\" and no pager.",
                123, Payload(123, SpecClaimNumber, Array.Empty<object>())),

            new("encoding", "Special characters",
                "Filenames render as inert text — no script runs, quotes and ampersands survive.",
                123, Payload(123, "060034262-1", new[]
                {
                    File("tst2/OutboundDocuments/<script>alert(1)</script>.png",
                         "<script>alert(1)</script>.png", 2048, "2026-08-13T07:00:00-04:00",
                         "O'Brien & Co <b>", "Other", "encoding check"),
                    File("tst2/OutboundDocuments/quote\"and&amp.pdf",
                         "quote\"and&amp.pdf", 4096, "2026-08-12T07:00:00-04:00",
                         "Ünicode Nâme", "Other", "unicode: 日本語"),
                })),

            new("nopayload", "Empty payload field",
                "Red error: \"The payload form field was empty.\"",
                123, string.Empty),
        };
    }

    private static object File(
        string fileUrl, string filename, long fileSize,
        string dateUploaded, string updatedBy, string documentType, string comments) =>
        new { fileUrl, filename, fileSize, dateUploaded, updatedBy, documentType, comments };

    private static string Payload(long claimId, string claimNumber, IReadOnlyList<object> files) =>
        JsonSerializer.Serialize(new { claimId, claimNumber, files }, Pretty);

    /// <summary>Eleven files so the 5-per-page grid needs three pages.</summary>
    private static string ManyFiles()
    {
        var names = new[]
        {
            "bulk update check status.png", "InventoryReplacementReview_SK0063_0819072542.pdf",
            "ReplInv_MICHEAL11092MORRONE_SK0063_0819072256.pdf",
            "InventoryReplacementReview_SK0063_0819072108.pdf",
            "InsInv_MICHEAL11092MORRONE_SK0063_0819071043.pdf",
            "DryLog_Day1.pdf", "DryLog_Day2.pdf", "FNOL_MORRONE.pdf",
            "PhotoReport_Basement.pdf", "VendorInvoice_SK0063.pdf", "MoistureMap.pdf",
        };
        var types = new[] { "Supporting File", "Other", "FNOL", "Vendor Invoice", "Photo Report" };
        var people = new[] { "Tina Smitch", "Alex Cooper", "sateesh p", "13414" };
        var start = new DateTimeOffset(2026, 9, 12, 22, 2, 0, TimeSpan.FromHours(-4));

        var files = names.Select((n, i) => File(
            "tst2/OutboundDocuments/" + n, n,
            10_240 + (i * 24_576),
            start.AddHours(-6 * i).ToString("yyyy-MM-ddTHH:mm:sszzz"),
            people[i % people.Length],
            types[i % types.Length],
            i % 3 == 0 ? "Testing Only" : "")).ToArray();

        return Payload(123, SpecClaimNumber, files);
    }
}
