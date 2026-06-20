#!/usr/bin/env bash
set -euo pipefail

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
backend_solution="$repo_root/src/backend/FortressSouls.slnx"
frontend_dir="$repo_root/src/frontend"

run_step() {
  echo "==> $1"
  shift
  "$@"
}

run_step "backend format" dotnet format "$backend_solution" --verify-no-changes
(
  cd "$frontend_dir"
  run_step "frontend lint" npm run lint
)
