using InfTimestamper.Core.Settings;
using InfTimestamper.Core.YouTube;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

/// <summary>
/// YouTube 連携タブの ViewModel。
///
/// ログイン / ログアウトは「確定」を待たずにその場で反映する（ブラウザでの同意を挟むため、
/// ダイアログを閉じるまで保留すると結果の扱いがややこしくなる）。有効 / 無効・見出し行・更新間隔は
/// 他の設定と同じく「確定」で反映する。
/// </summary>
public sealed class YouTubeSettingsViewModel : ObservableBase
{
    /// <summary>ブラウザでの同意を待つ上限。</summary>
    public static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);

    private readonly YouTubeAccount? _account;
    private readonly IYouTubeApi? _api;
    private readonly IDialogService? _dialog;

    private bool _enabled;
    private string _descriptionHeading;
    private int _updateIntervalSeconds;
    private string _clientId;
    private string _clientSecret;
    private bool _isSigningIn;
    private CancellationTokenSource? _signInCts;

    public YouTubeSettingsViewModel(YouTubeSettings model)
        : this(model, null, null, null) { }

    public YouTubeSettingsViewModel(
        YouTubeSettings model,
        YouTubeAccount? account,
        IYouTubeApi? api,
        IDialogService? dialog)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        _enabled = model.Enabled;
        _descriptionHeading = model.ResolveHeading();
        _updateIntervalSeconds = model.UpdateIntervalSeconds;
        _account = account;
        _api = api;
        _dialog = dialog;
        _clientId = account?.ClientId ?? string.Empty;
        _clientSecret = account?.ClientSecret ?? string.Empty;

        SignInCommand = new RelayCommand(ExecuteSignIn,
            () => _account is not null && !_isSigningIn && HasClientCredentials);
        CancelSignInCommand = new RelayCommand(() => _signInCts?.Cancel(), () => _isSigningIn);
        SignOutCommand = new RelayCommand(ExecuteSignOut,
            () => _account?.IsSignedIn == true && !_isSigningIn);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    public string DescriptionHeading
    {
        get => _descriptionHeading;
        set
        {
            if (SetField(ref _descriptionHeading, value ?? string.Empty))
                RaisePropertyChanged(nameof(IsDescriptionHeadingValid));
        }
    }

    public bool IsDescriptionHeadingValid => !string.IsNullOrWhiteSpace(_descriptionHeading);

    public int UpdateIntervalSeconds
    {
        get => _updateIntervalSeconds;
        set
        {
            if (SetField(ref _updateIntervalSeconds, value))
                RaisePropertyChanged(nameof(IsUpdateIntervalValid));
        }
    }

    public bool IsUpdateIntervalValid => _updateIntervalSeconds >= AppSettings.MinYouTubeUpdateIntervalSeconds;

    public int MinUpdateIntervalSeconds => AppSettings.MinYouTubeUpdateIntervalSeconds;

    public string ClientId
    {
        get => _clientId;
        set
        {
            if (SetField(ref _clientId, value ?? string.Empty))
                SignInCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>PasswordBox から流し込む（Binding できないため）。</summary>
    public string ClientSecret
    {
        get => _clientSecret;
        set
        {
            if (SetField(ref _clientSecret, value ?? string.Empty))
                SignInCommand.RaiseCanExecuteChanged();
        }
    }

    private bool HasClientCredentials
        => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);

    public bool IsSignedIn => _account?.IsSignedIn == true;

    public bool IsSigningIn
    {
        get => _isSigningIn;
        private set
        {
            if (!SetField(ref _isSigningIn, value)) return;
            RaisePropertyChanged(nameof(AccountStatusText));
            RaisePropertyChanged(nameof(CanEditCredentials));
            SignInCommand.RaiseCanExecuteChanged();
            CancelSignInCommand.RaiseCanExecuteChanged();
            SignOutCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>ログイン処理中はクライアント ID / シークレットを触らせない。</summary>
    public bool CanEditCredentials => !_isSigningIn;

    public string AccountStatusText
    {
        get
        {
            if (_isSigningIn) return "ブラウザで Google アカウントへのアクセスを許可してください…";
            if (_account is null) return "YouTube 連携を利用できません";
            if (!_account.IsSignedIn) return "未ログイン";
            return string.IsNullOrEmpty(_account.ChannelTitle)
                ? "ログイン中"
                : $"ログイン中: {_account.ChannelTitle}";
        }
    }

    public RelayCommand SignInCommand { get; }
    public RelayCommand CancelSignInCommand { get; }
    public RelayCommand SignOutCommand { get; }

    public YouTubeSettings ToModel() => new()
    {
        Enabled = _enabled,
        DescriptionHeading = string.IsNullOrWhiteSpace(_descriptionHeading)
            ? AppSettings.DefaultYouTubeDescriptionHeading
            : _descriptionHeading.Trim(),
        UpdateIntervalSeconds = Math.Max(AppSettings.MinYouTubeUpdateIntervalSeconds, _updateIntervalSeconds),
    };

    private async void ExecuteSignIn()
    {
        if (_account is null) return;

        using var cts = new CancellationTokenSource(SignInTimeout);
        _signInCts = cts;
        IsSigningIn = true;
        try
        {
            await _account.SignInAsync(
                new YouTubeClientCredentials(_clientId.Trim(), _clientSecret.Trim()),
                cts.Token).ConfigureAwait(true);

            // チャンネル名は表示用。取れなくてもログイン自体は成功している
            if (_api is not null)
            {
                try
                {
                    _account.SetChannelTitle(await _api.GetMyChannelTitleAsync(cts.Token).ConfigureAwait(true));
                }
                catch (Exception)
                {
                    // 表示が「ログイン中」になるだけ
                }
            }
        }
        catch (OperationCanceledException)
        {
            _dialog?.ShowError("YouTube ログイン", "ログインを中断しました。");
        }
        catch (Exception ex)
        {
            _dialog?.ShowError("YouTube ログイン", "ログインに失敗しました: " + ex.Message);
        }
        finally
        {
            _signInCts = null;
            IsSigningIn = false;
            RaiseAccountChanged();
        }
    }

    private async void ExecuteSignOut()
    {
        if (_account is null) return;
        if (_dialog is not null
            && !_dialog.Confirm("YouTube ログアウト", "YouTube からログアウトします。よろしいですか？"))
        {
            return;
        }

        IsSigningIn = true;
        try
        {
            await _account.SignOutAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _dialog?.ShowError("YouTube ログアウト", "ログアウトに失敗しました: " + ex.Message);
        }
        finally
        {
            IsSigningIn = false;
            RaiseAccountChanged();
        }
    }

    private void RaiseAccountChanged()
    {
        RaisePropertyChanged(nameof(IsSignedIn));
        RaisePropertyChanged(nameof(AccountStatusText));
        SignInCommand.RaiseCanExecuteChanged();
        SignOutCommand.RaiseCanExecuteChanged();
    }
}
