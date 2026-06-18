using System.Collections.Generic;

namespace AudioDeviceTrayApp
{
    /// <summary>
    /// Minimal in-code localization. Set <see cref="Lang"/> ("en" or "tr")
    /// then read strings with <see cref="T"/>.
    /// </summary>
    internal static class Localization
    {
        public static string Lang = "en";

        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
        {
            ["en"] = new()
            {
                // Navigation / headings
                ["nav_output"] = "🎧   Output",
                ["nav_mic"] = "🎤   Microphone",
                ["nav_general"] = "⚙️   General",
                ["head_output"] = "🎧  Output",
                ["head_mic"] = "🎤  Microphone",
                ["head_general"] = "⚙️  General",
                ["sub_hotkeys"] = "Hotkeys",

                // Field labels
                ["lbl_headset"] = "Headset:",
                ["lbl_speakers"] = "Speakers:",
                ["lbl_mic1"] = "Mic 1:",
                ["lbl_mic2"] = "Mic 2:",
                ["lbl_switch"] = "Switch:",
                ["lbl_language"] = "Language:",

                // Hints
                ["hint_click"] = "Click & press (Del clears)",
                ["hint_toggle_output"] = "Toggle Headset / Speakers",
                ["hint_toggle_mic"] = "Toggle Mic 1 / Mic 2",

                // General page
                ["chk_startup"] = "🚀  Start with Windows",
                ["btn_check_updates"] = "🔄  Check for Updates",
                ["btn_save"] = "💾  Save & Close",

                // Tray
                ["tray_headset"] = "🎧  Switch to Headset",
                ["tray_speakers"] = "🔊  Switch to Speakers",
                ["tray_output_toggle"] = "🔁  Toggle Output (Headset ⇄ Speakers)",
                ["tray_mic1"] = "🎤  Switch to Mic 1",
                ["tray_mic2"] = "🎙️  Switch to Mic 2",
                ["tray_mic_toggle"] = "🔁  Toggle Microphone (Mic 1 ⇄ Mic 2)",
                ["tray_updates"] = "🔄  Check for Updates...",
                ["tray_settings"] = "⚙️  Settings...",
                ["tray_exit"] = "❌  Exit",

                // Device names (used in messages/balloons)
                ["dev_headset"] = "Headset",
                ["dev_speakers"] = "Speakers",
                ["dev_output"] = "Output",

                // Balloons
                ["balloon_output"] = "Output Device",
                ["balloon_switched"] = "Switched to: {0}",
                ["balloon_mic"] = "Microphone",
                ["balloon_mic_default"] = "Default mic: {0}",

                // Messages
                ["msg_not_configured_title"] = "Not configured",
                ["msg_output_not_config"] = "{0} device is not configured. Open Settings first.",
                ["msg_mic_not_config"] = "Microphone is not configured. Open Settings first.",
                ["msg_config_output_both"] = "Configure both Headset and Speakers first.",
                ["msg_config_mic_both"] = "Configure both Mic 1 and Mic 2 first.",
                ["msg_device_not_found_title"] = "Device not found",
                ["msg_device_not_found"] = "Device not found. Maybe it is disconnected?",
                ["msg_mic_not_found"] = "Microphone not found. Maybe it is disconnected?",

                // Updates
                ["update_available_title"] = "Update available",
                ["update_available_msg"] = "A new version ({0}) is available.\n\nDownload and update now? SoundDeck will restart and keep all your settings.",
                ["update_latest"] = "You're on the latest version. 🎉",
                ["update_only_installed"] = "Auto-update is only available in the installed version of SoundDeck (not when run from source).",
                ["update_downloaded"] = "Update {0} downloaded — it will be installed the next time you start SoundDeck.",
                ["update_downloading"] = "Downloading update...",

                // What's new
                ["whats_new_heading"] = "What's New in v{0}",
                ["btn_close"] = "Close",

                // Language switch
                ["lang_restart_title"] = "Language",
                ["lang_restart_msg"] = "SoundDeck will restart to apply the new language.",
                ["lang_english"] = "English",
                ["lang_turkish"] = "Türkçe",
            },
            ["tr"] = new()
            {
                // Navigation / headings
                ["nav_output"] = "🎧   Çıkış",
                ["nav_mic"] = "🎤   Mikrofon",
                ["nav_general"] = "⚙️   Genel",
                ["head_output"] = "🎧  Çıkış",
                ["head_mic"] = "🎤  Mikrofon",
                ["head_general"] = "⚙️  Genel",
                ["sub_hotkeys"] = "Kısayollar",

                // Field labels
                ["lbl_headset"] = "Kulaklık:",
                ["lbl_speakers"] = "Hoparlör:",
                ["lbl_mic1"] = "Mikrofon 1:",
                ["lbl_mic2"] = "Mikrofon 2:",
                ["lbl_switch"] = "Geçiş:",
                ["lbl_language"] = "Dil:",

                // Hints
                ["hint_click"] = "Tıkla & tuşa bas (Del siler)",
                ["hint_toggle_output"] = "Kulaklık / Hoparlör geçişi",
                ["hint_toggle_mic"] = "Mikrofon 1 / 2 geçişi",

                // General page
                ["chk_startup"] = "🚀  Windows ile başlat",
                ["btn_check_updates"] = "🔄  Güncellemeleri Denetle",
                ["btn_save"] = "💾  Kaydet & Kapat",

                // Tray
                ["tray_headset"] = "🎧  Kulaklığa Geç",
                ["tray_speakers"] = "🔊  Hoparlöre Geç",
                ["tray_output_toggle"] = "🔁  Çıkış Değiştir (Kulaklık ⇄ Hoparlör)",
                ["tray_mic1"] = "🎤  Mikrofon 1'e Geç",
                ["tray_mic2"] = "🎙️  Mikrofon 2'ye Geç",
                ["tray_mic_toggle"] = "🔁  Mikrofon Değiştir (Mik 1 ⇄ Mik 2)",
                ["tray_updates"] = "🔄  Güncellemeleri Denetle...",
                ["tray_settings"] = "⚙️  Ayarlar...",
                ["tray_exit"] = "❌  Çıkış",

                // Device names
                ["dev_headset"] = "Kulaklık",
                ["dev_speakers"] = "Hoparlör",
                ["dev_output"] = "Çıkış",

                // Balloons
                ["balloon_output"] = "Çıkış Aygıtı",
                ["balloon_switched"] = "Geçildi: {0}",
                ["balloon_mic"] = "Mikrofon",
                ["balloon_mic_default"] = "Varsayılan mikrofon: {0}",

                // Messages
                ["msg_not_configured_title"] = "Yapılandırılmadı",
                ["msg_output_not_config"] = "{0} aygıtı yapılandırılmadı. Önce Ayarlar'ı açın.",
                ["msg_mic_not_config"] = "Mikrofon yapılandırılmadı. Önce Ayarlar'ı açın.",
                ["msg_config_output_both"] = "Önce hem Kulaklık hem Hoparlör seçin.",
                ["msg_config_mic_both"] = "Önce hem Mikrofon 1 hem Mikrofon 2 seçin.",
                ["msg_device_not_found_title"] = "Aygıt bulunamadı",
                ["msg_device_not_found"] = "Aygıt bulunamadı. Bağlantısı kesilmiş olabilir mi?",
                ["msg_mic_not_found"] = "Mikrofon bulunamadı. Bağlantısı kesilmiş olabilir mi?",

                // Updates
                ["update_available_title"] = "Güncelleme mevcut",
                ["update_available_msg"] = "Yeni bir sürüm ({0}) mevcut.\n\nŞimdi indirilip güncellensin mi? SoundDeck yeniden başlar ve tüm ayarların korunur.",
                ["update_latest"] = "En güncel sürümü kullanıyorsun. 🎉",
                ["update_only_installed"] = "Otomatik güncelleme yalnızca kurulu SoundDeck sürümünde çalışır (kaynaktan çalıştırınca değil).",
                ["update_downloaded"] = "{0} güncellemesi indirildi — SoundDeck'i bir sonraki açışında kurulacak.",
                ["update_downloading"] = "Güncelleme indiriliyor...",

                // What's new
                ["whats_new_heading"] = "v{0} Yenilikleri",
                ["btn_close"] = "Kapat",

                // Language switch
                ["lang_restart_title"] = "Dil",
                ["lang_restart_msg"] = "Yeni dili uygulamak için SoundDeck yeniden başlatılacak.",
                ["lang_english"] = "English",
                ["lang_turkish"] = "Türkçe",
            },
        };

        public static string T(string key, params object[] args)
        {
            var table = Strings.TryGetValue(Lang, out var t) ? t : Strings["en"];
            if (!table.TryGetValue(key, out var value))
            {
                Strings["en"].TryGetValue(key, out value);
            }
            value ??= key;
            return args.Length > 0 ? string.Format(value, args) : value;
        }
    }
}
