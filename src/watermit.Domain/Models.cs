using System;
using System.Collections.Generic;
using Watermit.Contracts;

namespace Watermit.Domain;

/// <summary>
/// Canonical, persistence-agnostic model of a Case header. The Storage layer maps this
/// to/from an Azure Table entity; the API maps it to/from Contracts DTOs.
/// </summary>
public sealed class CaseHeader
{
    public required string TenantId { get; init; }
    public required string CaseId { get; init; }
    public required string ProcessTypeId { get; init; }
    public required string Name { get; set; }
    public required string Number { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Draft;
    public IngestChannel Channel { get; set; } = IngestChannel.Web;
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public int FileCount { get; set; }
    public bool NeedsHumanReview { get; set; }

    /// <summary>Pointer to the Package Manifest blob (set in A-1 finalize; null until then).</summary>
    public string? ManifestBlobUrl { get; set; }

    /// <summary>Pointer to the EstimateComparison artifact blob (set when the comparison is produced).</summary>
    public string? ComparisonBlobUrl { get; set; }

    // Intake + 72-hour SLA.
    public DateTimeOffset? ReceivedUtc { get; set; }   // assignment received ("Review Estimate" pressed)
    public DateTimeOffset? DueUtc { get; set; }        // ReceivedUtc + 72h

    // Milestones (replicating the CMS activity dates).
    public DateTimeOffset? ComparativeStartedUtc { get; set; } // CMS 'Inspection Scheduled'
    public DateTimeOffset? ComparativeSentUtc { get; set; }    // CMS 'Inspection Completed'
    public DateTimeOffset? SettledUtc { get; set; }

    // Human decision trail on the comparison review (the WMS who acted; decision carried by Status).
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedUtc { get; set; }
    public string? ReviewNotes { get; set; }
}

/// <summary>An uploaded file attached to a Case.</summary>
public sealed class CaseDocument
{
    public required string DocId { get; init; }
    public required int Ordinal { get; init; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public int? PageCount { get; set; }
    public required string BlobUrl { get; set; }

    // Category fields are populated later (A-1). Declared = human choice, Auto = classifier.
    public string? DeclaredCategory { get; set; }
    public string? AutoCategory { get; set; }
    public double? AutoConfidence { get; set; }
    public CategorySource? CategorySource { get; set; }

    public DateTimeOffset UploadedUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// The settlement closeout for a Case — the CMS "ESTIMATE details" (Step 9), captured by the WMS
/// once negotiation concludes. Persistence-agnostic; maps to Contracts.SettlementRecord for the artifact form.
/// </summary>
public sealed class CaseSettlement
{
    public bool WasAcorObtained { get; set; }
    public decimal OriginalVendorInvoiceAmount { get; set; }
    public decimal ComparativeEstimateAmount { get; set; }
    public decimal? AcorAmount { get; set; }
    public decimal? NonAcorAmount { get; set; }
    public string? Notes { get; set; }
    public string? SettledBy { get; set; }
    public DateTimeOffset SettledUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>The whole Case aggregate loaded from one partition.</summary>
public sealed class CaseAggregate
{
    public required CaseHeader Header { get; init; }
    public List<CaseDocument> Documents { get; init; } = new();

    /// <summary>Settlement closeout, once captured; null until the case is Settled.</summary>
    public CaseSettlement? Settlement { get; set; }
}
