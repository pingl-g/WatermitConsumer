using Watermit.Persistence.Tables;

// ─────────────────────────────────────────────────────────────────────────────
// Sample MVC consumer of the water-mitigation pipeline.
//
// The entire integration is these three lines + pointing "Storage" at the same
// account the worker polls. This app NEVER references the worker/pipeline code —
// it only writes work (files + a Pending row) and reads results (status + the
// work/OUT~{stage}.json artifacts) through Azure Storage.
// ─────────────────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddWatermitPersistence(builder.Configuration);   // TableServiceClient + BlobServiceClient + StorageOptions + repos
builder.Services.AddWatermitTableDispatcher();                    // the CLIENT seam: IPipelineDispatcher + IClaimStatusReader + IClaimWorkList

var app = builder.Build();

// Ensure the blob container + Cases table exist (the ClaimWork intake table self-creates on first use).
await app.Services.InitWatermitPersistenceAsync();

app.UseStaticFiles();
app.MapControllers();                                             // attribute routes, e.g. /Claim/{claimId}/attachments
app.MapControllerRoute(name: "default", pattern: "{controller=Claim}/{action=Index}/{id?}");

app.Run();
