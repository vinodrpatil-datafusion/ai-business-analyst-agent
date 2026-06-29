#!/usr/bin/env bash
#
# Azure-side setup for the SAS upload endpoint: RBAC + blob CORS.
# Idempotent and safe to re-run. Run from the VS Code terminal after `az login`.
#
# Usage:
#   cp scripts/azure-setup.env.example scripts/azure-setup.env   # gitignored
#   # edit scripts/azure-setup.env with your real values
#   chmod +x scripts/azure-setup.sh
#   ./scripts/azure-setup.sh
#
# Prereqs on YOU (the signed-in user): permission to create role assignments
# on the storage account (Owner / User Access Administrator) and to set CORS.
#
# What it does:
#   1. Storage Blob Data Contributor  -> scoped to the `uploads` container
#   2. Storage Blob Delegator         -> scoped to the storage account
#   3. Blob CORS: allow PUT/OPTIONS from your frontend origin(s)
# Role names/scopes per Azure as of 2026-06; verify in your tenant if `create` rejects a role.

set -euo pipefail

# ---- load config ------------------------------------------------------------
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$HERE/azure-setup.env"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: $ENV_FILE not found." >&2
  echo "       cp scripts/azure-setup.env.example scripts/azure-setup.env  and fill it in." >&2
  exit 1
fi
# shellcheck disable=SC1090
source "$ENV_FILE"

: "${SUBSCRIPTION_ID:?set in azure-setup.env}"
: "${STORAGE_RG:?set in azure-setup.env}"
: "${STORAGE_ACCOUNT:?set in azure-setup.env}"
: "${MI_RG:?set in azure-setup.env}"
: "${MI_NAME:?set in azure-setup.env}"
: "${UPLOADS_CONTAINER:=uploads}"
: "${FRONTEND_ORIGINS:?space-separated allowed origins, e.g. http://localhost:5173}"

# ---- preflight --------------------------------------------------------------
echo "==> Checking Azure login..."
az account show >/dev/null 2>&1 || { echo "Run 'az login' first." >&2; exit 1; }
az account set --subscription "$SUBSCRIPTION_ID"

echo "==> Resolving managed identity principal id ($MI_NAME in $MI_RG)..."
MI_PRINCIPAL_ID="$(az identity show -g "$MI_RG" -n "$MI_NAME" --query principalId -o tsv)"
[[ -n "$MI_PRINCIPAL_ID" ]] || { echo "Could not resolve principalId for $MI_NAME." >&2; exit 1; }
echo "    principalId = $MI_PRINCIPAL_ID"

ACCOUNT_SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$STORAGE_RG/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT"
CONTAINER_SCOPE="$ACCOUNT_SCOPE/blobServices/default/containers/$UPLOADS_CONTAINER"

# ---- idempotent role assignment helper -------------------------------------
assign_role () {
  local role="$1" scope="$2"
  if az role assignment list --assignee "$MI_PRINCIPAL_ID" --role "$role" --scope "$scope" \
        --query "[0].id" -o tsv 2>/dev/null | grep -q .; then
    echo "    [skip] '$role' already assigned"
  else
    echo "    [add ] '$role'"
    az role assignment create \
      --assignee-object-id "$MI_PRINCIPAL_ID" \
      --assignee-principal-type ServicePrincipal \
      --role "$role" \
      --scope "$scope" >/dev/null
  fi
}

echo "==> Assigning RBAC..."
assign_role "Storage Blob Data Contributor" "$CONTAINER_SCOPE"
assign_role "Storage Blob Delegator"        "$ACCOUNT_SCOPE"

# ---- blob CORS --------------------------------------------------------------
# NOTE: clear+add makes this idempotent but RESETS all blob CORS rules on this
# account. Fine for a single-purpose dev account; do not use as-is if the
# account hosts other CORS rules you need to keep.
echo "==> Fetching storage account connection string..."
STORAGE_CONN_STR="$(az storage account show-connection-string \
  -n "$STORAGE_ACCOUNT" -g "$STORAGE_RG" --query connectionString -o tsv)"

echo "==> Configuring blob CORS (origins: $FRONTEND_ORIGINS)..."
az storage cors clear --services b --connection-string "$STORAGE_CONN_STR" >/dev/null
for origin in $FRONTEND_ORIGINS; do
  az storage cors add \
    --services b \
    --methods PUT OPTIONS \
    --origins "$origin" \
    --allowed-headers "x-ms-blob-type" "content-type" "x-ms-version" \
    --exposed-headers "*" \
    --max-age 3600 \
    --connection-string "$STORAGE_CONN_STR" >/dev/null
  echo "    [add ] PUT/OPTIONS from $origin"
done

# ---- optional: drop legacy read-only assignment -----------------------------
# Contributor on the uploads container already covers reads there, and the
# pipeline only reads `uploads`. If a prior ACCOUNT-scoped read role exists and
# you want to remove it, uncomment:
# az role assignment delete --assignee-object-id "$MI_PRINCIPAL_ID" \
#   --role "Storage Blob Data Reader" --scope "$ACCOUNT_SCOPE" || true

# ---- verify -----------------------------------------------------------------
echo "==> Current role assignments for the identity:"
az role assignment list --assignee "$MI_PRINCIPAL_ID" --all -o table
echo "==> Current blob CORS rules:"
az storage cors list --services b --connection-string "$STORAGE_CONN_STR" -o table

echo
echo "Done. RBAC changes can take a few minutes to propagate (a 403 right after = wait)."
