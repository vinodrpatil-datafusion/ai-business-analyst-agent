/*==============================================================
  AI Business Analyst Agent
  Database Schema (V1.2 – retryable, append-with-attempt model)

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
  • Append-only reasoning artifacts (one row PER ATTEMPT, never updated)
  • No redundant status flags
  • Auditability without over-normalization

  V1.2 change (retry support):
  ----------------------------
  • Jobs gains RetryCount; a Failed job may be re-locked for reprocessing
    until RetryCount reaches the application-configured cap.
  • BusinessSignals / BusinessInsights move from a 1:1 (JobId PRIMARY KEY)
    model to 1:N append-per-attempt. A surrogate IDENTITY key replaces the
    JobId primary key so a reprocess cannot collide; JobId becomes a
    non-unique foreign key. The "current" row for a job is the latest by
    GeneratedAt. This keeps persistence append-only (INSERT only) while
    making retries safe — every attempt is preserved for audit.

  NOTE (dev): this script DROP/CREATEs. There is no production data to
  migrate. If that changes, replace this with an ALTER migration.
==============================================================*/

IF OBJECT_ID('BusinessInsights', 'U') IS NOT NULL DROP TABLE BusinessInsights;
IF OBJECT_ID('BusinessSignals', 'U')  IS NOT NULL DROP TABLE BusinessSignals;
IF OBJECT_ID('Jobs', 'U')             IS NOT NULL DROP TABLE Jobs;


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
    --   Pending     -> Created but not yet processing
    --   Processing  -> Concurrency lock acquired
    --   Completed   -> Signals + Insights persisted
    --   Failed      -> Processing attempted but failed (may be retried)
    Status NVARCHAR(50) NOT NULL
        CHECK (Status IN ('Pending', 'Processing', 'Completed', 'Failed')),

    -- Number of times processing has FAILED for this job.
    -- Incremented on every Failed transition; used to cap retries so a
    -- permanently-failing job cannot loop forever (esp. under a queue trigger).
    RetryCount INT NOT NULL CONSTRAINT DF_Jobs_RetryCount DEFAULT (0),

    -- Creation audit timestamp
    SubmittedAt DATETIMEOFFSET NOT NULL,

    -- Updated on every state transition
    LastUpdatedAt DATETIMEOFFSET NOT NULL,

    -- Execution lifecycle tracking (reset at the start of each attempt)
    ProcessingStartedAt DATETIMEOFFSET NULL,
    ProcessingCompletedAt DATETIMEOFFSET NULL,

    -- Computed duration (ms) between start and completion of the last attempt
    ProcessingDurationMs INT NULL
);

CREATE INDEX IX_Jobs_Status ON Jobs(Status);
CREATE INDEX IX_Jobs_SubmittedAt ON Jobs(SubmittedAt);


/*==============================================================
  TABLE: BusinessSignals
  -------------------------------------------------------------
  Purpose:
    Stores deterministic extraction results, one row per processing
    attempt (append-only). Raw business data is never sent to the LLM.
==============================================================*/
CREATE TABLE BusinessSignals (
    -- Surrogate key: makes each attempt's row unique so a reprocess
    -- of the same JobId cannot collide.
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    -- Non-unique FK to the owning job (1:N — one row per attempt).
    JobId UNIQUEIDENTIFIER NOT NULL,

    -- 1-based attempt number for this job (audit/debug aid).
    Attempt INT NOT NULL CONSTRAINT DF_BusinessSignals_Attempt DEFAULT (1),

    -- Serialized BusinessSignalsV1 DTO (deterministic structured signals)
    SignalsJson NVARCHAR(MAX) NOT NULL,

    -- Timestamp when signals were generated
    GeneratedAt DATETIMEOFFSET NOT NULL,

    CONSTRAINT FK_BusinessSignals_Jobs
        FOREIGN KEY (JobId)
        REFERENCES Jobs(JobId)
        ON DELETE CASCADE
);

-- "Latest signals for a job" reads + per-job attempt history
CREATE INDEX IX_BusinessSignals_JobId_GeneratedAt
    ON BusinessSignals(JobId, GeneratedAt DESC);


/*==============================================================
  TABLE: BusinessInsights
  -------------------------------------------------------------
  Purpose:
    Stores AI reasoning output and audit metadata, one row per
    processing attempt (append-only).
==============================================================*/
CREATE TABLE BusinessInsights (
    -- Surrogate key: unique per attempt (see BusinessSignals rationale).
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    -- Non-unique FK to the owning job (1:N — one row per attempt).
    JobId UNIQUEIDENTIFIER NOT NULL,

    -- 1-based attempt number for this job (audit/debug aid).
    Attempt INT NOT NULL CONSTRAINT DF_BusinessInsights_Attempt DEFAULT (1),

    -- Structured BusinessInsightsV1 DTO (clean, validated output for API/UI)
    InsightsJson NVARCHAR(MAX) NOT NULL,

    -- Summary used for LLM reasoning (compressed signal view)
    SummaryJson NVARCHAR(MAX) NULL,

    -- Raw LLM JSON response (stored for audit & debugging)
    RawResponse NVARCHAR(MAX) NULL,

    -- Timestamp when AI reasoning completed
    GeneratedAt DATETIMEOFFSET NOT NULL,

    -- Governance & Observability
    PromptVersion NVARCHAR(20) NULL,
    ModelDeployment NVARCHAR(100) NULL,

    -- Cost Tracking
    InputTokens INT NULL,
    OutputTokens INT NULL,
    TotalTokens INT NULL,

    CONSTRAINT FK_BusinessInsights_Jobs
        FOREIGN KEY (JobId)
        REFERENCES Jobs(JobId)
        ON DELETE CASCADE
);

-- "Latest insights for a job" reads (GET /jobs/{id}/insights) + attempt history
CREATE INDEX IX_BusinessInsights_JobId_GeneratedAt
    ON BusinessInsights(JobId, GeneratedAt DESC);
