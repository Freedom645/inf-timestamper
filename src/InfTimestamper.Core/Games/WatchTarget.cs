namespace InfTimestamper.Core.Games;

/// <summary>
/// ゲーム検知の監視対象。
/// INFINITAS / pop'n music は外部ツールの出力ディレクトリ（<see cref="Directory"/>）だけ。
/// SOUND VOLTEX は SDVX Helper のデータ配信 WebSocket（<see cref="Endpoint"/>）に加えて、
/// 任意で SDVX Helper のフォルダ（<see cref="Directory"/>。ログ監視に使う）を持つ。
/// </summary>
public sealed record WatchTarget(string? Directory = null, string? Endpoint = null)
{
    public static WatchTarget Empty { get; } = new();

    public static WatchTarget ForDirectory(string? directory)
        => new(Directory: Normalize(directory));

    public static WatchTarget ForEndpoint(string? endpoint, string? directory = null)
        => new(Directory: Normalize(directory), Endpoint: Normalize(endpoint));

    /// <summary>監視対象が何も指定されていない（＝プレイ検知を行わない）。</summary>
    public bool IsEmpty => Directory is null && Endpoint is null;

    public bool HasDirectory => Directory is not null;
    public bool HasEndpoint => Endpoint is not null;

    /// <summary>ログや状態表示向けの 1 行表記。</summary>
    public override string ToString()
    {
        if (IsEmpty) return "(未設定)";
        if (Endpoint is null) return Directory!;
        return Directory is null ? Endpoint : $"{Endpoint} + {Directory}";
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
