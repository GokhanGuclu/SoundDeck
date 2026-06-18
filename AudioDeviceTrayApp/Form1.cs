using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.Win32;
using Velopack;
using Velopack.Sources;

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
        private readonly Button _saveButton;

        // ---- Navigation ----
        private readonly List<Button> _navButtons = new();
        private readonly List<Panel> _navPages = new();

        private readonly CoreAudioController _audioController = new CoreAudioController();
        private AppSettings _settings = new AppSettings();
        private readonly Icon? _appIcon;
        private readonly bool _openSettingsOnStart;

        // ---- Hotkey infrastructure ----
        private const int HK_HEADSET = 1;
        private const int HK_SPEAKERS = 2;
        private const int HK_OUTPUT_TOGGLE = 3;
        private const int HK_MIC1 = 4;
        private const int HK_MIC2 = 5;
        private const int HK_MIC_TOGGLE = 6;

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "SoundDeck";

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

        private readonly string _settingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SoundDeck", "settings.json");

        // Theme colors
        private static readonly Color Bg = Color.FromArgb(24, 24, 27);
        private static readonly Color Sidebar = Color.FromArgb(30, 30, 33);
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

            BuildTitleBar();
            BuildSidebar();
            BuildContent();

            // ---------- Save button ----------
            _saveButton = new Button
            {
                Text = "💾  Save & Close",
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
                new ToolStripMenuItem("🎧  Switch to Headset", null, (s, e) => SwitchOutput(_settings.HeadsetDeviceId, "Headset")),
                new ToolStripMenuItem("🔊  Switch to Speakers", null, (s, e) => SwitchOutput(_settings.SpeakersDeviceId, "Speakers")),
                new ToolStripMenuItem("🔁  Toggle Output (Headset ⇄ Speakers)", null, (s, e) => ToggleOutput()),
                new ToolStripSeparator(),
                new ToolStripMenuItem("🎤  Switch to Mic 1", null, (s, e) => SwitchMic(_settings.Mic1DeviceId)),
                new ToolStripMenuItem("🎙️  Switch to Mic 2", null, (s, e) => SwitchMic(_settings.Mic2DeviceId)),
                new ToolStripMenuItem("🔁  Toggle Microphone (Mic 1 ⇄ Mic 2)", null, (s, e) => ToggleMic()),
                new ToolStripSeparator(),
                new ToolStripMenuItem("🔄  Check for Updates...", null, async (s, e) => await CheckForUpdatesInteractiveAsync()),
                new ToolStripMenuItem("⚙️  Settings...", null, OnSettings),
                new ToolStripSeparator(),
                new ToolStripMenuItem("❌  Exit", null, OnExit)
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
                new() { Id = HK_HEADSET,       Get = () => _settings.HeadsetHotkey,      Action = () => SwitchOutput(_settings.HeadsetDeviceId, "Headset") },
                new() { Id = HK_SPEAKERS,      Get = () => _settings.SpeakersHotkey,     Action = () => SwitchOutput(_settings.SpeakersDeviceId, "Speakers") },
                new() { Id = HK_OUTPUT_TOGGLE, Get = () => _settings.OutputToggleHotkey, Action = ToggleOutput },
                new() { Id = HK_MIC1,          Get = () => _settings.Mic1Hotkey,         Action = () => SwitchMic(_settings.Mic1DeviceId) },
                new() { Id = HK_MIC2,          Get = () => _settings.Mic2Hotkey,         Action = () => SwitchMic(_settings.Mic2DeviceId) },
                new() { Id = HK_MIC_TOGGLE,    Get = () => _settings.MicToggleHotkey,    Action = ToggleMic },
            };

            FormClosing += Form1_FormClosing;
            Load += Form1_Load;

            LoadDevicesToCombos();
            ShowPage(0);

            if (IsHandleCreated)
            {
                RegisterAllHotkeys();
            }
            else
            {
                HandleCreated += (s, e) => RegisterAllHotkeys();
            }
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
                BackColor = Sidebar
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
            closeBtn.MouseLeave += (s, e) => { closeBtn.BackColor = Sidebar; closeBtn.ForeColor = TextDim; };
            closeBtn.Click += (s, e) => { Hide(); ShowInTaskbar = false; };
            titleBar.Controls.Add(closeBtn);

            var minBtn = MakeChromeButton("—", ClientSize.Width - 82);
            minBtn.MouseEnter += (s, e) => minBtn.BackColor = Color.FromArgb(55, 55, 62);
            minBtn.MouseLeave += (s, e) => minBtn.BackColor = Sidebar;
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
                BackColor = Sidebar,
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
                BackColor = Sidebar
            };
            Controls.Add(sidebar);

            string[] items = { "🎧   Output", "🎤   Microphone", "⚙️   General" };
            int top = 14;
            for (int i = 0; i < items.Length; i++)
            {
                int index = i;
                var b = new Button
                {
                    Text = items[i],
                    Left = 0,
                    Top = top,
                    Width = 150,
                    Height = 48,
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(18, 0, 0, 0),
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = TextDim,
                    BackColor = Sidebar,
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
                _navButtons[i].BackColor = selected ? NavSelected : Sidebar;
                _navButtons[i].ForeColor = selected ? Accent : TextDim;
                _navButtons[i].Font = new Font("Segoe UI", 10F,
                    selected ? FontStyle.Bold : FontStyle.Regular);
                if (i < _navPages.Count)
                {
                    _navPages[i].Visible = selected;
                    if (selected) _navPages[i].BringToFront();
                }
            }
        }

        // ===================== Pages =====================

        private void BuildPages()
        {
            // ---- Output ----
            var outPage = NewPage("🎧  Output");
            _headsetCombo = AddComboRow(outPage, "Headset:", 64);
            _speakersCombo = AddComboRow(outPage, "Speakers:", 104);
            AddSubHeading(outPage, "Hotkeys", 152);
            AddHotkeyRow(outPage, "Headset:", 186,
                () => _settings.HeadsetHotkey, v => _settings.HeadsetHotkey = v);
            AddHotkeyRow(outPage, "Speakers:", 224,
                () => _settings.SpeakersHotkey, v => _settings.SpeakersHotkey = v);
            AddHotkeyRow(outPage, "Switch:", 262,
                () => _settings.OutputToggleHotkey, v => _settings.OutputToggleHotkey = v,
                "Toggle Headset / Speakers");

            // ---- Microphone ----
            var micPage = NewPage("🎤  Microphone");
            _mic1Combo = AddComboRow(micPage, "Mic 1:", 64);
            _mic2Combo = AddComboRow(micPage, "Mic 2:", 104);
            AddSubHeading(micPage, "Hotkeys", 152);
            AddHotkeyRow(micPage, "Mic 1:", 186,
                () => _settings.Mic1Hotkey, v => _settings.Mic1Hotkey = v);
            AddHotkeyRow(micPage, "Mic 2:", 224,
                () => _settings.Mic2Hotkey, v => _settings.Mic2Hotkey = v);
            AddHotkeyRow(micPage, "Switch:", 262,
                () => _settings.MicToggleHotkey, v => _settings.MicToggleHotkey = v,
                "Toggle Mic 1 / Mic 2");

            // ---- General ----
            var genPage = NewPage("⚙️  General");
            _startWithWindowsCheckbox = new CheckBox
            {
                Text = "🚀  Start with Windows",
                Left = 24,
                Top = 70,
                Width = 280,
                ForeColor = LabelColor,
                Font = new Font("Segoe UI", 9.5F),
                Checked = _settings.StartWithWindows
            };
            _startWithWindowsCheckbox.CheckedChanged += StartWithWindowsCheckbox_CheckedChanged;
            genPage.Controls.Add(_startWithWindowsCheckbox);

            var updateBtn = new Button
            {
                Text = "🔄  Check for Updates",
                Left = 24,
                Top = 118,
                Width = 200,
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
            page.Controls.Add(label);
            page.Controls.Add(combo);
            return combo;
        }

        private void AddHotkeyRow(Panel page, string labelText, int top,
            Func<HotkeyConfig?> get, Action<HotkeyConfig?> set,
            string hintText = "Click & press (Del clears)")
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

            // Silently check for updates in the background on every launch.
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

        private void SaveButton_Click(object? sender, EventArgs e)
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
            RegisterAllHotkeys();

            Hide();
            ShowInTaskbar = false;
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
                MessageBox.Show($"{label} device is not configured. Open Settings first.",
                    "Not configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (!Guid.TryParse(deviceId, out var guid))
                {
                    MessageBox.Show("Invalid device id stored in settings.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var device = _audioController.GetDevice(guid);
                if (device == null)
                {
                    MessageBox.Show("Device not found. Maybe it is disconnected?",
                        "Device not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                device.SetAsDefault();
                ShowBalloon("Output Device", "Switched to: " + device.FullName);
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
                MessageBox.Show("Configure both Headset and Speakers first.",
                    "Not configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var current = _audioController.DefaultPlaybackDevice?.Id.ToString();
            string target = current == headset ? speakers : headset;
            SwitchOutput(target, "Output");
        }

        private void SwitchMic(string? deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                MessageBox.Show("Microphone is not configured. Open Settings first.",
                    "Not configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (!Guid.TryParse(deviceId, out var guid))
                {
                    MessageBox.Show("Invalid microphone id stored in settings.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var device = _audioController.GetDevice(guid);
                if (device == null)
                {
                    MessageBox.Show("Microphone not found. Maybe it is disconnected?",
                        "Device not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                device.SetAsDefault();
                ShowBalloon("Microphone", "Default mic: " + device.FullName);
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
                MessageBox.Show("Configure both Mic 1 and Mic 2 first.",
                    "Not configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        // ===================== Auto-update (Velopack) =====================

        private const string UpdateRepoUrl = "https://github.com/GokhanGuclu/SoundDeck";

        private static UpdateManager CreateUpdateManager()
            => new UpdateManager(new GithubSource(UpdateRepoUrl, null, false));

        private async Task AutoUpdateOnStartupAsync()
        {
            try
            {
                var mgr = CreateUpdateManager();
                if (!mgr.IsInstalled) return;

                var info = await mgr.CheckForUpdatesAsync();
                if (info == null) return;

                await mgr.DownloadUpdatesAsync(info);

                // Apply the next time the app exits — settings live in %AppData% and are kept.
                mgr.WaitExitThenApplyUpdates(info.TargetFullRelease, silent: false, restart: false);
                ShowBalloon("SoundDeck",
                    $"Update {info.TargetFullRelease.Version} downloaded — it will be installed the next time you start SoundDeck.");
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
                    MessageBox.Show(
                        "Auto-update is only available in the installed version of SoundDeck (not when run from source).",
                        "SoundDeck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var info = await mgr.CheckForUpdatesAsync();
                if (info == null)
                {
                    MessageBox.Show("You're on the latest version. 🎉",
                        "SoundDeck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var answer = MessageBox.Show(
                    $"A new version ({info.TargetFullRelease.Version}) is available.\n\n" +
                    "Download and update now? SoundDeck will restart and keep all your settings.",
                    "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;

                ShowBalloon("SoundDeck", "Downloading update...");
                await mgr.DownloadUpdatesAsync(info);
                mgr.ApplyUpdatesAndRestart(info);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update check failed: " + ex.Message,
                    "SoundDeck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

        // General
        public bool StartWithWindows { get; set; }
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
