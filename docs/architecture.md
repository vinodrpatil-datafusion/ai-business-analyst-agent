# Architecture

## Design Goals
- Scalability
- Cost efficiency
- Determinism before AI
- Auditability
- Enterprise readiness

## Core Components
- Azure Blob Storage (ingestion)
- Azure Functions (agents)
- Azure Logic Apps (orchestration)
- Azure SQL Database (system of record)
- Azure OpenAI (reasoning)

## Execution Flow
1. File uploaded to Blob Storage
2. Job submitted with BlobPath
3. Status set to Pending
4. Signal Extraction Agent runs
5. Insight Reasoning Agent runs
6. Results persisted
7. Job marked Completed or Failed

This separation ensures clarity, resilience, and explainability.
