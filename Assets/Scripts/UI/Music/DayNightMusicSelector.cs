using UnityEngine;

namespace TopDownTacticalAI.UI.Music
{
    /// <summary>
    /// Day/Night music selector. Each call to <see cref="GetCurrentPeriod"/>
    /// **randomly** picks "Day" or "Night" — so re-entering the menu can give
    /// either song. The probability is uniform (50/50) via <see cref="Random.value"/>.
    ///
    /// The two tracks are looked up by their <see cref="MusicTrack.sceneName"/>
    /// field — set <c>sceneName = "Day"</c> on the Day entry and
    /// <c>sceneName = "Night"</c> on the Night entry of the MusicPlaylistSO.
    /// </summary>
    public static class DayNightMusicSelector
    {
        /// <summary>
        /// Returns "Day" or "Night" randomly. Each call picks independently,
        /// so consecutive menu entries can repeat the same period.
        /// </summary>
        public static string GetCurrentPeriod()
        {
            return Random.value > 0.5f ? "Day" : "Night";
        }

        /// <summary>
        /// Pick the day-or-night track from the supplied playlist. The track
        /// is selected by matching <see cref="MusicTrack.sceneName"/> against
        /// "Day" or "Night" first, then by falling back to the first
        /// <see cref="MusicContext.MainMenu"/> entry (this lets a user with
        /// no Day/Night tagged entries still get *something* playing).
        /// Returns null if nothing matches.
        /// </summary>
        public static MusicPlaylistSO.MusicTrack PickDayOrNightTrack(MusicPlaylistSO playlist)
        {
            if (playlist == null || playlist.tracks == null) return null;
            string period = GetCurrentPeriod();

            // First pass: exact match on (context=MainMenu, sceneName="Day"/"Night" case-insensitive)
            for (int i = 0; i < playlist.tracks.Count; i++)
            {
                var t = playlist.tracks[i];
                if (t != null && t.context == MusicContext.MainMenu &&
                    !string.IsNullOrEmpty(t.sceneName) &&
                    string.Equals(t.sceneName, period, System.StringComparison.OrdinalIgnoreCase) &&
                    t.clip != null)
                {
                    return t;
                }
            }
            // Second pass: any clip whose sceneName matches the period (case-insensitive, any context)
            for (int i = 0; i < playlist.tracks.Count; i++)
            {
                var t = playlist.tracks[i];
                if (t != null && !string.IsNullOrEmpty(t.sceneName) &&
                    string.Equals(t.sceneName, period, System.StringComparison.OrdinalIgnoreCase) &&
                    t.clip != null)
                {
                    return t;
                }
            }
            // Final fallback: first MainMenu track
            for (int i = 0; i < playlist.tracks.Count; i++)
            {
                var t = playlist.tracks[i];
                if (t != null && t.context == MusicContext.MainMenu && t.clip != null)
                    return t;
            }
            return null;
        }
    }
}
