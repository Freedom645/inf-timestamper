#!/bin/bash
set -e  # いずれかのコマンドが失敗したら終了

echo "=== Running MyPy ==="
uv run mypy . --explicit-package-bases

echo "=== Running Pyright ==="
uv run pyright

echo "=== Running Ruff ==="
uv run ruff check . --fix
