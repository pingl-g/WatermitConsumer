using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Watermit.Contracts;
using Watermit.Pipeline.Abstractions;

namespace Watermit.Persistence.Tables;

/// <summary>
/// Blob-backed <see cref="IDocumentAccessor"/>. Reads the files the frontend uploaded under a claim's
/// input prefix: <c>{tenant}/{caseId}/input/{docId}</c> in the claims container. The docId is the blob
/// name relative to that prefix (i.e. the uploaded file name). This is the read side of the storage
/// seam between the MVC upload frontend and the extraction pipeline.
/// </summary>
public sealed class BlobDocumentAccessor : IDocumentAccessor
{
    private readonly BlobContainerClient _container;
    private readonly string _inputPrefix; // e.g. "genpact/CLAIM-123/input/"

    public BlobDocumentAccessor(BlobContainerClient container, string tenantId, string caseId)
    {
        _container = container;
        _inputPrefix = $"{tenantId}/{caseId}/input/";
    }

    /// <summary>List the uploaded document ids (blob names under the input prefix) - the A-1 inventory.</summary>
    public async Task<IReadOnlyList<string>> ListDocumentIdsAsync(CancellationToken ct = default)
    {
        var ids = new List<string>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, _inputPrefix, ct))
        {
            var docId = item.Name[_inputPrefix.Length..];
            if (!string.IsNullOrWhiteSpace(docId)) ids.Add(docId);
        }
        return ids;
    }

    public async Task<Stream> OpenOriginalAsync(string docId, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(_inputPrefix + docId);
        var download = await blob.DownloadStreamingAsync(cancellationToken: ct);
        return download.Value.Content;
    }
}

/// <summary>
/// Blob-backed <see cref="IStageOutputStore"/>. Writes each stage's artifact to
/// <c>{tenant}/{caseId}/work/OUT~{stageId}.json</c> and raw byproducts (layout markdown) to
/// <c>{tenant}/{caseId}/work/{name}</c> in the claims container. This is the write side of the seam:
/// extraction outputs land back in the same storage account the frontend can read.
/// </summary>
public sealed class BlobStageOutputStore : IStageOutputStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly BlobContainerClient _container;
    private readonly string _workPrefix; // e.g. "genpact/CLAIM-123/work/"

    public BlobStageOutputStore(BlobContainerClient container, string tenantId, string caseId)
    {
        _container = container;
        _workPrefix = $"{tenantId}/{caseId}/work/";
    }

    public async Task<StageEnvelope<T>?> ReadAsync<T>(string stageId, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(_workPrefix + $"OUT~{stageId}.json");
        try
        {
            var download = await blob.DownloadContentAsync(ct);
            return JsonSerializer.Deserialize<StageEnvelope<T>>(download.Value.Content.ToString(), Json);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task WriteAsync<T>(StageEnvelope<T> envelope, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(envelope, Json);
        await UploadAsync($"OUT~{envelope.StageId}.json", json, "application/json", ct);
    }

    public Task WriteTextAsync(string name, string content, CancellationToken ct = default) =>
        UploadAsync(name, content, name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? "text/markdown" : "text/plain", ct);

    private async Task UploadAsync(string name, string content, string contentType, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(_workPrefix + name);
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(ms,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
    }
}
