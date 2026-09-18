using System;
using System.Collections.Generic;

namespace Watermit.Contracts;

/// <summary>Request to create a new claim (Case) from the web channel.</summary>
public sealed record CreateClaimRequest(
    string Name,
    string Number,
    string? ProcessTypeId = WaterMitigationTaxonomy.ProcessTypeId);

/// <summary>Response after a Case is created.</summary>
public sealed record CreateClaimResponse(
    string CaseId,
    string TenantId,
    CaseStatus Status);

/// <summary>Form input for the settlement-capture screen (the CMS "ESTIMATE details", Step 9).</summary>
public sealed record SettlementInput(
    bool WasAcorObtained,
    decimal OriginalVendorInvoiceAmount,
    decimal ComparativeEstimateAmount,
    decimal? AcorAmount,
    decimal? NonAcorAmount,
    string? Notes);

/// <summary>Response after a file is uploaded and its Document row created.</summary>
public sealed record AddDocumentResponse(
    string CaseId,
    string DocId,
    int Ordinal,
    string BlobUrl);

/// <summary>A document (uploaded file) as returned to the client.</summary>
public sealed record DocumentDto(
    string DocId,
    int Ordinal,
    string FileName,
    string ContentType,
    long SizeBytes,
    int? PageCount,
    string BlobUrl,
    string? DeclaredCategory,
    string? AutoCategory,
    double? AutoConfidence,
    CategorySource? CategorySource,
    DateTimeOffset UploadedUtc);

/// <summary>The full Case aggregate (header + its documents) as returned to the client.</summary>
public sealed record CaseDto(
    string CaseId,
    string TenantId,
    string ProcessTypeId,
    string Name,
    string Number,
    CaseStatus Status,
    IngestChannel Channel,
    int FileCount,
    bool NeedsHumanReview,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<DocumentDto> Documents);
