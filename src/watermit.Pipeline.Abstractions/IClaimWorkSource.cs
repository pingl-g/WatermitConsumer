using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Watermit.Contracts;

namespace Watermit.Pipeline.Abstractions;

/// <summary>
/// A work row paired with the opaque optimistic-concurrency token (the storage ETag) that a
/// claim / renew / complete transition must present. The token is round-tripped verbatim — callers
/// never parse it, so this interface carries no Azure dependency and any host can consume it.
/// </summary>
public sealed record ClaimWork(ClaimWorkRow Row, string ConcurrencyToken);

/// <summary>
/// The <b>pull</b> trigger seam — the intake queue the worker polls. Sibling to
/// <see cref="IPipelineDispatcher"/> (the <b>push</b> seam a client uses to enqueue). In the
/// distributed topology the worker and every client run in separate App Services, so they meet only in
/// durable storage: a client writes a Pending row (+ uploads files to blob), the worker claims and runs.
/// "Claiming" is an atomic ETag If-Match update, so multiple worker instances can poll the same table
/// and only one wins a given row — no queue, no message lock to lose on a big/slow claim. A crashed
/// worker's lease expires and the row becomes claimable again; because each stage persists its
/// artifact, a reclaimed run resumes from the last completed stage rather than redoing LLM work.
/// </summary>
public interface IClaimWorkSource
{
    /// <summary>Human-readable identity of the backing store (for logs).</summary>
    string Name { get; }

    /// <summary>
    /// Rows a worker may take: <see cref="ClaimStatus.Pending"/>, or Claimed/Processing rows whose lease
    /// has expired (a crashed worker). Up to <paramref name="max"/> rows.
    /// </summary>
    Task<IReadOnlyList<ClaimWork>> GetClaimableAsync(int max, CancellationToken ct = default);

    /// <summary>
    /// Atomically take one row (ETag If-Match): Status -> Claimed, lease set to now + <paramref name="lease"/>,
    /// Attempts incremented. Returns the updated work with a fresh token on success; <c>null</c> if another
    /// worker won the race (the ETag no longer matches).
    /// </summary>
    Task<ClaimWork?> TryClaimAsync(ClaimWork work, string owner, TimeSpan lease, CancellationToken ct = default);

    /// <summary>
    /// Extend the lease while processing (heartbeat); also moves Status -> Processing on the first call.
    /// Returns the refreshed work, or <c>null</c> if the lease was lost (the row was reclaimed elsewhere) —
    /// the caller should stop, since another worker now owns it.
    /// </summary>
    Task<ClaimWork?> RenewAsync(ClaimWork work, TimeSpan lease, CancellationToken ct = default);

    /// <summary>
    /// Terminal transition: Status -> <paramref name="terminal"/>, lease cleared,
    /// <see cref="ClaimWorkRow.LastError"/> set from <paramref name="error"/> on failure.
    /// </summary>
    Task CompleteAsync(ClaimWork work, ClaimStatus terminal, string? error = null, CancellationToken ct = default);
}
