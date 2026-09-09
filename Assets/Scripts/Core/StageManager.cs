using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TopDownTacticalAI.Player;

namespace TopDownTacticalAI.Core
{
    /// <summary>
    /// Lives in each stage scene. Watches the alive enemies and, once they
    /// are all gone, marks the current stage as cleared via
    /// <see cref="LevelProgressManager"/>. Also exposes a public
    /// <see cref="MarkCleared"/> hook so designers can wire other victory
    /// conditions (e.g. reach a goal point) in the Inspector.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class StageManager : MonoBehaviour
    {
        [Header("── Victory Detection ──")]
        [Tooltip("Tag used to count enemies. When zero enemies with this tag are alive, the stage is considered cleared.")]
        public string enemyTag = "Enemy";
        [Tooltip("If true, the stage is auto-cleared when no enemies remain. If false, only the manual MarkCleared() call will count.")]
        public bool autoDetectOnAllEnemiesDead = true;
        [Tooltip("Optional delay (seconds) after victory before marking the stage as cleared (lets death FX play).")]
        public float clearDelay = 1.5f;
        [Tooltip("If true, returns to LevelSelect after the stage is marked cleared.")]
        public bool returnToLevelSelectOnClear = false;
        [Tooltip("Scene name to load after clearing (only used if returnToLevelSelectOnClear is true).")]
        public string levelSelectSceneName = "LevelSelect";

        private bool _cleared;
        private float _clearTimer;
        private readonly List<GameObject> _trackedEnemies = new List<GameObject>();

        private void Start()
        {
            if (LevelProgressManager.Instance == null)
            {
                var go = new GameObject("LevelProgressManager");
                go.AddComponent<LevelProgressManager>();
            }
            RefreshTrackedEnemies();
        }

        private void Update()
        {
            if (_cleared) return;

            if (autoDetectOnAllEnemiesDead)
            {
                // Drop dead references first.
                for (int i = _trackedEnemies.Count - 1; i >= 0; i--)
                {
                    if (_trackedEnemies[i] == null) _trackedEnemies.RemoveAt(i);
                }
                if (_trackedEnemies.Count == 0)
                {
                    // Try to pick up newly spawned enemies next frame.
                    RefreshTrackedEnemies();
                    if (_trackedEnemies.Count == 0) return;
                    if (AllEnemiesDead()) BeginClearCountdown();
                }
                else if (AllEnemiesDead())
                {
                    BeginClearCountdown();
                }
            }
        }

        private void RefreshTrackedEnemies()
        {
            _trackedEnemies.Clear();
            if (string.IsNullOrEmpty(enemyTag)) return;
            var found = GameObject.FindGameObjectsWithTag(enemyTag);
            for (int i = 0; i < found.Length; i++) _trackedEnemies.Add(found[i]);
        }

        private bool AllEnemiesDead()
        {
            for (int i = 0; i < _trackedEnemies.Count; i++)
            {
                if (_trackedEnemies[i] == null) continue;
                var hp = _trackedEnemies[i].GetComponent<Player.Health>();
                if (hp != null && hp.CurrentHP > 0f) return false;
                // If no Health component, treat as alive.
                if (hp == null) return false;
            }
            return _trackedEnemies.Count > 0;
        }

        private void BeginClearCountdown()
        {
            if (_cleared) return;
            _cleared = true;
            _clearTimer = clearDelay;
        }

        private void LateUpdate()
        {
            if (!_cleared) return;
            if (clearDelay <= 0f)
            {
                MarkCleared();
                return;
            }
            _clearTimer -= Time.deltaTime;
            if (_clearTimer <= 0f) MarkCleared();
        }

        /// <summary>
        /// Public hook for designers to call from the Inspector (e.g. from a
        /// UnityEvent on a goal trigger). Safe to call multiple times.
        /// </summary>
        public void MarkCleared()
        {
            if (_cleared && LevelProgressManager.Instance != null
                && LevelProgressManager.Instance.IsCleared(SceneManager.GetActiveScene().name))
            {
                // Already saved - skip the duplicate work below.
                _cleared = true;
                if (returnToLevelSelectOnClear) GoToLevelSelect();
                return;
            }
            _cleared = true;

            var mgr = LevelProgressManager.Instance;
            if (mgr == null)
            {
                var go = new GameObject("LevelProgressManager");
                mgr = go.AddComponent<LevelProgressManager>();
            }
            string scene = SceneManager.GetActiveScene().name;
            mgr.MarkCleared(scene);

            if (returnToLevelSelectOnClear) GoToLevelSelect();
        }

        private void GoToLevelSelect()
        {
            if (string.IsNullOrEmpty(levelSelectSceneName)) return;
            SceneManager.LoadScene(levelSelectSceneName);
        }
    }
}
