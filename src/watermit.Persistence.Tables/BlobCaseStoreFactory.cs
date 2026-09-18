using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Watermit.Pipeline.Abstractions;

namespace Watermit.Persistence.Tables;

/// <summary>
/// Blob-backed <see cref="ICaseStoreFactory"/>: builds a <see cref="BlobStageOutputStore"/> /
/// <see cref="BlobDocumentAccessor"/> bound to one case's <c>{tenant}/{caseId}/work</c> and
/// <c>/input</c> prefixes in the claims container. One factory instance (singleton) serves every case;
/// the per-case binding is cheap (just a path prefix), so a worker can process many cases concurrently.
/// </summary>
public sealed class BlobCaseStoreFactory : ICaseStoreFactory
{
    private readonly BlobContainerClient _container;

    public BlobCaseStoreFactory(BlobServiceClient service, IOptions<StorageOptions> options)
    {
        _container = service.GetBlobContainerClient(options.Value.BlobContainer);
    }

    public IStageOutputStore Outputs(string tenant, string caseId) =>
        new BlobStageOutputStore(_container, tenant, caseId);

    public IDocumentAccessor Documents(string tenant, string caseId) =>
        new BlobDocumentAccessor(_container, tenant, caseId);
}
