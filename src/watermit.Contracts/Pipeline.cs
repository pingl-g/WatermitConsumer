using System;
using System.Collections.Generic;

namespace Watermit.Contracts;

/// <summary>Severity of a pipeline flag.</summary>
public enum FlagLevel { Info, Warning, Error }

/// <summary>A non-fatal note raised by a stage (missing evidence, low confidence, mismatch, …).</summary>
public sealed record Flag(FlagLevel Level, string Message, string? Code = null);

/// <summary>Points a value back to where it was found in a source document (provenance).</summary>
public sealed record SourceRef(string? DocId = null, int? Page = null, double[]? BoundingBox = null);

/// <summary>Outcome status of running a stage.</summary>
public enum StageStatus
{
    Completed,
    Skipped,       // preconditions not met / not applicable for this process
    NeedsReview,   // produced output but flagged for a human
    Failed
}

/// <summary>Identifies a Case across the platform.</summary>
public sealed record CaseRef(string TenantId, string CaseId, string ProcessTypeId);

/// <summary>
/// Uniform wrapper around every stage's output artifact. Provides consistent provenance,
/// versioning, and gating for the whole pipeline. The stage runner stamps StageId /
/// SchemaVersion / InputsHash / ProducedUtc; downstream stages read Payload as their input.
/// </summary>
public sealed record StageEnvelope<T>(
    string StageId,
    string SchemaVersion,
    string InputsHash,
    DateTimeOffset ProducedUtc,
    bool NeedsHumanReview,
    IReadOnlyList<Flag> Flags,
    double? Confidence,
    T Payload);

/// <summary>
/// Control result a stage returns to the runner. The runner wraps <see cref="Payload"/> in a
/// <see cref="StageEnvelope{T}"/> (adding StageId/InputsHash/etc.) and persists it.
/// </summary>
public sealed record StageOutcome(
    StageStatus Status,
    object? Payload = null,
    bool NeedsHumanReview = false,
    IReadOnlyList<Flag>? Flags = null,
    string? Message = null)
{
    public static StageOutcome Completed(object payload, IReadOnlyList<Flag>? flags = null) =>
        new(StageStatus.Completed, payload, false, flags);

    public static StageOutcome Review(object payload, IReadOnlyList<Flag>? flags = null, string? message = null) =>
        new(StageStatus.NeedsReview, payload, true, flags, message);

    public static StageOutcome Skipped(string message, IReadOnlyList<Flag>? flags = null) =>
        new(StageStatus.Skipped, null, false, flags, message);
}
