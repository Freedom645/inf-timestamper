using System.Text.Json;
using InfTimestamper.Core.Reflux;

namespace InfTimestamper.Core.Tests.TestHelpers;

/// <summary>
/// <see cref="RefluxPlayWatcher"/> を FileSystemWatcher のタイミングに依存せず駆動するテストハーネス。
/// 一時ディレクトリに Reflux のファイルを書き、<c>ProcessPlayState()</c> を直接呼んで遷移を起こす。
/// watcher は debounce=0 で構築すること。
/// </summary>
public sealed class RefluxTestHarness : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly RefluxPlayWatcher _watcher;

    public RefluxTestHarness(RefluxPlayWatcher watcher)
    {
        _watcher = watcher;
        _watcher.ConfigureForTest(_dir.Path);
    }

    public string Path => _dir.Path;

    /// <summary>off/menu → play 遷移を起こす（プレイ開始）。</summary>
    public void EnterPlay(string title, int level)
    {
        Write(RefluxPlayWatcher.TitleFileName, title);
        Write(RefluxPlayWatcher.LevelFileName, level.ToString());
        Write(RefluxPlayWatcher.PlayStateFileName, RefluxPlayWatcher.PlayStatePlay);
        _watcher.ProcessPlayState();
    }

    /// <summary>play → 非play 遷移を起こす（プレイリザルト）。</summary>
    public void LeavePlay(RefluxLatestJson latest, string newState = "menu")
    {
        Write(RefluxPlayWatcher.LatestJsonFileName, JsonSerializer.Serialize(latest));
        Write(RefluxPlayWatcher.PlayStateFileName, newState);
        _watcher.ProcessPlayState();
    }

    /// <summary>playstate.txt を任意の値にして遷移処理を走らせる。</summary>
    public void SetPlayState(string state)
    {
        Write(RefluxPlayWatcher.PlayStateFileName, state);
        _watcher.ProcessPlayState();
    }

    private void Write(string name, string content)
        => File.WriteAllText(System.IO.Path.Combine(_dir.Path, name), content);

    public void Dispose() => _dir.Dispose();
}
