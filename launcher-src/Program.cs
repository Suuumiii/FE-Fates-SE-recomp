using System.Diagnostics;
using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using SharpCompress.Archives;

namespace FatesLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, error) => MessageBox.Show(error.Exception.ToString(), "Launcher error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new LauncherForm());
    }
}

internal sealed class LauncherSettings
{
    public string Game { get; set; } = "";
    public string Recompiled { get; set; } = "";
    public string Renderer { get; set; } = "Vulkan";
    public string Rasterizer { get; set; } = "Hardware";
    public int Scale { get; set; } = 3;
    public string Layout { get; set; } = "top_only";
    public bool FrameInterpolation { get; set; }
    public bool New3DS { get; set; }
    public bool Interpreter { get; set; }
    public bool Mute { get; set; }
}

internal sealed class LauncherForm : Form
{
    private static readonly HttpClient ModHttp = new(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly string _settingsPath = Path.Combine(AppContext.BaseDirectory, "launcher-settings.json");
    private readonly TextBox _game = PathBox();
    private readonly TextBox _recompiled = PathBox();
    private static string ToolPath(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "tools", .. parts]);
    private readonly ComboBox _renderer = Combo();
    private readonly ComboBox _rasterizer = Combo();
    private readonly ComboBox _layout = Combo();
    private readonly NumericUpDown _scale = new();
    private readonly CheckBox _interpolation = Check();
    private readonly CheckBox _new3ds = Check();
    private readonly CheckBox _interpreter = Check();
    private readonly CheckBox _mute = Check();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _buildButton = new();
    private readonly Button _launchButton = new();
    private bool _building;
    private readonly ConcurrentQueue<string> _buildOutput = new();
    private readonly DataGridView _bindings = new();
    private readonly System.Windows.Forms.Timer _padCaptureTimer = new() { Interval = 50 };
    private DataGridViewCell? _captureCell;
    private BindingSpec? _captureBinding;
    private Color _captureOriginalBackColor;
    private readonly XInputState[] _lastPadStates = new XInputState[4];
    private readonly bool[] _padConnected = new bool[4];

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftX;
        public short LeftY;
        public short RightX;
        public short RightY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState { public uint PacketNumber; public XInputGamepad Gamepad; }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState14(uint index, out XInputState state);
    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetStateLegacy(uint index, out XInputState state);

    private enum ModInstallKind { Manual, TitleIdFolder, TexturePack }
    private sealed record ModDownload(string Name, string FileName, string DownloadUrl, string PageUrl, long Size, string Md5, ModInstallKind InstallKind, string Credits, string Note);
    private static readonly ModDownload TextureCompilation = new(
        "Fire Emblem Fates Texture Compilation 6.7", "fire_emblem_fates_texture_compilation_67.rar",
        "https://gamebanana.com/dl/1783110", "https://gamebanana.com/mods/388979", 542065715,
        "66c3625e475b38f8e2fda0a240855f9c", ModInstallKind.Manual,
        "Uploader and compiler: YiannisG\n\nContributors credited on GameBanana:\nAlgo112233 â€” Ports\nAzureFEF â€” Textures\nBarakawa â€” Textures\nBooLean1 â€” Textures\nBrian_alien â€” Ports, animations, models, textures\ncamusrigan â€” Texture ideas\nDeathChaosV2 â€” Textures and ports\nEnkad â€” Textures\nExcadrill1201 â€” Textures\nGrandmasterMorgan â€” Models\nhissingfit â€” Textures\nIndiiea â€” Playtesting\nJamesB210 â€” Ports and models\nL498 â€” Textures\nlivipup â€” Textures\nltranc â€” Textures\nLuminousClarity â€” Models\nMaester42 â€” Textures\nManakete_Smoocher â€” Ports\nMathcat â€” Textures\nMeanProtagonist â€” Textures\nMerrill â€” Textures\nMoonling â€” Textures and models\nPrincess Blucy â€” Textures and models\nPuddles â€” Textures and models\nSoleth â€” Ports\nSucky Wucky â€” Playtesting\nThane98 â€” Paragon\nThatUnknownSkull â€” Textures\nthirteen â€” Textures\nTildeHat â€” Ports, models, textures, animations\nTrustyWusty â€” Playtesting and textures\nYiannisG â€” Textures, ports, compilation\n\nSee the linked GameBanana page for updates and any additional credits.",
        "542 MB RAR  |  manual extraction and game rebuild required; see the source page.");
    private static readonly ModDownload UnofficialGayFates = new(
        "Unofficial Gay Fates", "unofficial_gay_fates_v252.7z",
        "https://gamebanana.com/dl/1765179", "https://gamebanana.com/mods/51420", 20990459,
        "e337ed224a71dc01582b1d2f6df3bd3f", ModInstallKind.TitleIdFolder,
        "GameBanana uploader: Milfnuggy\n\nCurrent development team:\ndingleflongle, flutea, Cherrycrow/Sunny\n\nRetired development team:\nMilfnuggy, Montblanc, TrustyWusty, PrettySketchi, Tea Sunny, hetcopogg, Mathcat, leodiniles\n\nSpecial thanks:\nUnassumingVenusaur, Thane98, TildeHat, Moonling, DaddyIke, Manakete_Smoocher, AnshiAneko, BigFake, Faye, Laplus_Darknesss, all support writers, and the community.\n\nSee the linked GameBanana page for each contributor's role and full project credits.",
        "21 MB 7z  |  installation selects the title ID found in the generated DLL.");
    private static readonly ModDownload FatesUiRemastered = new(
        "Fates UI Remastered", "fates_ui_remastered.zip",
        "https://gamebanana.com/dl/1469099", "https://gamebanana.com/mods/542115", 21301723,
        "ae8b2e4030cdcb7b476929623f243ab5", ModInstallKind.TexturePack,
        "Creator: Korum\n\nCredit: L498 â€” Various UI Elements\n\nAdditional source asset credits named by the author: Aoryn and FEPortraits, with assets from Fates Icon Project and Fates HD Additions. See Asset References.txt in the download and the GameBanana page for full attribution.",
        "21 MB ZIP  |  installs as a Zakuro texture pack for the detected title ID.");

    private static readonly (string Label, string Value)[] Layouts =
    [
        ("Top screen only", "top_only"),
        ("Bottom screen only", "bottom_only"),
        ("Top over bottom", "stacked"),
        ("Screens side by side", "side_by_side"),
        ("Big top, small bottom", "large_top")
    ];

