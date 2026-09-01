using System.Text.Json;
using InfTimestamper.Core.Popn;

namespace InfTimestamper.Core.Tests.TestHelpers;

/// <summary>
/// <see cref="PopnPlayWatcher"/> を FileSystemWatcher のタイミングに依存せず駆動するテストハーネス。
/// 一時ディレクトリに popn-lively-tracker のファイルを書き、<c>ProcessState()</c> /
/// <c>ProcessResult()</c> を直接呼んで遷移を起こす。
/// </summary>
public sealed class PopnTestHarness : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly PopnPlayWatcher _watcher;

    public PopnTestHarness(PopnPlayWatcher watcher)
    {
        _watcher = watcher;
        _watcher.ConfigureForTest(_dir.Path);
    }

    public string Path => _dir.Path;

    /// <summary>state.txt を任意の値にして状態処理を走らせる。</summary>
    public void SetState(string state)
    {
        Write(PopnPlayWatcher.StateFileName, state);
        _watcher.ProcessState();
    }

    /// <summary>非プレイ中 → プレイ中 の遷移を起こす（プレイ開始）。</summary>
    public void EnterPlay()
    {
        SetState(PopnPlayWatcher.StateMusicSelect);
        SetState(PopnPlayWatcher.StatePlaying);
    }

    /// <summary>プレイ中 → プレイ終了 の遷移だけを起こす（result.json はまだ書かれない）。</summary>
    public void LeavePlay() => SetState(PopnPlayWatcher.StatePlayEnded);

    /// <summary>result.json を書き換えて、その検知処理を走らせる。</summary>
    public void WriteResult(PopnResultJson result)
    {
        Write(PopnPlayWatcher.ResultFileName, JsonSerializer.Serialize(result));
        _watcher.ProcessResult();
    }

    /// <summary>result.json に任意の生 JSON を書いて検知処理を走らせる。</summary>
    public void WriteRawResult(string json)
    {
        Write(PopnPlayWatcher.ResultFileName, json);
        _watcher.ProcessResult();
    }

    private void Write(string name, string content)
        => File.WriteAllText(System.IO.Path.Combine(_dir.Path, name), content);

    public void Dispose() => _dir.Dispose();
}
