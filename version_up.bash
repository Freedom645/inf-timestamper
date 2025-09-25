#!/usr/bin/env bash
set -euo pipefail

# 引数チェック
if [[ $# -ne 1 ]]; then
  echo "Usage: $0 {patch|minor|major}"
  exit 1
fi

PART=$1

# バージョンを更新
uv version "$PART"

# 更新後のバージョンを取得
VERSION=$(uv version)

# コミットに含める（pyproject.toml の変更をコミット）
git add pyproject.toml
git commit -m "Bump version to v$VERSION"

# Annotated タグを作成
git tag -a "v$VERSION" -m "Release v$VERSION"

echo "Version updated to v$VERSION and tagged."