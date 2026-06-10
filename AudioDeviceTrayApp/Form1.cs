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

namespace AudioDeviceTrayApp
{
    public partial class Form1 : Form
    {
        // ---- Tray ----
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;

        // ---- Output devices ----
        private readonly ComboBox _headsetCombo;
        private readonly ComboBox _speakersCombo;

        // ---- Microphone ----
        private readonly ComboBox _micCombo;

        // ---- General ----
        private readonly NumericUpDown _volumeStepInput;
        private readonly CheckBox _startWithWindowsCheckbox;
        private readonly Button _saveButton;

        private readonly CoreAudioController _audioController = new CoreAudioController();
        private AppSettings _settings = new AppSettings();
        private readonly Icon? _appIcon;
        private readonly bool _openSettingsOnStart;

        // ---- Hotkey infrastructure ----
        private const int HK_HEADSET = 1;
        private const int HK_SPEAKERS = 2;
        private const int HK_CYCLE = 3;
        private const int HK_MIC_SWITCH = 4;
        private const int HK_MIC_MUTE = 5;
        private const int HK_VOL_UP = 6;
        private const int HK_VOL_DOWN = 7;
        private const int HK_VOL_MUTE = 8;

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

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

        private readonly string _settingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SoundDeck", "settings.json");

        // Theme colors
        private static readonly Color Bg = Color.FromArgb(24, 24, 27);
        private static readonly Color Surface = Color.FromArgb(39, 39, 42);
        private static readonly Color Accent = Color.FromArgb(139, 92, 246);
        private static readonly Color AccentHover = Color.FromArgb(124, 58, 237);
        private static readonly Color TextMain = Color.FromArgb(228, 228, 231);
        private static readonly Color TextDim = Color.FromArgb(161, 161, 170);
        private static readonly Color TextHint = Color.FromArgb(113, 113, 122);

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

            Text = "🎛️ SoundDeck";
            Width = 600;
            Height = 660;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            var titleLabel = new Label
            {
                Text = "🎛️  SoundDeck",
                Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
                ForeColor = Accent,
                AutoSize = true,
                Left = 25,
                Top = 18
            };
            Controls.Add(titleLabel);

            var subtitleLabel = new Label
            {
                Text = "Your audio control center — devices, microphone and volume",
                Font = new Font("Segoe UI", 9F),
                ForeColor = TextDim,
                AutoSize = true,
                Left = 25,
                Top = 48
            };
            Controls.Add(subtitleLabel);

            var content = new Panel
            {
                Left = 18,
                Top = 78,
                Width = 562,
                Height = 500,
                AutoScroll = true,
                BackColor = Bg
            };
            Controls.Add(content);

            int y = 6;

            // ---------- Output devices ----------
            var outGroup = CreateGroup(" 🎵  Output Devices ", y, 110);
            _headsetCombo = AddComboRow(outGroup, "🎧  Headset:", 30);
            _speakersCombo = AddComboRow(outGroup, "🔊  Speakers:", 65);
            content.Controls.Add(outGroup);
            y += outGroup.Height + 12;

            // ---------- Output hotkeys ----------
            var outHk = CreateGroup(" ⌨️  Output Hotkeys ", y, 150);
            AddHotkeyRow(outHk, "🎧  Headset:", 30,
                () => _settings.HeadsetHotkey, v => _settings.HeadsetHotkey = v);
            AddHotkeyRow(outHk, "🔊  Speakers:", 65,
                () => _settings.SpeakersHotkey, v => _settings.SpeakersHotkey = v);
            AddHotkeyRow(outHk, "🔁  Cycle device:", 100,
                () => _settings.CycleOutputHotkey, v => _settings.CycleOutputHotkey = v);
            content.Controls.Add(outHk);
            y += outHk.Height + 12;

            // ---------- Microphone ----------
            var micGroup = CreateGroup(" 🎤  Microphone ", y, 150);
            _micCombo = AddComboRow(micGroup, "🎤  Device:", 30);
            AddHotkeyRow(micGroup, "🎤  Set default:", 65,
                () => _settings.MicSwitchHotkey, v => _settings.MicSwitchHotkey = v);
            AddHotkeyRow(micGroup, "🔇  Mute toggle:", 100,
                () => _settings.MicMuteHotkey, v => _settings.MicMuteHotkey = v);
            content.Controls.Add(micGroup);
            y += micGroup.Height + 12;

