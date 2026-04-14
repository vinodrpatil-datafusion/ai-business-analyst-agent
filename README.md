# AI Business Analyst Agent

---

## Overview

Enterprises generate large volumes of structured data across Excel, CSV, 
and operational systems — but converting that data into clear, 
actionable decisions remains a manual, time-intensive process.

Traditional dashboards show metrics — but they rarely answer the most 
important question:

> **"What should I do next?"**

**AI Business Analyst Agent** is a **serverless, Azure-native system** 
that transforms structured business data into **executive-level 
insights**, risks, opportunities, and recommended actions using 
**agent-oriented AI design**.

The system behaves like a **virtual business analyst** — reliable, 
explainable, and production-ready.

---

## Core Idea

The solution uses an **agent-oriented architecture**, where each 
responsibility is handled by a focused, autonomous agent.

### Agent Roles

- **Signal Extraction Agent**
  Deterministically analyzes raw CSV / Excel data and extracts 
  structured business signals.

- **Insight Reasoning Agent**
  Uses **Azure OpenAI** to convert signals into executive insights, 
  risks, and recommendations.

- **Orchestration Agent**
  Implemented using **Azure Logic Apps**, coordinating execution, 
  retries, and job lifecycle.

- **Observation Agent**
  Provides read-only job status and insight availability for UI, 
  orchestration, and monitoring.

> **Key principle:**
> Raw data is never sent directly to the LLM. Deterministic 
> preprocessing always happens first.

---

## Key Features

- Blob-first ingestion for scalable file uploads
- Deterministic signal extraction
- AI-powered business insights using Azure OpenAI
- Explicit job lifecycle management
- Auditable, reproducible outputs
- Serverless execution with linear cost scaling

---

## Architecture Overview

### High-Level Flow

1. User uploads file to **Azure Blob Storage**
2. Job is submitted with Blob reference
3. **Azure Functions** execute agents
4. Signals are extracted deterministically
5. Insights are generated via Azure OpenAI
6. Results are stored in **Azure SQL**
7. Status and insights are queried asynchronously

---

## Repository Structure

The repository enforces clear separation of concerns and long-term 
maintainability:

- **src/Contracts** — Versioned, immutable contracts (never break)
- **src/FunctionApp** — Azure Functions with agent-oriented business 
  logic
- **logicapp/** — Workflow orchestration (Azure Logic Apps)
- **database/** — SQL schema (append-only, auditable)
- **docs/** — Architecture, design decisions, and documentation
- **samples/** — Sample input data for testing

Each layer has a single responsibility, making the system easier to 
evolve without architectural rewrites.

---

## Technology Stack

### Microsoft & Azure Services

- **Azure Functions (Consumption)** — serverless compute for agents
- **Azure Logic Apps (Consumption)** — orchestration and workflow 
  control
- **Azure OpenAI** — AI reasoning and insight generation
- **Azure SQL Database** — system of record and audit trail
- **Azure Blob Storage** — raw file storage

### Developer Tooling

- **GitHub** — source control and collaboration
- **GitHub Actions** — CI/CD automation
- **Visual Studio** — development environment
- **GitHub Copilot** — AI-assisted development

---

## Cost Architecture

The system is designed for cost efficiency at any scale:

- Azure Functions & Logic Apps — zero idle cost
- Azure SQL Database as the system of record
- Azure OpenAI usage minimised through deterministic preprocessing

Costs scale **linearly with usage**, not with infrastructure size — 
making the architecture suitable for both early-stage deployment and 
enterprise scale.

---

## Documentation

The following documents explain the system design progressively:

1. `docs/architecture.md` — Overall system design and execution flow
2. `docs/contracts.md` — Contract versioning and backward 
   compatibility strategy
3. `docs/decisions.md` — Key architectural trade-offs and rationale
4. `docs/responsible-ai.md` — Responsible AI principles, trust, 
   and governance

---

## Testing Strategy

The project focuses on **confidence over complexity**:

- Contract safety tests to prevent breaking changes
- Deterministic behaviour tests for signal extraction
- Read-only validation for job status queries

This ensures reliability without unnecessary test infrastructure.

---

## Responsible AI & Enterprise Readiness

- Deterministic preprocessing before AI reasoning
- Structured prompts to reduce hallucinations
- No raw data sent to the LLM
- Immutable storage of signals and insights
- Versioned contracts for backward compatibility

This design supports **enterprise adoption**, governance, and 
future platform scaling.

---

## Status

**Architecture complete — core pipeline operational.**

Active development continues with additional documentation, 
deployment instructions, and extended sample datasets.

---

## Author

**Vinod Patil**
Enterprise AI & Data Architect

Building practical, production-ready AI systems that balance 
intelligence, reliability, and cost efficiency.

[LinkedIn](https://www.linkedin.com/in/vinodrpatil/) | 
[DataFusion Innovation](https://github.com/vinodrpatil-datafusion)

---

## Hackathon

This project was submitted to the **Microsoft AI Dev Days Global 
Hackathon 2026**.
