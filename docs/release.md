# リリース手順

INF-TIMESTAMPER (C# 実装) を GitHub Releases に配布するための手順。
Velopack による自動アップデートを前提とする。

## 前提

- Windows 11（x64）
- .NET SDK 9.0 以降（プロジェクトのターゲットは net8.0 / net8.0-windows）
- `vpk` CLI（Velopack のリリースツール）
- GitHub の Personal Access Token（リリース作成権限）

## 1. リリース前チェック

- [ ] `dotnet test` がすべてグリーン
- [ ] `dotnet build` で警告 0 / エラー 0
- [ ] `docs/要件.md` と `docs/実装計画.md` の整合性
- [ ] `src/InfTimestamper/InfTimestamper.csproj` のバージョン（後述）を更新
- [ ] 楽曲 DB を最新化（INFINITAS に新曲が追加されている場合）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/generate_songs_json.ps1
git add src/InfTimestamper.Core/Resources/INFINITAS/songs.json
git commit -m "data: songs.json を最新化"
```

## 2. バージョン番号の更新

`src/InfTimestamper/InfTimestamper.csproj` に以下を追加（既になければ）:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
</PropertyGroup>
```

リリース時にこの値を更新する。セマンティックバージョニング (Major.Minor.Patch) を採用。

## 3. single-file publish

```powershell
dotnet publish src/InfTimestamper/InfTimestamper.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish/InfTimestamper-win-x64
```

- `--self-contained true`: .NET ランタイムを exe に同梱（インストール不要で動く）
- `-p:PublishSingleFile=true`: 単一 exe へまとめる
- `-p:IncludeNativeLibrariesForSelfExtract=true`: OpenCvSharp / Tesseract / Velopack のネイティブ DLL を同梱

出力フォルダ `publish/InfTimestamper-win-x64/` 内に exe と必要なリソース（`Resources/INFINITAS/songs.json` 等）が並ぶ。

### `tessdata` の同梱

OCR を有効化するには `publish/InfTimestamper-win-x64/tessdata/eng.traineddata` を手動配置する必要がある。
未配置時は `TesseractOcrService.IsAvailable = false` で OCR が無効化されるだけでアプリは起動する。

## 4. Velopack でリリースバンドル化

`vpk` CLI を未インストールならインストール:

```powershell
dotnet tool install -g vpk
```

リリースバンドルの作成:

```powershell
vpk pack `
  --packId InfTimestamper `
  --packVersion 1.0.0 `
  --packDir publish/InfTimestamper-win-x64 `
  --mainExe InfTimestamper.exe `
  --packTitle "INF-TIMESTAMPER"
```

成果物は `Releases/` 配下に出る:
- `Setup.exe`: 新規インストーラ
- `*.nupkg`: 差分アップデートに使う Velopack パッケージ
- `RELEASES`: バージョン索引ファイル

## 5. GitHub Releases へ公開

GitHub Web 上で新規 Release を作成し、上記成果物をアップロード。または `vpk` の upload コマンドで:

```powershell
vpk upload github `
  --repoUrl https://github.com/Freedom645/inf-timestamper `
  --token $env:GITHUB_TOKEN `
  --releaseName "v1.0.0"
```

`token` は事前に環境変数または `gh auth login` で設定。

タグは `v1.0.0` 形式。`VersionComparer` が `v` プレフィックスを許容するので、これでアプリ側のバージョンチェックが動く。

## 6. リリース後の動作確認

- [ ] 別 PC で `Setup.exe` を実行 → インストール完了
- [ ] アプリを起動して動作確認（OBS 接続、状態遷移、コピー）
- [ ] 設定ダイアログで「最新バージョンチェック」を押下 → "現在のバージョンが最新です"
- [ ] 次のリリースを行ったら、起動時自動チェックで更新検出 → ダウンロード → 再起動

## 既知の制約

- OpenCvSharp / Tesseract / Velopack のネイティブ DLL は `IncludeNativeLibrariesForSelfExtract` で同梱されるが、起動時に `%TEMP%` 配下に展開される。初回起動は若干遅延する場合がある
- `tessdata` をアプリに同梱する場合、ライセンス（Apache 2.0）の表記をアプリの About やリリースノートに含めること
- Velopack は exe を「インストール済みの場所」から動作させる前提なので、`Program Files` や `%LOCALAPPDATA%` 配下にインストールされる。ZIP 解凍配置や開発実行（`dotnet run`）では `IUpdateService.IsInstalled = false` で自動アップデートは動かず、リリースページ起動フォールバックが使われる

## ロールバック

リリース後に問題が見つかったら、GitHub Release を Draft / Unpublished に変更すれば新規ユーザへの配布を停止できる（既にダウンロード済みのユーザには影響しない）。次のパッチリリースで修正する。
