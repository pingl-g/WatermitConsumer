using System;
using Azure;
using Azure.Data.Tables;
using Watermit.Contracts;
using Watermit.Domain;

namespace Watermit.Persistence.Tables;

/// <summary>Azure Table persistence model for the Case header row (RowKey = "CASE").</summary>
public sealed class CaseHeaderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string TenantId { get; set; } = default!;
    public string CaseId { get; set; } = default!;
    public string ProcessTypeId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Number { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public int FileCount { get; set; }
    public bool NeedsHumanReview { get; set; }
    public string? ManifestBlobUrl { get; set; }
    public string? ComparisonBlobUrl { get; set; }
    public DateTimeOffset? ReceivedUtc { get; set; }
    public DateTimeOffset? DueUtc { get; set; }
    public DateTimeOffset? ComparativeStartedUtc { get; set; }
    public DateTimeOffset? ComparativeSentUtc { get; set; }
    public DateTimeOffset? SettledUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedUtc { get; set; }
    public string? ReviewNotes { get; set; }

    public static CaseHeaderEntity FromModel(CaseHeader h) => new()
    {
        PartitionKey = CaseKeys.Partition(h.TenantId, h.CaseId),
        RowKey = CaseKeys.HeaderRowKey,
        TenantId = h.TenantId,
        CaseId = h.CaseId,
        ProcessTypeId = h.ProcessTypeId,
        Name = h.Name,
        Number = h.Number,
        Status = h.Status.ToString(),
        Channel = h.Channel.ToString(),
        CreatedBy = h.CreatedBy,
        CreatedUtc = h.CreatedUtc,
        FileCount = h.FileCount,
        NeedsHumanReview = h.NeedsHumanReview,
        ManifestBlobUrl = h.ManifestBlobUrl,
        ComparisonBlobUrl = h.ComparisonBlobUrl,
        ReceivedUtc = h.ReceivedUtc,
        DueUtc = h.DueUtc,
        ComparativeStartedUtc = h.ComparativeStartedUtc,
        ComparativeSentUtc = h.ComparativeSentUtc,
        SettledUtc = h.SettledUtc,
        ReviewedBy = h.ReviewedBy,
        ReviewedUtc = h.ReviewedUtc,
        ReviewNotes = h.ReviewNotes
    };

    public CaseHeader ToModel() => new()
    {
        TenantId = TenantId,
        CaseId = CaseId,
        ProcessTypeId = ProcessTypeId,
        Name = Name,
        Number = Number,
        Status = Enum.Parse<CaseStatus>(Status),
        Channel = Enum.Parse<IngestChannel>(Channel),
        CreatedBy = CreatedBy,
        CreatedUtc = CreatedUtc,
        FileCount = FileCount,
        NeedsHumanReview = NeedsHumanReview,
        ManifestBlobUrl = ManifestBlobUrl,
        ComparisonBlobUrl = ComparisonBlobUrl,
        ReceivedUtc = ReceivedUtc,
        DueUtc = DueUtc,
        ComparativeStartedUtc = ComparativeStartedUtc,
        ComparativeSentUtc = ComparativeSentUtc,
        SettledUtc = SettledUtc,
        ReviewedBy = ReviewedBy,
        ReviewedUtc = ReviewedUtc,
        ReviewNotes = ReviewNotes
    };
}

/// <summary>Azure Table persistence model for the settlement row (RowKey = "SETTLEMENT").</summary>
public sealed class CaseSettlementEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public bool WasAcorObtained { get; set; }
    public double OriginalVendorInvoiceAmount { get; set; }
    public double ComparativeEstimateAmount { get; set; }
    public double? AcorAmount { get; set; }
    public double? NonAcorAmount { get; set; }
    public string? Notes { get; set; }
    public string? SettledBy { get; set; }
    public DateTimeOffset SettledUtc { get; set; }

    // Azure Tables has no decimal type — money is stored as double (Edm.Double) and mapped back.
    public static CaseSettlementEntity FromModel(string tenantId, string caseId, CaseSettlement s) => new()
    {
        PartitionKey = CaseKeys.Partition(tenantId, caseId),
        RowKey = CaseKeys.SettlementRowKey,
        WasAcorObtained = s.WasAcorObtained,
        OriginalVendorInvoiceAmount = (double)s.OriginalVendorInvoiceAmount,
        ComparativeEstimateAmount = (double)s.ComparativeEstimateAmount,
        AcorAmount = s.AcorAmount is null ? null : (double)s.AcorAmount.Value,
        NonAcorAmount = s.NonAcorAmount is null ? null : (double)s.NonAcorAmount.Value,
        Notes = s.Notes,
        SettledBy = s.SettledBy,
        SettledUtc = s.SettledUtc
    };

    public CaseSettlement ToModel() => new()
    {
        WasAcorObtained = WasAcorObtained,
        OriginalVendorInvoiceAmount = (decimal)OriginalVendorInvoiceAmount,
        ComparativeEstimateAmount = (decimal)ComparativeEstimateAmount,
        AcorAmount = AcorAmount is null ? null : (decimal)AcorAmount.Value,
        NonAcorAmount = NonAcorAmount is null ? null : (decimal)NonAcorAmount.Value,
        Notes = Notes,
        SettledBy = SettledBy,
        SettledUtc = SettledUtc
    };
}

/// <summary>Azure Table persistence model for a document row (RowKey = "DOC~{ordinal:000}").</summary>
public sealed class CaseDocumentEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string DocId { get; set; } = default!;
    public int Ordinal { get; set; }
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public int? PageCount { get; set; }
    public string BlobUrl { get; set; } = default!;
    public string? DeclaredCategory { get; set; }
    public string? AutoCategory { get; set; }
    public double? AutoConfidence { get; set; }
    public string? CategorySource { get; set; }
    public DateTimeOffset UploadedUtc { get; set; }

    public static CaseDocumentEntity FromModel(string tenantId, string caseId, CaseDocument d) => new()
    {
        PartitionKey = CaseKeys.Partition(tenantId, caseId),
        RowKey = CaseKeys.DocumentRowKey(d.Ordinal),
        DocId = d.DocId,
        Ordinal = d.Ordinal,
        FileName = d.FileName,
        ContentType = d.ContentType,
        SizeBytes = d.SizeBytes,
        PageCount = d.PageCount,
        BlobUrl = d.BlobUrl,
        DeclaredCategory = d.DeclaredCategory,
        AutoCategory = d.AutoCategory,
        AutoConfidence = d.AutoConfidence,
        CategorySource = d.CategorySource?.ToString(),
        UploadedUtc = d.UploadedUtc
    };

    public CaseDocument ToModel() => new()
    {
        DocId = DocId,
        Ordinal = Ordinal,
        FileName = FileName,
        ContentType = ContentType,
        SizeBytes = SizeBytes,
        PageCount = PageCount,
        BlobUrl = BlobUrl,
        DeclaredCategory = DeclaredCategory,
        AutoCategory = AutoCategory,
        AutoConfidence = AutoConfidence,
        CategorySource = string.IsNullOrEmpty(CategorySource)
            ? null
            : Enum.Parse<Watermit.Contracts.CategorySource>(CategorySource),
        UploadedUtc = UploadedUtc
    };
}
