# リリース手順

INF-TIMESTAMPER (C# 実装) を GitHub Releases に配布するための手順。
Velopack による自動アップデートを前提とする。

## 前提

- Windows 11（x64）
- .NET SDK 9.0 以降（プロジェクトのターゲットは net8.0 / net8.0-windows）
- `vpk` CLI（Velopack のリリースツール）。**アプリが参照している Velopack パッケージと同じバージョンを入れること**
- GitHub の Personal Access Token（リリース作成権限）

## 1. リリース前チェック

- [ ] `dotnet test` がすべてグリーン
- [ ] `dotnet build` で警告 0 / エラー 0
- [ ] `docs/要件.md` と `docs/実装計画.md` の整合性
- [ ] `src/InfTimestamper/InfTimestamper.csproj` のバージョン（後述）を更新
- [ ] INFINITAS / pop'n の双方で実機の通し確認

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
- `-p:IncludeNativeLibrariesForSelfExtract=true`: WPF / Velopack のネイティブ DLL を単一 exe に取り込む

出力フォルダ `publish/InfTimestamper-win-x64/` に `InfTimestamper.exe` と pdb だけが並ぶ。
外部リソースファイルは持たない（プレイ検知は外部ツールの出力を読むだけで、同梱データが不要なため）。

`EnableCompressionInSingleFile` を csproj で有効にしているので、exe は約 73MB になる
（無効時は約 165MB）。初回起動時に `%TEMP%` へ展開されるぶん、初回だけ起動が少し遅い。

## 4. Velopack でリリースバンドル化

`vpk` CLI を未インストールならインストールする。
**`src/InfTimestamper/InfTimestamper.csproj` の `Velopack` パッケージと同じバージョンを指定する**
（CLI とライブラリのバージョンが食い違うと生成物が壊れる）。

```powershell
dotnet tool install -g vpk --version 0.0.1298
```

新しい vpk があると警告が出るが、上げるときは csproj の `Velopack` も合わせて上げること。

リリースバンドルの作成:

```powershell
vpk pack `
  --packId InfTimestamper `
  --packVersion 1.0.0 `
  --packDir publish/InfTimestamper-win-x64 `
  --mainExe InfTimestamper.exe `
  --packTitle "INF-TIMESTAMPER" `
  --packAuthors "Freedom645" `
  --icon src/InfTimestamper/Assets/icon.ico
```

成果物は `Releases/` 配下に出る（`.gitignore` 済み）:

| ファイル | 内容 |
| --- | --- |
| `InfTimestamper-win-Setup.exe` | 新規インストーラ（約 69MB）。Releases に上げる主役 |
| `InfTimestamper-1.0.0-full.nupkg` | 自己アップデートが取得する Velopack パッケージ（約 66MB） |
| `InfTimestamper-win-Portable.zip` | インストール不要の展開配置版。自己アップデートは効かない |
| `RELEASES` / `releases.win.json` / `assets.win.json` | バージョン索引。**アップデート検出に必要なので必ず一緒に上げる** |

`--exclude` の既定が `.*\.pdb` なので pdb はパッケージに入らない。

### コード署名について

署名パラメータを渡していないため、生成物は未署名になる（`No signing parameters provided` の警告が出る）。
未署名の実行ファイルは Windows SmartScreen で警告が表示され、ユーザは「詳細情報」→「実行」を
選ぶ必要がある。コード署名証明書を用意する場合は `--signParams` または `--signTemplate` を使う。

## 5. GitHub Releases へ公開

GitHub Web 上で新規 Release を作成し、`Releases/` 配下の成果物を**すべて**アップロードする
（索引ファイルが無いと自己アップデートが動かない）。または `vpk` の upload コマンドで:

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
- [ ] アプリを起動して動作確認（OBS 接続、ゲーム選択、状態遷移、コピー）
- [ ] 設定ダイアログで「最新バージョンチェック」を押下 → "現在のバージョンが最新です"
- [ ] 次のリリースを行ったら、起動時自動チェックで更新検出 → ダウンロード → 再起動

## 既知の制約

- ネイティブ DLL は `IncludeNativeLibrariesForSelfExtract` で同梱されるが、起動時に `%TEMP%` 配下に展開される。単一 exe を圧縮しているぶんと合わせて、初回起動は若干遅延する
- Velopack は exe を「インストール済みの場所」から動作させる前提なので、`Program Files` や `%LOCALAPPDATA%` 配下にインストールされる。ZIP 解凍配置や開発実行（`dotnet run`）では `IUpdateService.IsInstalled = false` で自動アップデートは動かず、リリースページ起動フォールバックが使われる

## ロールバック

リリース後に問題が見つかったら、GitHub Release を Draft / Unpublished に変更すれば新規ユーザへの配布を停止できる（既にダウンロード済みのユーザには影響しない）。次のパッチリリースで修正する。
