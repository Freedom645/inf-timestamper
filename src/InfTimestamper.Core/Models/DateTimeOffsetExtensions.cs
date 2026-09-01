namespace InfTimestamper.Core.Models;

public static class DateTimeOffsetExtensions
{
    /// <summary>
    /// 1 秒未満を切り捨てる。記録ファイルは秒精度で書出す（要件「日時フォーマット」）ため、
    /// メモリ上の値と読み直した値がずれないよう、記録に載せる時点で丸めておく。
    /// </summary>
    public static DateTimeOffset TruncateToSecond(this DateTimeOffset value)
        => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
}
