# 🧠 AI Business Analyst Agent for SMEs

🚀 Built for **Microsoft AI Dev Days Global Hackathon 2026**

---

## Overview

Small and medium-sized businesses (SMEs) generate large volumes of data in Excel and CSV files but often lack the time, tools, or expertise to turn that data into actionable decisions. Traditional dashboards show metrics, but rarely answer the most important question:

> **“What should I do next?”**

**AI Business Analyst Agent** is a serverless, Azure-native solution that transforms structured business data into **clear, executive-level insights**, including risks, opportunities, and recommended actions — without requiring complex BI tools or technical expertise.

The system acts as a **virtual business analyst**, helping decision-makers move from raw data to informed action quickly and reliably.

---

## Core Idea

The solution is built using an **agent-oriented architecture**, where each stage of analysis is handled by a specialized, autonomous component (“agent”), without introducing heavyweight agent frameworks.

### Agent Roles

- **Signal Extraction Agent**  
  Deterministically analyzes raw CSV/Excel data to extract business metrics, trends, and patterns.

- **Insight Reasoning Agent**  
  Uses **Azure OpenAI** to generate executive summaries, risks, opportunities, and recommendations from structured signals.

- **Orchestration Agent**  
  Implemented using **Azure Logic Apps**, coordinating execution, retries, and job state.

- **Observation Agent**  
  Provides read-only job status and insight availability for the UI and demo.

> **Key principle:** Raw data is never sent directly to the LLM.  
> Deterministic analysis always happens before AI reasoning.

---

## Key Features

- Upload CSV / Excel business data  
- Deterministic extraction of business signals  
- AI-generated executive insights using Azure OpenAI  
- Identification of key risks and opportunities  
- Actionable, plain-language recommendations  
- Real-time job status tracking  
- Auditable and reproducible outputs  

---

## Architecture Overview

The system prioritizes **reliability, explainability, and cost efficiency**.

### High-Level Flow

1. User uploads a CSV/Excel file via the web UI  
2. Azure Logic Apps orchestrates the workflow  
3. Signal Extraction Agent processes data deterministically  
4. Insight Reasoning Agent generates insights using Azure OpenAI  
5. Results and job state are stored in Azure SQL  
6. UI queries job status and displays insights  

---

## 📁 Repository Structure

The repository is organized to enforce clear separation of concerns and long-term maintainability:

- **src/Contracts → Versioned, immutable contracts (never break)**
- **src/FunctionApp → Azure Functions with agent-oriented business logic**
- **logicapp/ → Workflow orchestration (Azure Logic Apps)**
- **database/ → SQL schema (append-only, auditable)**
- **docs/ → Architecture, design decisions, and demo material**
- **samples/ → Sample input data for demo and testing**


Each layer has a single responsibility, making the system easier to evolve without architectural rewrites.

---

## Technology Stack

### Microsoft & Azure Services

- **Azure Functions (Consumption)** – serverless compute for agents  
- **Azure Logic Apps (Consumption)** – orchestration and workflow control  
- **Azure OpenAI** – AI reasoning and insight generation  
- **Azure SQL Database** – system of record and audit trail  
- **Azure Blob Storage** – raw file storage  

### Developer Tooling

- **GitHub** – source control and collaboration  
- **GitHub Actions** – CI/CD automation  
- **VS Code** – development environment  
- **GitHub Copilot** – AI-assisted development  

---

## 💰 Cost Model (Startup-Friendly)

The architecture is optimized for early-stage startups:

- Azure Functions & Logic Apps → **zero idle cost**
- Azure SQL Database used as the system of record
- Azure OpenAI usage minimized through deterministic preprocessing

Costs scale **linearly with usage**, not with infrastructure size.

---

## 📚 Documentation (Read in This Order)

The following documents explain the system design and decisions progressively:

1. `docs/architecture.md` – Overall system design and execution flow  
2. `docs/contracts.md` – Contract versioning and backward compatibility strategy  
3. `docs/decisions.md` – Key architectural trade-offs and rationale  
4. `docs/responsible-ai.md` – Responsible AI principles, trust, and governance  
5. `docs/demo-script.md` – 2-minute demo narration for judges  

---

## 🧪 Testing Strategy

The project focuses on **confidence over complexity**:

- Contract safety tests to prevent breaking changes  
- Deterministic behavior tests for signal extraction  
- Read-only validation for job status queries  

This ensures reliability without introducing unnecessary test infrastructure.

---

## Responsible AI & Enterprise Readiness

- Deterministic preprocessing before AI reasoning  
- Structured prompts to reduce hallucinations  
- No raw data sent to the LLM  
- Immutable storage of signals and insights  
- Versioned contracts for backward compatibility  

This design supports **enterprise adoption**, governance, and future SaaS scaling.

---

## Hackathon Alignment

This project aligns strongly with the **Microsoft AI Dev Days Hackathon** by:

- Demonstrating deep **Azure service integration**
- Applying **agent-oriented AI design** responsibly
- Solving a **real-world business problem**
- Being fully **serverless and production-ready**
- Maintaining a public **GitHub repository**
- Using Microsoft developer tooling throughout

**Primary Category Fit:**  
🏆 **Best Azure Integration**

**Secondary Fit:**  
🏆 **Best Enterprise Solution**

---

## Status

🛠️ **Active Development**

This repository will be incrementally updated during the hackathon with:
- Code implementations  
- Architecture documentation  
- Deployment instructions  
- Demo assets  

---

## Author

**Vinod Patil**  
Azure | Data | AI Architect  

Focused on building **practical, production-ready AI systems** that balance intelligence, reliability, and cost efficiency.
