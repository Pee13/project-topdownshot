using UnityEngine;
using UnityEngine.SceneManagement;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// ตัวควบคุมหน้าเลือกด่าน (Level Select) — แต่ละปุ่มด่านผูกฟังก์ชัน LoadLevel(sceneName) ผ่าน OnClick() ใน Inspector โดยตรง
    /// ไม่ได้สร้างปุ่มแบบไดนามิก เพื่อให้ตั้งค่าใน Editor ง่าย ไม่ต้องทำ Prefab ปุ่มแยก
    ///
    /// ระดับความยาก (Easy/Normal/Hard) เลือกได้ในหน้านี้ด้วย (Option เสริมที่แนะนำเพิ่ม) — เลือกไว้ก่อนกดเข้าเล่นด่านไหนก็ได้
    ///
    /// วิธีติดตั้ง: แปะสคริปต์นี้ที่ Canvas ของฉาก LevelSelect
    /// สร้างปุ่มด่านตามจำนวนที่ต้องการ (ตอนนี้มีด่านเดียวก็ได้ เพิ่มทีหลังได้เรื่อยๆ)
    /// แต่ละปุ่ม -> OnClick() -> ลาก GameObject นี้มา -> เลือกฟังก์ชัน LoadLevel(string) -> พิมพ์ชื่อ Scene ของด่านนั้น
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        [Header("── ชื่อ Scene เมนูหลัก (ปุ่มย้อนกลับ) ──")]
        public string mainMenuSceneName = "MainMenu";

        [Header("── ปุ่มความยาก (ไม่บังคับ — ลากปุ่ม/Toggle มาผูก OnClick กับฟังก์ชัน SetDifficulty ด้านล่าง) ──")]
        public UnityEngine.UI.Text difficultyLabel; // ถ้าใช้ TMP ให้เปลี่ยนเป็น TMPro.TMP_Text แทน

        private void Start()
        {
            RefreshDifficultyLabel();
        }

        /// <summary>เรียกจากปุ่มแต่ละด่าน ผูก sceneName ให้ตรงกับชื่อ Scene จริงที่เพิ่มไว้ใน Build Settings</summary>
        public void LoadLevel(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        public void BackToMainMenu()
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }

        // ── ระดับความยาก (Option เสริม) ──
        public void SetDifficultyEasy() => SetDifficulty(GameDifficulty.Level.Easy);
        public void SetDifficultyNormal() => SetDifficulty(GameDifficulty.Level.Normal);
        public void SetDifficultyHard() => SetDifficulty(GameDifficulty.Level.Hard);

        private void SetDifficulty(GameDifficulty.Level level)
        {
            GameDifficulty.Current = level;
            RefreshDifficultyLabel();
        }

        private void RefreshDifficultyLabel()
        {
            if (difficultyLabel != null)
                difficultyLabel.text = $"ระดับความยาก: {GameDifficulty.GetDisplayName()}";
        }
    }
}
