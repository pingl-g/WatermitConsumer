# Claims Manager Simulator

A test client for **`POST /Claim/{claimId:long}/attachments`** on `SampleClient.Web`.

In production, the user opens a claim in **Claims Manager** and clicks **Launch Sample Client**.
Claims Manager then performs a top-level browser form POST into `SampleClient.Web`, which parses the
payload and shows the **Send File for AI Insights** grid. This project stands in for Claims Manager so
that hand-off can be exercised locally, including the failure cases.

It exists because the interesting part of the contract is a **real cross-origin, top-level form POST** —
something `curl` and `WebApplicationFactory` tests cannot reproduce faithfully. A browser is the thing
under test as much as the endpoint is.

---

## Deliberately reference-free

This project has **no `ProjectReference`** — not even to `SampleClient.Web`. It knows the endpoint only
by its URL and the shape of the form post, exactly as the real Claims Manager does. Sharing a DTO
assembly would make the two sides agree by construction and hide contract drift, which is the main
thing this harness is meant to catch.

---

## Prerequisites

- .NET 8 SDK
- A running `SampleClient.Web` (see the [root README](../../README.md) for its storage configuration)

---

## Configure

`appsettings.json`:

```json
"SampleClient": {
  "BaseUrl": "http://localhost:55555"
}
```

This is only the default shown in the form — the base URL is editable on the page, so you can retarget
a deployed environment without restarting.

| App | HTTPS | HTTP |
|---|---|---|
| `SampleClient.Web` | 55554 | 55555 |
| `ClaimsManager.Simulator` | 55556 | 55557 |

> **Use the same scheme on both.** A form POST from an `https://` page to an `http://` endpoint is
> mixed content and browsers block it silently. Either stay on HTTP for both (the default), or set
> `BaseUrl` to `https://localhost:55554` when you run the simulator over HTTPS.

---

## Run

Start `SampleClient.Web` first, then the simulator:

```bash
dotnet run --project src/SampleClient.Web
```

```bash
dotnet run --project src/ClaimsManager.Simulator
```

Open the simulator, pick a scenario, and click **Launch Sample Client →**. Each scenario states the
expected result above the form, so a run is pass/fail by inspection.

`SampleClient.Web` opens in a **popup window**, and every relaunch reuses that same popup rather than
piling up windows. The mechanic is a fixed window name (`sampleClientWindow`): the form targets that
name, and `window.open` with an existing name returns the existing window instead of creating one.
Because the features string is ignored on reuse, a popup you have moved or resized stays where you
put it. The simulator page itself never navigates, so your payload edits survive a launch.

> **Allow popups for the simulator's origin.** If the browser blocks it, the page says so and does
> not post — nothing is lost, but nothing happens either until popups are permitted.

---

## The contract under test

**Route** — `claimId` is a SQL **bigint** in Claims Manager, so the constraint is `:long`, not `:int`:

```
POST {BaseUrl}/Claim/{claimId:long}/attachments
Content-Type: application/x-www-form-urlencoded
```

**Form fields** — a top-level form post cannot send `application/json`, so the payload travels as a
string in a form field:

| Field | Meaning |
|---|---|
| `classId` | passed through by Claims Manager |
| `payload` | the JSON document below, as a string |

**Payload:**

```json
{
  "claimId": 123,
  "claimNumber": "060034262-1",
  "files": [
    {
      "fileUrl": "tst2/OutboundDocuments/bulk update check status.png",
      "filename": "bulk update check status.png",
      "fileSize": 56643,
      "dateUploaded": "2026-09-10T02:05:00-04:00",
      "updatedBy": "Tina Smitch",
      "documentType": "Supporting File",
      "comments": ""
    }
  ]
}
```

Notes on the field types, which the scenarios below pin down:

- **`claimId`** is int64. It appears in both the route and the body; the **route value is
  authoritative** and a mismatch is rejected rather than silently preferred.
- **`claimNumber`** is a string with punctuation (`060034262-1`), never numeric.
- **`fileUrl`** is a `{container}/{path}` pair relative to the Claims Manager storage account — *not*
  an absolute URL, despite the name.
- **`fileSize`** is bytes; the grid renders KB.
- **`dateUploaded`** carries a UTC offset, so it maps to `DateTimeOffset`, not `DateTime`.

---

## Scenarios

| Scenario | Expected in `SampleClient.Web` |
|---|---|
| **Spec example (2 files)** | 2 rows, newest first, pager reads "2 items in 1 page" |
| **11 files (paging)** | "11 items in 3 pages"; select-all ticks only the 5 on the current page |
| **Bigint claimId** | `8000000000123` binds and renders — overflows `int`, so an `:int` route would 404 |
| **claimId mismatch** | Red error, no grid: route wins over the body |
| **Malformed JSON** | Red error naming the parse failure — must not be a 500 |
| **Empty files array** | "No attachments on this claim." and no pager |
| **Special characters** | Filenames render as inert text; quotes, `&`, accents and CJK survive |
| **Empty payload field** | Red error: "The payload form field was empty." |

Scenarios are data, not code paths — see `Models/LaunchScenario.cs`. Add one by appending to
`Scenarios.Build()`; the picker, the payload editor and the expectation banner pick it up with no
other change.

The payload textarea is freely editable, so anything not covered above can be tested by hand. The
page flags invalid JSON client-side before sending, but still sends it — that is the point of the
malformed case.

---

## What this does *not* cover

- **File bytes.** The payload carries metadata only. Nothing fetches the blobs named by `fileUrl`, so
  the grid's file names are inert text rather than download links.
- **The grid's Submit button.** `SendForInsights` in `SampleClient.Web` echoes the selection as plain
  text; it does not yet validate each `fileUrl`, copy the blobs in, or call `CreateCaseAsync` /
  `EnqueueAsync`.
- **Authentication.** The endpoint carries `[IgnoreAntiforgeryToken]` because this is a cross-app POST.
  Authenticating the calling application is still an open item.
