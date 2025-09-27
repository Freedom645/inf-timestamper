#!/usr/bin/env bash
set -u  # 未定義変数をエラーにする

failed=0

echo "=== Running MyPy ==="
uv run mypy . --explicit-package-bases || failed=1

echo "=== Running Pyright ==="
uv run pyright || failed=1

echo "=== Running Ruff ==="
uv run ruff check . || failed=1

if [ $failed -ne 0 ]; then
  echo "One or more linting checks failed."
  exit 1
else
  echo "All linting checks passed."
fi
