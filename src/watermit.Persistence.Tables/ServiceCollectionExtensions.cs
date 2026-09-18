using System;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Watermit.Persistence;
using Watermit.Pipeline.Abstractions;

namespace Watermit.Persistence.Tables;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Composition root for persistence. Selects a provider from config
    /// ("Persistence:Provider", default "Tables") and registers it. When a SQL provider
    /// (watermit.Persistence.Sql) is added, extend the switch here — callers (api) don't change.
    /// </summary>
    public static IServiceCollection AddWatermitPersistence(
        this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetValue<string>("Persistence:Provider") ?? "Tables";

        return provider.ToLowerInvariant() switch
        {
            "tables" => services.AddTablesPersistence(o =>
                config.GetSection(StorageOptions.SectionName).Bind(o)),
            "sql" => throw new NotSupportedException(
                "Persistence:Provider 'Sql' selected but watermit.Persistence.Sql is not installed."),
            _ => throw new NotSupportedException($"Unknown Persistence:Provider '{provider}'.")
        };
    }

    /// <summary>
    /// Registers the Azure Tables + Blob provider (DefaultAzureCredential, or a connection string
    /// when StorageOptions.ConnectionString is set) and the Case repository / blob store.
    /// </summary>
    public static IServiceCollection AddTablesPersistence(
        this IServiceCollection services, Action<StorageOptions> configure)
    {
        services.Configure(configure);

        // One credential instance, only used when not authenticating via connection string.
        var credential = new DefaultAzureCredential();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var conn = ConnectionStringOrFallback(opts.ConnectionString);
            return !string.IsNullOrWhiteSpace(conn)
                ? new TableServiceClient(conn)
                : new TableServiceClient(new Uri(opts.ResolvedTableEndpoint), credential);
        });

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var conn = ConnectionStringOrFallback(opts.ConnectionString);
            return !string.IsNullOrWhiteSpace(conn)
                ? new BlobServiceClient(conn)
                : new BlobServiceClient(new Uri(opts.ResolvedBlobEndpoint), credential);
        });

        services.AddSingleton<ICaseRepository, CaseRepository>();
        services.AddSingleton<IBlobStore, BlobStore>();

        return services;
    }

    /// <summary>
    /// Registers the intake <see cref="IClaimWorkSource"/> the worker polls. "Table" (default) uses the
    /// Azure Table intake queue with ETag claim/lease; "AppSettings" reads a static list from config for
    /// local dev. The Table source needs AddTablesPersistence/AddWatermitPersistence first
    /// (TableServiceClient + StorageOptions). Selected via "Extraction:WorkSource:Provider".
    /// </summary>
    public static IServiceCollection AddWatermitWorkSource(this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetValue<string>("Extraction:WorkSource:Provider") ?? "Table";
        if (string.Equals(provider, "AppSettings", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IClaimWorkSource, AppSettingsClaimWorkSource>();
        else
            services.AddSingleton<IClaimWorkSource, TableClaimWorkSource>();
        return services;
    }

    /// <summary>
    /// Wires the client-side <b>push</b> seam: a <see cref="TableEnqueueDispatcher"/> that writes Pending
    /// intake rows, so a front-end's <see cref="IPipelineDispatcher"/> hands work to the worker via the
    /// table. Call this from a client App Service (MVC/REST) <i>after</i> AddWatermitPersistence
    /// (needs TableServiceClient + StorageOptions + ICaseRepository). Replaces the default no-op dispatcher.
    /// </summary>
    public static IServiceCollection AddWatermitTableDispatcher(this IServiceCollection services)
    {
        services.AddSingleton<TableClaimWorkSource>();
        services.AddSingleton<IClaimWorkEnqueuer>(sp => sp.GetRequiredService<TableClaimWorkSource>());
        services.AddSingleton<IClaimStatusReader>(sp => sp.GetRequiredService<TableClaimWorkSource>());
        services.AddSingleton<IClaimWorkList>(sp => sp.GetRequiredService<TableClaimWorkSource>());
        services.AddSingleton<IPipelineDispatcher, TableEnqueueDispatcher>();
        return services;
    }

    // TEMPORARY (no RBAC yet): fall back to the hardcoded LocalSecrets connection string when config
    // supplies none, so the apps authenticate to storage with a key instead of a Managed Identity.
    // Blank LocalSecrets.StorageConnectionString to revert to Managed Identity; delete this method +
    // LocalSecrets.cs once the identity has Storage Blob/Table Data roles.
    internal static string? ConnectionStringOrFallback(string? configured) =>
        !string.IsNullOrWhiteSpace(configured)
            ? configured
            : string.IsNullOrWhiteSpace(LocalSecrets.StorageConnectionString) ? null : LocalSecrets.StorageConnectionString;

    /// <summary>Ensures backing stores exist (tables + blob container). Call at startup.</summary>
    public static async System.Threading.Tasks.Task InitWatermitPersistenceAsync(this IServiceProvider provider)
    {
        await provider.GetRequiredService<ICaseRepository>().InitAsync();
        await provider.GetRequiredService<IBlobStore>().InitAsync();
    }
}
