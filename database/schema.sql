/*==============================================================
  AI Business Analyst Agent
  Database Schema (V1 – Blob-first ingestion)

  Key decisions:
  --------------
  - Blob Storage is the ingestion layer
  - Jobs reference BlobPath (source of truth)
  - File metadata may be derived later
  - Deterministic signals precede AI reasoning
==============================================================*/

---------------------------------------------------------------
-- TABLE: Jobs
---------------------------------------------------------------
CREATE TABLE Jobs (
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

    -- Azure Blob path (authoritative source of input data)
    BlobPath NVARCHAR(512) NOT NULL,

    -- Job lifecycle status
    -- Pending | Processing | Completed | Failed
    Status NVARCHAR(50) NOT NULL,

    -- Audit fields
    SubmittedAt DATETIMEOFFSET NOT NULL,
    LastUpdatedAt DATETIMEOFFSET NOT NULL
);

---------------------------------------------------------------
-- TABLE: BusinessSignals
---------------------------------------------------------------
CREATE TABLE BusinessSignals (
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

    -- Serialized BusinessSignalsV1
    SignalsJson NVARCHAR(MAX) NOT NULL,

    GeneratedAt DATETIMEOFFSET NOT NULL,

    CONSTRAINT FK_BusinessSignals_Jobs
        FOREIGN KEY (JobId) REFERENCES Jobs(JobId)
);

---------------------------------------------------------------
-- TABLE: BusinessInsights
---------------------------------------------------------------
CREATE TABLE BusinessInsights (
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

    -- Serialized BusinessInsightsV1
    InsightsJson NVARCHAR(MAX) NOT NULL,

    GeneratedAt DATETIMEOFFSET NOT NULL,

    CONSTRAINT FK_BusinessInsights_Jobs
        FOREIGN KEY (JobId) REFERENCES Jobs(JobId)
);