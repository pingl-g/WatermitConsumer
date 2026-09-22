// ─────────────────────────────────────────────────────────────────────────────
// Claims Manager simulator — a test client for SampleClient.Web's
// POST /Claim/{claimId:long}/attachments endpoint.
//
// It exists to exercise the one thing curl and unit tests cannot reproduce
// faithfully: a real cross-origin, top-level browser form POST, the way the
// production Claims Manager "Launch Sample Client" button behaves.
//
// Point "SampleClient:BaseUrl" at a running SampleClient.Web and open this app.
// ─────────────────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.MapControllerRoute(name: "default", pattern: "{controller=Claim}/{action=Index}/{id?}");

app.Run();
