using UnityEngine;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// เก็บระดับความยากที่ผู้เล่นเลือกไว้ ข้ามฉากได้โดยไม่ต้องมี GameObject (Static Class ธรรมดา ไม่ใช่ MonoBehaviour)
    /// ให้หน้า Level Select เรียก GameDifficulty.Current = ... ตอนเลือกความยาก ก่อนโหลดฉากเกมจริง
    ///
    /// ระบบ AI (EnemyBrain.cs / EnemyAI.cs) ยังไม่ได้ผูกกับตัวนี้อัตโนมัติ — ถ้าอยากให้ความยากมีผลกับ AI จริง
    /// (เช่น ตัวคูณความเร็ว/ความดุ) ต้องเพิ่มโค้ดเล็กน้อยในฝั่ง Enemy ให้อ่านค่าจากตัวนี้ไปคูณเอง (บอกได้เลยถ้าต้องการ)
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

        /// <summary>ตัวคูณความดุ/ความเร็วของศัตรู แนะนำให้ EnemyBrain/EnemyAI คูณค่านี้เข้ากับ maxSpeed หรือ Aggression ของตัวเอง</summary>
        public static float GetEnemyIntensityMultiplier()
        {
            switch (Current)
            {
                case Level.Easy: return 0.7f;
                case Level.Hard: return 1.3f;
                default: return 1f;
            }
        }

        public static string GetDisplayName()
        {
            switch (Current)
            {
                case Level.Easy: return "ง่าย";
                case Level.Hard: return "ยาก";
                default: return "ปกติ";
            }
        }
    }
}