    private static readonly string[] KeyChoices =
        ["KeyA", "KeyB", "KeyC", "KeyD", "KeyE", "KeyF", "KeyG", "KeyH", "KeyI", "KeyJ", "KeyK", "KeyL", "KeyM", "KeyN", "KeyO", "KeyP", "KeyQ", "KeyR", "KeyS", "KeyT", "KeyU", "KeyV", "KeyW", "KeyX", "KeyY", "KeyZ",
         "Digit0", "Digit1", "Digit2", "Digit3", "Digit4", "Digit5", "Digit6", "Digit7", "Digit8", "Digit9", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Enter", "Backspace", "Space", "Tab", "Escape",
         "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Home", "End", "PageUp", "PageDown", "Insert", "Delete", "ShiftLeft", "ShiftRight", "ControlLeft", "ControlRight", "AltLeft", "AltRight",
         "Numpad0", "Numpad1", "Numpad2", "Numpad3", "Numpad4", "Numpad5", "Numpad6", "Numpad7", "Numpad8", "Numpad9", "Minus", "Equal", "BracketLeft", "BracketRight", "Backslash", "Semicolon", "Quote", "Comma", "Period", "Slash", "Backquote"];
    private static readonly string[] PadChoices =
        ["South", "East", "West", "North", "C", "Z", "LeftTrigger", "RightTrigger", "LeftTrigger2", "RightTrigger2", "Select", "Start", "Mode", "LeftThumb", "RightThumb", "DPadUp", "DPadDown", "DPadLeft", "DPadRight"];

    private sealed record BindingSpec(string Label, string? KeyField, string? PadField, string DefaultKey, string DefaultPad, bool PadAxis = false, bool Special = false);
    private static readonly BindingSpec[] Bindings =
    [
        new("A", "a", "a", "KeyX", "East"), new("B", "b", "b", "KeyZ", "South"),
        new("X", "x", "x", "KeyS", "North"), new("Y", "y", "y", "KeyA", "West"),
        new("L", "l", "l", "KeyQ", "LeftTrigger"), new("R", "r", "r", "KeyW", "RightTrigger"),
        new("Start", "start", "start", "Enter", "Start"), new("Select", "select", "select", "Backspace", "Select"),
        new("D-pad up", "up", "up", "ArrowUp", "DPadUp"), new("D-pad down", "down", "down", "ArrowDown", "DPadDown"),
        new("D-pad left", "left", "left", "ArrowLeft", "DPadLeft"), new("D-pad right", "right", "right", "ArrowRight", "DPadRight"),
        new("Circle pad up", "circle_up", null, "KeyI", "Left stick â†‘", true), new("Circle pad down", "circle_down", null, "KeyK", "Left stick â†“", true),
        new("Circle pad left", "circle_left", null, "KeyJ", "Left stick â†", true), new("Circle pad right", "circle_right", null, "KeyL", "Left stick â†’", true),
        new("ZL", "zl", "zl", "Digit1", "LeftTrigger2"), new("ZR", "zr", "zr", "Digit2", "RightTrigger2"),
        new("C-stick up", "c_up", null, "KeyT", "Right stick â†‘", true), new("C-stick down", "c_down", null, "KeyG", "Right stick â†“", true),
        new("C-stick left", "c_left", null, "KeyF", "Right stick â†", true), new("C-stick right", "c_right", null, "KeyH", "Right stick â†’", true),
        new("Bottom-screen overlay", "overlay_key", "overlay_button", "F12", "RightThumb", false, true),
        new("Fast-forward (hold)", "fast_forward_key", "fast_forward_button", "Tab", "LeftThumb", false, true),
        new("Decrease fast-forward speed", "speed_down_key", "speed_down_button", "Digit9", "LeftTrigger2", false, true),
        new("Increase fast-forward speed", "speed_up_key", "speed_up_button", "Digit0", "RightTrigger2", false, true)
    ];

    private static readonly Color Ink = Color.FromArgb(67, 35, 25);
    private static readonly Color Parchment = Color.FromArgb(241, 224, 174);
    private static readonly Color ParchmentLight = Color.FromArgb(250, 241, 207);
    private static readonly Color Wine = Color.FromArgb(133, 37, 25);

    public LauncherForm()
    {
        Text = "Fire Emblem Fates - Recomp Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1040, 790);
        MinimumSize = new Size(940, 740);
        BackColor = Color.FromArgb(34, 24, 25);
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;
        KeyDown += CaptureKeyDown;
        _padCaptureTimer.Tick += (_, _) => PollControllerCapture();
        BuildUi();
        LoadSettings();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 3, BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        Controls.Add(root);

        var header = new FatesHeader { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
        root.Controls.Add(header, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Georgia", 10, FontStyle.Bold), Padding = new Point(18, 7) };
        var playTab = new TabPage("PLAY & SETTINGS") { BackColor = BackColor, Padding = new Padding(4) };
        var controlsTab = new TabPage("CONTROLS") { BackColor = BackColor, Padding = new Padding(4) };
        var modsTab = new TabPage("MODS") { BackColor = BackColor, Padding = new Padding(4) };
        var playLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, BackColor = BackColor };
        playLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BackColor, Margin = new Padding(0, 4, 0, 10) };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        columns.Controls.Add(BuildFilesCard(), 0, 0);
        columns.Controls.Add(BuildSettingsCard(), 1, 0);
        playLayout.Controls.Add(columns, 0, 0);
        playTab.Controls.Add(playLayout); controlsTab.Controls.Add(BuildBindingsPage()); modsTab.Controls.Add(BuildModsPage());
        tabs.TabPages.Add(playTab); tabs.TabPages.Add(controlsTab); tabs.TabPages.Add(modsTab);
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
    }

