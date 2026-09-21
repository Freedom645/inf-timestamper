using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;

namespace InfTimestamper.ViewModels.Settings;

/// <summary>
/// ゲーム別タブ（INFINITAS / pop'n music / SOUND VOLTEX）に共通する設定項目の ViewModel。
/// 「タイムスタンプフォーマット + 識別子の挿入 + プレビュー」という構成はゲーム間で同じなので、
/// ここに集約してゲーム固有の差分は <see cref="GameCatalog"/> と派生クラスに閉じる。
///
/// プレイ検知の設定はゲームによって形が違う（ファイル監視系は出力ディレクトリ、
/// SOUND VOLTEX は SDVX Helper の接続先 + 任意でフォルダ）ので、そこは派生クラスが持つ。
/// </summary>
public abstract class GameFormatSettingsViewModel : ObservableBase
{
    private string _timestampFormat;
    private string _selectedIdentifier;

    protected GameFormatSettingsViewModel(GameId game, string? timestampFormat)
    {
        Game = game;
        _timestampFormat = string.IsNullOrEmpty(timestampFormat)
            ? Core.Settings.AppSettings.DefaultTimestampFormat
            : timestampFormat;

        AvailableIdentifiers = GameCatalog.IdentifierChoices(game);
        _selectedIdentifier = AvailableIdentifiers.Count > 0 ? AvailableIdentifiers[0].Key : string.Empty;
    }

    public GameId Game { get; }

    public string TimestampFormat
    {
        get => _timestampFormat;
        set
        {
            if (!SetField(ref _timestampFormat, value ?? string.Empty)) return;
            RaisePropertyChanged(nameof(Preview));
        }
    }

    /// <summary>セレクトボックスとサジェストに出す識別子。「論理名 ($key)」で見せる。</summary>
    public IReadOnlyList<IdentifierChoice> AvailableIdentifiers { get; }

    /// <summary>選択中の識別子キー（<c>$</c> 抜き）。</summary>
    public string SelectedIdentifier
    {
        get => _selectedIdentifier;
        set => SetField(ref _selectedIdentifier, value ?? string.Empty);
    }

    /// <summary>要件どおり、ハードコードのダミーデータで展開結果を見せる。</summary>
    public string Preview => FormatExpander.Expand(_timestampFormat, GameCatalog.PreviewFields(Game));

    public void InsertIdentifierAtCursor(int cursorPosition, string? identifier = null)
    {
        var key = identifier ?? _selectedIdentifier;
        if (string.IsNullOrEmpty(key)) return;

        var pos = Math.Clamp(cursorPosition, 0, _timestampFormat?.Length ?? 0);
        TimestampFormat = (_timestampFormat ?? string.Empty).Insert(pos, "$" + key);
    }
}
