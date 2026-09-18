namespace Watermit.Pipeline.Abstractions;

/// <summary>
/// Builds the per-case storage seams so a single long-lived runner/worker can process many cases
/// (each bound to its own <c>{tenant}/{caseId}/…</c> blob paths), even concurrently. Keeps the
/// <see cref="IClaimPipelineRunner"/> free of any blob/table knowledge — the concrete factory
/// (persistence layer) decides where a case's inputs and outputs live.
/// </summary>
public interface ICaseStoreFactory
{
    /// <summary>The stage-artifact store for one case (writes OUT~{stage}.json + byproducts).</summary>
    IStageOutputStore Outputs(string tenant, string caseId);

    /// <summary>The uploaded-document reader for one case.</summary>
    IDocumentAccessor Documents(string tenant, string caseId);
}
