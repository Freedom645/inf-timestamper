namespace InfTimestamper.ViewModels;

/// <summary>
/// タイムスタンプリストの 1 行。実際のプレイ記録（<see cref="TimestampViewModel"/>）と、
/// 先頭に差し込む「配信開始」行（<see cref="StreamStartRowViewModel"/>）を同じ一覧に並べるための抽象。
/// 画面表示とクリップボードコピーは常にこの <see cref="DisplayText"/> を使うので、両者は必ず一致する。
/// </summary>
public interface ITimestampRow
{
    string DisplayText { get; }

    /// <summary>
    /// 選択されているか。チェックボックスと <c>ListBoxItem.IsSelected</c> の双方から書かれるので、
    /// Shift+クリック / Ctrl+クリックの範囲選択もそのままここに入る。
    /// </summary>
    bool IsSelected { get; set; }

    /// <summary>選択・日時編集の対象になれるか。配信開始行は対象外。</summary>
    bool IsSelectable { get; }
}
