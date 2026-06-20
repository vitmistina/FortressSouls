#!/usr/bin/env bash
set -euo pipefail

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
backend_project="$repo_root/src/backend/FortressSouls.Api/FortressSouls.Api.csproj"
frontend_dir="$repo_root/src/frontend"
frontend_pid=""

cleanup() {
  if [[ -n "$frontend_pid" ]] && kill -0 "$frontend_pid" 2>/dev/null; then
    kill "$frontend_pid" 2>/dev/null || true
    wait "$frontend_pid" 2>/dev/null || true
  fi
}

trap cleanup EXIT INT TERM

echo "==> frontend"
(
  cd "$frontend_dir"
  npm run dev -- --host 127.0.0.1 --strictPort
) &
frontend_pid=$!

echo "==> backend"
dotnet run --launch-profile http --project "$backend_project"
