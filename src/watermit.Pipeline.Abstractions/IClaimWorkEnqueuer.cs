using System.Threading;
using System.Threading.Tasks;
using Watermit.Contracts;

namespace Watermit.Pipeline.Abstractions;

/// <summary>
/// The <b>producer</b> side of the intake queue: writes a Pending row a worker can later claim. Used by
/// a client's <see cref="IPipelineDispatcher"/> (the MVC "Submit", a REST endpoint) to enqueue work
/// across the process boundary. Separate from <see cref="IClaimWorkSource"/> (the consumer/worker side)
/// so a front-end App Service takes only this narrow producer dependency — it never polls or claims.
/// </summary>
public interface IClaimWorkEnqueuer
{
    Task EnqueueAsync(ClaimWorkRow row, CancellationToken ct = default);
}
