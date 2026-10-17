using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Games;

/// <summary>
/// プレイ検知の取得元（外部ツール）。<c>RecordingCoordinator</c> はこれでどの <see cref="IPlayWatcher"/> を
/// 動かすかを決める。
///
/// 1 ゲームに取得元が 1 つとは限らないため、ゲーム（<see cref="GameId"/>）とは分けている。
/// INFINITAS は Reflux と打鍵カウンタのどちらかを設定で選ぶ（issue #13）。
/// </summary>
public enum PlaySourceId
{
    /// <summary>INFINITAS: Reflux の出力ファイルの監視。</summary>
    Reflux,

    /// <summary>INFINITAS: 打鍵カウンタ（inf_daken_counter_obsw）のデータ配信 WebSocket の購読。</summary>
    DakenCounter,

    /// <summary>pop'n music: popn-lively-tracker の出力ファイルの監視。</summary>
    PopnTracker,

    /// <summary>SOUND VOLTEX: SDVX Helper のデータ配信 WebSocket の購読。</summary>
    SdvxHelper,
}

public static class PlaySourceIdExtensions
{
    // INFINITAS の検知元の選択を settings.json に書くときの表記
    public const string RefluxSerialized = "reflux";
    public const string DakenCounterSerialized = "daken_counter";

    /// <summary>INFINITAS の検知元の永続化表記を解釈する。未知の値・欠損時は Reflux（従来どおり）。</summary>
    public static PlaySourceId ParseInfinitasSource(string? value)
        => value == DakenCounterSerialized ? PlaySourceId.DakenCounter : PlaySourceId.Reflux;

    public static string ToInfinitasSerializedString(this PlaySourceId source)
        => source == PlaySourceId.DakenCounter ? DakenCounterSerialized : RefluxSerialized;
}
