namespace InfTimestamper.Core.Games;

/// <summary>
/// ゲームのプレイ開始 / プレイリザルトを検知する監視機構。
///
/// 実装は取得元（<see cref="PlaySourceId"/>）ごとに 1 つ。INFINITAS が <c>Reflux.RefluxPlayWatcher</c> と
/// <c>DakenCounter.DakenCounterPlayWatcher</c>（設定で選ぶ）、pop'n music が <c>Popn.PopnPlayWatcher</c>、
/// SOUND VOLTEX が <c>Sdvx.SdvxHelperPlayWatcher</c>。
/// Reflux / pop'n は「外部ツールが出力する 1 行の状態ファイル + JSON のリザルトファイル」のファイル監視、
/// 打鍵カウンタ / SDVX は外部ツールのデータ配信 WebSocket の購読と、取得方式が異なる。
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
    /// 監視を開始する。<paramref name="target"/> のどの要素を使うかは実装ごとに違い、
    /// ファイル監視系は <see cref="WatchTarget.Directory"/>、打鍵カウンタは <see cref="WatchTarget.Endpoint"/>、
    /// SDVX は <see cref="WatchTarget.Endpoint"/>（+ 任意で <see cref="WatchTarget.Directory"/>）。
    /// 監視対象が不正な場合は例外を投げる。
    /// </summary>
    void Start(WatchTarget target);

    Task StopAsync();
}
