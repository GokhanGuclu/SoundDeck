namespace AudioDeviceTrayApp
{
    /// <summary>
    /// The product name shown to users.
    ///
    /// The Store build must use the name reserved in Partner Center, and the MSIX manifest
    /// must show exactly the same name — packaging\build-msix.ps1 reads
    /// <see cref="StoreDisplayName"/> from this file so the two can never drift apart.
    ///
    /// Internal identifiers are deliberately NOT derived from this: the executable name,
    /// the %AppData%\SoundDeck settings folder and the Equalizer APO include file keep
    /// their names, so renaming the product never loses a user's settings.
    /// </summary>
    internal static class AppInfo
    {
        // build-msix.ps1 parses the next line — keep it on one line in this exact shape.
        // Must be exactly the name reserved in Partner Center.
        public const string StoreDisplayName = "SoundPilot";

#if STORE
        public const string DisplayName = StoreDisplayName;
#else
        public const string DisplayName = "SoundDeck";
#endif
    }
}
