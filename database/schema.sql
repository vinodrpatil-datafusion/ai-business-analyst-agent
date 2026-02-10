/*==============================================================
  AI Business Analyst Agent
  Database Schema (V1)

  Purpose:
  --------
  This schema supports an asynchronous, agent-oriented system
  that transforms structured business data into AI-generated
  insights.

  Design principles:
  ------------------
  - Jobs are the system anchor
  - Deterministic signals are persisted before AI reasoning
  - AI outputs are stored separately for auditability
  - JSON is used to avoid schema churn as insights evolve
  - Schema is intentionally minimal and append-friendly

  NOTE:
  -----
  This schema is storage-agnostic by design and can be migrated
  to other data stores (e.g., Cosmos DB, Fabric, PostgreSQL)
  without changing application contracts.
==============================================================*/


/*--------------------------------------------------------------
  TABLE: Jobs
  --------------------------------------------------------------
  Represents a single analysis request submitted by a user.

  This table is the system of record for:
  - Job lifecycle
  - Status tracking
  - UI polling

  Status values (V1):
  - Pending
  - Processing
  - Completed
  - Failed
--------------------------------------------------------------*/
CREATE TABLE Jobs (
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    
    -- Original file metadata
    FileName NVARCHAR(256) NOT NULL,
    FileType NVARCHAR(50) NOT NULL, -- csv | xlsx
    
    -- Current job status
    Status NVARCHAR(50) NOT NULL,
    
    -- Audit fields
    SubmittedAt DATETIMEOFFSET NOT NULL,
    LastUpdatedAt DATETIMEOFFSET NOT NULL
);


/*--------------------------------------------------------------
  TABLE: BusinessSignals
  --------------------------------------------------------------
  Stores deterministic, reproducible signals extracted from
  the raw input data.

  IMPORTANT:
  ----------
  - Raw files are never sent directly to the LLM
  - Only these structured signals are used for AI reasoning
  - Enables replay, audit, and deterministic testing

  Relationship:
  -------------
  One-to-one with Jobs
--------------------------------------------------------------*/
CREATE TABLE BusinessSignals (
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    
    -- Serialized BusinessSignalsV1 contract
    SignalsJson NVARCHAR(MAX) NOT NULL,
    
    -- Timestamp of signal generation
    GeneratedAt DATETIMEOFFSET NOT NULL,
    
    CONSTRAINT FK_BusinessSignals_Jobs
        FOREIGN KEY (JobId) REFERENCES Jobs(JobId)
);


/*--------------------------------------------------------------
  TABLE: BusinessInsights
  --------------------------------------------------------------
  Stores AI-generated insights produced from deterministic
  business signals.

  Design notes:
  -------------
  - Insights are stored as JSON to support evolution
  - Enables comparison across model versions
  - Decouples storage from LLM implementation

  Relationship:
  -------------
  One-to-one with Jobs
--------------------------------------------------------------*/
CREATE TABLE BusinessInsights (
    JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    
    -- Serialized BusinessInsightsV1 contract
    InsightsJson NVARCHAR(MAX) NOT NULL,
    
    -- Timestamp of AI generation
    GeneratedAt DATETIMEOFFSET NOT NULL,
    
    CONSTRAINT FK_BusinessInsights_Jobs
        FOREIGN KEY (JobId) REFERENCES Jobs(JobId)
);

