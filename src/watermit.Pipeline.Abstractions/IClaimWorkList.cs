using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Watermit.Contracts;

namespace Watermit.Pipeline.Abstractions;

/// <summary>
/// Lists intake rows for a dashboard/list view — newest first. Separate from the worker's
/// <see cref="IClaimWorkSource"/> (which only yields claimable rows) and the single-row
/// <see cref="IClaimStatusReader"/>. Read-only.
/// </summary>
public interface IClaimWorkList
{
    Task<IReadOnlyList<ClaimWorkRow>> ListAsync(int max = 200, CancellationToken ct = default);
}
