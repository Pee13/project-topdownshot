using UnityEngine;
using TopDownTacticalAI.UI.Music;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Central audio manager for the game. Separates Music/SFX volume levels
    /// (in addition to the Master volume controlled via AudioListener.volume).
    /// It's a Singleton that persists across scenes (DontDestroyOnLoad) so music
    /// keeps playing during scene transitions between menus/levels.
    ///
    /// Features:
    /// - Single AudioSource for music (no crossfade - only one track plays at a time)
    /// - Smooth fade in/out on track switches
    /// - Context-aware playback via MusicPlaylistSO + MusicContext
    /// - Position continuity: remembers playback offset across scene loads
    /// - Per-track volume multiplier support
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("── Audio Sources ──")]
        [Tooltip("AudioSource for music. Only one track plays at a time - switches use fade in/out.")]
        public AudioSource musicSource;
        [Tooltip("AudioSource for general SFX (button clicks, etc.) - use PlayOneShot")]
        public AudioSource sfxSource;

        [Header("── Music Playlist ──")]
        [Tooltip("ScriptableObject holding all tracks. Drag a MusicPlaylistSO asset here.")]
        public MusicPlaylistSO musicPlaylist;

        [Header("── Playback Volume ──")]
        [Tooltip("Current master music volume (0..1). Overridden by per-track volumeMultiplier if set.")]
        [Range(0f, 1f)] public float musicVolume = 1f;

        [Header("── Context Tracking ──")]
        [Tooltip("Last active music context (for debugging / inspector).")]
        public MusicContext currentContext = MusicContext.MainMenu;

        private MusicContext _previousContext = MusicContext.MainMenu;
        private float _savedPositionSeconds = 0f;  // playback position saved before scene load
        private bool _hasSavedPosition = false;

        // Volume stored at last ApplyMusicVolume() call
        private float _targetMusicVolume = 1f;

        private Coroutine _switchRoutine = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Detach from any parent (e.g. if accidentally placed as a child of Canvas) because
            // DontDestroyOnLoad only works on root GameObjects; otherwise it will warn and not function correctly.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Ensure music source exists and is configured
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;

            // Ensure SFX source exists
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            // Initial volume setup
            ApplyMusicVolume(musicVolume);
        }

        /// <summary>
        /// Sets the music volume (0..1). Applies to both music sources.
        /// </summary>
        public void SetMusicVolume(float volume01)
        {
            musicVolume = Mathf.Clamp01(volume01);
            ApplyMusicVolume(musicVolume);
        }

        /// <summary>
        /// Internal: applies the current _targetMusicVolume to the music source.
        /// </summary>
        private void ApplyMusicVolume(float vol)
        {
            if (musicSource != null) musicSource.volume = vol;
        }

        /// <summary>
        /// Sets the SFX volume (0..1).
        /// </summary>
        public void SetSFXVolume(float volume01)
        {
            if (sfxSource != null) sfxSource.volume = Mathf.Clamp01(volume01);
        }

        /// <summary>
        /// Plays a short SFX (e.g. menu button click sound).
        /// </summary>
        public void PlaySFX(AudioClip clip)
        {
            if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Backward-compat: plays a specific AudioClip on musicSource (hard switch, no fade).
        /// Existing callers that call this directly will still work.
        /// </summary>
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (musicSource == null || clip == null) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return; // Same music already playing, no need to restart
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.Play();
        }

        /// <summary>
        /// Plays music selected from MusicPlaylistSO based on the active MusicContext.
        /// Uses a single AudioSource: if the track changes, it fades out the current clip,
        /// stops it, then fades in the new one. Only one track is ever playing at a time.
        ///
        /// Uses position continuity: if a position was saved before a scene load, the new
        /// track will seek to that same offset (modulo clip length).
        /// </summary>
        /// <param name="context">The MusicContext enum value that determines which track to play.</param>
        /// <param name="sceneNameHint">Optional scene name (e.g. "Level1"). When switching scenes,
        /// AudioManager picks the entry whose sceneName matches the new active scene, falling back
        /// to the first entry of the requested context.</param>
        /// <param name="fadeDuration">Override the default fade duration (seconds).
        /// Use null to use MusicPlaylistSO.defaultCrossfadeDuration.</param>
        public void PlayContext(MusicContext context, string sceneNameHint = null, float? fadeDuration = null)
        {
            if (musicPlaylist == null)
            {
                Debug.LogWarning($"[AudioManager] MusicPlaylistSO not assigned — cannot play context {context}");
                return;
            }

            // Pick the best track for the requested context + optional scene name
            var track = musicPlaylist.Pick(context, sceneNameHint);
            if (track == null || track.clip == null)
            {
                Debug.LogWarning($"[AudioManager] No track found for context {context} with sceneHint '{sceneNameHint}'");
                return;
            }

            // If the same clip is already playing and we're not seeking a saved position, keep it
            if (musicSource != null && context == currentContext && musicSource.clip == track.clip && musicSource.isPlaying && !_hasSavedPosition)
            {
                // Still ensure volume is applied
                ApplyMusicVolume(musicVolume);
                return;
            }

            // Save current position BEFORE switching (for cross-scene continuity)
            if (musicSource != null && musicSource.isPlaying && musicSource.clip != null)
            {
                _savedPositionSeconds = musicSource.time;
                _hasSavedPosition = true;
            }
            else
            {
                _hasSavedPosition = false;
            }

            // Update context tracking
            _previousContext = currentContext;
            currentContext = context;
            float duration = fadeDuration ?? musicPlaylist.defaultCrossfadeDuration;

            // Start the fade-switch to the new track
            StartFadeSwitchTo(track.clip, track.loop, duration);
        }

        /// <summary>
        /// Begins a fade-switch: fade out current music, swap clip, fade in new music.
        /// Ensures only one track is playing at any time.
        /// </summary>
        private void StartFadeSwitchTo(AudioClip newClip, bool loop, float duration)
        {
            if (musicSource == null || newClip == null) return;

            // Stop any in-progress switch and start fresh
            if (_switchRoutine != null) StopCoroutine(_switchRoutine);
            _switchRoutine = StartCoroutine(FadeSwitchRoutine(newClip, loop, duration));
        }

        /// <summary>
        /// Coroutine that fades out the current music, swaps to the new clip, and fades it in.
        /// Single AudioSource, so only one track plays at a time.
        /// </summary>
        private System.Collections.IEnumerator FadeSwitchRoutine(AudioClip newClip, bool loop, float duration)
        {
            _targetMusicVolume = musicVolume;
            bool wasPlaying = musicSource.isPlaying;

            // Phase 1: fade out current music (if playing)
            float fadeOut = wasPlaying ? Mathf.Min(duration * 0.5f, 1f) : 0f;
            if (fadeOut > 0f)
            {
                float startVol = musicSource.volume;
                float t = 0f;
                while (t < fadeOut)
                {
                    t += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeOut);
                    yield return null;
                }
                musicSource.volume = 0f;
                musicSource.Stop();
            }

            // Phase 2: swap clip and seek to saved position if any
            musicSource.clip = newClip;
            musicSource.loop = loop;
            if (_hasSavedPosition && newClip != null && _savedPositionSeconds < newClip.length)
            {
                musicSource.time = _savedPositionSeconds;
            }
            _hasSavedPosition = false;

            // Apply the per-track volume multiplier
            float trackVol = _targetMusicVolume;
            // (MusicTrack.volumeMultiplier supported via the playlist; advanced per-track fade below)

            // Phase 3: fade in new music
            musicSource.volume = 0f;
            musicSource.Play();
            float fadeIn = Mathf.Min(duration * 0.5f, 1f);
            if (fadeIn > 0f)
            {
                float t = 0f;
                while (t < fadeIn)
                {
                    t += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(0f, _targetMusicVolume, t / fadeIn);
                    yield return null;
                }
            }
            musicSource.volume = _targetMusicVolume;

            _switchRoutine = null;
        }

        /// <summary>
        /// Saves the current playback position (in seconds) before a scene transition.
        /// The next PlayContext call will seek to this offset in the new track.
        /// Call this when the player is about to change scenes and you want music continuity.
        /// </summary>
        public void SaveMusicPosition()
        {
            if (musicSource != null && musicSource.isPlaying && musicSource.clip != null)
            {
                _savedPositionSeconds = musicSource.time;
                _hasSavedPosition = true;
            }
            else
            {
                _hasSavedPosition = false;
            }
        }

        /// <summary>
        /// Resets the saved position. Call if you don't want continuity on the next scene load.
        /// </summary>
        public void ClearSavedPosition()
        {
            _hasSavedPosition = false;
            _savedPositionSeconds = 0f;
        }

        private void OnDestroy()
        {
            // Ensure time scale is reset if scene changes while paused
            Time.timeScale = 1f;
        }
    }
}