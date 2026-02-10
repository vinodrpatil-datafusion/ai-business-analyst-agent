# Architectural Decisions

## Why Blob-first ingestion?
- Scales for large files
- Faster uploads
- Decouples UI from processing

## Why Azure Functions + Logic Apps?
- Zero idle cost
- Clear separation of concerns
- Easy retries & observability

## Why deterministic preprocessing?
- Reduced hallucinations
- Lower token usage
- Explainable AI outputs
