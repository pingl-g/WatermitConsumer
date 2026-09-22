# Water-Mitigation — Consumer Kit

A self-contained starter for building a **new front-end** (MVC / REST / console) that submits claims to
the water-mitigation pipeline and shows results. It contains the client-side backend projects plus a
working **sample MVC app** (`SampleClient.Web`).

The worker (the App Service that actually runs the A1→F pipeline) is **not** in this kit and is not
needed to build or run a client. A client and the worker meet only in **Azure Storage**.

---

## What's inside

```
watermit-consumer-kit/
├─ WatermitConsumer.sln
├─ README.md
└─ src/
   ├─ watermit.Contracts/               # ClaimWorkRow, ClaimStatus, CaseRef, taxonomy, enums
   ├─ watermit.Domain/                  # CaseHeader, CaseDocument, CaseAggregate
   ├─ watermit.Persistence.Abstractions/# ICaseRepository, IBlobStore
   ├─ watermit.Pipeline.Abstractions/   # IPipelineDispatcher, IClaimStatusReader, IClaimWorkList
   ├─ watermit.Persistence.Tables/      # Azure Tables + Blob implementation + DI registration
   ├─ SampleClient.Web/                 # ← the sample consumer app (start here)
   └─ ClaimsManager.Simulator/          # test client for the Claims Manager hand-off (see its README)
```

These five `watermit.*` projects are the **entire** client dependency set. A consumer must **never**
reference the worker-only projects (`watermit.Processing`, `watermit.Extraction`, `watermit.Stages.*`,
`watermit.Pipeline`, `watermit.Rules`, `watermit.Calculation`).

---

## Prerequisites

- .NET 8 SDK
- Access to the **same Azure Storage account** the worker polls (or any storage account, if you also
  run a worker against it). You need the account to hold: a blob container, a `Cases` table, and a
  `ClaimWork` table — the app creates the container/tables on first run.

---

## Configure

Edit `src/SampleClient.Web/appsettings.json` → the `Storage` section:

```json
"Storage": {
  "AccountName": "REPLACE_WITH_STORAGE_ACCOUNT",
  "ConnectionString": "",
  "BlobContainer": "watermitigation",
  "CasesTable": "Cases",
  "ClaimWorkTable": "ClaimWork",
  "DefaultTenantId": "genpact"
}
```

**Authentication — pick one:**

| How | Set |
|---|---|
| **Managed Identity / `az login`** (preferred) | leave `ConnectionString` empty; set `AccountName`. The app uses `DefaultAzureCredential`. Locally, run `az login`; in Azure, enable a Managed Identity with **Storage Blob Data Contributor** + **Storage Table Data Contributor**. |
| **Connection string** (quick local dev) | put the account connection string in `ConnectionString`. `AccountName` is then ignored. |

> The `BlobContainer` / `DefaultTenantId` **must match the worker's**, or the worker won't see the
> claims this app submits and this app won't find the worker's outputs.

There is also a `src/watermit.Persistence.Tables/LocalSecrets.cs` (a temporary hardcoded-key fallback
used by the original app). It ships **empty** here — leave it empty and use the config above.

---

## Run

```bash
dotnet run --project src/SampleClient.Web
```

Open the printed URL. Then:

1. **Submit** — enter a claim number, pick the documents, submit. The app creates a Case, uploads the
   files to blob, and enqueues a `Pending` row for the worker. It redirects to the status page.
2. **Status** — auto-refreshes; shows the claim status (`Pending → Claimed → Processing → NeedsReview |
   Completed | Failed`) and ticks off each stage artifact as the worker writes it.
3. **View JSON** — opens a produced stage's raw artifact (`work/OUT~{stage}.json`).

(For anything to *process*, a worker must be running against the same storage account. Without a worker,
submitted claims simply stay `Pending` — which still proves the client side works.)

---

## The Claims Manager hand-off

Besides its own submit form, `SampleClient.Web` accepts claims launched from **Claims Manager**, an
existing web app. Clicking **Launch Sample Client** there performs a top-level browser form POST:

```
POST /Claim/{claimId:long}/attachments     fields: classId, payload (JSON as a string)
```

`SampleClient.Web` parses the payload and renders the **Send File for AI Insights** grid of the
claim's attachments. To exercise this locally — including the mismatch, malformed-payload and
bigint-`claimId` cases — run `src/ClaimsManager.Simulator`, which stands in for Claims Manager.
See [its README](src/ClaimsManager.Simulator/README.md) for the full contract and scenario list.

---

## How the integration works (the whole thing)

**Registration** — `src/SampleClient.Web/Program.cs`:

```csharp
builder.Services.AddWatermitPersistence(builder.Configuration);  // storage + repos + StorageOptions
builder.Services.AddWatermitTableDispatcher();                   // the client seam
await app.Services.InitWatermitPersistenceAsync();               // ensure container + tables
```

`AddWatermitTableDispatcher()` registers everything a client uses: `IPipelineDispatcher`,
`IClaimStatusReader`, `IClaimWorkList`, `IClaimWorkEnqueuer`. **A client never calls
`AddWatermitWorker()`** — that wires the full pipeline and belongs only to the worker.

**Submit** (see `Controllers/ClaimController.Submit`):

```csharp
await _cases.CreateCaseAsync(new CaseHeader { TenantId = tenant, CaseId = caseId,
    ProcessTypeId = WaterMitigationTaxonomy.ProcessTypeId, Status = CaseStatus.Draft, ... });   // (a) case
await _blobs.UploadOriginalAsync(tenant, caseId, docId, fileName, stream, contentType);          // (b) files
await _dispatcher.EnqueueAsync(new CaseRef(tenant, caseId, WaterMitigationTaxonomy.ProcessTypeId)); // (c) hand off
```

`EnqueueAsync` writes a self-describing `Pending` row and returns immediately — no waiting on the
pipeline.

**Read results** (see `Controllers/ClaimController.Status` / `Stage`):

```csharp
ClaimWorkRow? row = await _status.GetAsync(tenant, caseId);         // status + attempts + last error
// artifacts: {tenant}/{caseId}/work/OUT~{stage}.json  for A1, BMD, A23, B, C, DEHU, E, F
```

Each artifact is a `StageEnvelope<T>` (schema version, produced-UTC, flags, payload). Presence of a
stage's blob also tells you how far the run has progressed.

---

## One-line summary

Reference the five `watermit.*` projects, call `AddWatermitPersistence()` +
`AddWatermitTableDispatcher()`, point `Storage` at the worker's storage account, then **create case →
upload files → `IPipelineDispatcher.EnqueueAsync`**, and read back with **`IClaimStatusReader` +
`work/OUT~{stage}.json`**. Never reference the worker/pipeline projects.
