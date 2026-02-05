# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

INF TimeStamper — beatmania IIDX INFINITAS および SOUND VOLTEX の配信向けタイムスタンプ自動生成 Windows デスクトップアプリ。Python 3.13 + PySide6 (Qt) で構築。

## Commands

```bash
# 依存関係インストール
uv sync --all-extras --dev

# アプリ実行
uv run python src/inf_timestamper/main.py

# リント（全チェック一括実行）
bash lint-all.bash

# 個別リント
uv run mypy . --explicit-package-bases
uv run pyright
uv run ruff check .

# ビルド（PyInstaller → Windows .exe）
bash build.bash

# バージョン更新（pyproject.toml, uv.lock, version.py を更新しタグ作成）
bash bump-version.bash {patch|minor|major}
```

## Architecture

Clean Architecture に基づく4層構造。すべてのソースは `src/inf_timestamper/` 配下。

### レイヤー構成

- **domain/** — ビジネスロジック（外部依存なし）
  - `entity/` — ゲームデータモデル（`inf_game_entity.py`, `sdvx_game_entity.py`）、セッション管理（`stream_entity.py`）、設定（`settings_entity.py`）、フォーマッタ（`timestamp_formatter.py` 抽象クラス、`inf_game_format.py`, `sdvx_game_format.py` 実装）
  - `port/` — インターフェース定義（`IPlayWatcher` ゲーム監視、`IStreamGateway` OBS連携）
  - `repository/` — 永続化の抽象インターフェース
  - `value/` — 値オブジェクト・列挙型（`StreamKind` で INF/SDVX を区別）
  - `factory/` — `GameTimestampFormatterFactory` でゲーム種別に応じたフォーマッタ生成

- **infrastructure/** — domain のインターフェース実装
  - `reflux_file_watcher.py` — INFINITAS の Reflux 出力ファイル監視（watchdog）
  - `sdvx_helper_file_watcher.py` — SDVX Helper 出力ファイル監視
  - `obs_connector_v5.py` — OBS WebSocket v5 連携（`obs_connector_v4.py` はレガシー）
  - リポジトリ実装（JSON ファイル永続化、インメモリ状態管理）

- **usecase/** — アプリケーションロジック
  - `play_recording_use_case.py` — メイン録音ワークフロー（監視開始/停止、OBS連動、タイムスタンプ登録）
  - `presenter/` — ユースケースからUIへの通知

- **ui/** — PySide6 GUI
  - `views/` — ウィンドウ・ダイアログ
  - `widgets/` — 再利用コンポーネント
  - `view_model/` — 状態管理
  - `factory/` — ウィジェット生成ファクトリ

### DI（依存性注入）

`core/app_module.py` で Injector ライブラリを使い全バインディングを定義。ポート → インフラ実装の紐付け、シングルトンスコープ管理、ファクトリ関数の提供をここで行う。

### ゲーム種別の拡張パターン

新しいゲームを追加する場合:
1. `domain/entity/` に新ゲームエンティティとフォーマッタを作成（`GameTimestampFormatter` を継承）
2. `domain/value/stream_value.py` の `StreamKind` に種別追加
3. `infrastructure/` に `IPlayWatcher` 実装を作成
4. `core/app_module.py` でバインディング登録
5. `domain/entity/settings_entity.py` に設定モデル追加
6. `ui/views/` に設定タブ追加

## Code Style & Conventions

- 型チェック: Pyright strict モード + MyPy strict（`disallow_untyped_defs`）。すべての関数に型注釈必須
- Ruff: line-length 120、`build`, `dist`, `.venv`, `src/mock_obs_ws` は除外
- 言語: コメント・コミットメッセージは日本語
- Git ブランチ: `feature/#N-description`, `bugfix/#N-description`

## CI/CD

PR チェック（`.github/workflows/pr-check.yml`）: Ruff → MyPy → Pyright → PyInstaller ビルド検証
リリース（`.github/workflows/build_release.yml`）: `v*.*.*` タグでトリガー、GitHub Releases に ZIP アップロード
