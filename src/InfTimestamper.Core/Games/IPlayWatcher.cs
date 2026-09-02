namespace InfTimestamper.Core.Games;

/// <summary>
/// ゲームのプレイ開始 / プレイリザルトを検知する監視機構。
///
/// 実装は INFINITAS が <c>Reflux.RefluxPlayWatcher</c>、pop'n music が <c>Popn.PopnPlayWatcher</c>、
/// SOUND VOLTEX が <c>Sdvx.SdvxHelperPlayWatcher</c>。
/// 前 2 者は「外部ツールが出力する 1 行の状態ファイル + JSON のリザルトファイル」のファイル監視、
/// SDVX は SDVX Helper のデータ配信 WebSocket の購読と、取得元が異なる。
/// RecordingCoordinator からはこの抽象だけを見る。
/// </summary>
public interface IPlayWatcher : IAsyncDisposable
{
    /// <summary>プレイ開始検知。CapturedAt はエッジ検知時刻。</summary>
    event EventHandler<PlayStartedEventArgs>? PlayStarted;

    /// <summary>プレイリザルト検知。Fields は識別子へ変換済みの値。</summary>
    event EventHandler<PlayResultEventArgs>? PlayResultDetected;

    bool IsRunning { get; }

    /// <summary>
    /// 監視を開始する。<paramref name="source"/> は実装ごとの監視対象で、
    /// ファイル監視系は出力ディレクトリのパス、SDVX は <c>ws://host:port</c> 形式の接続先。
    /// 監視対象が不正な場合は例外を投げる。
    /// </summary>
    void Start(string source);

    Task StopAsync();
}
