# データ整備手順（tessdata / 認識リソース）

INF-TIMESTAMPER の画像認識を動かすために必要な「ユーザ整備データ」の準備手順をまとめる。
コード側はすべての参照データが空でもビルド・起動できる構造になっているので、ここで整備したデータを `Resources/INFINITAS/` と `tessdata/` に置くと認識が動き始める。

## 必要なもの

| 種類 | 配置先 | 役割 |
| --- | --- | --- |
| `eng.traineddata` | `tessdata/` | Tesseract OCR の英字+数字辞書（曲名・数値の認識） |
| `songs.json` | `Resources/INFINITAS/` | 楽曲データベース（既に同梱済み） |
| `hashes.json` | `Resources/INFINITAS/` | 状態判定・固定アイコン照合用 aHash 集合 |
| `rois.json` | `Resources/INFINITAS/` | OCR 用 ROI 座標 |
| 参照画像 | `Resources/INFINITAS/reference_images/`（任意、開発用） | aHash 生成元の正解画像 |

## 1. tessdata の同梱

### 入手

[tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata) リポジトリから `eng.traineddata` を取得。

- 標準版: [tessdata/eng.traineddata](https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata) （約 22 MB、曲名 OCR 推奨）
- 軽量版: [tessdata_fast/eng.traineddata](https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata) （約 4 MB、数字 OCR のみなら十分）

ライセンス: Apache 2.0

### 配置

- 開発実行: `src/InfTimestamper/bin/Debug/net8.0-windows/tessdata/eng.traineddata`
- publish: `publish/InfTimestamper-win-x64/tessdata/eng.traineddata`

ファイル名は固定（`eng.traineddata`）。`TesseractOcrService` は `AppContext.BaseDirectory/tessdata/eng.traineddata` を見に行く。未配置時は `IsAvailable=false` で OCR が無効化されるだけでアプリは起動する。

### リリース時の自動同梱（任意）

毎回手作業でコピーする手間を減らすため、tessdata を git に含めて csproj から自動コピーする手もある。ただしファイルサイズが大きいので、git LFS 利用 or サブモジュール化を推奨。本リポジトリでは未採用。

## 2. 参照画像の収集

INFINITAS のキャプチャ（1920×1080）を以下の状態ごとに用意する。

### 必須キャプチャ

- **選曲中（song_select）**: 1P側、2P側、SP/DP のサブパターンごと
- **プレイ開始（play_start）**: 譜面読み込み・カウントダウンなど選曲が確定した状態
- **リザルト（result）**: スコア・ランプ・DJ レベル等が表示された画面
- **譜面難易度アイコン**: SPB / SPN / SPH / SPA / SPL / DPB / DPN / DPH / DPA / DPL（最大 10 種）
- **DJ レベル**: F / E / D / C / B / A / AA / AAA（8 種）
- **クリアランプ**: FAILED / A-EASY / EASY / NORMAL / HARD / EX-HARD / FC（7 種）

`Resources/INFINITAS/reference_images/` 配下に整理して保存（開発用、配布バイナリには含めない）:

```
reference_images/
  states/
    song_select_1p.png
    song_select_2p.png
    play_start.png
    result.png
  difficulty/
    SPA.png  SPB.png  SPN.png  ...
  dj_level/
    AAA.png  AA.png  A.png  ...
  lamp/
    FC.png  EX-HARD.png  HARD.png  ...
```

### 参考情報

