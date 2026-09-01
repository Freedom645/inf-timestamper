namespace InfTimestamper.Services;

/// <summary>
/// ファイルをゴミ箱へ送る。要件「異常終了からの復旧」で `削除する` を選んだ場合、
/// 即時完全削除ではなくゴミ箱送りにする必要があるため抽象化している。
/// </summary>
public interface IFileRecycler
{
    void SendToRecycleBin(string path);
}
