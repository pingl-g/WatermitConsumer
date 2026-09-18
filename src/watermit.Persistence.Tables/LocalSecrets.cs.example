// TEMPORARY hardcoded storage credential — a stopgap until the App Service Managed Identity is granted
// Storage Blob/Table Data roles (no RBAC access yet). Copy this file to "LocalSecrets.cs" (same folder)
// and paste the storage account connection string. LocalSecrets.cs is git-ignored, so the secret is not
// committed, but it DOES compile into the app and publish with it.
//
// Leave the value empty to use Managed Identity (DefaultAzureCredential) instead. When RBAC is granted,
// delete LocalSecrets.cs and the ConnectionStringOrFallback method that reads it.
namespace Watermit.Persistence.Tables;

internal static class LocalSecrets
{
    public const string StorageConnectionString = "";
}
