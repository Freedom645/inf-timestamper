namespace InfTimestamper.Core.Tests.TestHelpers;

internal static class TestPaths
{
    /// <summary>
    /// リポジトリルート。テストは
    /// <c>tests/InfTimestamper.Core.Tests/bin/{Config}/{Tfm}/</c> から実行されるので 5 階層上。
    /// </summary>
    public static string RepositoryRoot { get; } = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static string RepositoryFile(params string[] segments)
        => Path.Combine(new[] { RepositoryRoot }.Concat(segments).ToArray());
}