参考実装: [dj-kata/inf_daken_counter_obsw](https://github.com/dj-kata/inf_daken_counter_obsw) （Apache 2.0）の `src/screen_reader.py` と参照画像が、ROI 座標とサブパターン分けの設計参考になる。aHash 値は実装ライブラリが異なるため再計算が必要。

## 3. ROI 座標の取得

GIMP / Paint.NET / Snipping Tool 等で 1920×1080 のキャプチャから ROI を測定する。

各 ROI は `[x, y, width, height]` の 4 要素配列で記述する。座標は 1920×1080 基準。

## 4. aHash の計算 (`tools/HashExtractor`)

aHash 値を計算する補助ツールを `tools/HashExtractor` に同梱している。

### 単一 ROI

```powershell
dotnet run --project tools/HashExtractor -- `
  --image reference_images/states/song_select_1p.png `
  --roi 1700,30,100,60

# 出力: hash    [1700,30,100,60]    0x00ff00ff12345678
```

### バッチ（CSV）

`tools/sample_rois.csv` のような CSV を用意:

```csv
# name,x,y,w,h
song_select_1p,1700,30,100,60
song_select_2p,120,30,100,60
difficulty_SPA,720,540,80,30
difficulty_SPN,720,540,80,30
dj_level_AAA,1200,400,100,80
lamp_FC,900,500,120,40
```

実行:

```powershell
dotnet run --project tools/HashExtractor -- `
  --image reference_images/result.png `
  --batch tools/sample_rois.csv
```

出力（TSV）を貼り付けて `hashes.json` に組み立てる。

## 5. `hashes.json` の構造

```json
{
  "states": {
    "song_select": [
      { "name": "1p_controller", "roi": [1700, 30, 100, 60], "ahash": "0x00ff...", "threshold": 10 },
      { "name": "2p_controller", "roi": [120, 30, 100, 60],  "ahash": "0xff00...", "threshold": 10 }
    ],
    "play_start": [ /* 同様にサブパターンを並べる */ ],
    "result":     [ /* 同様 */ ]
  },
  "difficulty": [
    { "value": "SPA", "roi": [720, 540, 80, 30], "ahash": "0x...", "threshold": 10 },
    { "value": "SPN", "roi": [720, 540, 80, 30], "ahash": "0x...", "threshold": 10 }
  ],
  "dj_level": [
    { "value": "AAA", "roi": [1200, 400, 100, 80], "ahash": "0x...", "threshold": 10 }
  ],
  "lamp": [
    { "value": "FC", "roi": [900, 500, 120, 40], "ahash": "0x...", "threshold": 10 }
  ]
}
```

- `roi`: 1920×1080 基準の `[x, y, w, h]`
- `ahash`: HashExtractor 出力の hex 値（`"0x..."`）
- `threshold`: ハミング距離許容値（既定 10、要件 L409 の参考値）

state の `name` キーは識別用のラベル（自由）。`difficulty` / `dj_level` / `lamp` の `value` は要件で定義された値（SPA、AAA、FC 等）。

## 6. `rois.json` の構造

OCR 用 ROI のみ。キー名は `FrameRecognizer` が参照（`RecognitionFieldKeys` の定数）:

```json
{
  "title":      [600, 100, 720, 40],
  "level":      [800, 110, 80, 30],
  "miss_count": [1500, 800, 120, 50],
  "ex_score":   [1200, 800, 200, 50]
}
```

| キー | 内容 | 取得手段 |
| --- | --- | --- |
| `title` | 曲名 | Tesseract 一般 OCR + 楽曲 DB fuzzy match |
| `level` | レベル数値 (1-12) | Tesseract 数字限定 OCR |
| `miss_count` | BAD POOR 合計 | Tesseract 数字限定 OCR |
| `ex_score` | EX SCORE | Tesseract 数字限定 OCR |

## 7. 動作確認

データを配置したら、`dotnet run --project src/InfTimestamper` で起動 → 設定ダイアログで OBS 接続情報とゲームソース名を入力 → 開始 → INFINITAS をプレイして、タイムスタンプリストに自動でエントリが追加されることを確認する。

認識が安定しない場合:
- `threshold` を上げる（10 → 12 など）
- 参照画像を別パターンも追加して `hashes.json` の `states.song_select` などに増やす
- `--log-level=Debug` で起動して `logs/app_*.log` に出る検知ログを確認

## 8. ライセンス表記

リリース時には以下のサードパーティ著作物のライセンスを About 等に明記する:
- Tesseract `eng.traineddata`: Apache License 2.0
- 参考実装の状態判定アプローチ: [dj-kata/inf_daken_counter_obsw](https://github.com/dj-kata/inf_daken_counter_obsw)（Apache License 2.0）
