# AI Business Analyst Agent

A serverless, Azure-native reference implementation that turns structured
business data (CSV / Excel) into executive-level insights, risks, and
recommended actions — built on a **deterministic-before-probabilistic**
design so the language model narrates pre-computed facts rather than
calculating them.

> **Status:** Working reference implementation and portfolio build.
> The core pipeline runs end-to-end. It is **not** production-deployed,
> and parts of the design described under *Roadmap* are not yet built.

---

## The core idea

Dashboards show metrics; they rarely answer *"what should I do next?"*
This project does — but with a deliberate guardrail for regulated and
data-sensitive contexts:

> **Raw data is never sent to the LLM.** Statistics and anomalies are
> computed deterministically first; the model receives a bounded,
> pre-computed summary and narrates insight from it. It does not see the
> dataset and does not do the arithmetic.

This is the design's main point of differentiation: the numbers are
trustworthy because they are computed in code, and the LLM is constrained
to interpretation only.

---

## What actually runs today

The pipeline is a two-step HTTP flow over Azure Functions (.NET 8,
isolated worker):

1. **`POST /api/jobs`** — registers a job for a previously uploaded blob
   and returns a `JobId`. The caller sends a blob reference, not raw data.
2. **`POST /api/jobs/{jobId}/process`** — runs the pipeline for that job.
3. **`GET /api/jobs/{jobId}`** — returns job status and insight
   availability (read-only).

Processing (step 2) executes three stages:

- **Signal Extraction (deterministic)** — reads the blob, parses CSV/Excel,
  infers column types, computes per-column statistics, and detects
  anomalies. Output is a structured `BusinessSignalsV1` contract. No raw
  rows leave this stage.
- **Signal Summarization (deterministic)** — reduces the full signal set to
  a bounded summary sized for the model, so prompt size stays controlled.
- **Insight Reasoning (Azure OpenAI)** — sends the bounded summary to the
  model and parses a structured JSON response into `BusinessInsightsV1`.
  Includes adaptive output-token sizing, token-usage capture
  (input / output / total), and typed error handling.

### Reliability characteristics (implemented)

The processing function is built to be safe to call repeatedly:

- **Idempotency** — a completed job returns `409 Conflict` rather than
  re-running.
- **Concurrency-safe state transition** — `Pending → Processing` is guarded
  so two callers cannot process the same job at once.
- **Timeout safeguard** — processing is bounded by a 90-second linked
  cancellation token.
- **Explicit failure state** — failures transition the job to `Failed`.

---

## Architecture

![Architecture](./docs/ai-business-analyst-agent-architecture.svg)

| Layer | Responsibility |
|-------|----------------|
| `src/Contracts` | Versioned (`*V1`) contracts shared across stages |
| `src/FunctionApp` | Azure Functions: parsing, deterministic analysis, agents, persistence |
| `database/` | SQL schema for jobs, signals, and insights |
| `docs/` | Architecture, design decisions, responsible-AI notes |
| `samples/` | Sample input datasets |

**Stack:** Azure Functions (Consumption) · Azure OpenAI · Azure Blob
Storage · SQL database · GitHub Actions (CI/CD).

---

## Scope and current limitations

This is a focused reference build, scoped deliberately to the
deterministic-to-probabilistic handoff. Known boundaries:

- **Orchestration is manual.** The submit → process flow is triggered by
  explicit HTTP calls. There is no automated orchestrator or retry policy
  yet (see *Roadmap*).
- **CSV parsing handles simple delimited files.** Fields containing the
  delimiter inside quotes are not yet parsed correctly.
- **Numeric parsing is not yet locale-aware.** Type inference and statistics
  should share a single, explicit culture; aligning them is a known fix.
- **No automated test suite yet.** See *Roadmap*.

These are tracked, not hidden — the design is honest about what it does and
does not yet guarantee.

---

## Roadmap

Planned, not yet implemented:

- **Automated orchestration** (e.g. Logic Apps or a durable orchestrator)
  to replace the manual process trigger, with retry and lifecycle handling.
- **Test suite** — deterministic unit tests for parsing, type inference,
  statistics, and anomaly detection (including the quoted-field and
  locale-aware-numeric edge cases above), plus contract-compatibility tests.
- **Robust CSV parsing** via a dedicated parser (quoted fields, embedded
  delimiters, embedded newlines).
- **Explicit culture handling** unified across inference and statistics.
- **Expanded sample datasets and deployment instructions.**

---

## Responsible-AI posture

- Deterministic preprocessing precedes all AI reasoning.
- No raw data is sent to the model — only a bounded, pre-computed summary.
- Structured prompts and structured-JSON parsing constrain model output.
- Signals and insights are persisted for auditability.
- Contracts are versioned for backward compatibility.

See `docs/responsible-ai.md` for detail.

---

## Author

**Vinod Patil** — Lead Data & AI Engineer

[LinkedIn](https://www.linkedin.com/in/vinodrpatil/) ·
[GitHub](https://github.com/vinodrpatil-datafusion)

Originally built for the Microsoft AI Dev Days Global Hackathon 2026.
