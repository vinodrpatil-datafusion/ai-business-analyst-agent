# CLAUDE.md

Guidance for Claude Code when working in this repository. Read this before proposing edits.

## What this is

**AI Business Analyst Agent** — a serverless, Azure-native .NET 8 pipeline that turns
uploaded CSV/Excel files into executive-level business insights. A user uploads a file to
Blob Storage, submits a job, deterministic C# extracts statistical *signals*, those signals
are summarized, and only the summary is sent to Azure OpenAI to produce insights. Results are
persisted to Azure SQL and queried asynchronously.

Solution: `AiBusinessAnalyst.slnx` (new XML solution format) — two projects:
- `src/Contracts` — versioned, immutable DTOs (the contract layer)
- `src/FunctionApp` — Azure Functions (isolated worker) with the agent logic

---

## Golden rules (do not violate without explicit instruction)

1. **Deterministic before probabilistic.** All math, statistics, type inference, and anomaly
   detection happen in C# (`src/FunctionApp/Analysis/*`). The LLM never computes numbers.
   If a calculation is needed, add it to the deterministic layer — not to a prompt.

2. **Raw data never crosses the signal boundary.** The model (`InsightReasoningAgent`) receives
   **only** an `InsightSignalSummaryV1`. Never pass raw file rows, full file contents, or even
   the complete `BusinessSignalsV1` to Azure OpenAI. If you need the model to "see" something
   new, surface it as a field on the summary, computed deterministically first.

3. **Contracts are versioned and immutable.** Any type in `src/Contracts` whose name ends in
   `V1` is frozen. Do **not** add, rename, remove, or retype fields on an existing `V#` record.
   To change shape, create a new `V2` alongside it and migrate callers. This protects backward
   compatibility for API consumers.

4. **Persistence is append-only.** Signals and insights are inserted, never updated in place
   (`SignalStore`, `InsightStore`). Do not introduce `UPDATE`/`DELETE` against the
   `BusinessSignals` or `BusinessInsights` tables. The `Jobs` table is the only mutable record,
   and only via its concurrency-safe lifecycle transitions.

5. **LLM output is untrusted.** Keep the existing guardrails in `InsightReasoningAgent`: JSON
   object validation, required-property checks, array-type checks, markdown rejection, and size
   limits. Do not weaken these. If you change the expected schema, update the validation and the
   prompt template together.

6. **One responsibility per agent.** Agents implement `IAgent<TInput, TOutput>` and do one thing.
   Don't merge extraction, summarization, and reasoning. Keep file-format logic in `Parsing/*`,
   not in agents.

7. **Multi-tenancy is on the roadmap.** When touching data paths, prefer designs that can later
   carry a `tenantId` cleanly. Don't add patterns that would be hard to scope per-tenant.

---

## Build, run, and tooling

```bash
# Build the solution (.NET 8)
dotnet build AiBusinessAnalyst.slnx

# Run the Functions host locally (requires Azure Functions Core Tools v4)
cd src/FunctionApp
func start
```

- Target framework: `net8.0`, Azure Functions v4, isolated worker model.
- `Nullable` and `ImplicitUsings` are enabled in both projects — keep new code null-aware.
- Local run needs a `local.settings.json` in `src/FunctionApp` (git-ignored, see Configuration).
- **Tests:** there is no test project in the solution yet. The intended strategy is
  contract-safety tests, deterministic signal-extraction tests, and read-only status-query tests.
  If you add tests, create a separate test project and reference it from `AiBusinessAnalyst.slnx`;
  do not add test code into `FunctionApp` or `Contracts`.

---

## Data flow

```
User → Blob Storage (raw file)
     → SubmitJob        (registers Job, status = Pending)
     → ProcessJob       (Pending → Processing, concurrency-safe lock)
          SignalExtractionAgent   (deterministic: parse → infer types → stats → anomalies)
             → BusinessSignalsV1   (persisted to BusinessSignals)
          InsightSignalSummarizer (compress to AI-safe summary)
             → InsightSignalSummaryV1   ◄── ONLY this crosses to the LLM
          InsightReasoningAgent   (Azure OpenAI → validated JSON)
             → BusinessInsightsV1  (persisted to BusinessInsights, with token/cost metadata)
          → status = Completed (or Failed on any exception)
     → GetJobStatus     (read-only; InsightsAvailable derived from BusinessInsights existence)
```

