# Audio Device Switcher - Simple Installer
# PowerShell Installer Script

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "  Audio Device Switcher - Installer   " -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

$AppName = "SoundDeck"
$ExeName = "SoundDeck.exe"
$InstallDir = "$env:LOCALAPPDATA\Programs\SoundDeck"
$SourcePath = "bin\Release\net10.0-windows\win-x64\publish\$ExeName"

# Check if source exists
if (-not (Test-Path $SourcePath)) {
    Write-Host "HATA: Publish edilmiş dosya bulunamadı!" -ForegroundColor Red
    Write-Host "Önce projeyi publish edin: dotnet publish -c Release -r win-x64 --self-contained true" -ForegroundColor Yellow
    Read-Host "Devam etmek için Enter'a basın"
    exit 1
}

Write-Host "✓ Uygulama dosyası bulundu" -ForegroundColor Green

# Create install directory
Write-Host "Kurulum klasoru olusturuluyor: $InstallDir"
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

# Copy executable
Write-Host "Dosyalar kopyalaniyor..."
Copy-Item -Path $SourcePath -Destination "$InstallDir\$ExeName" -Force

# Create Start Menu shortcut
Write-Host "Baslat menusu kisayolu olusturuluyor..."
$StartMenuPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\$AppName"
New-Item -ItemType Directory -Force -Path $StartMenuPath | Out-Null

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$StartMenuPath\$AppName.lnk")
$Shortcut.TargetPath = "$InstallDir\$ExeName"
$Shortcut.Description = "Audio Device Switcher"
$Shortcut.Save()

# Ask for desktop shortcut
$desktop = Read-Host "`nMasaustu kisayolu olusturulsun mu? (E/H)"
if ($desktop -eq "E" -or $desktop -eq "e") {
    Write-Host "Masaustu kisayolu olusturuluyor..."
    $DesktopShortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\$AppName.lnk")
    $DesktopShortcut.TargetPath = "$InstallDir\$ExeName"
    $DesktopShortcut.Description = "Audio Device Switcher"
    $DesktopShortcut.Save()
}

# Ask for auto-start
$autostart = Read-Host "`nWindows baslangicinda otomatik baslatilsin mi? (E/H)"
if ($autostart -eq "E" -or $autostart -eq "e") {
    Write-Host "Otomatik baslatma ekleniyor..."
    $RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    Set-ItemProperty -Path $RegPath -Name $AppName -Value "`"$InstallDir\$ExeName`""
}

# Create uninstaller
Write-Host "Kaldirma scripti olusturuluyor..."
$UninstallerScript = @"
# Audio Device Switcher - Uninstaller
Write-Host "Audio Device Switcher kaldiriliyor..." -ForegroundColor Yellow

# Remove registry entry
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "$AppName" -ErrorAction SilentlyContinue

# Remove shortcuts
Remove-Item "$StartMenuPath" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$env:USERPROFILE\Desktop\$AppName.lnk" -Force -ErrorAction SilentlyContinue

# Remove installation directory
Remove-Item "$InstallDir" -Recurse -Force -ErrorAction SilentlyContinue

# Ask to remove settings
`$removeSettings = Read-Host "Ayarlarinizi da silmek ister misiniz? (E/H)"
if (`$removeSettings -eq "E" -or `$removeSettings -eq "e") {
    Remove-Item "$env:APPDATA\SoundDeck" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Ayarlar silindi" -ForegroundColor Green
}

Write-Host "Audio Device Switcher basariyla kaldirildi" -ForegroundColor Green
Read-Host "Devam etmek icin Enter'a basin"
"@

Set-Content -Path "$InstallDir\Uninstall.ps1" -Value $UninstallerScript

# Create uninstaller shortcut
$UninstallShortcut = $WshShell.CreateShortcut("$StartMenuPath\$AppName Kaldır.lnk")
$UninstallShortcut.TargetPath = "powershell.exe"
$UninstallShortcut.Arguments = "-ExecutionPolicy Bypass -File `"$InstallDir\Uninstall.ps1`""
$UninstallShortcut.Description = "Uninstall Audio Device Switcher"
$UninstallShortcut.Save()

Write-Host ""
Write-Host "=======================================" -ForegroundColor Green
Write-Host "  Kurulum Basariyla Tamamlandi!       " -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Kurulum yeri: $InstallDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Uygulamayi Baslatmak Icin:" -ForegroundColor Yellow
Write-Host "  - Baslat Menusu -> Audio Device Switcher" -ForegroundColor White
Write-Host "  - Veya sistem tepsisinde arayin" -ForegroundColor White
Write-Host ""

$launch = Read-Host "Simdi uygulamayi baslatmak ister misiniz? (E/H)"
if ($launch -eq "E" -or $launch -eq "e") {
    Start-Process "$InstallDir\$ExeName"
}

Write-Host ""
Read-Host "Devam etmek icin Enter'a basin"
