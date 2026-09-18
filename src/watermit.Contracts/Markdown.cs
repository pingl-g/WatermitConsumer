using System.Collections.Generic;

namespace Watermit.Contracts;

/// <summary>One document's extracted layout/vision markdown (legacy "Call B" output).</summary>
public sealed record DocMarkdown(string DocId, string? Category, string Markdown);

/// <summary>
/// Stage BMD output: per-document markdown for EVERY uploaded document (text docs and photo reports —
/// Claude reads images natively), plus the vendor "rooms" markdown (legacy "Call A"). A-2/3 and B read
/// this instead of re-extracting, so each raw document is extracted exactly once.
/// </summary>
public sealed record DocumentMarkdownSet(
    string CaseId,
    IReadOnlyList<DocMarkdown> Documents,
    string? RoomsMarkdown);
