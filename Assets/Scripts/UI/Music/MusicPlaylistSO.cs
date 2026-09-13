using System;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownTacticalAI.UI.Music
{
    /// <summary>
    /// Data asset that maps each <see cref="MusicContext"/> to one or more
    /// AudioClips. The AudioManager reads this at runtime to pick the track for
    /// the active scene state and to perform crossfades between contexts.
    ///
    /// "Connected" design: when transitioning between two contexts (e.g. MainMenu
    /// -> LevelSelect), the AudioManager seeks the new track to (current time of
    /// outgoing track) so the music feels like one continuous piece, as requested.
    ///
    /// Multiple clips per context are supported (e.g. one per level) — the manager
    /// picks the entry whose <see cref="MusicTrack.sceneName"/> matches the
    /// outgoing track's context+name; if none match, it picks the first entry.
    /// </summary>
    [CreateAssetMenu(fileName = "MusicPlaylist", menuName = "TopDownTacticalAI/Music Playlist", order = 0)]
    public class MusicPlaylistSO : ScriptableObject
    {
        [Serializable]
        public class MusicTrack
        {
            [Tooltip("Which context this track plays in. Multiple entries with the same context are allowed (e.g. one per level).")]
            public MusicContext context = MusicContext.MainMenu;

            [Tooltip("Optional scene name (e.g. 'Level1'). When switching scenes, AudioManager picks the entry whose sceneName matches the new active scene, falling back to the first entry of the requested context.")]
            public string sceneName = "";

            [Tooltip("The actual audio clip to play.")]
            public AudioClip clip;

            [Tooltip("Loop the clip when it reaches the end (true for menu tracks, false for one-shot level intros).")]
            public bool loop = true;

            [Tooltip("Optional per-track volume multiplier (0..1). Useful for tracks that are mixed louder than others.")]
            [Range(0f, 1f)] public float volumeMultiplier = 1f;
        }

        [Header("── Tracks ──")]
        [Tooltip("The actual list. Order matters only when two entries share both context+sceneName (first wins).")]
        public List<MusicTrack> tracks = new List<MusicTrack>();

        [Header("── Crossfade ──")]
        [Tooltip("Default crossfade duration in seconds when switching contexts. Per-call overrides win.")]
        [Range(0.1f, 6f)] public float defaultCrossfadeDuration = 2f;

        [Tooltip("Volume curve used during the fade. X axis 0..1 (time), Y axis 0..1 (volume factor).")]
        public AnimationCurve crossfadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // ── Lookup helpers ──

        /// <summary>Return all entries for a context, in declaration order.</summary>
        public List<MusicTrack> GetTracksFor(MusicContext context)
        {
            var list = new List<MusicTrack>();
            if (tracks == null) return list;
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] != null && tracks[i].context == context) list.Add(tracks[i]);
            }
            return list;
        }

        /// <summary>
        /// Pick the best entry for the requested context. Prefers a row whose
        /// sceneName matches <paramref name="sceneNameHint"/>; otherwise the
        /// first row of that context. Returns null if nothing matches.
        /// </summary>
        public MusicTrack Pick(MusicContext context, string sceneNameHint = null)
        {
            if (tracks == null) return null;
            // First pass: exact sceneName match
            if (!string.IsNullOrEmpty(sceneNameHint))
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    var t = tracks[i];
                    if (t != null && t.context == context &&
                        !string.IsNullOrEmpty(t.sceneName) &&
                        t.sceneName == sceneNameHint)
                    {
                        return t;
                    }
                }
            }
            // Second pass: first entry of the requested context
            for (int i = 0; i < tracks.Count; i++)
            {
                var t = tracks[i];
                if (t != null && t.context == context) return t;
            }
            return null;
        }
    }
}
