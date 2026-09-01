namespace InfTimestamper.ViewModels;

/// <summary>
/// タイムスタンプリストの 1 行。実際のプレイ記録（<see cref="TimestampViewModel"/>）と、
/// 先頭に差し込む「配信開始」行（<see cref="StreamStartRowViewModel"/>）を同じ一覧に並べるための抽象。
/// 画面表示とクリップボードコピーは常にこの <see cref="DisplayText"/> を使うので、両者は必ず一致する。
/// </summary>
public interface ITimestampRow
{
    string DisplayText { get; }
}
