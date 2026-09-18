using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Watermit.Contracts;

namespace Watermit.Pipeline.Abstractions;

/// <summary>Reads the bytes/pages of a document belonging to the current Case (blob-backed).</summary>
public interface IDocumentAccessor
{
    Task<Stream> OpenOriginalAsync(string docId, CancellationToken ct = default);
}

/// <summary>
/// Reads prior stage artifacts and persists this stage's artifact. The concrete
/// implementation (Storage layer) reads/writes OUT~{stageId} rows/blobs in the Case partition.
/// </summary>
public interface IStageOutputStore
{
    Task<StageEnvelope<T>?> ReadAsync<T>(string stageId, CancellationToken ct = default);
    Task WriteAsync<T>(StageEnvelope<T> envelope, CancellationToken ct = default);

    /// <summary>
    /// Persist a raw text byproduct of a stage (e.g. a document's layout markdown) alongside the
    /// JSON artifacts, so downstream consumers and the frontend can read it. <paramref name="name"/>
    /// is a relative file name such as "Cooper_WM_Invoice.md".
    /// </summary>
    Task WriteTextAsync(string name, string content, CancellationToken ct = default);
}

/// <summary>Everything a stage needs to run: which Case, how to read inputs, how to read documents.</summary>
public sealed class StageContext
{
    public StageContext(
        CaseRef @case,
        IStageOutputStore outputs,
        IDocumentAccessor documents,
        IReadOnlyList<string>? documentIds = null)
    {
        Case = @case;
        Outputs = outputs;
        Documents = documents;
        DocumentIds = documentIds ?? System.Array.Empty<string>();
    }

    public CaseRef Case { get; }
    public IStageOutputStore Outputs { get; }
    public IDocumentAccessor Documents { get; }

    /// <summary>
    /// The raw uploaded documents' ids (the CaseDocument inventory), supplied by the runner. Needed by
    /// the intake stage (A-1), which classifies each one; other stages read their inputs via <see cref="Outputs"/>.
    /// </summary>
    public IReadOnlyList<string> DocumentIds { get; }
}

/// <summary>
/// A single unit of the pipeline (a phase / step / segment). Implementations do ONLY the
/// transform: read declared inputs via context, return a payload + control flags. The runner
/// handles envelope creation, InputsHash/staleness, human-review gating, persistence, and telemetry.
/// </summary>
public interface IPipelineStage
{
    /// <summary>Stable id, also the OUT~{StageId} artifact key (e.g. "A1", "A23", "B", "C", "E").</summary>
    string StageId { get; }

    /// <summary>Schema version of this stage's output artifact; stamped onto the envelope.</summary>
    string OutputSchemaVersion { get; }

    /// <summary>StageIds whose artifacts this stage reads. Drives precondition checks and InputsHash.</summary>
    IReadOnlyCollection<string> RequiredInputs { get; }

    Task<StageOutcome> RunAsync(StageContext context, CancellationToken ct = default);
}

/// <summary>
/// Executes a stage for a Case: loads inputs, checks preconditions + InputsHash (skip if
/// unchanged), runs the stage, wraps the payload in a StageEnvelope, applies gates, and persists.
/// Sequence per ProcessType is declarative (PhasePipelineJson). Implementation comes later.
/// </summary>
public interface IStageRunner
{
    Task<StageOutcome> RunAsync(string stageId, CaseRef caseRef, CancellationToken ct = default);
}
