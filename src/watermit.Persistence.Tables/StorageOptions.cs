namespace Watermit.Persistence.Tables;

/// <summary>
/// Configuration for Azure Storage access. Bound from the "Storage" config section.
/// No secrets: authentication is via DefaultAzureCredential (az login locally,
/// managed identity in Azure), so only the account name / endpoints are needed.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Storage account name, e.g. "watermitdev". Used to derive table/blob endpoints
    /// when authenticating with DefaultAzureCredential. Ignored if ConnectionString is set.</summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Optional account/connection string. When set (typically via user-secrets for local dev
    /// against a shared account where you lack data-plane RBAC), clients authenticate with it
    /// instead of DefaultAzureCredential. Leave empty in deployed environments to use managed identity.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>True when key/connection-string auth should be used instead of DefaultAzureCredential.</summary>
    public bool UsesConnectionString => !string.IsNullOrWhiteSpace(ConnectionString);

    /// <summary>Optional explicit table endpoint. Defaults to https://{AccountName}.table.core.windows.net.</summary>
    public string? TableEndpoint { get; set; }

    /// <summary>Optional explicit blob endpoint. Defaults to https://{AccountName}.blob.core.windows.net.</summary>
    public string? BlobEndpoint { get; set; }

    /// <summary>Single table holding all Case rows (heterogeneous, partitioned by {TenantId}|{CaseId}).</summary>
    public string CasesTable { get; set; } = "Cases";

    /// <summary>Intake queue the worker polls: one row per claim, PK={Tenant}, RK={CaseId}, claimed via ETag.</summary>
    public string ClaimWorkTable { get; set; } = "ClaimWork";

    /// <summary>Blob container for claim files and outputs.</summary>
    public string BlobContainer { get; set; } = "claims";

    /// <summary>Default tenant until multi-tenant onboarding exists.</summary>
    public string DefaultTenantId { get; set; } = "genpact";

    public string ResolvedTableEndpoint =>
        string.IsNullOrWhiteSpace(TableEndpoint)
            ? $"https://{AccountName}.table.core.windows.net"
            : TableEndpoint!;

    public string ResolvedBlobEndpoint =>
        string.IsNullOrWhiteSpace(BlobEndpoint)
            ? $"https://{AccountName}.blob.core.windows.net"
            : BlobEndpoint!;
}
