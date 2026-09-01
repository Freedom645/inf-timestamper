using System.IO;
using System.Windows;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Services;

namespace InfTimestamper.Views;

/// <summary>
/// 異常終了復旧の確認ダイアログ。要件どおり「はい（読み込む）／いいえ（無視する）／削除する」の 3 択を出す。
/// </summary>
public partial class UnfinishedRecordDialog : Window
{
    public UnfinishedRecordDialog()
    {
        InitializeComponent();
    }

    public UnfinishedRecordDialog(UnfinishedRecord record) : this()
    {
        FileNameText.Text = Path.GetFileName(record.FilePath);
        LastModifiedText.Text = record.LastModified.ToString("yyyy-MM-dd HH:mm:ss");
        TimestampCountText.Text = $"{record.Record.Timestamps.Count} 件";
    }

    /// <summary>ユーザの選択。ウィンドウを × で閉じた場合は「無視する」扱い。</summary>
    public UnfinishedRecordChoice Choice { get; private set; } = UnfinishedRecordChoice.Ignore;

    private void OnLoadClick(object sender, RoutedEventArgs e) => Finish(UnfinishedRecordChoice.Load);

    private void OnIgnoreClick(object sender, RoutedEventArgs e) => Finish(UnfinishedRecordChoice.Ignore);

    private void OnDeleteClick(object sender, RoutedEventArgs e) => Finish(UnfinishedRecordChoice.Delete);

    private void Finish(UnfinishedRecordChoice choice)
    {
        Choice = choice;
        DialogResult = true;
        Close();
    }
}
