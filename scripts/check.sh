#!/usr/bin/env bash
set -euo pipefail

script_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

bash "$script_dir/format.sh"
bash "$script_dir/test.sh"
