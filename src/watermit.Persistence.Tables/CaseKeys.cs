using System;

namespace Watermit.Persistence.Tables;

/// <summary>
/// Azure Table key conventions — a TABLES-specific concern, so it lives in the Tables provider
/// (not in Domain). Every row of one Case shares PartitionKey = "{TenantId}|{CaseId}" so the whole
/// case loads in a single partition query and can be written in one transaction. A SQL provider
/// would instead use TenantId/CaseId columns and never reference this type.
/// </summary>
internal static class CaseKeys
{
    // '|' is a legal Table key char; '/', '\\', '#', '?' are not.
    public const char Delimiter = '|';

    public const string HeaderRowKey = "CASE";
    public const string SettlementRowKey = "SETTLEMENT";

    public static string Partition(string tenantId, string caseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        return $"{tenantId}{Delimiter}{caseId}";
    }

    /// <summary>RowKey for a document row, e.g. ordinal 1 -> "DOC~001".</summary>
    public static string DocumentRowKey(int ordinal) => $"DOC~{ordinal:000}";

    public static bool IsDocumentRowKey(string rowKey) =>
        rowKey is not null && rowKey.StartsWith("DOC~", StringComparison.Ordinal);
}