Job lifecycle: `Pending → Processing → Completed | Failed`. Transitions live in `JobStore` and
are concurrency-safe (atomic `Pending → Processing`). `ProcessJobFunction` enforces idempotency
(rejects already-Completed jobs) and a 90-second execution timeout. Preserve these guarantees.

---

## Project structure

```
src/Contracts/
  Signals/      BusinessSignalsV1, ColumnMetadataV1, InferredColumnType,
                InsightSignalSummaryV1, CategoryHighlightV1
  Insights/     BusinessInsightsV1
  Invocation/   SubmitJobRequestV1, SubmitJobResponseV1, JobStatusResponseV1
  Jobs/         JobStatuses

src/FunctionApp/
  Functions/    SubmitJobFunction, ProcessJobFunction, GetJobStatusFunction (HTTP triggers)
  Agents/       IAgent, SignalExtractionAgent, InsightReasoningAgent,
                JobStatusQueryAgent, InsightSignalSummarizer
  Analysis/     SchemaValidator, ColumnTypeInference, ColumnStatisticsCalculator, AnomalyDetector
  Parsing/      IFileParser, CsvFileParser, ExcelFileParser, FileParserFactory, BlobFileReader
  Persistence/  JobStore, SignalStore, InsightStore
  Configurations/ AIExecutionOptions
  Prompts/      insight.prompt.txt
  Program.cs    DI composition root

database/schema.sql   Jobs, BusinessSignals, BusinessInsights
docs/                 architecture, contracts, decisions, responsible-ai, demo-script
```

---

## Conventions

- **DI:** everything is wired in `Program.cs` as singletons. New services register there. Agents
  are resolved by their `IAgent<TIn, TOut>` interface.
- **AI execution budgeting:** `AIExecutionOptions` controls token limits and adaptive output
  budgeting. The agent throws if the prompt exceeds `MaxPromptTokens` or if no context window
  remains — keep that fail-fast behavior.
- **Prompt changes:** `Prompts/insight.prompt.txt` is a template with `{{Placeholder}}` tokens
  filled by `BuildPrompt`. If you add a placeholder, add the matching `.Replace(...)` and a
  corresponding field on the summary. Bump `PromptVersion` in `InsightReasoningAgent` when prompt
  semantics change — it's persisted for audit.
- **Type inference order matters** in `ColumnTypeInference` (Boolean → DateTime → Numeric →
  Text/Categorical). Don't reorder casually; it changes classification.
- **File parsing** is selected by extension in `FileParserFactory` (`.csv`, `.xlsx`, `.xls`).
  Add new formats here behind `IFileParser`, not inside agents.

---

## Configuration & secrets

Required config keys (read in `Program.cs`), supplied via `local.settings.json` locally and app
settings / Key Vault in Azure:

- `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, `AzureOpenAI:DeploymentName`
- `BlobConnectionString`
- `SqlConnectionString`
- `AIExecution:*` (optional overrides for `AIExecutionOptions`)

**Never** commit real credentials. `local.settings.json` is git-ignored and must stay that way.
Do not hardcode connection strings, keys, or endpoints in source. When you see a secret in
context, do not echo it back or write it to a file.

---

## When making changes

- Match the existing style: file-scoped namespaces, `sealed` records/classes, async with
  `CancellationToken` threaded through.
- Respect the contract/append-only rules above before refactoring data shapes.
- If a change spans the deterministic layer and the AI layer, update both plus the prompt and its
  validation in the same change, and explain the boundary impact.
- Prefer small, reviewable diffs. Flag any change that alters a `V1` contract, a persisted schema,
  the prompt schema, or the job lifecycle — these are high-blast-radius.

## Known constraints

- New repo / new VS 2026 + tooling; verify build locally after structural changes.
- CSV parser does simple delimiter splitting (no quoted-field/embedded-comma handling) — be aware
  before relying on it for messy data; treat parser hardening as a deliberate, tested change.
