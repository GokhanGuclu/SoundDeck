using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.Win32;
using Velopack;
using Velopack.Sources;
using static AudioDeviceTrayApp.Localization;

namespace AudioDeviceTrayApp
{
    public partial class Form1 : Form
    {
        // ---- Tray ----
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;

        // ---- Output devices ----
        private ComboBox _headsetCombo;
        private ComboBox _speakersCombo;

        // ---- Microphone ----
        private ComboBox _mic1Combo;
        private ComboBox _mic2Combo;

        // ---- General ----
        private CheckBox _startWithWindowsCheckbox;
        private ComboBox _languageCombo;
        private readonly Button _saveButton;

        // ---- Effects ----
        private CheckBox _swapCheckbox;

        // ---- Navigation ----
        private readonly List<Button> _navButtons = new();
        private readonly List<Panel> _navPages = new();
        private Panel _navStripe = null!;

        private readonly CoreAudioController _audioController = new CoreAudioController();
        private AppSettings _settings = new AppSettings();
        private readonly Icon? _appIcon;
        private readonly bool _openSettingsOnStart;
        private bool _uiReady;

        // ---- Hotkey infrastructure ----
        private const int HK_HEADSET = 1;
        private const int HK_SPEAKERS = 2;
        private const int HK_OUTPUT_TOGGLE = 3;
        private const int HK_MIC1 = 4;
        private const int HK_MIC2 = 5;
        private const int HK_MIC_TOGGLE = 6;
        private const int HK_SWAP = 7;

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "SoundDeck";
        private const string RepoUrl = "https://github.com/GokhanGuclu/SoundDeck";

        private sealed class HotkeyBinding
        {
            public int Id { get; init; }
            public Func<HotkeyConfig?> Get { get; init; } = () => null;
            public Action Action { get; init; } = () => { };
        }

        private readonly List<HotkeyBinding> _hotkeyBindings;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        private readonly string _settingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SoundDeck", "settings.json");

        // Theme colors
        private static readonly Color Bg = Color.FromArgb(24, 24, 27);
        private static readonly Color SidebarColor = Color.FromArgb(30, 30, 33);
        private static readonly Color NavSelected = Color.FromArgb(45, 40, 66);
        private static readonly Color Surface = Color.FromArgb(39, 39, 42);
        private static readonly Color Accent = Color.FromArgb(139, 92, 246);
        private static readonly Color AccentHover = Color.FromArgb(124, 58, 237);
        private static readonly Color TextMain = Color.FromArgb(228, 228, 231);
        private static readonly Color TextDim = Color.FromArgb(161, 161, 170);
        private static readonly Color TextHint = Color.FromArgb(113, 113, 122);
        private static readonly Color LabelColor = Color.FromArgb(212, 212, 216);

        public Form1()
        {
            InitializeComponent();

            LoadSettings();

            // Pick the language (saved preference, otherwise the system language)
            if (string.IsNullOrEmpty(_settings.Language))
            {
                _settings.Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";
            }
            Localization.Lang = _settings.Language;

            _appIcon = LoadAppIcon();
            if (_appIcon != null)
            {
                Icon = _appIcon;
            }

            _openSettingsOnStart = Environment.GetCommandLineArgs()
                .Any(a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase));

            // ---------- Window ----------
            Text = "SoundDeck";
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(660, 480);
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            DoubleBuffered = true;

            BuildTitleBar();
            BuildSidebar();
            BuildContent();