            // ---------- Master volume ----------
            var volGroup = CreateGroup(" 🔊  Master Volume ", y, 185);
            AddHotkeyRow(volGroup, "🔊  Volume up:", 30,
                () => _settings.VolumeUpHotkey, v => _settings.VolumeUpHotkey = v);
            AddHotkeyRow(volGroup, "🔉  Volume down:", 65,
                () => _settings.VolumeDownHotkey, v => _settings.VolumeDownHotkey = v);
            AddHotkeyRow(volGroup, "🔇  Mute toggle:", 100,
                () => _settings.VolumeMuteHotkey, v => _settings.VolumeMuteHotkey = v);

            var stepLabel = new Label
            {
                Text = "📏  Step size:",
                AutoSize = true,
                Left = 20,
                Top = 138,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(212, 212, 216)
            };
            _volumeStepInput = new NumericUpDown
            {
                Left = 130,
                Top = 135,
                Width = 70,
                Minimum = 1,
                Maximum = 50,
                Value = Math.Clamp(_settings.VolumeStep, 1, 50),
                BackColor = Surface,
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F)
            };
            _volumeStepInput.ValueChanged += (s, e) =>
            {
                _settings.VolumeStep = (int)_volumeStepInput.Value;
                SaveSettings();
            };
            var stepHint = new Label
            {
                Text = "% per key press",
                AutoSize = true,
                Left = 210,
                Top = 138,
                ForeColor = TextHint,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic)
            };
            volGroup.Controls.Add(stepLabel);
            volGroup.Controls.Add(_volumeStepInput);
            volGroup.Controls.Add(stepHint);
            content.Controls.Add(volGroup);
            y += volGroup.Height + 12;

            // ---------- General ----------
            var genGroup = CreateGroup(" ⚙️  General ", y, 65);
            _startWithWindowsCheckbox = new CheckBox
            {
                Text = "🚀  Start with Windows",
                Left = 20,
                Top = 28,
                Width = 250,
                ForeColor = Color.FromArgb(212, 212, 216),
                Font = new Font("Segoe UI", 9.5F),
                Checked = _settings.StartWithWindows
            };
            _startWithWindowsCheckbox.CheckedChanged += StartWithWindowsCheckbox_CheckedChanged;
            genGroup.Controls.Add(_startWithWindowsCheckbox);
            content.Controls.Add(genGroup);

            // ---------- Save button ----------
            _saveButton = new Button
            {
                Text = "💾  Save & Close",
                Left = 420,
                Top = 588,
                Width = 160,
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
                new ToolStripMenuItem("🔁  Cycle Output Device", null, (s, e) => CycleOutput()),
                new ToolStripSeparator(),
                new ToolStripMenuItem("🎤  Switch to Microphone", null, (s, e) => SwitchMic()),
                new ToolStripMenuItem("🔇  Mute Microphone", null, (s, e) => ToggleMicMute()),
                new ToolStripSeparator(),
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
                new() { Id = HK_HEADSET,    Get = () => _settings.HeadsetHotkey,      Action = () => SwitchOutput(_settings.HeadsetDeviceId, "Headset") },
                new() { Id = HK_SPEAKERS,   Get = () => _settings.SpeakersHotkey,     Action = () => SwitchOutput(_settings.SpeakersDeviceId, "Speakers") },
                new() { Id = HK_CYCLE,      Get = () => _settings.CycleOutputHotkey,  Action = CycleOutput },
                new() { Id = HK_MIC_SWITCH, Get = () => _settings.MicSwitchHotkey,    Action = SwitchMic },
                new() { Id = HK_MIC_MUTE,   Get = () => _settings.MicMuteHotkey,      Action = ToggleMicMute },
                new() { Id = HK_VOL_UP,     Get = () => _settings.VolumeUpHotkey,     Action = () => AdjustVolume(+_settings.VolumeStep) },
                new() { Id = HK_VOL_DOWN,   Get = () => _settings.VolumeDownHotkey,   Action = () => AdjustVolume(-_settings.VolumeStep) },
                new() { Id = HK_VOL_MUTE,   Get = () => _settings.VolumeMuteHotkey,   Action = ToggleVolumeMute },
            };

            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            Paint += Form1_Paint;

            LoadDevicesToCombos();

            if (IsHandleCreated)
            {
                RegisterAllHotkeys();
            }
            else
            {
                HandleCreated += (s, e) => RegisterAllHotkeys();
            }
        }

        // ===================== UI helpers =====================

        private GroupBox CreateGroup(string title, int top, int height)
        {
            return new GroupBox
            {
                Text = title,
                Left = 8,
                Top = top,
                Width = 524,
                Height = height,
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
        }

        private ComboBox AddComboRow(GroupBox group, string labelText, int top)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Left = 20,
                Top = top + 3,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(212, 212, 216)
            };
            var combo = new ComboBox
            {
                Left = 130,
                Top = top,
                Width = 370,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Surface,
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                DisplayMember = "Name"
            };
            group.Controls.Add(label);
            group.Controls.Add(combo);
            return combo;
        }

        private void AddHotkeyRow(GroupBox group, string labelText, int top,
            Func<HotkeyConfig?> get, Action<HotkeyConfig?> set)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Left = 20,
                Top = top + 3,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(212, 212, 216)
            };

            var box = new TextBox
            {
                Left = 130,
                Top = top,
                Width = 220,
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

                // Backspace / Delete clears the hotkey
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
                Text = "Click & press (Del clears)",
                AutoSize = true,
                Left = 360,
                Top = top + 3,
                ForeColor = TextHint,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic)
            };

            group.Controls.Add(label);
            group.Controls.Add(box);
            group.Controls.Add(hint);
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

        // ===================== Painting / lifecycle =====================

        private void Form1_Paint(object? sender, PaintEventArgs e)
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(0, 0, Width, 3),
                Accent,
                Color.FromArgb(59, 130, 246),
                System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, 0, 0, Width, 3);
        }

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
            FillCombo(_micCombo, capture, _settings.MicDeviceId);
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

            if (_micCombo.SelectedItem is AudioDeviceView mic)
                _settings.MicDeviceId = mic.Id;

            _settings.VolumeStep = (int)_volumeStepInput.Value;

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

        private void CycleOutput()
        {
            try
            {
                var devices = _audioController.GetPlaybackDevices(DeviceState.Active).ToList();
                if (devices.Count == 0) return;

                var current = _audioController.DefaultPlaybackDevice;
                int index = current != null
                    ? devices.FindIndex(d => d.Id == current.Id)
                    : -1;

                var next = devices[(index + 1) % devices.Count];
                next.SetAsDefault();
                ShowBalloon("Output Device", "Switched to: " + next.FullName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to cycle device: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SwitchMic()
        {
            if (string.IsNullOrEmpty(_settings.MicDeviceId))
            {
                MessageBox.Show("Microphone device is not configured. Open Settings first.",
                    "Not configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (!Guid.TryParse(_settings.MicDeviceId, out var guid)) return;

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

        private void ToggleMicMute()
        {
            try
            {
                var mic = _audioController.DefaultCaptureDevice;
                if (mic == null) return;

                bool target = !mic.IsMuted;
                mic.Mute(target);
                ShowBalloon("Microphone", target ? "Microphone muted 🔇" : "Microphone on 🎤");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to toggle microphone: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AdjustVolume(int delta)
        {
            try
            {
                var dev = _audioController.DefaultPlaybackDevice;
                if (dev == null) return;

                double target = Math.Clamp(dev.Volume + delta, 0, 100);
                dev.Volume = target;
                ShowBalloon("Volume", $"{dev.FullName}: {Math.Round(target)}%");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to change volume: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleVolumeMute()
        {
            try
            {
                var dev = _audioController.DefaultPlaybackDevice;
                if (dev == null) return;

                bool target = !dev.IsMuted;
                dev.Mute(target);
                ShowBalloon("Volume", target ? "Muted 🔇" : "Unmuted 🔊");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to toggle mute: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowBalloon(string title, string text)
        {
            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = text;
            _trayIcon.ShowBalloonTip(1000);
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
        public HotkeyConfig? CycleOutputHotkey { get; set; }

        // Microphone
        public string? MicDeviceId { get; set; }
        public HotkeyConfig? MicSwitchHotkey { get; set; }
        public HotkeyConfig? MicMuteHotkey { get; set; }

        // Volume
        public HotkeyConfig? VolumeUpHotkey { get; set; }
        public HotkeyConfig? VolumeDownHotkey { get; set; }
        public HotkeyConfig? VolumeMuteHotkey { get; set; }
        public int VolumeStep { get; set; } = 5;

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
