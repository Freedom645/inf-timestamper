namespace InfTimestamper.Core.Games;

/// <summary>
/// ゲームのプレイ開始 / プレイリザルトを検知する監視機構。
///
/// 対応ゲームはいずれも「外部ツールが出力する 1 行の状態ファイル + JSON のリザルトファイル」を
/// 監視する構造なので、RecordingCoordinator からはこの抽象だけを見る。
/// 実装は INFINITAS が <c>Reflux.RefluxPlayWatcher</c>、pop'n music が <c>Popn.PopnPlayWatcher</c>。
/// </summary>
public interface IPlayWatcher : IAsyncDisposable
{
    /// <summary>プレイ開始検知。CapturedAt はエッジ検知時刻。</summary>
    event EventHandler<PlayStartedEventArgs>? PlayStarted;

    /// <summary>プレイリザルト検知。Fields は識別子へ変換済みの値。</summary>
    event EventHandler<PlayResultEventArgs>? PlayResultDetected;

    bool IsRunning { get; }

    /// <summary>指定ディレクトリの監視を開始する。ディレクトリが無ければ例外を投げる。</summary>
    void Start(string directory);

    Task StopAsync();
}
