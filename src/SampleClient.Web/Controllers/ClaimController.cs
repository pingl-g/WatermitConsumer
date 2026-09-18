using Azure;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Watermit.Contracts;
using Watermit.Domain;
using Watermit.Persistence;                 // ICaseRepository, IBlobStore
using Watermit.Persistence.Tables;          // StorageOptions
using Watermit.Pipeline.Abstractions;       // IPipelineDispatcher, IClaimStatusReader, IClaimWorkList

namespace SampleClient.Web.Controllers;

/// <summary>
/// The whole consumer flow in one controller:
///   1. Submit  — create a Case, upload files to blob, hand the claim to the worker (a Pending row).
///   2. Status  — poll the claim's status and see which stage artifacts the worker has produced.
///   3. Stage   — read one produced artifact (work/OUT~{stage}.json).
/// It only uses the client seams; it never runs a pipeline stage.
/// </summary>
public sealed class ClaimController : Controller
{
    // The 8 pipeline stages, in order, as the worker writes them (work/OUT~{stage}.json).
    private static readonly string[] Stages = { "A1", "BMD", "A23", "B", "C", "DEHU", "E", "F" };

    private readonly ICaseRepository _cases;
    private readonly IBlobStore _blobs;
    private readonly IPipelineDispatcher _dispatcher;
    private readonly IClaimStatusReader _status;
    private readonly IClaimWorkList _list;
    private readonly BlobContainerClient _container;
    private readonly StorageOptions _opts;

    public ClaimController(
        ICaseRepository cases, IBlobStore blobs, IPipelineDispatcher dispatcher,
        IClaimStatusReader status, IClaimWorkList list,
        BlobServiceClient blobService, IOptions<StorageOptions> opts)
    {
        _cases = cases;
        _blobs = blobs;
        _dispatcher = dispatcher;
        _status = status;
        _list = list;
        _opts = opts.Value;
        _container = blobService.GetBlobContainerClient(_opts.BlobContainer);
    }

    // GET /  — submit form + recent claims.
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        IReadOnlyList<ClaimWorkRow> rows;
        try { rows = await _list.ListAsync(50, ct); }
        catch { rows = Array.Empty<ClaimWorkRow>(); }
        return View(rows);
    }

    // POST /Claim/Submit  — create the case, upload files, enqueue for the worker.
    [HttpPost]
    public async Task<IActionResult> Submit(string claimNumber, string? name, List<IFormFile>? files, CancellationToken ct)
    {
        var picked = (files ?? new()).Where(f => f is { Length: > 0 }).ToList();
        if (string.IsNullOrWhiteSpace(claimNumber) || picked.Count == 0)
        {
            TempData["Error"] = "A claim number and at least one file are required.";
            return RedirectToAction(nameof(Index));
        }

        var tenant = _opts.DefaultTenantId;
        var caseId = Guid.NewGuid().ToString("n");   // your id; also the blob folder + intake RowKey

        // (a) create the case record
        await _cases.CreateCaseAsync(new CaseHeader
        {
            TenantId      = tenant,
            CaseId        = caseId,
            ProcessTypeId = WaterMitigationTaxonomy.ProcessTypeId,
            Name          = string.IsNullOrWhiteSpace(name) ? claimNumber : name!.Trim(),
            Number        = claimNumber.Trim(),
            Status        = CaseStatus.Draft,
            Channel       = IngestChannel.Web,
        }, ct);

        // (b) upload each file -> {tenant}/{caseId}/input/{docId}-{fileName}, recorded as a CaseDocument
        var ordinal = 0;
        foreach (var file in picked)
        {
            var docId = Guid.NewGuid().ToString("n");
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            string blobUrl;
            await using (var stream = file.OpenReadStream())
                blobUrl = await _blobs.UploadOriginalAsync(tenant, caseId, docId, file.FileName, stream, contentType, ct);

            await _cases.AddDocumentAsync(tenant, caseId, new CaseDocument
            {
                DocId = docId, Ordinal = ++ordinal, FileName = file.FileName,
                ContentType = contentType, SizeBytes = file.Length, BlobUrl = blobUrl,
            }, ct);
        }

        // (c) hand off to the worker — writes a Pending row and returns immediately.
        await _dispatcher.EnqueueAsync(new CaseRef(tenant, caseId, WaterMitigationTaxonomy.ProcessTypeId), ct);

        return RedirectToAction(nameof(Status), new { id = caseId });
    }

    // GET /Claim/Status/{id}  — status + which stage artifacts exist yet.
    [HttpGet]
    public async Task<IActionResult> Status(string id, CancellationToken ct)
    {
        var tenant = _opts.DefaultTenantId;
        var row = await _status.GetAsync(tenant, id, ct);

        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await foreach (var b in _container.GetBlobsAsync(default, default, $"{tenant}/{id}/work/OUT~", ct))
            {
                var file = b.Name[(b.Name.LastIndexOf('/') + 1)..];
                if (file.StartsWith("OUT~") && file.EndsWith(".json")) present.Add(file[4..^5]);
            }
        }
        catch { /* none yet */ }

        return View(new StatusVm(id, row, Stages, present));
    }

    // GET /Claim/Stage?caseId=&stage=  — the raw artifact one stage produced.
    [HttpGet]
    public async Task<IActionResult> Stage(string caseId, string stage, CancellationToken ct)
    {
        if (!Stages.Contains(stage)) return BadRequest("Unknown stage.");
        var tenant = _opts.DefaultTenantId;
        var blob = _container.GetBlobClient($"{tenant}/{caseId}/work/OUT~{stage}.json");
        try
        {
            var dl = await blob.DownloadContentAsync(ct);
            return Content(dl.Value.Content.ToString(), "application/json");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return NotFound("That stage has not been produced yet.");
        }
    }
}

public sealed record StatusVm(string CaseId, ClaimWorkRow? Row, string[] Stages, HashSet<string> Present);
