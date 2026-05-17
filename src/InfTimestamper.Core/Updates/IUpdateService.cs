namespace InfTimestamper.Core.Updates;

public interface IUpdateService
{
    /// <summary>
    /// Velopack 経由でインストールされた状態かどうか。
    /// false なら自動アップデートは適用できない（開発実行・ZIP 解凍配置等）。
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// 最新バージョンを確認し、見つかればダウンロードまで実施する。
    /// 戻り値: true=新バージョンをダウンロード済み、false=最新または未インストール。
    /// progress は 0-100 のパーセンテージを報告する。
    /// </summary>
    Task<bool> CheckAndDownloadAsync(IProgress<int> progress, CancellationToken cancellationToken);

    /// <summary>
    /// ダウンロード済みのアップデートを適用してアプリを再起動する。
    /// CheckAndDownloadAsync が true を返した後に呼び出すこと。
    /// </summary>
    void ApplyAndRestart();
}