    private Control BuildBindingsPage()
    {
        var card = Card("NEW NINTENDO 3DS CONTROLS", "Click a keyboard or controller cell, then press the input you want. Stick directions use the matching analog stick.");
        _bindings.Dock = DockStyle.Fill;
        _bindings.BackgroundColor = ParchmentLight;
        _bindings.GridColor = Color.FromArgb(195, 164, 112);
        _bindings.BorderStyle = BorderStyle.None;
        _bindings.RowHeadersVisible = false;
        _bindings.AllowUserToAddRows = false;
        _bindings.AllowUserToDeleteRows = false;
        _bindings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _bindings.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _bindings.RowTemplate.Height = 26;
        _bindings.DefaultCellStyle.BackColor = ParchmentLight;
        _bindings.DefaultCellStyle.ForeColor = Ink;
        _bindings.DefaultCellStyle.SelectionBackColor = Color.FromArgb(223, 193, 126);
        _bindings.DefaultCellStyle.SelectionForeColor = Ink;
        _bindings.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(218, 184, 113);
        _bindings.ColumnHeadersDefaultCellStyle.ForeColor = Wine;
        _bindings.EnableHeadersVisualStyles = false;
        _bindings.Columns.Add(new DataGridViewTextBoxColumn { Name = "control", HeaderText = "3DS CONTROL", ReadOnly = true, FillWeight = 95 });
        _bindings.Columns.Add(new DataGridViewTextBoxColumn { Name = "keyboard", HeaderText = "KEYBOARD  |  CLICK TO CAPTURE", ReadOnly = true, FillWeight = 110 });
        _bindings.Columns.Add(new DataGridViewTextBoxColumn { Name = "gamepad", HeaderText = "CONTROLLER  |  CLICK TO CAPTURE", ReadOnly = true, FillWeight = 130 });

        foreach (var binding in Bindings)
        {
            var row = _bindings.Rows[_bindings.Rows.Add()];
            row.Cells[0].Value = binding.Label;
            row.Cells[1].Value = KeyDisplay(binding.DefaultKey);
            row.Cells[1].Tag = binding.DefaultKey;
            if (binding.PadAxis)
            {
                row.Cells[2] = new DataGridViewTextBoxCell { Value = binding.DefaultPad, Style = { BackColor = Color.FromArgb(235, 219, 175), ForeColor = Ink } };
                row.Cells[2].ReadOnly = true;
            }
            else row.Cells[2].Value = binding.DefaultPad;
            if (!binding.PadAxis) row.Cells[2].Tag = binding.DefaultPad;
            row.Tag = binding;
        }
        _bindings.CellClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 1) return;
            if (_bindings.Rows[e.RowIndex].Tag is not BindingSpec binding) return;
            if (e.ColumnIndex == 2 && binding.PadAxis)
            {
                _status.Text = "Stick direction is read from the analog stick; it does not need a button binding.";
                return;
            }
            BeginBindingCapture(e.RowIndex, e.ColumnIndex, binding);
        };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = ParchmentLight, Padding = new Padding(10, 5, 10, 6) };
        var reset = ActionButton("RESET TO DEFAULTS", false); reset.Dock = DockStyle.Right; reset.Width = 175;
        reset.Click += (_, _) => ResetControlBindings();
        var note = new Label { Text = "Click Save Settings below to apply these bindings next time Zakuro launches.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink };
        bottom.Controls.Add(note); bottom.Controls.Add(reset);
        card.Content.Controls.Add(_bindings); card.Content.Controls.Add(bottom);
        return card.Root;
    }

    private Control BuildModsPage()
    {
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = BackColor, Padding = new Padding(4) };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var intro = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Mods use Zakuro's per-game mod folder. Downloads come directly from GameBanana; each card links to its source and contributor credits.",
            ForeColor = Color.FromArgb(233, 211, 158), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 8, 0)
        };
        page.Controls.Add(intro, 0, 0);
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = BackColor, Padding = new Padding(0, 2, 0, 8) };
        for (var i = 0; i < 3; i++) cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
        cards.Controls.Add(BuildModCard(UnofficialGayFates), 0, 0);
        cards.Controls.Add(BuildModCard(TextureCompilation), 0, 1);
        cards.Controls.Add(BuildModCard(FatesUiRemastered), 0, 2);
        var openMods = ActionButton("OPEN FATES MOD FOLDER", false);
        openMods.Width = 190;
        openMods.Click += (_, _) => OpenFatesModsFolder();
        var actions = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = BackColor, Padding = new Padding(4, 6, 4, 0) };
        openMods.Dock = DockStyle.Right;
        actions.Controls.Add(openMods);
        var content = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        content.Controls.Add(cards); content.Controls.Add(actions);
        page.Controls.Add(content, 0, 1);
        return page;
    }

    private Control BuildModCard(ModDownload mod)
    {
        var card = Card(mod.Name.ToUpperInvariant(), mod.Note);
        card.Root.Dock = DockStyle.Fill;
        card.Root.Margin = new Padding(7, 5, 7, 5);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(8, 4, 4, 2), BackColor = ParchmentLight };
        var download = ActionButton(mod.InstallKind == ModInstallKind.Manual ? "DOWNLOAD ARCHIVE" : "DOWNLOAD & INSTALL", true);
        download.Width = 178;
        download.Click += async (_, _) => await DownloadModAsync(mod, download);
        var credits = ActionButton("CREDITS", false);
        credits.Width = 100;
        credits.Click += (_, _) => ShowCredits(mod);
        var source = ActionButton("GAMEBANANA PAGE", false);
        source.Width = 162;
        source.Click += (_, _) => OpenWebPage(mod.PageUrl);
        buttons.Controls.Add(download); buttons.Controls.Add(credits); buttons.Controls.Add(source);
        card.Content.Controls.Add(buttons);
        return card.Root;
    }

    private static string ModDownloadsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zakuro", "mod-downloads");

    private void ShowCredits(ModDownload mod)
    {
        using var dialog = new Form { Text = $"Credits â€” {mod.Name}", StartPosition = FormStartPosition.CenterParent, Size = new Size(660, 620), MinimumSize = new Size(480, 360), BackColor = ParchmentLight, Font = Font };
        var content = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Text = mod.Credits, BackColor = ParchmentLight, ForeColor = Ink, BorderStyle = BorderStyle.None, Padding = new Padding(14), Font = new Font("Segoe UI", 10) };
        var link = new LinkLabel { Dock = DockStyle.Bottom, Height = 34, Text = "Open the GameBanana page for current credits and update notes", TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
        link.LinkClicked += (_, _) => OpenWebPage(mod.PageUrl);
        dialog.Controls.Add(content); dialog.Controls.Add(link); dialog.ShowDialog(this);
    }

    private async Task DownloadModAsync(ModDownload mod, Button button)
    {
        if (button.Enabled == false) return;
        var archivePath = Path.Combine(ModDownloadsDirectory, mod.FileName);
        button.Enabled = false;
        try
        {
            Directory.CreateDirectory(ModDownloadsDirectory);
            if (!File.Exists(archivePath) || !await HasExpectedHashAsync(archivePath, mod.Md5))
            {
                if (File.Exists(archivePath)) File.Delete(archivePath);
                _progress.Visible = true; _progress.Style = ProgressBarStyle.Marquee;
                _status.Text = $"Downloading {mod.Name}â€¦";
                using var response = await ModHttp.GetAsync(mod.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await using (var source = await response.Content.ReadAsStreamAsync())
                await using (var destination = new FileStream(archivePath + ".download", FileMode.Create, FileAccess.Write, FileShare.None, 131072, useAsync: true))
                {
                    var buffer = new byte[131072]; long received = 0; int read; var total = response.Content.Headers.ContentLength ?? mod.Size;
                    _progress.Style = ProgressBarStyle.Blocks; _progress.Minimum = 0; _progress.Maximum = 100; _progress.Value = 0;
                    while ((read = await source.ReadAsync(buffer)) > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, read)); received += read;
                        var percent = (int)Math.Clamp(received * 100L / Math.Max(1, total), 0, 100);
                        _progress.Value = percent;
                        _status.Text = $"Downloading {mod.Name}â€¦ {received / 1048576:N0} / {total / 1048576:N0} MB";
                    }
                }
                File.Move(archivePath + ".download", archivePath, overwrite: true);
                if (!await HasExpectedHashAsync(archivePath, mod.Md5)) throw new InvalidDataException("The downloaded file did not match GameBanana's published checksum; it was not installed.");
            }

            if (mod.InstallKind != ModInstallKind.Manual)
            {
                var titleId = FindFatesTitleId();
                if (titleId is null)
                {
                    MessageBox.Show("Build the PC files first. The launcher uses the title ID in the generated DLL to select the matching mod files.", "Build needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _status.Text = "Downloaded and checksum-verified. Build PC files to install the matching edition.";
                    return;
                }
                var result = MessageBox.Show($"Install {mod.Name} for title {titleId}? Files with matching paths in Zakuro's Fates mod folder will be replaced.", "Install mod", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (result != DialogResult.OK) { _status.Text = $"Downloaded {mod.Name}; archive saved in {ModDownloadsDirectory}."; return; }
                var installed = await Task.Run(() => mod.InstallKind switch
                {
                    ModInstallKind.TitleIdFolder => InstallTitleIdArchive(archivePath, titleId),
                    ModInstallKind.TexturePack => InstallTexturePackArchive(archivePath, titleId),
                    _ => 0
                });
                _status.Text = $"Installed {mod.Name} for {titleId}. It will load the next time Zakuro starts.";
                MessageBox.Show($"{mod.Name} was installed for {titleId} ({installed:N0} files). Restart the game if it is already running.", "Mod installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _status.Text = $"Downloaded and verified {mod.Name}. Follow the creator's install steps on the GameBanana page.";
                MessageBox.Show($"Downloaded and verified:\n{archivePath}\n\nThis package needs the creator's manual extraction and rebuild steps. See the linked GameBanana page.", "Download complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception error)
        {
            _status.Text = "Mod download or installation failed.";
            MessageBox.Show(error.Message, "Mod download failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _progress.Visible = false;
            button.Enabled = true;
        }
    }

    private static async Task<bool> HasExpectedHashAsync(string path, string expected)
    {
        if (!File.Exists(path)) return false;
        await using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(await MD5.HashDataAsync(stream)).ToLowerInvariant();
        return hash.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private string? FindFatesTitleId()
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "0004000000179400", "0004000000179500", "0004000000179600", "0004000000179700", "0004000000179800", "000400000017A800",
            "000400000012DC00", "000400000012DD00", "000400000012DE00"
        };
        if (!Directory.Exists(_recompiled.Text)) return null;
        foreach (var file in Directory.EnumerateFiles(_recompiled.Text, "*.dll"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (known.Contains(id)) return id.ToUpperInvariant();
        }
        return null;
    }

    private int InstallTitleIdArchive(string archivePath, string titleId)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToArray();
        var destinationRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zakuro", "mods", titleId);
        var rootFull = Path.GetFullPath(destinationRoot) + Path.DirectorySeparatorChar;
        var selected = 0;
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)) continue;
            var parts = entry.Key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var idIndex = Array.FindIndex(parts, part => part.Equals(titleId, StringComparison.OrdinalIgnoreCase));
            if (idIndex < 0 || idIndex == parts.Length - 1) continue;
            var relative = Path.Combine(parts[(idIndex + 1)..]);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "." or "..")) throw new InvalidDataException("The mod archive contains an unsafe file path.");
            var target = Path.GetFullPath(Path.Combine(destinationRoot, relative));
            if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The mod archive contains a file outside the mods folder.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.OpenEntryStream();
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
            selected++;
        }
        if (selected == 0) throw new InvalidDataException($"No files for title {titleId} were found in this archive.");
        return selected;
    }

    private int InstallTexturePackArchive(string archivePath, string titleId)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        var entries = archive.Entries.Where(entry => !entry.IsDirectory && !string.IsNullOrWhiteSpace(entry.Key)).ToArray();
        var packManifest = entries.FirstOrDefault(entry => Path.GetFileName(entry.Key!.Replace('\\', '/')).Equals("pack.json", StringComparison.OrdinalIgnoreCase));
        if (packManifest is null) throw new InvalidDataException("No pack.json texture-pack manifest was found in this archive.");
        var manifestPath = packManifest.Key!.Replace('\\', '/');
        var packPrefix = manifestPath.Contains('/') ? manifestPath[..(manifestPath.LastIndexOf('/') + 1)] : "";
        var destinationRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zakuro", "mods", titleId, "textures");
        var rootFull = Path.GetFullPath(destinationRoot) + Path.DirectorySeparatorChar;
        var installed = 0;
        foreach (var entry in entries)
        {
            var key = entry.Key!.Replace('\\', '/');
            if (!key.StartsWith(packPrefix, StringComparison.Ordinal) || !Path.GetExtension(key).Equals(".png", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(key).Equals("pack.json", StringComparison.OrdinalIgnoreCase)) continue;
            var relative = key[packPrefix.Length..];
            if (relative.Length == 0 || relative.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part is "." or "..")) throw new InvalidDataException("The texture archive contains an unsafe file path.");
            var target = Path.GetFullPath(Path.Combine(destinationRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The texture archive contains a file outside the mods folder.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.OpenEntryStream();
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
            installed++;
        }
        if (installed <= 1) throw new InvalidDataException("The texture archive did not contain any PNG files alongside pack.json.");
        return installed;
    }

    private void OpenFatesModsFolder()
    {
        var titleId = FindFatesTitleId();
        if (titleId is null)
        {
            MessageBox.Show("Build PC files first so the launcher can identify the game edition.", "Build needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zakuro", "mods", titleId);
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private static void OpenWebPage(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private Control BuildFilesCard()
    {
        var card = Card("GAME FILES", "Choose the decrypted game image and output folder.");
        var rows = new TableLayoutPanel { Dock = DockStyle.Top, Height = 144, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Padding = new Padding(10, 8, 10, 8) };
        rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        var gameRow = BuildPathRow("Game image", _game, "3DS images|*.3ds;*.cci;*.cxi|All files|*.*", false);
        var outputRow = BuildPathRow("Output folder", _recompiled, "", true);
        rows.Controls.Add(gameRow, 0, 0);
        rows.Controls.Add(outputRow, 0, 1);
        card.Content.Controls.Add(rows);
        return card.Root;
    }

    private TableLayoutPanel BuildPathRow(string label, TextBox box, string filter, bool folder)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 2, 0, 2) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fieldLabel = FieldLabel(label);
        fieldLabel.Margin = Padding.Empty;
        var browse = ActionButton("â€¦", false);
        browse.Dock = DockStyle.Fill;
        browse.Margin = new Padding(2, 8, 0, 8);
        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(3, 16, 4, 16);
        StyleInput(box);
        row.Controls.Add(fieldLabel, 0, 0);
        row.Controls.Add(box, 1, 0);
        row.Controls.Add(browse, 2, 0);
        browse.Click += (_, _) =>
        {
            if (folder)
            {
                using var dialog = new FolderBrowserDialog { SelectedPath = Directory.Exists(box.Text) ? box.Text : AppContext.BaseDirectory };
                if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.SelectedPath;
            }
            else
            {
                using var dialog = new OpenFileDialog { Filter = filter, FileName = File.Exists(box.Text) ? box.Text : "" };
                if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.FileName;
            }
        };
        return row;
    }

    private Control BuildSettingsCard()
    {
        var card = Card("DISPLAY & SYSTEM", "Set the way this build starts.");
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(14, 3, 14, 8), BackColor = Color.Transparent };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        AddCombo(grid, 0, "Renderer", _renderer, ["Vulkan", "GL", "Software"]);
        AddCombo(grid, 1, "3D rasterizer", _rasterizer, ["Hardware", "Software"]);
        AddCombo(grid, 2, "Screen layout", _layout, Layouts.Select(x => x.Label).ToArray());
        grid.Controls.Add(FieldLabel("Window scale"), 0, 3);
        _scale.Minimum = 1; _scale.Maximum = 8; _scale.Width = 90; _scale.Anchor = AnchorStyles.Left;
        StyleInput(_scale); grid.Controls.Add(_scale, 1, 3);
        _interpolation.Text = "Frame interpolation";
        _interpolation.Anchor = AnchorStyles.Left;
        grid.Controls.Add(_interpolation, 1, 4);
        grid.Controls.Add(FieldHint("May smooth games below refresh rate; Fates already runs at 60 FPS."), 1, 5);
        var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = false, BackColor = Color.Transparent, Margin = new Padding(0) };
        _new3ds.Text = "New 3DS"; _interpreter.Text = "Interpreter"; _mute.Text = "Mute";
        foreach (var box in new[] { _new3ds, _interpreter, _mute }) { box.Margin = new Padding(0, 2, 12, 0); flags.Controls.Add(box); }
        grid.Controls.Add(flags, 0, 6); grid.SetColumnSpan(flags, 2);
        card.Content.Controls.Add(grid);
        return card.Root;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BackColor, Padding = new Padding(0, 9, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450));
        var statusPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = BackColor, Padding = new Padding(0, 2, 12, 0) };
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        _status.Dock = DockStyle.Fill; _status.TextAlign = ContentAlignment.MiddleLeft; _status.ForeColor = Color.FromArgb(233, 211, 158); _status.Text = "Ready";
        _progress.Dock = DockStyle.Fill; _progress.Minimum = 0; _progress.Maximum = 100; _progress.Visible = false;
        statusPanel.Controls.Add(_status, 0, 0); statusPanel.Controls.Add(_progress, 0, 1);
        footer.Controls.Add(statusPanel, 0, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = BackColor };
        _launchButton.Text = "LAUNCH GAME"; StyleActionButton(_launchButton, true); _launchButton.Click += (_, _) => Launch();
        _buildButton.Text = "BUILD PC FILES"; StyleActionButton(_buildButton, false); _buildButton.Width = 142; _buildButton.Click += async (_, _) => await BuildFromDump();
        var save = ActionButton("SAVE SETTINGS", primary: false); save.Click += (_, _) => SaveSettings();
        actions.Controls.Add(_launchButton); actions.Controls.Add(_buildButton); actions.Controls.Add(save); footer.Controls.Add(actions, 1, 0);
        return footer;
    }

    private (Panel Root, Panel Content) Card(string title, string subtitle)
    {
        var root = new Panel { Dock = DockStyle.Fill, Margin = new Padding(7), Padding = new Padding(2), BackColor = Color.FromArgb(111, 61, 37) };
        var interior = new Panel { Dock = DockStyle.Fill, BackColor = Parchment };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = ParchmentLight };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 59));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(218, 184, 113), Padding = new Padding(14, 7, 12, 4) };
        var titleLabel = new Label { Text = title, AutoSize = true, Font = new Font("Georgia", 10.5f, FontStyle.Bold), ForeColor = Wine, Location = new Point(12, 5) };
        var descLabel = new Label { Text = subtitle, AutoSize = false, Font = new Font("Segoe UI", 8.5f), ForeColor = Ink, Location = new Point(13, 29), Size = new Size(Math.Max(0, heading.Width - 25), 22), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, AutoEllipsis = true };
        heading.Controls.Add(titleLabel); heading.Controls.Add(descLabel);
        heading.Resize += (_, _) => descLabel.Width = Math.Max(0, heading.ClientSize.Width - 25);
        var content = new Panel { Dock = DockStyle.Fill, BackColor = ParchmentLight, Padding = new Padding(0) };
        table.Controls.Add(heading, 0, 0); table.Controls.Add(content, 0, 1);
        interior.Controls.Add(table); root.Controls.Add(interior);
        return (root, content);
    }

    private void AddPath(TableLayoutPanel grid, int row, string label, TextBox box, string filter, bool folder)
    {
        var fieldLabel = FieldLabel(label);
        fieldLabel.AutoSize = false;
        grid.Controls.Add(fieldLabel, 0, row);
        box.Dock = DockStyle.Fill; box.Margin = new Padding(3, 8, 6, 8); StyleInput(box); grid.Controls.Add(box, 1, row);
        var browse = ActionButton("â€¦", false); browse.Dock = DockStyle.Fill; browse.Margin = new Padding(0, 7, 0, 7);
        browse.Click += (_, _) =>
        {
            if (folder)
            {
                using var dialog = new FolderBrowserDialog { SelectedPath = Directory.Exists(box.Text) ? box.Text : AppContext.BaseDirectory };
                if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.SelectedPath;
            }
            else
            {
                using var dialog = new OpenFileDialog { Filter = filter, FileName = File.Exists(box.Text) ? box.Text : "" };
                if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.FileName;
            }
        };
        grid.Controls.Add(browse, 2, row);
    }

    private void AddCombo(TableLayoutPanel grid, int row, string label, ComboBox combo, string[] items)
    {
        grid.Controls.Add(FieldLabel(label), 0, row);
        combo.Items.AddRange(items); combo.DropDownStyle = ComboBoxStyle.DropDownList; combo.Anchor = AnchorStyles.Left | AnchorStyles.Right; combo.Margin = new Padding(3, 0, 3, 0);
        StyleInput(combo); grid.Controls.Add(combo, 1, row);
    }

    private static Control ControlBadge(string key, string text)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(5, 1, 5, 1) };
        var keyLabel = new Label { Text = key, AutoSize = true, Font = new Font("Georgia", 10, FontStyle.Bold), ForeColor = Wine, Location = new Point(0, 3) };
        var textLabel = new Label { Text = text, AutoSize = true, ForeColor = Ink, Location = new Point(54, 4) };
        panel.Controls.Add(keyLabel); panel.Controls.Add(textLabel); return panel;
    }

    private static Label FieldLabel(string text) => new() { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink, Margin = new Padding(3) };
    private static Label FieldHint(string text) => new() { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(113, 83, 54), Font = new Font("Segoe UI", 8.1f), Margin = new Padding(4, 0, 4, 0) };
    private static TextBox PathBox() => new();
    private static ComboBox Combo() => new();
    private static CheckBox Check() => new() { AutoSize = true, ForeColor = Ink, Margin = new Padding(4, 3, 6, 3) };

    private static void StyleInput(Control control)
    {
        control.BackColor = Color.FromArgb(255, 249, 222);
        control.ForeColor = Ink;
        if (control is TextBox text) text.BorderStyle = BorderStyle.FixedSingle;
    }

    private static Button ActionButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text, Width = primary ? 146 : 130, Height = 39, FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(158, 42, 27) : Color.FromArgb(118, 68, 39),
            ForeColor = Color.FromArgb(255, 238, 188), Font = new Font("Georgia", 9, FontStyle.Bold),
            Cursor = Cursors.Hand, Margin = new Padding(7, 1, 0, 0)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(217, 177, 97);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static void StyleActionButton(Button button, bool primary)
    {
        button.Width = 138; button.Height = 39; button.FlatStyle = FlatStyle.Flat;
        button.BackColor = primary ? Color.FromArgb(158, 42, 27) : Color.FromArgb(118, 68, 39);
        button.ForeColor = Color.FromArgb(255, 238, 188); button.Font = new Font("Georgia", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand; button.Margin = new Padding(7, 1, 0, 0);
        button.FlatAppearance.BorderColor = Color.FromArgb(217, 177, 97); button.FlatAppearance.BorderSize = 1;
    }

    private void LoadSettings()
    {
        var settings = new LauncherSettings();
        try
        {
            if (File.Exists(_settingsPath)) settings = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(_settingsPath)) ?? settings;
        }
        catch { }
        _game.Text = settings.Game; _recompiled.Text = settings.Recompiled;
        _renderer.SelectedItem = settings.Renderer; _rasterizer.SelectedItem = settings.Rasterizer;
        _scale.Value = Math.Clamp(settings.Scale, (int)_scale.Minimum, (int)_scale.Maximum);
        var savedLayout = settings.Layout.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
        _layout.SelectedIndex = Math.Max(0, Array.FindIndex(Layouts, x => x.Value.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant() == savedLayout));
        _interpolation.Checked = settings.FrameInterpolation; _new3ds.Checked = settings.New3DS; _interpreter.Checked = settings.Interpreter; _mute.Checked = settings.Mute;
        LoadControlBindings();
        _launchButton.Enabled = Directory.Exists(settings.Recompiled) && Directory.EnumerateFiles(settings.Recompiled, "*.dll").Any();
    }

    private LauncherSettings CurrentSettings() => new()
    {
        Game = _game.Text.Trim(), Recompiled = _recompiled.Text.Trim(),
        Renderer = _renderer.SelectedItem?.ToString() ?? "Vulkan", Rasterizer = _rasterizer.SelectedItem?.ToString() ?? "Hardware",
        Scale = (int)_scale.Value,
        Layout = Layouts[Math.Max(0, _layout.SelectedIndex)].Value,
        FrameInterpolation = _interpolation.Checked, New3DS = _new3ds.Checked, Interpreter = _interpreter.Checked, Mute = _mute.Checked
    };

    private void SaveSettings()
    {
        _bindings.EndEdit();
        var settings = CurrentSettings();
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        UpdateZakuroSettings(settings);
        _status.Text = "Settings saved  |  layout and frame interpolation updated.";
    }

    private void UpdateZakuroSettings(LauncherSettings settings)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zakuro", "settings.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        text = SetToml(text, "layout", $"\"{settings.Layout}\"");
        text = SetToml(text, "frame_interpolation", settings.FrameInterpolation.ToString().ToLowerInvariant());
        foreach (DataGridViewRow row in _bindings.Rows)
        {
            if (row.Tag is not BindingSpec binding || binding.KeyField is null) continue;
            var section = binding.Special ? "controls" : "keys";
            var keyValue = row.Cells[1].Tag?.ToString() ?? binding.DefaultKey;
            text = SetTomlSection(text, section, binding.KeyField, $"\"{keyValue}\"");
            if (!binding.PadAxis && binding.PadField is not null)
            {
                section = binding.Special ? "controls" : "pad";
                var padValue = row.Cells[2].Tag?.ToString() ?? binding.DefaultPad;
                text = SetTomlSection(text, section, binding.PadField, $"\"{padValue}\"");
            }
        }
        File.WriteAllText(path, text);
    }

    private static string SetToml(string text, string key, string value)
    {
        var pattern = $"(?m)^{Regex.Escape(key)}\\s*=.*$";
        var line = $"{key} = {value}";
        return Regex.IsMatch(text, pattern) ? Regex.Replace(text, pattern, line) : text.TrimEnd() + Environment.NewLine + line + Environment.NewLine;
    }

    private static string SetTomlSection(string text, string section, string key, string value)
    {
        var header = $"[{section}]";
        var matches = Regex.Matches(text, "(?m)^\\[[^\\]]+\\]\\s*$");
        var start = matches.Cast<Match>().FirstOrDefault(m => m.Value.Trim() == header);
        if (start is null)
        {
            return text.TrimEnd() + Environment.NewLine + Environment.NewLine + header + Environment.NewLine + $"{key} = {value}" + Environment.NewLine;
        }
        var next = matches.Cast<Match>().FirstOrDefault(m => m.Index > start.Index);
        var end = next?.Index ?? text.Length;
        var block = text[start.Index..end];
        var updated = SetToml(block, key, value);
        return text[..start.Index] + updated + text[end..];
    }

    private static string ReadTomlSection(string text, string section, string key, string fallback)
    {
        var header = $"[{section}]";
        var matches = Regex.Matches(text, "(?m)^\\[[^\\]]+\\]\\s*$");
        var start = matches.Cast<Match>().FirstOrDefault(m => m.Value.Trim() == header);
        if (start is null) return fallback;
        var next = matches.Cast<Match>().FirstOrDefault(m => m.Index > start.Index);
        var block = text[start.Index..(next?.Index ?? text.Length)];
        var match = Regex.Match(block, $"(?m)^{Regex.Escape(key)}\\s*=\\s*\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : fallback;
    }

    private void LoadControlBindings()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zakuro", "settings.toml");
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        foreach (DataGridViewRow row in _bindings.Rows)
        {
            if (row.Tag is not BindingSpec binding || binding.KeyField is null) continue;
            var section = binding.Special ? "controls" : "keys";
            var key = ReadTomlSection(text, section, binding.KeyField, binding.DefaultKey);
            row.Cells[1].Tag = key;
            row.Cells[1].Value = KeyDisplay(key);
            if (!binding.PadAxis && binding.PadField is not null)
            {
                section = binding.Special ? "controls" : "pad";
                var pad = ReadTomlSection(text, section, binding.PadField, binding.DefaultPad);
                row.Cells[2].Tag = pad;
                row.Cells[2].Value = pad;
            }
        }
    }

    private void ResetControlBindings()
    {
        foreach (DataGridViewRow row in _bindings.Rows)
        {
            if (row.Tag is not BindingSpec binding) continue;
            row.Cells[1].Tag = binding.DefaultKey;
            row.Cells[1].Value = KeyDisplay(binding.DefaultKey);
            if (!binding.PadAxis)
            {
                row.Cells[2].Tag = binding.DefaultPad;
                row.Cells[2].Value = binding.DefaultPad;
            }
        }
    }

    private static string KeyDisplay(string code)
    {
        if (code.StartsWith("Key", StringComparison.Ordinal) && code.Length == 4) return code[3..];
        if (code.StartsWith("Digit", StringComparison.Ordinal) && code.Length == 6) return code[5..];
        if (code.StartsWith("Numpad", StringComparison.Ordinal)) return $"Num {code[6..]}";
        return code switch
        {
            "ArrowUp" => "â†‘", "ArrowDown" => "â†“", "ArrowLeft" => "â†", "ArrowRight" => "â†’",
            "ShiftLeft" => "Left Shift", "ShiftRight" => "Right Shift",
            "ControlLeft" => "Left Ctrl", "ControlRight" => "Right Ctrl",
            "AltLeft" => "Left Alt", "AltRight" => "Right Alt",
            "BracketLeft" => "[", "BracketRight" => "]", "Backslash" => "\\",
            "Semicolon" => ";", "Quote" => "'", "Comma" => ",", "Period" => ".",
            "Slash" => "/", "Backquote" => "`", "Minus" => "-", "Equal" => "=",
            _ => code
        };
    }

    private void BeginBindingCapture(int rowIndex, int columnIndex, BindingSpec binding)
    {
        EndBindingCapture();
        _captureCell = _bindings.Rows[rowIndex].Cells[columnIndex];
        _captureBinding = binding;
        _captureOriginalBackColor = _captureCell.Style.BackColor.IsEmpty ? ParchmentLight : _captureCell.Style.BackColor;
        _captureCell.Style.BackColor = Color.FromArgb(255, 225, 140);
        _captureCell.ToolTipText = columnIndex == 1 ? "Press the key to assign" : "Press a button on an XInput-compatible controller";
        if (columnIndex == 1)
        {
            _status.Text = $"Press the keyboard key for {binding.Label}â€¦";
            return;
        }

        for (uint i = 0; i < 4; i++)
        {
            _padConnected[i] = TryGetXInputState(i, out _lastPadStates[i]);
        }
        _status.Text = $"Press a controller button for {binding.Label}â€¦";
        if (!_padConnected.Any(connected => connected)) _status.Text = "No XInput controller detected yet. Connect an XInput-compatible controller, then press a buttonâ€¦";
        _padCaptureTimer.Start();
    }

    private void EndBindingCapture()
    {
        _padCaptureTimer.Stop();
        if (_captureCell is not null)
        {
            _captureCell.Style.BackColor = _captureOriginalBackColor;
            _captureCell.ToolTipText = "";
        }
        _captureCell = null;
        _captureBinding = null;
    }

    private void CaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (_captureCell is null || _bindings.CurrentCell != _captureCell || _bindings.CurrentCell.ColumnIndex != 1) return;
        var code = KeyCode(e.KeyCode);
        if (code is null || !KeyChoices.Contains(code))
        {
            _status.Text = "That key is not supported for a binding. Try a letter, number, arrow, modifier, or function key.";
            e.Handled = true; e.SuppressKeyPress = true;
            return;
        }
        _captureCell.Tag = code;
        _captureCell.Value = KeyDisplay(code);
        var action = _captureBinding?.Label ?? "control";
        EndBindingCapture();
        _status.Text = $"{action} keyboard binding set to {KeyDisplay(code)}. Click Save Settings to keep it.";
        e.Handled = true; e.SuppressKeyPress = true;
    }

    private static string? KeyCode(Keys key)
    {
        if (key >= Keys.A && key <= Keys.Z) return $"Key{key}";
        if (key >= Keys.D0 && key <= Keys.D9) return $"Digit{(int)key - (int)Keys.D0}";
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return $"Numpad{(int)key - (int)Keys.NumPad0}";
        return key switch
        {
            Keys.Up => "ArrowUp", Keys.Down => "ArrowDown", Keys.Left => "ArrowLeft", Keys.Right => "ArrowRight",
            Keys.Enter => "Enter", Keys.Back => "Backspace", Keys.Space => "Space", Keys.Tab => "Tab", Keys.Escape => "Escape",
            Keys.F1 => "F1", Keys.F2 => "F2", Keys.F3 => "F3", Keys.F4 => "F4", Keys.F5 => "F5", Keys.F6 => "F6",
            Keys.F7 => "F7", Keys.F8 => "F8", Keys.F9 => "F9", Keys.F10 => "F10", Keys.F11 => "F11", Keys.F12 => "F12",
            Keys.Home => "Home", Keys.End => "End", Keys.PageUp => "PageUp", Keys.PageDown => "PageDown", Keys.Insert => "Insert", Keys.Delete => "Delete",
            Keys.ShiftKey => "ShiftLeft", Keys.ControlKey => "ControlLeft", Keys.Menu => "AltLeft",
            Keys.OemMinus => "Minus", Keys.Oemplus => "Equal", Keys.OemOpenBrackets => "BracketLeft", Keys.Oem6 => "BracketRight",
            Keys.OemPipe => "Backslash", Keys.OemSemicolon => "Semicolon", Keys.OemQuotes => "Quote", Keys.Oemcomma => "Comma",
            Keys.OemPeriod => "Period", Keys.OemQuestion => "Slash", Keys.Oemtilde => "Backquote",
            _ => null
        };
    }

    private void PollControllerCapture()
    {
        if (_captureCell is null || _bindings.CurrentCell != _captureCell || _captureCell.ColumnIndex != 2) { EndBindingCapture(); return; }
        for (uint i = 0; i < 4; i++)
        {
            var wasConnected = _padConnected[i];
            if (!TryGetXInputState(i, out var state)) { _padConnected[i] = false; continue; }
            _padConnected[i] = true;
            var previous = _lastPadStates[i].Gamepad;
            var pressed = wasConnected ? (ushort)(state.Gamepad.Buttons & ~previous.Buttons) : state.Gamepad.Buttons;
            var button = XInputButton(pressed);
            if (button is null && state.Gamepad.LeftTrigger >= 32 && (!wasConnected || previous.LeftTrigger < 32)) button = "LeftTrigger2";
            if (button is null && state.Gamepad.RightTrigger >= 32 && (!wasConnected || previous.RightTrigger < 32)) button = "RightTrigger2";
            _lastPadStates[i] = state;
            if (button is null) continue;
            _captureCell.Tag = button;
            _captureCell.Value = button;
            var action = _captureBinding?.Label ?? "control";
            EndBindingCapture();
                _status.Text = $"{action} controller binding set to {button}. Click Save Settings to keep it.";
            return;
        }
    }

    private static string? XInputButton(ushort buttons)
    {
        (ushort Mask, string Name)[] choices =
        [
            (0x0001, "DPadUp"), (0x0002, "DPadDown"), (0x0004, "DPadLeft"), (0x0008, "DPadRight"),
            (0x0010, "Start"), (0x0020, "Select"), (0x0040, "LeftThumb"), (0x0080, "RightThumb"),
            (0x0100, "LeftTrigger"), (0x0200, "RightTrigger"), (0x1000, "South"), (0x2000, "East"), (0x4000, "West"), (0x8000, "North")
        ];
        foreach (var choice in choices) if ((buttons & choice.Mask) != 0) return choice.Name;
        return null;
    }

    private static bool TryGetXInputState(uint index, out XInputState state)
    {
        try { return XInputGetState14(index, out state) == 0; }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        try { return XInputGetStateLegacy(index, out state) == 0; }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        state = default;
        return false;
    }

    private void Launch()
    {
        var settings = CurrentSettings();
        var zakuro = ToolPath("zakuro", "zakuro.exe");
        SaveSettings();
        while (_buildOutput.TryDequeue(out _)) { }
        foreach (var (path, label) in new[] { (zakuro, "Bundled Zakuro executable"), (settings.Game, "Game dump"), (settings.Recompiled, "Compiled code folder") })
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show($"{label} was not found:\n{path}", "Can't launch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        var start = new ProcessStartInfo(zakuro) { WorkingDirectory = Path.GetDirectoryName(zakuro)! };
        var bundledCompilerBin = ToolPath("mingw64", "bin");
        start.Environment["PATH"] = bundledCompilerBin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        start.ArgumentList.Add(settings.Game); start.ArgumentList.Add("--recompiled"); start.ArgumentList.Add(settings.Recompiled);
        start.ArgumentList.Add("--renderer"); start.ArgumentList.Add(settings.Renderer.ToLowerInvariant());
        start.ArgumentList.Add("--rasterizer"); start.ArgumentList.Add(settings.Rasterizer.ToLowerInvariant());
        start.ArgumentList.Add("--scale"); start.ArgumentList.Add(settings.Scale.ToString());
        if (settings.New3DS) start.ArgumentList.Add("--new3ds");
        if (settings.Interpreter) start.ArgumentList.Add("--interpreter");
        if (settings.Mute) start.ArgumentList.Add("--mute");
        try
        {
            Process.Start(start);
            _status.Text = "Zakuro launched  |  F10 switches screens.";
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task BuildFromDump()
    {
        if (_building) return;
        var recompiler = ToolPath("3dsrecomp", "3dsrecomp.exe");
        var compiler = ToolPath("mingw64", "bin", "gcc.exe");
        if (!File.Exists(_game.Text))
        {
            MessageBox.Show("Choose a decrypted .3ds/.cci game image first.", "Game image required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(recompiler))
        {
            MessageBox.Show($"Bundled 3dsrecomp was not found:\n{recompiler}", "Recompiler missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!File.Exists(compiler))
        {
            MessageBox.Show($"Bundled GCC was not found:\n{compiler}", "C compiler missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveSettings();
        _building = true; _buildButton.Enabled = false; _launchButton.Enabled = false;
        _progress.Visible = true; _progress.Style = ProgressBarStyle.Marquee;
        _status.Text = "Recompiling the game codeâ€¦ this first build may take several minutes.";
        try
        {
            Directory.CreateDirectory(_recompiled.Text);
            var info = new ProcessStartInfo(recompiler)
            {
                WorkingDirectory = Path.GetDirectoryName(recompiler)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            info.ArgumentList.Add("build"); info.ArgumentList.Add(_game.Text); info.ArgumentList.Add(_recompiled.Text);
            var compilerDirectory = Path.GetDirectoryName(compiler)!;
            info.Environment["CC"] = compiler;
            info.Environment["PATH"] = compilerDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");

            using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) HandleBuildOutput(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) HandleBuildOutput(e.Data); };
            if (!process.Start()) throw new InvalidOperationException("Could not start 3dsrecomp.");
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"3dsrecomp exited with code {process.ExitCode}. Check the dump, recompiler and GCC paths.");
            if (!Directory.EnumerateFiles(_recompiled.Text, "*.dll").Any())
                throw new InvalidOperationException("The tool exited successfully, but no recompiled DLL appeared in the output folder.");

            _progress.Style = ProgressBarStyle.Blocks; _progress.Value = 100;
            _status.Text = "PC files built successfully  |  compiled DLL is ready to launch.";
            _launchButton.Enabled = Directory.Exists(_recompiled.Text);
        }
        catch (Exception error)
        {
            _status.Text = "Build failed  |  check the selected dump and tool paths.";
            var details = string.Join(Environment.NewLine, _buildOutput.ToArray().TakeLast(18));
            MessageBox.Show(error.Message + (details.Length == 0 ? "" : Environment.NewLine + Environment.NewLine + details), "Build failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _building = false; _buildButton.Enabled = true;
        }
    }

    private void HandleBuildOutput(string line)
    {
        _buildOutput.Enqueue(line);
        if (IsDisposed) return;
        BeginInvoke(new Action(() =>
        {
            var match = Regex.Match(line, @"compiled\s+(\d+)\s+of\s+(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var done) && int.TryParse(match.Groups[2].Value, out var total) && total > 0)
            {
                _progress.Style = ProgressBarStyle.Blocks; _progress.Maximum = total; _progress.Value = Math.Clamp(done, 0, total);
                _status.Text = $"Compiling game code  |  {done} of {total} source files";
            }
            else if (line.Contains("wrote", StringComparison.OrdinalIgnoreCase))
            {
                _status.Text = "Generated C source  |  compiling native PC filesâ€¦";
            }
        }));
    }
}

internal sealed class FatesHeader : Panel
{
    public FatesHeader()
    {
        DoubleBuffered = true;
        Padding = new Padding(22, 13, 12, 8);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bounds = ClientRectangle;
        using var brush = new LinearGradientBrush(bounds, Color.FromArgb(107, 43, 29), Color.FromArgb(173, 111, 57), 0f);
        e.Graphics.FillRectangle(brush, bounds);
        using var border = new Pen(Color.FromArgb(220, 181, 107), 2);
        e.Graphics.DrawRectangle(border, 1, 1, Width - 3, Height - 3);
        using var inner = new Pen(Color.FromArgb(86, 37, 27), 1);
        e.Graphics.DrawRectangle(inner, 7, 7, Width - 15, Height - 15);
        using var titleFont = new Font("Georgia", 19, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 9.5f);
        TextRenderer.DrawText(e.Graphics, "FIRE EMBLEM FATES", titleFont, new Point(23, 15), Color.FromArgb(255, 238, 186));
        TextRenderer.DrawText(e.Graphics, "RECOMP LAUNCHER     |     EUROPEAN SPECIAL EDITION", subFont, new Point(26, 55), Color.FromArgb(244, 219, 164));
    }
}


