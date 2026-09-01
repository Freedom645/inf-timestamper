using InfTimestamper.Core.Settings;

namespace InfTimestamper.Core.Tests.TestHelpers;

public static class TestSettingsFactory
{
    /// <summary>
    /// テスト用の設定。バックアップ保存先を空にして、自動バックアップが
    /// 実環境の %APPDATA% へ書き込まないようにする。
    /// </summary>
    public static AppSettings CreateDefault()
    {
        var settings = AppSettings.CreateDefault();
        settings.General.BackupDirectory = string.Empty;
        return settings;
    }
}
