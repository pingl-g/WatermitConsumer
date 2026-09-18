using System.Threading;
using System.Threading.Tasks;
using Watermit.Contracts;

namespace Watermit.Pipeline.Abstractions;

/// <summary>
/// The client-side <b>status-polling</b> seam: a cheap point-read of one claim's intake row, so a
/// front-end can follow a claim across the process boundary (Pending → … → Completed/NeedsReview/Failed)
/// without ever touching the pipeline. Paired with <see cref="IClaimWorkEnqueuer"/> (the push side);
/// together they are all a client App Service needs.
/// </summary>
public interface IClaimStatusReader
{
    /// <summary>The claim's current row, or <c>null</c> if no intake row exists for it.</summary>
    Task<ClaimWorkRow?> GetAsync(string tenant, string caseId, CancellationToken ct = default);
}
