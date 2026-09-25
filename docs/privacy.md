# プライバシーポリシー

INF-TIMESTAMPER（以下「本アプリ」）が扱う情報についての説明です。
本アプリは利用者の PC 上だけで動作するデスクトップアプリで、開発者が運営するサーバはありません。
**開発者が利用者の情報を受け取ることはありません。**

## YouTube 連携で扱う情報

YouTube 連携（ライブ配信の概要欄の自動更新）を有効にした場合、本アプリは Google の YouTube Data API を利用します。

### 取得する情報

| 情報 | 用途 |
| --- | --- |
| 配信中のライブ配信の ID・タイトル・開始時刻 | 記録中の配信に対応するライブを特定するため |
| そのライブ配信のタイトル・概要欄・カテゴリ・タグ | 概要欄を書き換える際に、他の項目を変えずに送り返すため |
| チャンネル名 | 設定画面にログイン中のアカウントを表示するため |

### 変更する情報

- 対象のライブ配信の**概要欄**のみを書き換えます（見出し行から末尾までをタイムスタンプに置き換える、または末尾に追記します）
- タイトル・カテゴリ・タグ等は取得した値をそのまま送り返し、変更しません
- 動画の削除・コメント・チャンネル設定の変更などは行いません

### 保存する情報

- OAuth クライアント ID / クライアント シークレット / リフレッシュトークン / チャンネル名を、利用者の PC の
  `%APPDATA%\inf-timestamper\youtube_credential.bin` に保存します。Windows の DPAPI で暗号化しており、同じ Windows ユーザでしか復号できません
- アクセストークンはメモリ上にのみ保持し、ファイルには保存しません
- 取得した概要欄等の内容は保存しません（ログファイルには件数や状態のみを記録します）

### 送信先

- 上記の情報は Google（`accounts.google.com` / `oauth2.googleapis.com` / `www.googleapis.com`）との間でのみ送受信します
- 第三者（開発者を含む）へ送信・共有することはありません

### アクセス権の取り消し

- 本アプリの設定画面の「ログアウト」で、Google 側のトークンを失効させ、保存した情報を削除します
- Google アカウントの [サードパーティのアクセス](https://myaccount.google.com/connections) からも取り消せます

本アプリによる Google API から受け取った情報の利用は、
[Google API サービスのユーザーデータに関するポリシー](https://developers.google.com/terms/api-services-user-data-policy)
（限定的使用の要件を含む）に従います。YouTube API サービスの利用には
[YouTube 利用規約](https://www.youtube.com/t/terms) と [Google プライバシー ポリシー](https://policies.google.com/privacy) が適用されます。

## その他の情報

- タイムスタンプの記録・設定は利用者の PC（`%APPDATA%\inf-timestamper\`）にのみ保存します
- 起動時のアップデート確認のため、GitHub（`api.github.com`）へ最新リリースを問い合わせます。利用者を識別する情報は送りません

## お問い合わせ

[GitHub の Issues](https://github.com/Freedom645/inf-timestamper/issues) までお願いします。
