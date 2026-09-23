# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## リポジトリの状態

実装は一通り完了している。MVP に必要な機能（状態機械・OBS 接続・ゲーム検知・フォーマット展開・永続化・設定画面・ログ・自己アップデート）に加え、3 ゲーム目（SOUND VOLTEX）まで通っている。`dotnet build` 警告 0 / `dotnet test` 全緑。残っているのは主にユーザ実環境での検証（SDVX の実機検証、Velopack バンドル作成、GitHub Releases アップロード、別 PC での通しテスト、実機での配信〜プレイ〜リザルトの通し確認）。

進捗と「次にやること」の正本は `docs/実装計画.md`（フェーズ別の DoD とセッション引き継ぎメモ）。**作業を始める前にその「現在のフェーズ」節を読むこと。**

### 定型コマンド

```powershell
dotnet build InfTimestamper.sln              # 警告 0 を維持する
dotnet test InfTimestamper.sln               # xUnit。Core.Tests のみ
dotnet publish src/InfTimestamper/InfTimestamper.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

- lint/formatter は導入していない（アナライザ既定のみ）。
- リリース手順は `docs/release.md`。

## プロジェクト概要

**INF-TIMESTAMPER** — Windows スタンドアロンアプリ。OBS を使った音楽ゲーム配信に対し、YouTube アーカイブ向けのタイムスタンプ（チャプター文字列）を自動生成する。

対応ゲームは **コナステ版 beatmaniaIIDX INFINITAS**（`INFINITAS`）、**pop'n music**（`POPN`）、**SOUND VOLTEX**（`SDVX`）の 3 つ。**1 配信 = 1 ゲーム**で、記録対象はメインウィンドウのゲーム選択コンボボックスで決める（`初期状態` でのみ変更可能）。

ゲーム抽象化は Phase 9 で `Core/Games/` に抽出済み。**ゲームを増やすときに触るのは 4 箇所**：`Models/GameId`（enum + シリアライズ表記）／`Games/GameCatalog`（表示名・識別子リスト・プレビューデータ・監視ツール名）／新しい `IPlayWatcher` 実装（+ 対応する FieldMapper）／設定ダイアログのタブと `AppSettings` のゲーム別セクション。

`IPlayWatcher.Start(WatchTarget target)` の `WatchTarget` は `Directory` / `Endpoint` を持つレコード。ファイル監視系は `Directory`（出力ディレクトリ）だけ、SDVX は `Endpoint`（`ws://host:port`）+ 任意で `Directory`（SDVX Helper のフォルダ。ログ監視用）。`AppSettings.WatchTargetFor(GameId)` がゲームごとにこれを組み立てる。

### 前段アプリとの関係

