using System;
using System.IO;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace Watermit.Persistence.Tables;

/// <summary>
/// Loads application settings from a JSON blob in Azure Storage, authenticated with
/// <see cref="DefaultAzureCredential"/> (the App Service's Managed Identity in Azure). This lets an app
/// run with <b>no Azure App Service application settings</b>: the only thing shipped with the app is a
/// small <c>AppConfig</c> bootstrap section (account + container + blob name — none of it secret) in its
/// own appsettings.json; the real config (data storage, Foundry keys, worker tuning) lives in the blob.
/// </summary>
public static class AzureBlobConfiguration
{
    /// <summary>
    /// Appends the config blob named by the <c>AppConfig</c> section as a JSON configuration source.
    /// No-op when <c>AppConfig:Enabled</c> is false (e.g. local dev). Fails fast (throws) when enabled but
    /// the blob can't be read, so a misconfigured deployment surfaces at startup rather than silently.
    /// Bootstrap keys under <c>AppConfig</c>: <c>AccountName</c> (or <c>BlobEndpoint</c>, or
    /// <c>ConnectionString</c> for local), <c>Container</c> (default "config"), <c>Blob</c>
    /// (default "appsettings.json").
    /// </summary>
    public static IConfigurationBuilder AddAzureBlobJson(this IConfigurationBuilder builder)
    {
        var boot = builder.Build();
        var s = boot.GetSection("AppConfig");
        if (!s.GetValue("Enabled", true))
            return builder; // disabled — use the shipped appsettings / env instead (local dev)

        var container = s["Container"] ?? "config";
        var blobName = s["Blob"] ?? "appsettings.json";
        // TEMPORARY (no RBAC yet): if no connection string is configured for the config-blob read, fall
        // back to the hardcoded LocalSecrets key so the read uses a key instead of a Managed Identity.
        var conn = ServiceCollectionExtensions.ConnectionStringOrFallback(s["ConnectionString"]);
        var endpoint = s["BlobEndpoint"];
        var account = s["AccountName"];

        BlobServiceClient service;
        if (!string.IsNullOrWhiteSpace(conn))
        {
            service = new BlobServiceClient(conn);
        }
        else
        {
            var uri = !string.IsNullOrWhiteSpace(endpoint) ? endpoint!
                : !string.IsNullOrWhiteSpace(account) ? $"https://{account}.blob.core.windows.net"
                : throw new InvalidOperationException(
                    "AppConfig is enabled but has no AccountName / BlobEndpoint / ConnectionString.");
            service = new BlobServiceClient(new Uri(uri), new DefaultAzureCredential());
        }

        // DownloadContent throws RequestFailedException (404 / 403) if the blob is missing or the
        // identity lacks access — intentionally fatal so the deployment is fixed, not run half-configured.
        var content = service.GetBlobContainerClient(container).GetBlobClient(blobName)
            .DownloadContent().Value.Content;
        builder.AddJsonStream(new MemoryStream(content.ToArray()));
        return builder;
    }
}
