namespace TopDownTacticalAI.UI.Music
{
    /// <summary>
    /// Logical "scene state" that the music system uses to pick a playlist entry
    /// and keep position continuous across scene changes. Adding a new context is
    /// just adding an enum value + a matching row in MusicPlaylistSO — the
    /// AudioManager looks everything up by enum, so the controllers never reference
    /// AudioClips directly.
    /// </summary>
    public enum MusicContext
    {
        /// <summary>Main menu (the game's hub, where you start and return to).</summary>
        MainMenu = 0,

        /// <summary>Stage / level selection screen (per-level intros / previews).</summary>
        LevelSelect = 1,

        /// <summary>In-game pause overlay (uses the same track as the active level, slightly ducked).</summary>
        Pause = 2,

        /// <summary>Gameplay / active level background track.</summary>
        Gameplay = 3,

        /// <summary>Settings panel (sub-overlay of MainMenu / Pause — keeps the parent track).</summary>
        Settings = 4,

        /// <summary>Splash / intro screen (pre-menu).</summary>
        Splash = 5,

        /// <summary>Day music track for MainMenu (toggles with Night on each menu entry).</summary>
        Day = 6,

        /// <summary>Night music track for MainMenu (toggles with Day on each menu entry).</summary>
        Night = 7,
    }
}
