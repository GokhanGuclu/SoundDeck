<div align="center">

<img src="assets/logo.png" alt="SoundDeck logo" width="120" />

# 🎛️ SoundDeck

### **Your audio control center — switch devices, manage your microphone and control volume with global hotkeys**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.txt)
[![C#](https://img.shields.io/badge/C%23-14.0-239120?logo=csharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)

---

</div>

## 📸 Demo

<div align="center">

![SoundDeck Demo](assets/audio.gif)

**See it in action!** Switch audio devices, toggle your mic and change the volume — all without leaving your game or app.

</div>

---

## 🚀 Features

- 🎧 **Quick Device Switching** — Jump between your headset and speakers instantly
- 🔁 **Cycle Output Devices** — One hotkey to rotate through every active playback device
- 🎤 **Microphone Control** — Set your default mic and mute/unmute it with a hotkey
- 🔊 **Master Volume Hotkeys** — Volume up, down and mute from anywhere, with a configurable step
- ⌨️ **Global Hotkeys** — Custom keyboard shortcuts that work system-wide
- 🔔 **Notifications** — Visual feedback every time something changes
- 💾 **Auto-Save** — Your preferences are stored automatically
- 🚀 **Startup Option** — Launch automatically with Windows
- 🎯 **Lightweight** — Runs quietly in the system tray with minimal resources

---

## 🛠️ Built With

| Technology | Purpose |
|------------|---------|
| **[.NET 10](https://dotnet.microsoft.com/)** | Application framework |
| **[C# 14.0](https://docs.microsoft.com/en-us/dotnet/csharp/)** | Programming language |
| **[Windows Forms](https://docs.microsoft.com/en-us/dotnet/desktop/winforms/)** | User interface |
| **[AudioSwitcher.AudioApi.CoreAudio](https://github.com/xenolightning/AudioSwitcher)** | Audio device & volume management |
| **[System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json)** | Settings serialization |
| **Win32 API** | Global hotkey registration |

---

## 📦 Installation

### Option 1: Installer (Recommended)
1. Download the latest setup from [Releases](../../releases)
2. Run the installer and follow the setup wizard
3. SoundDeck starts automatically in your system tray

### Option 2: Portable / from source
1. Build the project (see [Development](#-development))
2. Run `SoundDeck.exe`
3. Requires the [.NET 10.0 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

---

## 📖 Usage Guide

### Initial Setup

1. **Open settings** — double-click the tray icon 🎛️ or right-click → ⚙️ Settings
2. **Output Devices** — pick your **Headset** and **Speakers**
3. **Microphone** — pick the mic you want as default
4. **Set hotkeys** — click any hotkey box and press your key combo (`Ctrl` / `Alt` / `Shift` + key). Press `Delete` to clear a hotkey.
5. **Master Volume** — set the step size (% per key press)
6. **Start with Windows** (optional) — enable auto-start
7. Click 💾 **Save & Close**

Settings are stored in: `%AppData%\SoundDeck\settings.json`

### What you can bind

| Action | Description |
|--------|-------------|
| 🎧 Switch to Headset | Set your headset as the default output |
| 🔊 Switch to Speakers | Set your speakers as the default output |
| 🔁 Cycle output device | Rotate through all active playback devices |
| 🎤 Set default mic | Make your chosen microphone the default |
| 🔇 Mic mute toggle | Mute/unmute the current microphone |
| 🔊 Volume up / down | Raise or lower master volume by the step size |
| 🔇 Volume mute toggle | Mute/unmute master volume |

### Switching from the tray

Right-click the tray icon for quick access to device switching, microphone mute and settings.

---

## ⌨️ Hotkey Configuration

Combine any modifiers with a key:

- **Modifiers**: `Ctrl`, `Alt`, `Shift`
- **Keys**: `A–Z`, `0–9`, `F1–F12`, NumPad keys, `Space`, `Enter`, and more

### Example combinations
```
Ctrl+Alt+H       → Switch to Headset
Ctrl+Alt+S       → Switch to Speakers
Ctrl+Alt+C       → Cycle output device
Ctrl+Alt+M       → Toggle microphone mute
Ctrl+Alt+Up      → Volume up
Ctrl+Alt+Down    → Volume down
```

---

## 💻 System Requirements

- **OS**: Windows 10 (1809+) or Windows 11
- **Runtime**: .NET 10.0 Runtime
- **Architecture**: x64 or ARM64
- **Permissions**: Standard user (no admin required)

---

## 🔧 Development

### Prerequisites
- Visual Studio 2022/2026 or the .NET 10.0 SDK
- Windows 10/11

### Build & run

```powershell
# Clone the repository
git clone https://github.com/GokhanGuclu/SoundDeck.git
cd SoundDeck

# Restore, build and run
dotnet restore
dotnet build
dotnet run --project AudioDeviceTrayApp
```

### Regenerating the logo / icon

The app icon and logo are generated from a single script:

```powershell
powershell -ExecutionPolicy Bypass -File assets\make_logo.ps1
```

This produces `assets/logo.png` and `AudioDeviceTrayApp/app.ico`.

### Project structure
```
SoundDeck/
├── README.md
├── LICENSE.txt
├── .gitignore
├── AudioDeviceTrayApp.slnx          # Solution
├── assets/
│   ├── logo.png                     # App logo (for README)
│   ├── audio.gif                    # Demo animation
│   └── make_logo.ps1                # Logo / icon generator
└── AudioDeviceTrayApp/              # Project
    ├── Program.cs                   # Entry point
    ├── Form1.cs                     # Main application logic
    ├── Form1.Designer.cs            # Designer generated code
    ├── app.ico                      # Application icon
    ├── setup.iss                    # Inno Setup installer script
    ├── SimpleInstaller.ps1          # Lightweight PowerShell installer
    └── AudioDeviceTrayApp.csproj
```

---

## 🐛 Troubleshooting

**Hotkeys not working?** Another app may be using the same combo — pick a different one.

**Device not switching?** Make sure it's connected and active, then re-select it in settings.

**Volume / mic hotkeys do nothing?** They act on the *current default* device — check your Windows sound settings.

**App not starting with Windows?** Re-enable "Start with Windows" in settings and check Task Manager → Startup.

---

## 📝 License

This project is licensed under the MIT License — see [LICENSE.txt](LICENSE.txt).

---

## 👨‍💻 Author

**Gökhan Güçlü** — created with ❤️ and ☕

If you find SoundDeck useful, consider giving it a ⭐ on GitHub!

---

## 🤝 Contributing

Contributions, issues and feature requests are welcome!

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 🙏 Acknowledgments

- [AudioSwitcher Library](https://github.com/xenolightning/AudioSwitcher) by @xenolightning
- Icons from Windows Segoe UI Emoji

---

<div align="center">

**If you found this helpful, please ⭐ star this repository!**

[Report Bug](../../issues) · [Request Feature](../../issues) · [View Releases](../../releases)

</div>
