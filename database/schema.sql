/*==============================================================
  AI Business Analyst Agent
  Database Schema (V1 – Blob-first ingestion)

  Key Architecture Decisions:
  ---------------------------
  - Azure Blob Storage is the ingestion layer
  - Jobs reference BlobPath (authoritative input source)
  - File metadata is derived from Blob Storage
  - Deterministic signals precede AI reasoning
  - Insights availability is derived (not stored)
==============================================================*/


---------------------------------------------------------------
-- TABLE: Jobs
---------------------------------------------------------------
CREATE TABLE Jobs (
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

    -- Azure Blob path (authoritative source of input data)
    BlobPath NVARCHAR(512) NOT NULL,

    -- Job lifecycle status
    -- Allowed values: Pending | Processing | Completed | Failed
    Status NVARCHAR(50) NOT NULL
        CHECK (Status IN ('Pending', 'Processing', 'Completed', 'Failed')),

    -- Audit fields
    SubmittedAt DATETIMEOFFSET NOT NULL,
    LastUpdatedAt DATETIMEOFFSET NOT NULL
);

-- Indexes for operational queries
CREATE INDEX IX_Jobs_Status ON Jobs(Status);
CREATE INDEX IX_Jobs_SubmittedAt ON Jobs(SubmittedAt);



---------------------------------------------------------------
-- TABLE: BusinessSignals
---------------------------------------------------------------
CREATE TABLE BusinessSignals (
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

    -- Serialized BusinessSignalsV1 (deterministic extraction output)
    SignalsJson NVARCHAR(MAX) NOT NULL,

    -- Timestamp when signals were generated
    GeneratedAt DATETIMEOFFSET NOT NULL,

    CONSTRAINT FK_BusinessSignals_Jobs
        FOREIGN KEY (JobId) REFERENCES Jobs(JobId)
        ON DELETE CASCADE
);

CREATE INDEX IX_BusinessSignals_GeneratedAt
    ON BusinessSignals(GeneratedAt);



---------------------------------------------------------------
-- TABLE: BusinessInsights
---------------------------------------------------------------
CREATE TABLE BusinessInsights (
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

    -- Final structured DTO
    InsightsJson NVARCHAR(MAX) NOT NULL,

    -- Raw LLM JSON response for audit/debug
    RawResponse NVARCHAR(MAX) NULL,

    -- When insights were generated
    GeneratedAt DATETIMEOFFSET NOT NULL,

    -- Prompt governance (manual versioning)
    PromptVersion NVARCHAR(20) NULL,

    -- Model metadata
    ModelDeployment NVARCHAR(100) NULL,

    -- Cost tracking
    InputTokens INT NULL,
    OutputTokens INT NULL,
    TotalTokens INT NULL,

    CONSTRAINT FK_BusinessInsights_Jobs
        FOREIGN KEY (JobId) REFERENCES Jobs(JobId)
        ON DELETE CASCADE
);

CREATE INDEX IX_BusinessInsights_GeneratedAt
    ON BusinessInsights(GeneratedAt);