本プロジェクトは [Freedom645/inf-timestamper](https://github.com/Freedom645/inf-timestamper)（Python 実装、v0.6.1 まで公開）の **C# フルリライト**。**同一リポジトリで v1.0.0 としてリリース**予定。`v0.6.1` タグで Python 実装を保存し、`main` を C# 実装で全面置換する。**v0.x の JSON データは v1.0 で読み込まない（クリーン切替）**。詳細は要件.md「前段アプリと本リライトの位置付け」節参照。

権威ある仕様は `docs/要件.md`。UI 文言・画面構成・ボタン挙動・編集ダイアログ仕様・JSON スキーマ・データ取得仕様などはすべてそちらを参照する。本ファイルは「複数ファイルを横断しないと掴めない大局的な構造」のみを抽出している。

## 技術スタック（確定）

- 言語：C# / .NET 8
- GUI：WPF（MVVM パターン）
- 配布形態：single-file self-contained publish（単一 exe）
- 主要ライブラリ：
  - OBS WebSocket：`OBSWebSocketDotNet`（配信開始/終了の検知）
  - ファイル監視：`System.IO.FileSystemWatcher`（標準。外部ツールの出力監視 = ゲーム検知の主機構）
  - JSON：`System.Text.Json`（標準）
  - ロギング：`Microsoft.Extensions.Logging` + `Serilog`（`Serilog.Sinks.File`）
  - DI / ホスト：`Microsoft.Extensions.Hosting`
  - 自己アップデート：`Velopack`
  - ULID 生成：`NUlid`
  - テスト：xUnit

TFM は Core が `net8.0`、WPF / Tests が `net8.0-windows`。WPF は独自 `Main`（`Program.cs`）で Velopack をブートストラップするため、`App.xaml` を `ApplicationDefinition` から外して `Page` として扱っている。単一 exe は `EnableCompressionInSingleFile` で圧縮している（約 73MB）。

## アーキテクチャ上の要点

### 状態遷移

メイン状態機械は `初期状態 → 配信開始待ち → 記録中 → 記録終了` の 4 状態。`記録操作ボタン` 1 つの表示文言・挙動が状態によって 4 通りに切り替わる UX なので、ボタンを状態ごとに分けず**現在状態をビューに射影する設計**が必須（WPF の DataTrigger / Style で表現可能）。

`記録中` は内側にゲーム検知のサブ状態機械（`プレイ開始 → プレイリザルト`）を持つ。タイムスタンプとして記録される日時は **`プレイ開始` 遷移のタイミング**であり、`配信開始時間`（= `記録中` への遷移時刻）が相対時刻計算の基準になる。ゲーム検知は Reflux のファイル監視で行う（下記）。

遷移トリガ:
- `配信開始待ち → 記録中`: OBS WebSocket の配信開始イベント、または「強制開始」ボタン
- `記録中 → 記録終了`: OBS WebSocket の配信終了イベント、または「停止」ボタン
- `記録終了 → 記録中`: 「記録再開」ボタン（過去ファイルを開いた場合もここから再開可能）

OBS 接続が一時切断されても `記録中` 状態は維持し、自動再接続（指数バックオフ）でリカバリする方針。「停止」ボタンか、再接続後に配信が既に終了していたことが判明した場合のみ `記録終了` へ。

### OBS 接続の役割

OBS WebSocket は **配信開始/終了の検知**（= タイムスタンプの基準時刻となる `記録中` への遷移）にのみ使う。ゲームのプレイ検知は Reflux 側で行うため、OBS のゲーム画面取得（`GetSourceScreenshot`）・2 台 PC 構成は廃止した。接続管理（再接続バックオフ等）は `Obs/` に残る。

### ゲーム検知の方針（外部ツールの出力を読む）

INFINITAS と pop'n music は「外部ツールが出力する 1 行の状態ファイル + JSON のリザルトファイル」を `FileSystemWatcher` で監視する。SDVX だけは SDVX Helper のデータ配信 WebSocket を購読する（下記）。取得方式が違っても `Core/Games/IPlayWatcher`（`PlayStarted` / `PlayResultDetected`）で抽象化してあり、`RecordingCoordinator` は `IReadOnlyDictionary<GameId, IPlayWatcher>` を受け取って `Options.Game` で動かすウォッチャーを選ぶ。

#### INFINITAS（Reflux ファイル監視）

INFINITAS のプレイ開始とプレイデータは、**Reflux が出力するファイル群のファイル監視**で取得する（`Core/Reflux/`）。当初は OBS スクリーンショットの画像認識 + OCR で取得する設計だったが、OCR の精度向上が困難なため Reflux 方式へ切り替えた（画像認識の実装は Phase 9 で削除済み）。前段 Python 実装（`reflux_file_watcher.py`）の移植。

- **監視**：`RefluxPlayWatcher` が `FileSystemWatcher` で `playstate.txt` を監視。`off`/`menu` → `play` でプレイ開始、`play` → 非 `play` でプレイリザルトと判定。
- **プレイ開始**：`title.txt` / `level.txt` を読み `$title` / `$level` を充填。`playStartedAt` = エッジ検知時刻。
- **プレイリザルト**：`latest.json` を読み、`RefluxFieldMapper` で各識別子（`$diff_*` / `$dj_level` / `$lamp` / `$miss_count` / `$ex_score` 等）へ変換して直近エントリにマージ。マッピングは `RefluxFieldMapper` に集約（調整はここだけ）。実機サンプル（`docs/sample/reflux/`）で全フィールドが正しく展開されることを確認済み。
- **重複防止**：エントリ操作は状態遷移エッジでのみ行う（同一状態の連続通知はノーオペ）。
- 監視失敗・読込失敗はダイアログを出さず、配信開始/終了の記録には影響させない（ログのみ）。
#### pop'n music（popn-lively-tracker ファイル監視）

`PopnPlayWatcher` が `state.txt` と `result.json` の**両方**を監視する（`Core/Popn/`）。入力仕様は `popn-lively-tracker` の `docs/file-output.md`（互換契約）。

**INFINITAS と構造が違う一点**：`result.json` は譜面の特定にレコード表の更新を待つため、**リザルト画面の表示から数秒（既定タイムアウト 20 秒）遅れて書かれる**。`state.txt` のエッジ（`プレイ中` 離脱）でリザルトを読むと、**直前のプレイの成績を拾ってしまう**。そのため：

- `プレイ中` 突入で `PlayStarted` を発火し、「リザルト待ち」に入る。この時点では曲情報が無いので `fields` は空
- `result.json` **自身の書き換え**を検知した時点で `PlayResultDetected` を発火する
- `result.json` の `time` がプレイ開始より前（1 秒の猶予つき）なら、前回プレイのものとして棄却する
- リザルト待ちでない状態での `result.json` の変化は無視する（起動直後に残っているファイルを拾わない）

マッピングは `PopnFieldMapper` に集約。**`record` セクションと `previous_best` / `new_record` / `previous_medal` / `new_medal` は自己ベスト側の値でこのプレイの成績ではないため、意図的に取り込んでいない**（`rank` / `medal` / `score` がこのプレイの値）。譜面が特定できなかったプレイ（`music` が `null`）は曲情報だけを欠損扱いにし、成績側は実測値として記録する。

#### SOUND VOLTEX（SDVX Helper の WebSocket 購読）

**ここだけファイル監視ではない。** SDVX Helper は v1.0 系まで `out/history_cursong.xml` を出力していたが、v2 系（v.2.0.0 以降）でファイル出力を廃止し、OBS ブラウザソース向けの WebSocket 配信（既定 `ws://127.0.0.1:8767`）へ移行した。**SDVX Helper 側は `localhost` にしか bind しない**（`websockets.serve(handler, 'localhost', port)` でホストはハードコード）ので、別 PC からは繋がらない。設定でホストを変えられるようにはしてあるが、それはポートフォワード等を挟む場合のためで既定は `127.0.0.1`。`SdvxHelperPlayWatcher` はこの配信サーバへ `ClientWebSocket` で接続し、切断時は OBS 接続と同じ `BackoffSchedule` で再接続する（`Core/Sdvx/`）。

**INFINITAS / pop'n と構造が違う三点**：

- **WebSocket にはプレイ開始のイベントが無い。** プレイ開始は SDVX Helper のログ `log/sdvx_helper.log` の `モード変更: X → play` 行で決める（`SdvxHelperLogTail` が追記を tail する）。リザルト画面からのリトライでもこの行は出る。ログの書式（`src/logger.py`）と `detect_mode` の名前に依存するので、SDVX Helper 側が変わると壊れる箇所。フォルダが未設定ならログは使えず、`nowplaying` だけで動く（リトライは落ちる）
  - **`init` を状態として扱わないのが肝。** 画面判定は暗転・演出・ロード中に頻繁に `init` へ落ちるので、実ログでは遷移がほぼ全て `init` 経由になり、1 プレイ中に `play → init → play` が何度も起きる（実測サンプルで `→ play` 21 回 ＝ 実プレイ 12 回）。`init` は直前の既知モードを保ったまま読み飛ばし、既知モードが `play` 以外 → `play` のときだけ発火する。α.2 でここを素直に拾って二重記録・空の記録を出した
- **`nowplaying` は曲情報のキャッシュであってプレイ開始ではない**（曲を決めてからプレイせずに戻ることがある）。曲決定画面由来のもので更新し、選曲画面由来のもので捨てる。プレイ開始時にその時点のキャッシュを添えるので、リトライでも直前と同じ譜面の曲情報が入る。曲決定画面かどうかの判別は payload 依存で、`images` にジャケット以外（タイトル / レベル / BPM / エフェクター / イラストレーター）の切り出し画像が入るかで見る（`SdvxFieldMapper.IsSongDecided`）
- **リザルトは `today_results`（本日分の全リザルト）で飛んでくる**。`items` の `timestamp` 最大のエントリを採り、プレイ開始より前（2 秒の猶予つき）なら前のプレイのものとして棄却する。リザルト待ちでない状態の `today_results` は無視する（接続直後に本日分のキャッシュがまとめて飛んでくる）。SDVX Helper がリザルト画面を 2 回読み取って同じプレイを 2 回登録することがあり、そのとき `timestamp` がずれる（実測 2 秒差）ので、同一判定は譜面 + 成績の内容 + 30 秒の時間窓で行う

メッセージは base64 の切り出し画像を含んで大きいため、DTO へは起こさず `JsonDocument` のまま必要なフィールドだけ読む。マッピングは `SdvxFieldMapper` に集約。**`pre_score` / `pre_ex` / `is_*_updated` / `max_exscore` は自己ベスト・理論値でこのプレイの成績ではないため取り込んでいない。**

### フォーマット識別子システム

クリップボードコピー時の文字列はユーザがフォーマット文字列で定義する。`$timestamp` `$title` `$diff_s` などの識別子が実データに置換される（識別子一覧は `docs/要件.md` 参照）。

識別子は**ハイブリッド方針**（Phase 9 で決定）。ゲーム間で意味が変わらない `$timestamp` / `$title` / `$level` / `$diff_l` / `$diff_s` は共通で流用し、体系そのものが違う成績系はゲームごとに新設する（INFINITAS: `$dj_level` / `$lamp` / `$miss_count`、pop'n: `$rank` / `$medal` / `$bad`、SDVX: `$grade` / `$clear_lamp` / `$score_short`）。「そのプレイの得点」という意味が変わらない `$score`（pop'n / SDVX）と `$ex_score`（INFINITAS / SDVX）は複数ゲームで流用する。キーの正本は `Games/FieldKeys`、どの識別子がどのゲームで有効かは `Games/GameCatalog.Identifiers` が持つ。タイムスタンプフォーマットもゲームごとに別々に保持する（`AppSettings.Infinitas` / `AppSettings.Popn` / `AppSettings.Sdvx`）。

識別子の論理名（設定画面で「論理名 ($key)」と見せるための日本語名）は `Games/FieldLabels`、
セレクトボックス / サジェストに渡す 1 項目は `Games/IdentifierChoice`。サジェストの
トークン切り出し・絞り込み・置換は `Formatting/IdentifierCompletion`（UI 非依存なのでテストがある）で、
WPF 側の `Behaviors/IdentifierSuggestion` は Popup とキー操作だけを見る。

重要な制約:
- メインウィンドウの「タイムスタンプリスト」表示は、設定ウィンドウでのフォーマット変更を**リアクティブに反映**する（実コピー文字列と画面表示が常に一致）。WPF の `INotifyPropertyChanged` / `DataContext` でバインドする想定。
- 検知できなかった識別子フィールドはフォーマット展開時に**空文字列**に置換する。
- 設定ウィンドウのプレビューはハードコードのダミーデータで表示する。

### 永続化モデル

- **バックアップ**：1 配信 = 1 JSON ファイル。ファイル名は `{ゲーム}_{配信開始日時}.json`、UTF-8（BOM なし）、2 スペースインデント。
- **保存タイミング**：配信開始時／プレイ開始追加時／プレイリザルト充填時／時間編集時／配信終了時／アプリ終了時。
- **アトミック保存**：`tmp 書込 → fsync → 既存を .bak に退避 → tmp を本名に rename → .bak 削除` の 5 段手順で書込中クラッシュに耐える。
- **JSON スキーマ**：`schemaVersion`（前方互換用）、`game`、`stream.startedAt/endedAt`、`timestamps[]`（各 ULID + ISO8601 + `fields` マップ）。詳細は `docs/要件.md` 「データ仕様」節。
- **`fields` の扱い**：識別子名（`$` 抜き）→ 値の自由形マップ。検知できなかったキーは省略。未知の識別子キーは破棄せず保持して書出し時に戻す（前方互換）。
- **過去記録の読込**：JSON を開くと `記録終了` 状態に遷移し、`記録再開` で続きを記録できる。バックアップと手動保存は同一形式。
- **異常終了復旧**：起動時に `stream.endedAt == null` のファイルを検出して、ユーザに読込／無視／削除を選択させる。

### エラーハンドリングとログ

- ログは exe 同階層 `logs/app_yyyyMMdd.log`、Info 既定、7 日ローテーション。`--log-level=Debug` 引数で詳細化。
- OBS 切断はバックオフ自動再接続。状態ラベルに進捗表示。
- 認識失敗・取得失敗は配信妨害を避けるためダイアログを出さない。状態ラベルに簡潔な状態文言を出すのみ。
- ファイル I/O 失敗・GitHub API 失敗はそれぞれ既定動作が定義されている（詳細は要件.md「エラーハンドリング・ログ・異常終了復旧」節）。

### 自己アップデート

起動時に GitHub Releases API で最新バージョンを照会し、更新があればユーザ確認のうえ `Velopack` でセルフアップデートする。専用の進捗ウィンドウ（`UpdateProgressWindow`）を持つ。`基本設定タブ` で起動時自動チェックを OFF にできる。

Velopack 未インストール環境（開発実行・ZIP 解凍配置）では `IUpdateService.IsInstalled` が false になり、リリースページをブラウザで開くフォールバックへ切り替わる。

バージョンは `Updates/SemanticVersion`（SemVer 2.0、プレリリース対応）で扱う。実行中のバージョンは csproj の `<Version>` 由来の `AssemblyInformationalVersion` から読む（`1.2.0-alpha.1+<sha>` の `+` 以降は無視）。**α 版（プレリリース）を正式版の利用者に配らない**ために、GitHub API は `releases/latest`（プレリリースを含まない）を見て、Velopack も `GithubSource(prerelease: false)` にしている。実行中が α 版のときだけ `releases` 一覧からプレリリース込みで SemVer 最大を選び、`GithubSource(prerelease: true)` に切り替える。α 版の出し方は `docs/release.md`。

## ファイル構成

```
inf-timestamper/
├── README.md                       利用者向けの説明（ロゴ・状態遷移図つき）
├── LICENSE                         MIT
├── images/                         README 用のロゴ画像
├── InfTimestamper.sln              3 プロジェクト
├── docs/
│   ├── 要件.md                     仕様の正本
│   ├── 実装計画.md                 フェーズ別の進捗・DoD・引き継ぎメモ
│   ├── release.md                  publish → vpk pack → GitHub Releases のリリース手順
│   └── sample/                     外部ツールの実出力サンプル（reflux / popn-tracker / sdvx_helper）
├── src/
│   ├── InfTimestamper/             WPF 本体。Views / ViewModels / Services / Converters / Behaviors、
│   │                               App.xaml.cs（DI 組立）、Program.cs（独自 Main + Velopack）、
│   │                               Assets/icon.ico（exe と全 Window のアイコン）
│   └── InfTimestamper.Core/        UI 非依存のドメイン層（下記）
└── tests/
    └── InfTimestamper.Core.Tests/  xUnit。ViewModel のテストもここに置く
```

`InfTimestamper.Core/` の内訳:

| ディレクトリ | 役割 |
| --- | --- |
| `Coordination/` | `RecordingCoordinator` — 状態機械・プレイ監視・OBS 接続を束ねる中核 |
| `States/` | `AppStateMachine` — 4 状態のメイン状態機械 |
| `Games/` | ゲーム抽象化。`FieldKeys`（識別子キーの正本）/ `FieldLabels`（識別子の論理名）/ `GameCatalog`（ゲーム別メタデータ）/ `IPlayWatcher` / `WatchTarget` / `PlayEvents` |
| `Reflux/` | INFINITAS のゲーム検知。`RefluxPlayWatcher` / `RefluxLatestJson` / `RefluxFieldMapper` |
| `Popn/` | pop'n music のゲーム検知。`PopnPlayWatcher` / `PopnResultJson` / `PopnFieldMapper` |
| `Sdvx/` | SOUND VOLTEX のゲーム検知。`SdvxHelperPlayWatcher`（WebSocket 購読）/ `SdvxHelperLogTail`（ログ監視）/ `SdvxFieldMapper` |
| `Obs/` | OBS WebSocket 接続と再接続バックオフ。配信開始/終了の検知のみ |
| `Persistence/` | `JsonRecordStore` — バックアップ JSON のアトミック保存と異常終了復旧 |
| `Formatting/` | `FormatExpander`（`$identifier` の展開）/ `IdentifierCompletion`（サジェストのロジック） |
| `Models/` | `StreamRecord` / `TimestampEntry` / `GameId` 等 |
| `Settings/` | `AppSettings` / `SettingsStore` |
| `Updates/` | GitHub Releases 照会とバージョン比較 |
| `Threading/` | `IUiDispatcher`（UI スレッドへのマーシャリング抽象） |

同梱リソースは持たない。プレイ検知は外部ツールの出力を読むだけなので、publish 出力は exe 単体になる。SDVX の WebSocket 購読も BCL の `System.Net.WebSockets.ClientWebSocket` で済ませており、追加パッケージは無い。
