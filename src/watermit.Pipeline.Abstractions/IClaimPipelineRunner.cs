using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Watermit.Contracts;

namespace Watermit.Pipeline.Abstractions;

/// <summary>One line of a pipeline run: the stage, how it ended, and any message.</summary>
public sealed record StageRunLine(string StageId, StageStatus Status, string? Message);

/// <summary>
/// Outcome of running the whole ProcessDefinition for one case. <see cref="NeedsReview"/> is set if any
/// stage gated for human review (the audit always does); <see cref="Failed"/> if a stage failed. A host
/// maps this to whatever its trigger needs (e.g. the worker maps it to a <see cref="ClaimStatus"/>).
/// </summary>
public sealed record PipelineRunResult(
    CaseRef Case, bool NeedsReview, bool Failed, IReadOnlyList<StageRunLine> Stages);

/// <summary>
/// Runs the entire declared pipeline (A1 → … → F) for one case — the host-agnostic orchestration seam.
/// Any client (background worker, REST endpoint, console) drives a case through the pipeline by calling
/// this; the trigger (table poll, HTTP, queue) stays outside it. Reads/writes go through the
/// <see cref="ICaseStoreFactory"/>, so the runner itself is unaware of blob/table specifics.
/// </summary>
public interface IClaimPipelineRunner
{
    Task<PipelineRunResult> RunCaseAsync(
        CaseRef caseRef, IReadOnlyList<string> documentIds, CancellationToken ct = default);
}
