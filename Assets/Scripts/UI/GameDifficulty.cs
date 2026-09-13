using UnityEngine;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Stores the player's chosen difficulty level across scenes without needing a GameObject (a plain static class, not a MonoBehaviour).
    /// The Level Select page should set GameDifficulty.Current = ... when the player picks a difficulty, before loading the actual gameplay scene.
    ///
    /// Now wired to EnemyBrain - use GetMultipliers() to read all multipliers at once.
    /// </summary>
    public static class GameDifficulty
    {
        public enum Level { Easy, Normal, Hard }

        private const string PrefKey = "GameDifficulty";

        public static Level Current
        {
            get => (Level)PlayerPrefs.GetInt(PrefKey, (int)Level.Normal);
            set { PlayerPrefs.SetInt(PrefKey, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>Return all multipliers for AI behavior.</summary>
        public static DifficultyMultipliers GetMultipliers()
        {
            switch (Current)
            {
                case Level.Easy:
                    return new DifficultyMultipliers
                    {
                        // Movement
                        speedMultiplier = 0.8f,
                        patrolSpeedMultiplier = 0.8f,
                        chaseSpeedMultiplier = 0.75f,
                        searchSpeedMultiplier = 0.8f,

                        // Combat
                        damageMultiplier = 0.6f,
                        fireRateMultiplier = 0.7f,
                        accuracyMultiplier = 0.7f,
                        maxAmmoMultiplier = 1f,
                        reloadSpeedMultiplier = 1.2f,

                        // Vision/Awareness
                        viewRadiusMultiplier = 0.8f,
                        viewAngleMultiplier = 0.9f,
                        suspicionBuildTimeMultiplier = 1.5f, // Slower = harder to alert
                        suspicionDecayTimeMultiplier = 0.7f,  // Faster = forgets faster

                        // Tactical
                        dangerRangeMultiplier = 1.3f,         // Further = more cautious
                        coverSearchRadiusMultiplier = 1.2f,
                        dodgeDetectRadiusMultiplier = 1.3f,
                        dodgeSpeedMultiplier = 1.1f,

                        // Health
                        maxHPMultiplier = 0.8f,

                        // Group AI
                        alertShoutRadiusMultiplier = 0.8f,
                        flankRadiusMultiplier = 1.2f,

                        // Score
                        scoreMultiplier = 0.8f,
                        xpMultiplier = 0.8f
                    };

                case Level.Hard:
                    return new DifficultyMultipliers
                    {
                        // Movement
                        speedMultiplier = 1.2f,
                        patrolSpeedMultiplier = 1.1f,
                        chaseSpeedMultiplier = 1.3f,
                        searchSpeedMultiplier = 1.2f,

                        // Combat
                        damageMultiplier = 1.4f,
                        fireRateMultiplier = 1.3f,
                        accuracyMultiplier = 1.3f,
                        maxAmmoMultiplier = 1f,
                        reloadSpeedMultiplier = 0.8f,

                        // Vision/Awareness
                        viewRadiusMultiplier = 1.3f,
                        viewAngleMultiplier = 1.1f,
                        suspicionBuildTimeMultiplier = 0.7f,  // Faster = alerts faster
                        suspicionDecayTimeMultiplier = 1.5f,  // Slower = remembers longer

                        // Tactical
                        dangerRangeMultiplier = 0.7f,         // Closer = takes more risks
                        coverSearchRadiusMultiplier = 0.8f,
                        dodgeDetectRadiusMultiplier = 0.8f,
                        dodgeSpeedMultiplier = 1.3f,

                        // Health
                        maxHPMultiplier = 1.3f,

                        // Group AI
                        alertShoutRadiusMultiplier = 1.5f,
                        flankRadiusMultiplier = 0.8f,

                        // Score
                        scoreMultiplier = 1.5f,
                        xpMultiplier = 1.3f
                    };

                default: // Normal
                    return new DifficultyMultipliers
                    {
                        speedMultiplier = 1f,
                        patrolSpeedMultiplier = 1f,
                        chaseSpeedMultiplier = 1f,
                        searchSpeedMultiplier = 1f,
                        damageMultiplier = 1f,
                        fireRateMultiplier = 1f,
                        accuracyMultiplier = 1f,
                        maxAmmoMultiplier = 1f,
                        reloadSpeedMultiplier = 1f,
                        viewRadiusMultiplier = 1f,
                        viewAngleMultiplier = 1f,
                        suspicionBuildTimeMultiplier = 1f,
                        suspicionDecayTimeMultiplier = 1f,
                        dangerRangeMultiplier = 1f,
                        coverSearchRadiusMultiplier = 1f,
                        dodgeDetectRadiusMultiplier = 1f,
                        dodgeSpeedMultiplier = 1f,
                        maxHPMultiplier = 1f,
                        alertShoutRadiusMultiplier = 1f,
                        flankRadiusMultiplier = 1f,
                        scoreMultiplier = 1f,
                        xpMultiplier = 1f
                    };
            }
        }

        /// <summary>Returns the legacy "enemy aggression/speed" multiplier (kept for backward compatibility).</summary>
        public static float GetEnemyIntensityMultiplier()
        {
            return GetMultipliers().speedMultiplier;
        }

        public static string GetDisplayName()
        {
            switch (Current)
            {
                case Level.Easy: return "Easy";
                case Level.Hard: return "Hard";
                default: return "Normal";
            }
        }

        public static string GetDisplayNameEnglish()
        {
            switch (Current)
            {
                case Level.Easy: return "Easy";
                case Level.Hard: return "Hard";
                default: return "Normal";
            }
        }
    }

    /// <summary>
    /// Structure containing all difficulty multipliers for AI behavior.
    /// </summary>
    public struct DifficultyMultipliers
    {
        // Movement
        public float speedMultiplier;
        public float patrolSpeedMultiplier;
        public float chaseSpeedMultiplier;
        public float searchSpeedMultiplier;

        // Combat
        public float damageMultiplier;
        public float fireRateMultiplier;
        public float accuracyMultiplier;
        public float maxAmmoMultiplier;
        public float reloadSpeedMultiplier;

        // Vision/Awareness
        public float viewRadiusMultiplier;
        public float viewAngleMultiplier;
        public float suspicionBuildTimeMultiplier;
        public float suspicionDecayTimeMultiplier;

        // Tactical
        public float dangerRangeMultiplier;
        public float coverSearchRadiusMultiplier;
        public float dodgeDetectRadiusMultiplier;
        public float dodgeSpeedMultiplier;

        // Health
        public float maxHPMultiplier;

        // Group AI
        public float alertShoutRadiusMultiplier;
        public float flankRadiusMultiplier;

        // Rewards
        public float scoreMultiplier;
        public float xpMultiplier;
    }
}