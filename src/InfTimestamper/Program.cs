using System;
using Velopack;

namespace InfTimestamper;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack のブートストラップは Main の最初で呼ぶ必要がある。
        // 初回起動・アップデート適用・アンインストール時のフックを処理する。
        VelopackApp.Build()
            .OnFirstRun(_ =>
            {
                // 初回起動時のフック（必要に応じて）
            })
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
