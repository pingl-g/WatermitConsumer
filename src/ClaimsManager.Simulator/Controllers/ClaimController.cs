using ClaimsManager.Simulator.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsManager.Simulator.Controllers;

/// <summary>
/// Stands in for the Claims Manager claim page. The only thing that matters here is the
/// shape of the hand-off: a top-level browser form POST to
/// {SampleClient:BaseUrl}/Claim/{claimId}/attachments carrying "token" and "payload".
/// Nothing is referenced from SampleClient.Web — this is a genuine cross-app client.
/// </summary>
public sealed class ClaimController : Controller
{
    private readonly IConfiguration _config;

    public ClaimController(IConfiguration config) => _config = config;

    // GET /  — the claim page with the Launch button and a payload editor.
    [HttpGet]
    public IActionResult Index(string? scenario)
    {
        var baseUrl = (_config["SampleClient:BaseUrl"] ?? "http://localhost:55555").TrimEnd('/');
        var picked = Scenarios.Find(scenario) ?? Scenarios.Default;
        return View(new LaunchVm(baseUrl, picked, Scenarios.All));
    }
}

public sealed record LaunchVm(
    string BaseUrl, LaunchScenario Selected, IReadOnlyList<LaunchScenario> All);
