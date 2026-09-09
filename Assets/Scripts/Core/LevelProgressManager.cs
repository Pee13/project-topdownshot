using System;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownTacticalAI.Core
{
    /// <summary>
    /// Tracks which stages the player has cleared and unlocks the next one in
    /// sequence. Stored in PlayerPrefs so progress survives across sessions.
    /// Singleton - the first instance to wake up becomes the active one.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public class LevelProgressManager : MonoBehaviour
    {
        public const string ClearedPrefKey = "LevelProgress.ClearedStages";

        public static LevelProgressManager Instance { get; private set; }

        /// <summary>Fired after a stage has been marked as cleared.</summary>
        public event Action<string> OnStageCleared;

        private readonly HashSet<string> _clearedStages = new HashSet<string>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadFromPrefs();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Marks a stage (by scene name) as cleared. Idempotent.</summary>
        public void MarkCleared(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (_clearedStages.Add(sceneName))
            {
                SaveToPrefs();
                OnStageCleared?.Invoke(sceneName);
            }
        }

        /// <summary>Returns true if the stage has been cleared at least once.</summary>
        public bool IsCleared(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && _clearedStages.Contains(sceneName);
        }

        /// <summary>
        /// Returns true if the stage at <paramref name="index"/> in the supplied
        /// list should be unlocked. The first stage is always unlocked. Any
        /// other stage is unlocked when the previous stage is cleared.
        /// </summary>
        public bool IsUnlockedByIndex(int index, IList<string> sceneNamesInOrder)
        {
            if (index <= 0) return true;
            if (sceneNamesInOrder == null || index >= sceneNamesInOrder.Count) return true;
            return IsCleared(sceneNamesInOrder[index - 1]);
        }

        /// <summary>Counts how many stages in the supplied list are cleared.</summary>
        public int CountCleared(IList<string> sceneNames)
        {
            if (sceneNames == null) return 0;
            int count = 0;
            for (int i = 0; i < sceneNames.Count; i++)
            {
                if (IsCleared(sceneNames[i])) count++;
            }
            return count;
        }

        /// <summary>Removes all progress (handy for a "Reset Progress" button).</summary>
        public void ResetProgress()
        {
            _clearedStages.Clear();
            SaveToPrefs();
        }

        /// <summary>
        /// Removes any cleared scene names that are not in the supplied list.
        /// Useful when the level list has been edited and old entries should
        /// not influence the "X / Y CLEARED" counter anymore.
        /// </summary>
        public void PruneUnknownStages(IList<string> knownSceneNames)
        {
            if (knownSceneNames == null) return;
            var known = new HashSet<string>(knownSceneNames);
            int removed = 0;
            _clearedStages.RemoveWhere(name =>
            {
                if (known.Contains(name)) return false;
                removed++;
                return true;
            });
            if (removed > 0) SaveToPrefs();
        }

        private void LoadFromPrefs()
        {
            _clearedStages.Clear();
            string raw = PlayerPrefs.GetString(ClearedPrefKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;
            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i])) _clearedStages.Add(parts[i]);
            }
        }

        private void SaveToPrefs()
        {
            PlayerPrefs.SetString(ClearedPrefKey, string.Join("|", _clearedStages));
            PlayerPrefs.Save();
        }
    }
}
