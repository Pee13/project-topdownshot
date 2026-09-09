using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TopDownTacticalAI.UI.Music;

public class MusicBootstrapper
{
    [MenuItem("TopDownTacticalAI/Build Music Playlist")]
    public static void BuildPlaylist()
    {
        string musicFolderPath = "Assets/Music";
        string playlistAssetPath = "Assets/Music/MusicPlaylist.asset";

        // Ensure the Music folder exists
        if (!AssetDatabase.IsValidFolder(musicFolderPath))
        {
            Debug.LogError($"[MusicBootstrapper] Folder {musicFolderPath} does not exist.");
            return;
        }

        // Find all .mp3 files in the Music folder
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { musicFolderPath });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[MusicBootstrapper] No audio clips found in {musicFolderPath}");
            return;
        }

        // Load or create the playlist asset
        MusicPlaylistSO playlist = AssetDatabase.LoadAssetAtPath<MusicPlaylistSO>(playlistAssetPath);
        if (playlist == null)
        {
            playlist = ScriptableObject.CreateInstance<MusicPlaylistSO>();
            AssetDatabase.CreateAsset(playlist, playlistAssetPath);
            Debug.Log($"[MusicBootstrapper] Created new playlist at {playlistAssetPath}");
        }
        else
        {
            Debug.Log($"[MusicBootstrapper] Using existing playlist at {playlistAssetPath}");
        }

        // Clear existing tracks (we'll rebuild from scratch)
        playlist.tracks.Clear();

        // Build track list
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null) continue;

            // Determine context based on filename (simple heuristic)
            string fileName = Path.GetFileNameWithoutExtension(assetPath).ToLower();
            MusicContext context = MusicContext.MainMenu; // default

            // Day/Night tracks are tagged with sceneName "Day"/"Night" so
            // DayNightMusicSelector can pick them by time of day.
            string sceneName = "";

            if (fileName == "day" || fileName == "night")
            {
                context = MusicContext.MainMenu;
                sceneName = fileName; // "Day" or "Night"
            }
            else if (fileName.Contains("mainmenu") || fileName.Contains("menu"))
                context = MusicContext.MainMenu;
            else if (fileName.Contains("levelselect") || fileName.Contains("select"))
                context = MusicContext.LevelSelect;
            else if (fileName.Contains("pause"))
                context = MusicContext.Pause;
            else if (fileName.Contains("gameplay") || fileName.Contains("level") || fileName.Contains("in_game"))
                context = MusicContext.Gameplay;
            else if (fileName.Contains("settings"))
                context = MusicContext.Settings;
            else if (fileName.Contains("splash") || fileName.Contains("intro"))
                context = MusicContext.Splash;

            // Skip the legacy single-track file — replaced by Day/Night system
            if (fileName.Contains("after_the_engines_died"))
            {
                Debug.Log($"[MusicBootstrapper] Skipping legacy track: {fileName}");
                continue;
            }

            // Determine loop: menu tracks loop, gameplay/level tracks maybe loop as well
            bool loop = true; // default loop
            // One-shots could be detected by containing "one_shot" or "stinger"
            if (fileName.Contains("one_shot") || fileName.Contains("stinger") || fileName.Contains("fanfare"))
                loop = false;

            // Volume multiplier: default 1.0, can be adjusted by naming
            float volumeMultiplier = 1f;
            if (fileName.Contains("quiet") || fileName.Contains("ambient"))
                volumeMultiplier = 0.5f;
            else if (fileName.Contains("loud") || fileName.Contains("intense"))
                volumeMultiplier = 1.2f;

            // Create track entry
            var track = new MusicPlaylistSO.MusicTrack
            {
                context = context,
                sceneName = sceneName,
                clip = clip,
                loop = loop,
                volumeMultiplier = volumeMultiplier
            };

            playlist.tracks.Add(track);
            Debug.Log($"[MusicBootstrapper] Added track: {fileName} -> {context} (sceneName='{sceneName}', loop={loop}, vol={volumeMultiplier})");
        }

        // Save the asset
        EditorUtility.SetDirty(playlist);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MusicBootstrapper] Finished building playlist with {playlist.tracks.Count} tracks.");
    }
}