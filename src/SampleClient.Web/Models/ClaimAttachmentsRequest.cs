using System.Text.Json;
using System.Text.Json.Serialization;

namespace SampleClient.Web.Models;

/// <summary>
/// Body of POST /Claim/{claimId}/attachments — what Claims Manager hands us when the user
/// clicks "Launch Sample Client" on a claim page. Arrives as a JSON string in a form field
/// (a top-level browser form POST cannot send application/json), so it is parsed by hand.
/// </summary>
public sealed record ClaimAttachmentsRequest
{
    /// <summary>Case-insensitive so the payload's lowercase "filename" still binds.</summary>
    public static readonly JsonSerializerOptions ParseOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Claims Manager stores this as SQL bigint, so it is a long here, not an int.</summary>
    [JsonPropertyName("claimId")]
    public long ClaimId { get; init; }

    [JsonPropertyName("claimNumber")]
    public string ClaimNumber { get; init; } = string.Empty;

    [JsonPropertyName("files")]
    public IReadOnlyList<ClaimAttachment> Files { get; init; } = Array.Empty<ClaimAttachment>();

    /// <summary>Parse the form-field JSON. Returns null (with a reason) instead of throwing.</summary>
    public static ClaimAttachmentsRequest? TryParse(string? json, out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The payload form field was empty.";
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ClaimAttachmentsRequest>(json, ParseOptions);
            if (parsed is null)
            {
                error = "The payload deserialized to null.";
                return null;
            }
            error = null;
            return parsed;
        }
        catch (JsonException ex)
        {
            error = $"The payload is not valid JSON: {ex.Message}";
            return null;
        }
    }
}

/// <summary>One file already sitting in Claims Manager's outbound container.</summary>
public sealed record ClaimAttachment
{
    /// <summary>Blob path as {container}/{path}, e.g. "tst2/OutboundDocuments/foo.pdf".
    /// Relative to the Claims Manager storage account — not an absolute URL.</summary>
    [JsonPropertyName("fileUrl")]
    public string FileUrl { get; init; } = string.Empty;

    [JsonPropertyName("filename")]
    public string FileName { get; init; } = string.Empty;

    /// <summary>Size in bytes; the grid shows it in KB.</summary>
    [JsonPropertyName("fileSize")]
    public long FileSize { get; init; }

    [JsonPropertyName("dateUploaded")]
    public DateTimeOffset DateUploaded { get; init; }

    [JsonPropertyName("updatedBy")]
    public string? UpdatedBy { get; init; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; init; }

    [JsonPropertyName("comments")]
    public string? Comments { get; init; }
}
