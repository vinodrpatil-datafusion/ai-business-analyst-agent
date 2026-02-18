/*==============================================================
  AI Business Analyst Agent
  Database Schema (V1.1 – Blob-first ingestion model)

  Architecture Overview:
  ----------------------
  • Azure Blob Storage is the ingestion layer
  • Jobs table is the orchestration anchor
  • BlobPath is the authoritative input reference
  • Deterministic signal extraction ALWAYS precedes AI reasoning
  • Insights availability is derived from BusinessInsights existence
  • Execution timing is computed at the database layer
  • Concurrency safety is enforced via atomic status transitions

  Design Principles:
  ------------------
  • Single responsibility per table
  • Append-only reasoning artifacts
  • No redundant status flags
  • Auditability without over-normalization
==============================================================*/


/*==============================================================
  TABLE: Jobs
  -------------------------------------------------------------
  Purpose:
    Represents a single analysis request.
    Acts as the lifecycle controller and orchestration anchor.
==============================================================*/
CREATE TABLE Jobs (
    -- Primary identifier for orchestration
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

    -- Azure Blob path (authoritative source of input data)
    -- Format example: container/path/file.csv
    BlobPath NVARCHAR(512) NOT NULL,

    -- Job lifecycle state
    -- Allowed values:
    --   Pending     → Created but not yet processing
    --   Processing  → Concurrency lock acquired
    --   Completed   → Signals + Insights persisted
    --   Failed      → Processing attempted but failed
    Status NVARCHAR(50) NOT NULL
        CHECK (Status IN ('Pending', 'Processing', 'Completed', 'Failed')),

    -- Creation audit timestamp
    SubmittedAt DATETIMEOFFSET NOT NULL,

    -- Updated on every state transition
    LastUpdatedAt DATETIMEOFFSET NOT NULL,

    -- Execution lifecycle tracking
    ProcessingStartedAt DATETIMEOFFSET NULL,
    ProcessingCompletedAt DATETIMEOFFSET NULL,

    -- Computed duration (ms) between start and completion
    ProcessingDurationMs INT NULL
);


---------------------------------------------------------------
-- Index Strategy for Jobs
---------------------------------------------------------------

-- Enables fast filtering by lifecycle state
-- Useful for future background polling or dashboards
CREATE INDEX IX_Jobs_Status ON Jobs(Status);

-- Enables time-based reporting and analytics
CREATE INDEX IX_Jobs_SubmittedAt ON Jobs(SubmittedAt);



/*==============================================================
  TABLE: BusinessSignals
  -------------------------------------------------------------
  Purpose:
    Stores deterministic extraction results.
    Raw business data is never sent directly to the LLM.
==============================================================*/
CREATE TABLE BusinessSignals (
    -- 1:1 relationship with Jobs
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

    -- Serialized BusinessSignalsV1 DTO
    -- Represents deterministic structured signals
    SignalsJson NVARCHAR(MAX) NOT NULL,

    -- Timestamp when signals were generated
    GeneratedAt DATETIMEOFFSET NOT NULL,

    CONSTRAINT FK_BusinessSignals_Jobs
        FOREIGN KEY (JobId)
        REFERENCES Jobs(JobId)
        ON DELETE CASCADE
);

-- Enables signal generation time analytics
CREATE INDEX IX_BusinessSignals_GeneratedAt
    ON BusinessSignals(GeneratedAt);



/*==============================================================
  TABLE: BusinessInsights
  -------------------------------------------------------------
  Purpose:
    Stores AI reasoning output and audit metadata.
    Represents the "intelligence layer" of the system.
==============================================================*/
CREATE TABLE BusinessInsights (
    -- 1:1 relationship with Jobs
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

    -- Structured BusinessInsightsV1 DTO
    -- Clean, validated output returned to API/UI
    InsightsJson NVARCHAR(MAX) NOT NULL,

    -- Summary used for LLM reasoning (compressed signal view)
    SummaryJson NVARCHAR(MAX) NULL,

    -- Raw LLM JSON response (stored for audit & debugging)
    RawResponse NVARCHAR(MAX) NULL,

    -- Timestamp when AI reasoning completed
    GeneratedAt DATETIMEOFFSET NOT NULL,

    -----------------------------------------------------------
    -- Governance & Observability
    -----------------------------------------------------------

    -- Manual prompt versioning (e.g., v1, v1.1)
    PromptVersion NVARCHAR(20) NULL,

    -- Model deployment name (e.g., gpt-4o-mini-prod)
    ModelDeployment NVARCHAR(100) NULL,

    -----------------------------------------------------------
    -- Cost Tracking
    -----------------------------------------------------------

    InputTokens INT NULL,
    OutputTokens INT NULL,
    TotalTokens INT NULL,

    CONSTRAINT FK_BusinessInsights_Jobs
        FOREIGN KEY (JobId)
        REFERENCES Jobs(JobId)
        ON DELETE CASCADE
);

-- Enables AI execution reporting and cost analysis
CREATE INDEX IX_BusinessInsights_GeneratedAt
    ON BusinessInsights(GeneratedAt);