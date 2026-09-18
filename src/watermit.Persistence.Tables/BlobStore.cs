using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Watermit.Persistence;

namespace Watermit.Persistence.Tables;

/// <summary>Azure Blob Storage implementation of <see cref="IBlobStore"/>.</summary>
public sealed class BlobStore : IBlobStore
{
    private readonly BlobContainerClient _container;

    public BlobStore(BlobServiceClient serviceClient, IOptions<StorageOptions> options)
    {
        _container = serviceClient.GetBlobContainerClient(options.Value.BlobContainer);
    }

    public Task InitAsync(CancellationToken ct = default) =>
        _container.CreateIfNotExistsAsync(cancellationToken: ct);

    public async Task<string> UploadOriginalAsync(
        string tenantId, string caseId, string docId, string fileName,
        Stream content, string contentType, CancellationToken ct = default)
    {
        // Write to the documented extraction seam ({tenant}/{caseId}/input) so the worker's
        // BlobDocumentAccessor reads exactly what the frontend uploaded. The {docId}- prefix keeps
        // names unique within the claim; A-1 classifies by content, not by file name, so it's harmless.
        var blobPath = $"{tenantId}/{caseId}/input/{docId}-{fileName}";
        var blob = _container.GetBlobClient(blobPath);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            ct);
        return blob.Uri.ToString();
    }
}
