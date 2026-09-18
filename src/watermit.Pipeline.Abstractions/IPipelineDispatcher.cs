using System.Threading;
using System.Threading.Tasks;
using Watermit.Contracts;

namespace Watermit.Pipeline.Abstractions;

/// <summary>
/// Hands a Case off to background execution so the MVC request thread returns immediately.
/// The concrete implementation (Hangfire today) enqueues a job that drives the pipeline via
/// <see cref="IStageRunner"/>. Swappable — a queue/Durable-Functions dispatcher could implement
/// the same interface with no change to callers.
/// </summary>
public interface IPipelineDispatcher
{
    /// <summary>Enqueue the pipeline for a Case. Returns a background job/tracking id.</summary>
    Task<string> EnqueueAsync(CaseRef caseRef, CancellationToken ct = default);
}
