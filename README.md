# AI-Powered Alcohol Label Verification

A prototype tool for TTB compliance agents that verifies an alcohol beverage label
against its application data. Upload a label photo (or a whole batch), enter the
expected fields, and get an instant, explainable pass/fail on brand name, class/type,
alcohol content, net contents, producer, country of origin, and the mandatory
**Government Health Warning**.

Built with **ASP.NET Core + Blazor (C#, .NET 9)** and a vision LLM
(**OpenAI** or **Azure OpenAI**), with a no-key **Demo mode** so it runs out of the box.

---

## Why it's built this way

The design follows directly from the stakeholder interviews:

| Stakeholder need | How this app addresses it |
| --- | --- |
| "If we can't get results back in ~5 seconds, nobody's going to use it." (Sarah) | One vision call per label; images are resized client-side before upload; a loading state keeps it responsive. |
| "Something my mother could figure out... clean, obvious, no hunting for buttons." (Sarah) | Two screens only, large controls, high-contrast color-coded verdicts (green/amber/red), plain-language explanations. |
| "Handle batch uploads" - importers dump 200-300 at once. (Sarah/Janet) | **Batch Verify** screen: CSV manifest + many images, concurrency-limited processing, live progress, CSV export. |
| "STONE'S THROW vs Stone's Throw is obviously the same thing - you need judgment." (Dave) | Fuzzy matching normalizes case/spacing/punctuation and uses similarity scoring, so trivial differences pass while real mismatches fail. |
| "The warning has to be exact... 'GOVERNMENT WARNING:' in all caps and bold." (Jenny) | The warning is checked against the canonical TTB text: exact wording per sentence, ALL-CAPS lead-in (case-sensitive), and an advisory bold check. |
| "Handle images that aren't perfectly shot - angles, glare." (Jenny) | A vision model reads the label; if it truly can't, the app returns a clear "request a better photo" message instead of a false pass. |
| ".NET on Azure; our firewall blocks outbound to a lot of domains." (Marcus) | Built on .NET; supports **Azure OpenAI** so inference can stay inside the Azure tenant rather than calling a public endpoint. |
| "For a prototype, don't store anything sensitive." (Marcus) | Nothing is persisted server-side; images are processed in memory and discarded. |

## How it works

```
Blazor UI  ->  LabelVerificationService  ->  ILabelExtractor (vision LLM)  ->  VerificationService (rules)  ->  UI
```

The key design decision: **the LLM only extracts verbatim text; deterministic C# code
decides pass/fail.** This keeps every verdict explainable and repeatable, and prevents
the model from "being lenient" on things like the exact warning wording.

## Tech stack

- **.NET 9**, ASP.NET Core, **Blazor Web App** (Interactive Server)
- **OpenAI** / **Azure OpenAI** .NET SDKs (`OpenAI`, `Azure.AI.OpenAI`) for vision
- No JavaScript framework; one small helper for CSV download
- No database - stateless prototype

## Project layout

```
LabelVerifier/
  Program.cs                     # DI + configuration
  appsettings.json               # Llm provider config (no secrets)
  Models/LabelModels.cs          # ExpectedFields, ExtractedFields, FieldResult, VerificationResult
  Services/
    GovernmentWarning.cs         # canonical TTB warning text + rules
    LlmOptions.cs                # config binding
    ILabelExtractor.cs           # extraction abstraction
    VisionLabelExtractor.cs      # OpenAI/Azure vision implementation
    StubLabelExtractor.cs        # no-key demo provider
    LabelExtractorFactory.cs     # picks provider from config
    VerificationService.cs       # fuzzy field matching + warning checks
    LabelVerificationService.cs  # orchestrates extract -> verify
    SimpleCsv.cs                 # dependency-free CSV parse/write
  Components/
    Pages/Home.razor             # Single verify
    Pages/Batch.razor            # Batch verify
    Shared/ResultCard.razor      # result rendering
    Shared/StatusBadge.razor
samples/                         # sample labels + batch manifest (see samples/README.md)
Dockerfile                       # container for deployment (Render/Azure/etc.)
```

---

## Getting started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)

### 1. Run in Demo mode (no key needed)

```bash
cd LabelVerifier
dotnet run
```

Open the URL shown (e.g. `http://localhost:5080`). A yellow "Demo mode" banner appears;
every image returns the sample `OLD TOM DISTILLERY` extraction so you can explore the full
UI and batch flow. To read real labels, configure a provider below.

### 2. Run with OpenAI

Provide a key via **user-secrets** (recommended for local dev) or environment variables.

Using user-secrets:
```bash
cd LabelVerifier
dotnet user-secrets init
dotnet user-secrets set "Llm:Provider" "OpenAI"
dotnet user-secrets set "Llm:Model" "gpt-4o"
dotnet user-secrets set "Llm:OpenAI:ApiKey" "sk-..."
dotnet run
```

Using environment variables (note the `__` section separator):
```bash
# PowerShell
$env:Llm__Provider="OpenAI"; $env:Llm__Model="gpt-4o"; $env:Llm__OpenAI__ApiKey="sk-..."; dotnet run
# bash
Llm__Provider=OpenAI Llm__Model=gpt-4o Llm__OpenAI__ApiKey=sk-... dotnet run
```

### 3. Run with Azure OpenAI

```bash
cd LabelVerifier
dotnet user-secrets set "Llm:Provider" "AzureOpenAI"
dotnet user-secrets set "Llm:Model" "gpt-4o"
dotnet user-secrets set "Llm:AzureOpenAI:Endpoint" "https://<resource>.openai.azure.com/"
dotnet user-secrets set "Llm:AzureOpenAI:Deployment" "<your-vision-deployment>"
dotnet user-secrets set "Llm:AzureOpenAI:ApiKey" "<azure-key>"
dotnet run
```

> If a provider is selected but its key/endpoint is missing, the app logs a warning and
> safely falls back to Demo mode instead of crashing.

### Configuration reference (`Llm` section)

| Key | Values / example | Notes |
| --- | --- | --- |
| `Llm:Provider` | `OpenAI`, `AzureOpenAI`, `Stub` | `Stub` = demo mode (default) |
| `Llm:Model` | `gpt-4o` | Must be vision-capable |
| `Llm:OpenAI:ApiKey` | `sk-...` | OpenAI only |
| `Llm:AzureOpenAI:Endpoint` | `https://<res>.openai.azure.com/` | Azure only |
| `Llm:AzureOpenAI:Deployment` | deployment name | Azure only |
| `Llm:AzureOpenAI:ApiKey` | key | Azure only |

No keys are committed. `appsettings.json` ships with empty values.

---

## Deploying

This is a **Blazor Server** app: it keeps a persistent WebSocket (SignalR) connection, so
it needs an always-on host - not a serverless platform. (Vercel is **not** suitable for
this reason.) A `Dockerfile` is included and honors the `PORT` env var used by most hosts.

**Render (simple, no Azure needed):**
1. Push this repo to GitHub.
2. New -> Web Service -> connect the repo -> Runtime: **Docker**.
3. Add environment variables for your provider (e.g. `Llm__Provider`, `Llm__Model`, `Llm__OpenAI__ApiKey`).
4. Deploy. Render sets `$PORT` automatically.

The same container runs on Azure App Service / Container Apps, Railway, or Fly.io.

Build/run the container locally:
```bash
docker build -t label-verifier .
docker run -p 8080:8080 -e Llm__Provider=OpenAI -e Llm__OpenAI__ApiKey=sk-... label-verifier
```

---

## Design decisions, assumptions & trade-offs

- **LLM extracts, code decides.** More predictable and auditable than asking the model for
  a verdict; the warning check in particular must be strict.
- **Fuzzy matching thresholds** (92% match / 80% review) are tuned for the take-home; they
  are constants in `VerificationService` and easy to adjust.
- **Bold detection is advisory.** Typographic weight can't be proven from text alone, so a
  suspected non-bold warning is flagged as "Review" rather than a hard fail.
- **~5s target** is met by resizing images client-side and making a single model call.
  Actual latency depends on the chosen model/provider.
- **Stateless.** Nothing is stored; suitable for a prototype and aligned with the "don't
  store anything sensitive" guidance. A production version would add auth, audit logging,
  and retention controls.
- **Demo mode** returns fixed sample data so the app is fully demoable without a key.

## Uploading to GitHub (for the target account)

This project is delivered as a plain folder so it can be published under any account.
On the account you want it to live under:

**Option A - GitHub web UI:** create a new empty repository, choose
"uploading an existing file", and drag in the project folder (excluding `bin/` and `obj/`).

**Option B - command line:**
```bash
cd Alc_Label_Verification
git init
git add .
git commit -m "Initial commit: AI-powered alcohol label verification prototype"
git branch -M main
git remote add origin https://github.com/<account>/<repo>.git
git push -u origin main
```
Because `git init` runs on the target machine/account, the commit history is authored
natively by that account.
