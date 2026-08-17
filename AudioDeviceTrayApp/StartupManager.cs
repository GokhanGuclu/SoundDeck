using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
#if STORE
using Windows.ApplicationModel;
#endif

namespace AudioDeviceTrayApp
{
    /// <summary>
    /// "Start with Windows" handling.
    ///
    /// The classic build writes the usual HKCU\...\Run value. The Store (MSIX) build
    /// cannot: Run entries are not a supported startup mechanism for packaged apps and
    /// are rejected during Store certification, so it uses the windows.startupTask
    /// extension declared in the package manifest instead. The user can also flip that
    /// task in Task Manager > Startup, which we can neither override nor bypass.
    /// </summary>
    internal static class StartupManager
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "SoundDeck";

        /// <summary>Must match the TaskId in the package manifest.</summary>
        public const string StartupTaskId = "SoundDeckStartupTask";

        /// <summary>
        /// Applies the setting. Returns null when it took effect, or a message to show
        /// the user when Windows refused (only possible in the Store build).
        /// </summary>
        public static async Task<string?> SetEnabledAsync(bool enable)
        {
#if STORE
            var task = await StartupTask.GetAsync(StartupTaskId);

            if (enable)
            {
                switch (task.State)
                {
                    case StartupTaskState.Enabled:
                    case StartupTaskState.EnabledByPolicy:
                        return null;
                    case StartupTaskState.DisabledByUser:
                        return Localization.T("startup_blocked_user");
                    case StartupTaskState.DisabledByPolicy:
                        return Localization.T("startup_blocked_policy");
                    default:
                        var state = await task.RequestEnableAsync();
                        return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy
                            ? null
                            : Localization.T("startup_blocked_user");
                }
            }

            if (task.State == StartupTaskState.Enabled)
            {
                task.Disable();
            }
            return null;
#else
            await Task.CompletedTask;

            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return null;

            if (enable)
            {
                key.SetValue(AppName, Application.ExecutablePath);
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
            return null;
#endif
        }

        /// <summary>The startup state Windows actually reports, which the user may have changed behind our back.</summary>
        public static async Task<bool> IsEnabledAsync()
        {
#if STORE
            var task = await StartupTask.GetAsync(StartupTaskId);
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
#else
            await Task.CompletedTask;
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppName) != null;
#endif
        }
    }
}
