namespace InfTimestamper.Services;

/// <summary>異常終了復旧の確認ダイアログでユーザが選んだ操作（要件「異常終了からの復旧」）。</summary>
public enum UnfinishedRecordChoice
{
    /// <summary>読み込む。</summary>
    Load,

    /// <summary>無視する（次回起動時にも再度提示される）。</summary>
    Ignore,

    /// <summary>ファイルをゴミ箱へ送る。</summary>
    Delete,
}
