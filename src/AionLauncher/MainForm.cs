using System.Diagnostics;
using System.Reflection;
using AionLauncher.Core;

namespace AionLauncher;

/// <summary>
/// v0: one window. Find the client, tell the player whether it is patched, start it with
/// <c>-ip: -port: -loginex</c>. No manifest, no self-update — that is v1/v2.
/// </summary>
public sealed class MainForm : Form
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(8),
        DefaultRequestHeaders = { { "User-Agent", "AionLauncher/" + VersionText } },
    };

    private static string VersionText =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private readonly string _exeDir;
    private readonly string _configPath;
    private readonly string _userSettingsPath;

    private LauncherConfig _config = new();
    private UserSettings _userSettings = new();
    private string? _gameRoot;
    private PatchCheckResult? _patch;

    private readonly Label _headerLabel = new();
    private readonly Label _statusLabel = new();
    private readonly TextBox _pathBox = new();
    private readonly Button _browseButton = new();
    private readonly Label _patchLabel = new();
    private readonly Button _patchButton = new();
    private readonly Button _playButton = new();
    private readonly Button _newsButton = new();
    private readonly Label _messageLabel = new();
    private readonly System.Windows.Forms.Timer _statusTimer = new();

    public MainForm()
    {
        _exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        _configPath = Path.Combine(_exeDir, LauncherConfig.FileName);
        _userSettingsPath = UserSettings.DefaultPath(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        BuildLayout();
        LoadConfiguration();
        RefreshGameFolder();
        StartStatusPolling();
    }

    private void BuildLayout()
    {
        Text = "Aion 4.8 — лаунчер";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 300);
        Font = new Font("Segoe UI", 9F);
        Padding = new Padding(16);

        _headerLabel.SetBounds(16, 14, 528, 22);
        _headerLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _headerLabel.Text = "Aion 4.8";

        _statusLabel.SetBounds(16, 40, 528, 20);
        _statusLabel.ForeColor = SystemColors.GrayText;
        _statusLabel.Text = "Статус сервера: проверяем…";

        var pathCaption = new Label { Text = "Папка игры", ForeColor = SystemColors.GrayText };
        pathCaption.SetBounds(16, 76, 528, 16);

        _pathBox.SetBounds(16, 94, 430, 24);
        _pathBox.ReadOnly = true;
        _pathBox.BackColor = SystemColors.Window;

        _browseButton.SetBounds(454, 93, 90, 26);
        _browseButton.Text = "Обзор…";
        _browseButton.Click += (_, _) => BrowseForGameFolder();

        _patchLabel.SetBounds(16, 130, 380, 34);
        _patchLabel.Text = string.Empty;

        _patchButton.SetBounds(404, 130, 140, 28);
        _patchButton.Text = "Установить патч";
        _patchButton.Visible = false;
        _patchButton.Click += async (_, _) => await InstallPatchAsync().ConfigureAwait(true);

        _playButton.SetBounds(16, 186, 300, 56);
        _playButton.Text = "ИГРАТЬ";
        _playButton.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        _playButton.Click += (_, _) => Play();

        _newsButton.SetBounds(330, 186, 214, 56);
        _newsButton.Text = "Новости сервера";
        _newsButton.Click += (_, _) => OpenNews();

        _messageLabel.SetBounds(16, 252, 528, 34);
        _messageLabel.ForeColor = SystemColors.GrayText;

        Controls.AddRange(
        [
            _headerLabel, _statusLabel, pathCaption, _pathBox, _browseButton,
            _patchLabel, _patchButton, _playButton, _newsButton, _messageLabel,
        ]);

        AcceptButton = _playButton;
    }

    private void LoadConfiguration()
    {
        try
        {
            _config = LauncherConfig.LoadOrDefault(_configPath);
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
        {
            // Defaults must still let the player start the game; just say what is wrong.
            _config = new LauncherConfig();
            ShowMessage($"{LauncherConfig.FileName}: {e.Message} — используются значения по умолчанию.");
        }

        _userSettings = UserSettings.LoadOrDefault(_userSettingsPath);

        _headerLabel.Text = $"Aion 4.8 — {_config.ServerHost}:{_config.LoginPort}";
        _newsButton.Enabled = !string.IsNullOrWhiteSpace(_config.NewsUrl);
        if (!_newsButton.Enabled)
            _newsButton.Text = "Новости (не настроены)";
    }

    private void RefreshGameFolder()
    {
        _gameRoot = GameFolder.Detect(_config.GameDir, _userSettings.GameDir, _exeDir);

        if (_gameRoot is null)
        {
            _pathBox.Text = "не найдена — нажмите «Обзор…»";
            _patchLabel.Text = string.Empty;
            _patchButton.Visible = false;
            _playButton.Enabled = false;
            return;
        }

        _pathBox.Text = _gameRoot;
        _playButton.Enabled = true;
        RefreshPatchState();
    }

    private void RefreshPatchState()
    {
        if (_gameRoot is null)
            return;

        try
        {
            _patch = PatchCheck.Check(_gameRoot);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _patchLabel.Text = $"Не удалось проверить патч: {e.Message}";
            _patchButton.Visible = false;
            return;
        }

        _patchLabel.Text = _patch.ToDisplayString();
        _patchLabel.ForeColor = _patch.IsPatched ? SystemColors.ControlText : Color.Firebrick;
        _patchButton.Visible = _patch.NeedsInstall && !string.IsNullOrWhiteSpace(_config.PatchZipUrl);
    }

    private void BrowseForGameFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Укажите папку, в которой лежат bin32, bin64, Data и L10N",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (_gameRoot is not null)
            dialog.SelectedPath = _gameRoot;

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        GameFolderCheck check = GameFolder.Validate(dialog.SelectedPath);
        if (!check.IsValid)
        {
            MessageBox.Show(
                this,
                $"Это не похоже на папку игры.\n\n{check.ToDisplayString()}",
                "Папка игры",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _userSettings.GameDir = check.Path;
        TrySaveUserSettings();
        RefreshGameFolder();
        ShowMessage("Папка игры сохранена.");
    }

    private void TrySaveUserSettings()
    {
        try
        {
            _userSettings.Save(_userSettingsPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowMessage($"Не удалось запомнить папку игры: {e.Message}");
        }
    }

    private void Play()
    {
        if (_gameRoot is null)
            return;

        if (_patch is { IsPatched: false })
        {
            DialogResult answer = MessageBox.Show(
                this,
                $"{_patch.ToDisplayString()}.\n\nБез патча клиент не подключится к нашему серверу. Запустить всё равно?",
                "Клиент не пропатчен",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return;
        }

        try
        {
            string exe = LaunchArgs.ResolveClientExe(_gameRoot, _config.Prefer32Bit);
            IReadOnlyList<string> args = _config.BuildClientArgs();

            var startInfo = new ProcessStartInfo(exe)
            {
                WorkingDirectory = _gameRoot,
                UseShellExecute = false,
            };
            foreach (string arg in args)
                startInfo.ArgumentList.Add(arg);

            Process.Start(startInfo);
            ShowMessage($"Запуск: {Path.GetFileName(exe)} {LaunchArgs.ToCommandLine(args)}");
            WindowState = FormWindowState.Minimized;
        }
        catch (Exception e)
        {
            MessageBox.Show(this, e.Message, "Не удалось запустить игру", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenNews()
    {
        string? url = _config.NewsUrl;
        if (!LauncherConfig.IsSafeWebUrl(url) || string.IsNullOrWhiteSpace(url))
        {
            ShowMessage("Адрес новостей не настроен.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            ShowMessage($"Не удалось открыть браузер: {e.Message}");
        }
    }

    private async Task InstallPatchAsync()
    {
        if (_gameRoot is null)
            return;

        DialogResult answer = MessageBox.Show(
            this,
            $"Скачать официальный патч клиента ({PatchCheck.UpstreamVersion}) и распаковать его в bin32 и bin64?\n\n{_config.PatchZipUrl}",
            "Установка патча",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
            return;

        _patchButton.Enabled = false;
        ShowMessage("Скачиваем патч…");

        try
        {
            PatchInstallResult result = await PatchInstaller
                .DownloadAndInstallAsync(_config.PatchZipUrl, _config.PatchZipSha256, _gameRoot, Http)
                .ConfigureAwait(true);

            ShowMessage($"Установлено: {string.Join(", ", result.InstalledFiles)}");
            RefreshPatchState();
        }
        catch (Exception e)
        {
            MessageBox.Show(this, e.Message, "Патч не установлен", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ShowMessage("Патч не установлен.");
        }
        finally
        {
            _patchButton.Enabled = true;
        }
    }

    private void StartStatusPolling()
    {
        if (string.IsNullOrWhiteSpace(_config.StatusUrl))
        {
            _statusLabel.Text = string.Empty;
            return;
        }

        _statusTimer.Interval = 60_000;
        _statusTimer.Tick += async (_, _) => await RefreshStatusAsync().ConfigureAwait(true);
        _statusTimer.Start();

        _ = RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        string? url = _config.StatusUrl;
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            string json = await Http.GetStringAsync(url).ConfigureAwait(true);
            ServerStatus status = ServerStatus.Parse(json);
            _statusLabel.Text = status.ToDisplayString();
            _statusLabel.ForeColor = status.Online ? Color.SeaGreen : Color.Firebrick;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or FormatException)
        {
            _statusLabel.Text = ServerStatus.Unknown.ToDisplayString();
            _statusLabel.ForeColor = SystemColors.GrayText;
        }
    }

    private void ShowMessage(string text) => _messageLabel.Text = text;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _statusTimer.Dispose();
        base.Dispose(disposing);
    }
}