            // ---------- Save button ----------
            _saveButton = new Button
            {
                Text = T("btn_save"),
                Left = 490,
                Top = 426,
                Width = 150,
                Height = 40,
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _saveButton.FlatAppearance.BorderSize = 0;
            _saveButton.MouseEnter += (s, e) => _saveButton.BackColor = AccentHover;
            _saveButton.MouseLeave += (s, e) => _saveButton.BackColor = Accent;
            _saveButton.Click += SaveButton_Click;
            Controls.Add(_saveButton);

            // Build the pages
            _headsetCombo = null!;
            _speakersCombo = null!;
            _mic1Combo = null!;
            _mic2Combo = null!;
            _startWithWindowsCheckbox = null!;
            _languageCombo = null!;
            _swapCheckbox = null!;
            BuildPages();

            // ---------- Tray ----------
            _trayMenu = new ContextMenuStrip
            {
                BackColor = Surface,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9F)
            };
            _trayMenu.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem(T("tray_headset"), null, (s, e) => SwitchOutput(_settings.HeadsetDeviceId, T("dev_headset"))),
                new ToolStripMenuItem(T("tray_speakers"), null, (s, e) => SwitchOutput(_settings.SpeakersDeviceId, T("dev_speakers"))),
                new ToolStripMenuItem(T("tray_output_toggle"), null, (s, e) => ToggleOutput()),
                new ToolStripSeparator(),
                new ToolStripMenuItem(T("tray_mic1"), null, (s, e) => SwitchMic(_settings.Mic1DeviceId)),
                new ToolStripMenuItem(T("tray_mic2"), null, (s, e) => SwitchMic(_settings.Mic2DeviceId)),
                new ToolStripMenuItem(T("tray_mic_toggle"), null, (s, e) => ToggleMic()),
                new ToolStripSeparator(),
                new ToolStripMenuItem(T("tray_swap"), null, (s, e) => ToggleSwapFromMenu()),
                new ToolStripSeparator(),
                new ToolStripMenuItem(T("tray_updates"), null, async (s, e) => await CheckForUpdatesInteractiveAsync()),
                new ToolStripMenuItem(T("tray_settings"), null, OnSettings),
                new ToolStripSeparator(),
                new ToolStripMenuItem(T("tray_exit"), null, OnExit)
            });

            _trayIcon = new NotifyIcon
            {
                Icon = _appIcon ?? SystemIcons.Application,
                ContextMenuStrip = _trayMenu,
                Visible = true,
                Text = "SoundDeck"
            };
            _trayIcon.DoubleClick += OnSettings;

            // ---------- Hotkey bindings ----------
            _hotkeyBindings = new List<HotkeyBinding>
            {
                new() { Id = HK_HEADSET,       Get = () => _settings.HeadsetHotkey,      Action = () => SwitchOutput(_settings.HeadsetDeviceId, T("dev_headset")) },
                new() { Id = HK_SPEAKERS,      Get = () => _settings.SpeakersHotkey,     Action = () => SwitchOutput(_settings.SpeakersDeviceId, T("dev_speakers")) },
                new() { Id = HK_OUTPUT_TOGGLE, Get = () => _settings.OutputToggleHotkey, Action = ToggleOutput },
                new() { Id = HK_MIC1,          Get = () => _settings.Mic1Hotkey,         Action = () => SwitchMic(_settings.Mic1DeviceId) },
                new() { Id = HK_MIC2,          Get = () => _settings.Mic2Hotkey,         Action = () => SwitchMic(_settings.Mic2DeviceId) },
                new() { Id = HK_MIC_TOGGLE,    Get = () => _settings.MicToggleHotkey,    Action = ToggleMic },
                new() { Id = HK_SWAP,          Get = () => _settings.SwapChannelsHotkey, Action = ToggleSwapFromMenu },
            };

            FormClosing += Form1_FormClosing;
            Load += Form1_Load;

            LoadDevicesToCombos();
            ShowPage(0);
            ApplyRoundedRegion();
            _uiReady = true;

            // Keep the Equalizer APO config in sync with the saved swap setting.
            if (IsEqualizerApoInstalled())
            {
                ApplySwapConfig(_settings.SwapChannels);
            }

            if (IsHandleCreated)
            {
                RegisterAllHotkeys();
            }
            else
            {
                HandleCreated += (s, e) => RegisterAllHotkeys();
            }
        }

        private void ApplyRoundedRegion()
        {
            int r = 16;
            int w = Width, h = Height;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 0, r, r, 180, 90);
            path.AddArc(w - r, 0, r, r, 270, 90);
            path.AddArc(w - r, h - r, r, r, 0, 90);
            path.AddArc(0, h - r, r, r, 90, 90);
            path.CloseFigure();
            Region = new Region(path);
        }

        // ===================== Window chrome =====================

        private void BuildTitleBar()
        {
            var titleBar = new Panel
            {
                Left = 0,
                Top = 0,
                Width = ClientSize.Width,
                Height = 46,
                BackColor = SidebarColor
            };
            titleBar.Paint += (s, e) =>
            {
                using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, titleBar.Width, 3),
                    Accent, Color.FromArgb(59, 130, 246),
                    System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
                e.Graphics.FillRectangle(brush, 0, 0, titleBar.Width, 3);
                using var pen = new Pen(Color.FromArgb(45, 45, 50));
                e.Graphics.DrawLine(pen, 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
            };
            titleBar.MouseDown += TitleBar_Drag;
            Controls.Add(titleBar);

            if (_appIcon != null)
            {
                var iconBox = new PictureBox
                {
                    Image = new Icon(_appIcon, 32, 32).ToBitmap(),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Left = 16,
                    Top = 11,
                    Width = 24,
                    Height = 24,
                    BackColor = Color.Transparent
                };
                iconBox.MouseDown += TitleBar_Drag;
                titleBar.Controls.Add(iconBox);
            }

            var titleLbl = new Label
            {
                Text = "SoundDeck",
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                ForeColor = Accent,
                AutoSize = true,
                Left = 48,
                Top = 12,
                BackColor = Color.Transparent
            };
            titleLbl.MouseDown += TitleBar_Drag;
            titleBar.Controls.Add(titleLbl);

            var closeBtn = MakeChromeButton("✕", ClientSize.Width - 44);
            closeBtn.MouseEnter += (s, e) => { closeBtn.BackColor = Color.FromArgb(232, 17, 35); closeBtn.ForeColor = Color.White; };
            closeBtn.MouseLeave += (s, e) => { closeBtn.BackColor = SidebarColor; closeBtn.ForeColor = TextDim; };
            closeBtn.Click += (s, e) => { Hide(); ShowInTaskbar = false; };
            titleBar.Controls.Add(closeBtn);

            var minBtn = MakeChromeButton("—", ClientSize.Width - 82);
            minBtn.MouseEnter += (s, e) => minBtn.BackColor = Color.FromArgb(55, 55, 62);
            minBtn.MouseLeave += (s, e) => minBtn.BackColor = SidebarColor;
            minBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;
            titleBar.Controls.Add(minBtn);
        }

        private Button MakeChromeButton(string text, int left)
        {
            var b = new Button
            {
                Text = text,
                Left = left,
                Top = 8,
                Width = 34,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                ForeColor = TextDim,
                BackColor = SidebarColor,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private void TitleBar_Drag(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        }

        private void BuildSidebar()
        {
            var sidebar = new Panel
            {
                Left = 0,
                Top = 46,
                Width = 150,
                Height = ClientSize.Height - 46,
                BackColor = SidebarColor
            };
            Controls.Add(sidebar);

            _navStripe = new Panel
            {
                Left = 0,
                Top = 14,
                Width = 3,
                Height = 48,
                BackColor = Accent
            };
            sidebar.Controls.Add(_navStripe);

            string[] keys = { "nav_output", "nav_mic", "nav_effects", "nav_general" };
            int top = 14;
            for (int i = 0; i < keys.Length; i++)
            {
                int index = i;
                var b = new Button
                {
                    Text = T(keys[i]),
                    Left = 0,
                    Top = top,
                    Width = 150,
                    Height = 48,
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(20, 0, 0, 0),
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = TextDim,
                    BackColor = SidebarColor,
                    Cursor = Cursors.Hand,
                    TabStop = false
                };
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = NavSelected;
                b.Click += (s, e) => ShowPage(index);
                sidebar.Controls.Add(b);
                _navButtons.Add(b);
                top += 48;
            }
            _navStripe.BringToFront();
        }

        private Panel _content = null!;

        private void BuildContent()
        {
            _content = new Panel
            {
                Left = 150,
                Top = 46,
                Width = ClientSize.Width - 150,
                Height = 370,
                BackColor = Bg
            };
            Controls.Add(_content);
        }

        private void ShowPage(int index)
        {
            for (int i = 0; i < _navButtons.Count; i++)
            {
                bool selected = i == index;
                _navButtons[i].BackColor = selected ? NavSelected : SidebarColor;
                _navButtons[i].ForeColor = selected ? Accent : TextDim;
                _navButtons[i].Font = new Font("Segoe UI", 10F,
                    selected ? FontStyle.Bold : FontStyle.Regular);
                if (i < _navPages.Count)
                {
                    _navPages[i].Visible = selected;
                    if (selected) _navPages[i].BringToFront();
                }
            }

            if (index >= 0 && index < _navButtons.Count)
            {
                _navStripe.Top = _navButtons[index].Top;
                _navStripe.BringToFront();
            }
        }

        // ===================== Pages =====================

        private void BuildPages()
        {
            // ---- Output ----
            var outPage = NewPage(T("head_output"));
            _headsetCombo = AddComboRow(outPage, T("lbl_headset"), 64);
            _speakersCombo = AddComboRow(outPage, T("lbl_speakers"), 104);
            AddSubHeading(outPage, T("sub_hotkeys"), 152);
            AddHotkeyRow(outPage, T("lbl_headset"), 186,
                () => _settings.HeadsetHotkey, v => _settings.HeadsetHotkey = v);
            AddHotkeyRow(outPage, T("lbl_speakers"), 224,
                () => _settings.SpeakersHotkey, v => _settings.SpeakersHotkey = v);
            AddHotkeyRow(outPage, T("lbl_switch"), 262,
                () => _settings.OutputToggleHotkey, v => _settings.OutputToggleHotkey = v,
                T("hint_toggle_output"));

            // ---- Microphone ----
            var micPage = NewPage(T("head_mic"));
            _mic1Combo = AddComboRow(micPage, T("lbl_mic1"), 64);
            _mic2Combo = AddComboRow(micPage, T("lbl_mic2"), 104);
            AddSubHeading(micPage, T("sub_hotkeys"), 152);
            AddHotkeyRow(micPage, T("lbl_mic1"), 186,
                () => _settings.Mic1Hotkey, v => _settings.Mic1Hotkey = v);
            AddHotkeyRow(micPage, T("lbl_mic2"), 224,
                () => _settings.Mic2Hotkey, v => _settings.Mic2Hotkey = v);
            AddHotkeyRow(micPage, T("lbl_switch"), 262,
                () => _settings.MicToggleHotkey, v => _settings.MicToggleHotkey = v,
                T("hint_toggle_mic"));

            // ---- Effects (left/right channel swap) ----
            var fxPage = NewPage(T("head_effects"));
            _swapCheckbox = new CheckBox
            {
                Text = T("chk_swap"),
                Left = 24,
                Top = 66,
                Width = 320,
                ForeColor = LabelColor,
                Font = new Font("Segoe UI", 9.5F),
                Checked = _settings.SwapChannels
            };
            _swapCheckbox.CheckedChanged += SwapCheckbox_Changed;
            fxPage.Controls.Add(_swapCheckbox);

            AddSubHeading(fxPage, T("sub_hotkeys"), 104);
            AddHotkeyRow(fxPage, T("lbl_swap"), 138,
                () => _settings.SwapChannelsHotkey, v => _settings.SwapChannelsHotkey = v,
                T("hint_swap"));

            bool apo = IsEqualizerApoInstalled();
            var statusLabel = new Label
            {
                Text = apo ? T("swap_status_ok") : T("swap_status_missing"),
                AutoSize = true,
                Left = 24,
                Top = 188,
                ForeColor = apo ? Color.FromArgb(74, 222, 128) : Color.FromArgb(248, 113, 113),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            fxPage.Controls.Add(statusLabel);

            var setupBtn = new Button
            {
                Text = apo ? T("swap_open_setup") : T("swap_get_apo"),
                Left = 24,
                Top = 214,
                Width = 220,
                Height = 34,
                BackColor = Surface,
                ForeColor = TextMain,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            setupBtn.FlatAppearance.BorderColor = Accent;
            setupBtn.FlatAppearance.BorderSize = 1;
            setupBtn.Click += (s, e) => OpenEqualizerApoSetup();
            fxPage.Controls.Add(setupBtn);

            var helpLabel = new Label
            {
                Text = T("swap_help"),
                Left = 24,
                Top = 258,
                Width = fxPage.Width - 48,
                Height = 90,
                ForeColor = TextHint,
                Font = new Font("Segoe UI", 8.5F)
            };
            fxPage.Controls.Add(helpLabel);

            // ---- General ----
            var genPage = NewPage(T("head_general"));

            var langLabel = new Label
            {
                Text = T("lbl_language"),
                AutoSize = true,
                Left = 24,
                Top = 70,
                Font = new Font("Segoe UI", 9F),
                ForeColor = LabelColor
            };
            genPage.Controls.Add(langLabel);

            _languageCombo = new ComboBox
            {
                Left = 130,
                Top = 66,
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Surface,
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F)
            };
            StyleCombo(_languageCombo);
            _languageCombo.Items.Add(T("lang_english"));
            _languageCombo.Items.Add(T("lang_turkish"));
            _languageCombo.SelectedIndex = Localization.Lang == "tr" ? 1 : 0;
            _languageCombo.SelectedIndexChanged += LanguageCombo_Changed;
            genPage.Controls.Add(_languageCombo);

            _startWithWindowsCheckbox = new CheckBox
            {
                Text = T("chk_startup"),
                Left = 24,
                Top = 118,
                Width = 280,
                ForeColor = LabelColor,
                Font = new Font("Segoe UI", 9.5F),
                Checked = _settings.StartWithWindows
            };
            _startWithWindowsCheckbox.CheckedChanged += StartWithWindowsCheckbox_CheckedChanged;
            genPage.Controls.Add(_startWithWindowsCheckbox);

            var updateBtn = new Button
            {
                Text = T("btn_check_updates"),
                Left = 24,
                Top = 162,
                Width = 220,
                Height = 36,
                BackColor = Surface,
                ForeColor = TextMain,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            updateBtn.FlatAppearance.BorderColor = Accent;
            updateBtn.FlatAppearance.BorderSize = 1;
            updateBtn.Click += async (s, e) => await CheckForUpdatesInteractiveAsync();
            genPage.Controls.Add(updateBtn);

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionLabel = new Label
            {
                Text = $"SoundDeck v{version?.Major}.{version?.Minor}.{version?.Build}",
                AutoSize = true,
                Left = 24,
                Top = 300,
                ForeColor = TextHint,
                Font = new Font("Segoe UI", 8.5F)
            };
            genPage.Controls.Add(versionLabel);
        }

        private Panel NewPage(string heading)
        {
            var page = new Panel
            {
                Left = 0,
                Top = 0,
                Width = _content.Width,
                Height = _content.Height,
                BackColor = Bg,
                Visible = false
            };

            var headingLabel = new Label
            {
                Text = heading,
                Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
                ForeColor = Accent,
                AutoSize = true,
                Left = 22,
                Top = 18
            };
            page.Controls.Add(headingLabel);

            _content.Controls.Add(page);
            _navPages.Add(page);
            return page;
        }

        private void AddSubHeading(Panel page, string text, int top)
        {
            var lbl = new Label
            {
                Text = text.ToUpperInvariant(),
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = TextHint,
                AutoSize = true,
                Left = 24,
                Top = top
            };
            page.Controls.Add(lbl);

            var line = new Panel
            {
                Left = 24,
                Top = top + 20,
                Width = page.Width - 60,
                Height = 1,
                BackColor = Color.FromArgb(45, 45, 50)
            };
            page.Controls.Add(line);
        }

        // ===================== Row helpers =====================

        private void StyleCombo(ComboBox combo)
        {
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = 24;
            combo.DrawItem += Combo_DrawItem;
        }

        private void Combo_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox combo) return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(selected ? Accent : Surface))
            {
                e.Graphics.FillRectangle(bg, e.Bounds);
            }

            if (e.Index >= 0)
            {
                string text = combo.GetItemText(combo.Items[e.Index]);
                var rect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, text, combo.Font, rect,
                    selected ? Color.White : TextMain,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private ComboBox AddComboRow(Panel page, string labelText, int top)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Left = 24,
                Top = top + 4,
                Font = new Font("Segoe UI", 9F),
                ForeColor = LabelColor
            };
            var combo = new ComboBox
            {
                Left = 130,
                Top = top,
                Width = 340,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Surface,
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                DisplayMember = "Name"
            };
            StyleCombo(combo);
            page.Controls.Add(label);
            page.Controls.Add(combo);
            return combo;
        }

        private void AddHotkeyRow(Panel page, string labelText, int top,
            Func<HotkeyConfig?> get, Action<HotkeyConfig?> set,
            string? hintText = null)
        {
            hintText ??= T("hint_click");

            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Left = 24,
                Top = top + 4,
                Font = new Font("Segoe UI", 9F),
                ForeColor = LabelColor
            };

            var box = new TextBox
            {
                Left = 130,
                Top = top,
                Width = 190,
                Height = 28,
                ReadOnly = true,
                BackColor = Surface,
                ForeColor = Color.FromArgb(196, 181, 253),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                TextAlign = HorizontalAlignment.Center,
                Text = HotkeyToString(get())
            };

            box.KeyDown += (s, e) =>
            {
                e.SuppressKeyPress = true;

                if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
                {
                    set(null);
                    box.Text = "";
                    SaveSettings();
                    RegisterAllHotkeys();
                    return;
                }

                var config = CreateHotkeyFromKeyEvent(e);
                if (config == null) return;

                set(config);
                box.Text = HotkeyToString(config);
                SaveSettings();
                RegisterAllHotkeys();
            };
            box.GotFocus += (s, e) =>
            {
                box.SelectAll();
                box.BackColor = Color.FromArgb(55, 48, 107);
            };
            box.LostFocus += (s, e) => box.BackColor = Surface;

            var hint = new Label
            {
                Text = hintText,
                AutoSize = true,
                Left = 330,
                Top = top + 4,
                ForeColor = TextHint,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic)
            };

            page.Controls.Add(label);
            page.Controls.Add(box);
            page.Controls.Add(hint);
        }

        // ===================== Hotkey parsing =====================

        private HotkeyConfig? CreateHotkeyFromKeyEvent(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey ||
                e.KeyCode == Keys.ShiftKey ||
                e.KeyCode == Keys.Menu)
            {
                return null;
            }

            var mods = Control.ModifierKeys;
            return new HotkeyConfig
            {
                Ctrl = (mods & Keys.Control) == Keys.Control,
                Alt = (mods & Keys.Alt) == Keys.Alt,
                Shift = (mods & Keys.Shift) == Keys.Shift,
                Key = e.KeyCode
            };
        }

        private string HotkeyToString(HotkeyConfig? hk)
        {
            if (hk == null || hk.Key == Keys.None)
                return "";

            var parts = new List<string>();
            if (hk.Ctrl) parts.Add("Ctrl");
            if (hk.Alt) parts.Add("Alt");
            if (hk.Shift) parts.Add("Shift");
            parts.Add(KeyToDisplayString(hk.Key));
            return string.Join("+", parts);
        }

        private string KeyToDisplayString(Keys key)
        {
            switch (key)
            {
                case Keys.NumPad0: return "NumPad0";
                case Keys.NumPad1: return "NumPad1";
                case Keys.NumPad2: return "NumPad2";
                case Keys.NumPad3: return "NumPad3";
                case Keys.NumPad4: return "NumPad4";
                case Keys.NumPad5: return "NumPad5";
                case Keys.NumPad6: return "NumPad6";
                case Keys.NumPad7: return "NumPad7";
                case Keys.NumPad8: return "NumPad8";
                case Keys.NumPad9: return "NumPad9";
                case Keys.Add: return "NumPadAdd";
                case Keys.Subtract: return "NumPadSubtract";
                case Keys.Multiply: return "NumPadMultiply";
                case Keys.Divide: return "NumPadDivide";
                case Keys.Decimal: return "NumPadDecimal";
                default: return key.ToString();
            }
        }

        // ===================== Lifecycle =====================

        private void Form1_Load(object? sender, EventArgs e)
        {
            if (_openSettingsOnStart)
            {
                OnSettings(this, EventArgs.Empty);
            }
            else
            {
                Hide();
                ShowInTaskbar = false;
            }

            _ = ShowWhatsNewIfUpdatedAsync();
            _ = AutoUpdateOnStartupAsync();
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            foreach (var b in _hotkeyBindings)
            {
                UnregisterHotKey(Handle, b.Id);
            }

            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                var binding = _hotkeyBindings?.FirstOrDefault(b => b.Id == id);
                binding?.Action.Invoke();
            }

            base.WndProc(ref m);
        }

        // ===================== Settings persistence =====================

        private static Icon? LoadAppIcon()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream("AudioDeviceTrayApp.app.ico");
                if (stream != null)
                {
                    return new Icon(stream);
                }
            }
            catch
            {
                // fall back to the default application icon
            }

            return null;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
        }

        private void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir!);
                }

                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Settings could not be saved: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===================== Device loading =====================

        private void LoadDevicesToCombos()
        {
            var playback = _audioController.GetPlaybackDevices(DeviceState.Active)
                .Select(d => new AudioDeviceView { Id = d.Id.ToString(), Name = d.FullName })
                .ToList();

            var capture = _audioController.GetCaptureDevices(DeviceState.Active)
                .Select(d => new AudioDeviceView { Id = d.Id.ToString(), Name = d.FullName })
                .ToList();

            FillCombo(_headsetCombo, playback, _settings.HeadsetDeviceId);
            FillCombo(_speakersCombo, playback, _settings.SpeakersDeviceId);
            FillCombo(_mic1Combo, capture, _settings.Mic1DeviceId);
            FillCombo(_mic2Combo, capture, _settings.Mic2DeviceId);
        }

        private static void FillCombo(ComboBox combo, List<AudioDeviceView> devices, string? selectedId)
        {
            combo.Items.Clear();
            foreach (var d in devices)
            {
                combo.Items.Add(d);
            }

            if (!string.IsNullOrEmpty(selectedId))
            {
                var found = devices.Find(d => d.Id == selectedId);
                if (found != null) combo.SelectedItem = found;
            }
        }

        // ===================== Buttons / menu =====================

        private void SaveSelections()
        {
            if (_headsetCombo.SelectedItem is AudioDeviceView headset)
                _settings.HeadsetDeviceId = headset.Id;

            if (_speakersCombo.SelectedItem is AudioDeviceView speakers)
                _settings.SpeakersDeviceId = speakers.Id;

            if (_mic1Combo.SelectedItem is AudioDeviceView mic1)
                _settings.Mic1DeviceId = mic1.Id;

            if (_mic2Combo.SelectedItem is AudioDeviceView mic2)
                _settings.Mic2DeviceId = mic2.Id;

            SaveSettings();
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            SaveSelections();
            RegisterAllHotkeys();

            Hide();
            ShowInTaskbar = false;
        }

        private void LanguageCombo_Changed(object? sender, EventArgs e)
        {
            if (!_uiReady) return;

            string chosen = _languageCombo.SelectedIndex == 1 ? "tr" : "en";
            if (chosen == _settings.Language) return;

            SaveSelections();
            _settings.Language = chosen;
            SaveSettings();

            MessageBox.Show(T("lang_restart_msg"), T("lang_restart_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            Application.Restart();
        }

        private void OnSettings(object? sender, EventArgs e)
        {
            LoadDevicesToCombos();
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void OnExit(object? sender, EventArgs e)
        {
            Application.Exit();
        }

        // ===================== Audio actions =====================

        private void SwitchOutput(string? deviceId, string label)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                MessageBox.Show(T("msg_output_not_config", label),
                    T("msg_not_configured_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (!Guid.TryParse(deviceId, out var guid)) return;

                var device = _audioController.GetDevice(guid);
                if (device == null)
                {
                    MessageBox.Show(T("msg_device_not_found"),
                        T("msg_device_not_found_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                device.SetAsDefault();
                ShowBalloon(T("balloon_output"), T("balloon_switched", device.FullName));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to switch device: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleOutput()
        {
            var headset = _settings.HeadsetDeviceId;
            var speakers = _settings.SpeakersDeviceId;

            if (string.IsNullOrEmpty(headset) || string.IsNullOrEmpty(speakers))
            {
                MessageBox.Show(T("msg_config_output_both"),
                    T("msg_not_configured_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var current = _audioController.DefaultPlaybackDevice?.Id.ToString();
            string target = current == headset ? speakers : headset;
            SwitchOutput(target, T("dev_output"));
        }

        private void SwitchMic(string? deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                MessageBox.Show(T("msg_mic_not_config"),
                    T("msg_not_configured_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (!Guid.TryParse(deviceId, out var guid)) return;

                var device = _audioController.GetDevice(guid);
                if (device == null)
                {
                    MessageBox.Show(T("msg_mic_not_found"),
                        T("msg_device_not_found_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                device.SetAsDefault();
                ShowBalloon(T("balloon_mic"), T("balloon_mic_default", device.FullName));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to switch microphone: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleMic()
        {
            var mic1 = _settings.Mic1DeviceId;
            var mic2 = _settings.Mic2DeviceId;

            if (string.IsNullOrEmpty(mic1) || string.IsNullOrEmpty(mic2))
            {
                MessageBox.Show(T("msg_config_mic_both"),
                    T("msg_not_configured_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var current = _audioController.DefaultCaptureDevice?.Id.ToString();
            string target = current == mic1 ? mic2 : mic1;
            SwitchMic(target);
        }

        private void ShowBalloon(string title, string text)
        {
            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = text;
            _trayIcon.ShowBalloonTip(1000);
        }

        // ===================== Channel swap (Equalizer APO) =====================

        private const string SwapIncludeFile = "sounddeck-swap.txt";

        private static string? GetEqualizerApoConfigDir()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\EqualizerAPO");
                if (key?.GetValue("ConfigPath") is string cfg && Directory.Exists(cfg))
                {
                    return cfg;
                }
            }
            catch
            {
                // ignore and fall back to the default location
            }

            const string def = @"C:\Program Files\EqualizerAPO\config";
            return Directory.Exists(def) ? def : null;
        }

        private static bool IsEqualizerApoInstalled() => GetEqualizerApoConfigDir() != null;

        private void ApplySwapConfig(bool on)
        {
            try
            {
                var dir = GetEqualizerApoConfigDir();
                if (dir == null) return;

                var swapPath = Path.Combine(dir, SwapIncludeFile);
                string content = on
                    ? "# Managed by SoundDeck - left/right channel swap\r\n" +
                      "Copy: sd_tmpL=L sd_tmpR=R\r\n" +
                      "Copy: L=sd_tmpR R=sd_tmpL\r\n"
                    : "# Managed by SoundDeck - swap disabled\r\n";
                File.WriteAllText(swapPath, content);

                // Make sure the main config includes our file (once).
                var configPath = Path.Combine(dir, "config.txt");
                string existing = File.Exists(configPath) ? File.ReadAllText(configPath) : "";
                if (!existing.Contains(SwapIncludeFile))
                {
                    string updated = existing.TrimEnd() + Environment.NewLine +
                                     "Include: " + SwapIncludeFile + Environment.NewLine;
                    File.WriteAllText(configPath, updated.TrimStart());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update channel swap: " + ex.Message,
                    "SoundDeck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SwapCheckbox_Changed(object? sender, EventArgs e)
        {
            if (!_uiReady) return;

            if (!IsEqualizerApoInstalled())
            {
                MessageBox.Show(T("swap_need_apo"), "SoundDeck",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _swapCheckbox.Checked = false;
                return;
            }

            _settings.SwapChannels = _swapCheckbox.Checked;
            SaveSettings();
            ApplySwapConfig(_settings.SwapChannels);
            ShowBalloon(T("swap_title"), _settings.SwapChannels ? T("swap_on") : T("swap_off"));
        }

        private void ToggleSwapFromMenu()
        {
            if (!IsEqualizerApoInstalled())
            {
                MessageBox.Show(T("swap_need_apo"), "SoundDeck",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Flipping the checkbox triggers SwapCheckbox_Changed, which does the work.
            _swapCheckbox.Checked = !_swapCheckbox.Checked;
        }

        private void OpenEqualizerApoSetup()
        {
            try
            {
                var dir = GetEqualizerApoConfigDir();
                if (dir == null)
                {
                    Process.Start(new ProcessStartInfo(
                        "https://sourceforge.net/projects/equalizerapo/files/latest/download")
                    { UseShellExecute = true });
                    return;
                }

                var configurator = Path.Combine(Path.GetDirectoryName(dir)!, "Configurator.exe");
                if (File.Exists(configurator))
                {
                    Process.Start(new ProcessStartInfo(configurator) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open Equalizer APO setup: " + ex.Message,
                    "SoundDeck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ===================== Auto-update (Velopack) =====================

        private static UpdateManager CreateUpdateManager()
            => new UpdateManager(new GithubSource(RepoUrl, null, false));

        private async Task AutoUpdateOnStartupAsync()
        {
            try
            {
                var mgr = CreateUpdateManager();
                if (!mgr.IsInstalled) return;

                var info = await mgr.CheckForUpdatesAsync();
                if (info == null) return;

                await mgr.DownloadUpdatesAsync(info);

                mgr.WaitExitThenApplyUpdates(info.TargetFullRelease, silent: false, restart: false);
                ShowBalloon("SoundDeck", T("update_downloaded", info.TargetFullRelease.Version));
            }
            catch
            {
                // Never disturb startup because of a failed update check.
            }
        }

        private async Task CheckForUpdatesInteractiveAsync()
        {
            try
            {
                var mgr = CreateUpdateManager();
                if (!mgr.IsInstalled)
                {
                    MessageBox.Show(T("update_only_installed"),
                        "SoundDeck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var info = await mgr.CheckForUpdatesAsync();
                if (info == null)
                {
                    MessageBox.Show(T("update_latest"),
                        "SoundDeck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var answer = MessageBox.Show(
                    T("update_available_msg", info.TargetFullRelease.Version),
                    T("update_available_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;

                ShowBalloon("SoundDeck", T("update_downloading"));
                await mgr.DownloadUpdatesAsync(info);
                mgr.ApplyUpdatesAndRestart(info);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update check failed: " + ex.Message,
                    "SoundDeck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ===================== What's New (after update) =====================

        private static string CurrentVersionString()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return $"{v?.Major}.{v?.Minor}.{v?.Build}";
        }

        private async Task ShowWhatsNewIfUpdatedAsync()
        {
            try
            {
                string current = CurrentVersionString();
                string? last = _settings.LastRunVersion;

                if (string.Equals(last, current)) return;

                _settings.LastRunVersion = current;
                SaveSettings();

                // Don't show anything on a brand-new install (no previous version recorded).
                if (string.IsNullOrEmpty(last)) return;

                var notes = await FetchReleaseNotesAsync(current);
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    using var dlg = new WhatsNewForm(current, notes!, _appIcon);
                    dlg.ShowDialog();
                }
            }
            catch
            {
                // Showing release notes is best-effort only.
            }
        }

        private static async Task<string?> FetchReleaseNotesAsync(string version)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SoundDeck");
                var url = $"https://api.github.com/repos/GokhanGuclu/SoundDeck/releases/tags/v{version}";
                var json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("body", out var body))
                {
                    return body.GetString();
                }
            }
            catch
            {
                // offline / not found — skip
            }
            return null;
        }

        // ===================== Hotkey registration =====================

        private void RegisterAllHotkeys()
        {
            if (!IsHandleCreated) return;

            foreach (var b in _hotkeyBindings)
            {
                UnregisterHotKey(Handle, b.Id);
                RegisterHotkeyForConfig(b.Id, b.Get());
            }
        }

        private void RegisterHotkeyForConfig(int id, HotkeyConfig? config)
        {
            if (config == null || config.Key == Keys.None)
                return;

            uint modifiers = 0;
            if (config.Ctrl) modifiers |= MOD_CONTROL;
            if (config.Alt) modifiers |= MOD_ALT;
            if (config.Shift) modifiers |= MOD_SHIFT;

            RegisterHotKey(Handle, id, modifiers, (uint)config.Key);
        }

        // ===================== Startup registry =====================

        private void StartWithWindowsCheckbox_CheckedChanged(object? sender, EventArgs e)
        {
            _settings.StartWithWindows = _startWithWindowsCheckbox.Checked;
            SaveSettings();
            SetStartupRegistry(_startWithWindowsCheckbox.Checked);
        }

        private void SetStartupRegistry(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true);
                if (key == null) return;

                if (enable)
                {
                    key.SetValue(APP_NAME, Application.ExecutablePath);
                }
                else
                {
                    key.DeleteValue(APP_NAME, false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update startup settings: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class AppSettings
    {
        // Output devices
        public string? HeadsetDeviceId { get; set; }
        public string? SpeakersDeviceId { get; set; }
        public HotkeyConfig? HeadsetHotkey { get; set; }
        public HotkeyConfig? SpeakersHotkey { get; set; }
        public HotkeyConfig? OutputToggleHotkey { get; set; }

        // Microphone (two devices, toggle between them)
        public string? Mic1DeviceId { get; set; }
        public string? Mic2DeviceId { get; set; }
        public HotkeyConfig? Mic1Hotkey { get; set; }
        public HotkeyConfig? Mic2Hotkey { get; set; }
        public HotkeyConfig? MicToggleHotkey { get; set; }

        // Effects
        public bool SwapChannels { get; set; }
        public HotkeyConfig? SwapChannelsHotkey { get; set; }

        // General
        public bool StartWithWindows { get; set; }
        public string? Language { get; set; }
        public string? LastRunVersion { get; set; }
    }

    public class AudioDeviceView
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    public class HotkeyConfig
    {
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public Keys Key { get; set; }
    }
}
