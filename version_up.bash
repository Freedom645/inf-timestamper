#!/usr/bin/env bash
set -euo pipefail

# 引数チェック
if [[ $# -ne 1 ]]; then
  echo "Usage: $0 {patch|minor|major}"
  exit 1
fi

PART=$1

# バージョンを更新
uv version --bump "$PART"

# 更新後のバージョンを取得
VERSION=$(uv version --short)

# version.py を更新
echo "__version__ = '$VERSION'" > core/version.py

# コミット対象
git add pyproject.toml
git add uv.lock
git add core/version.py
git commit -m "Bump version to v$VERSION"

# Annotated タグを作成
git tag -a "v$VERSION" -m "Release v$VERSION"

echo "Version updated to v$VERSION and tagged."
