using UnityEngine;
using UnityEngine.SceneManagement;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// เมนูหยุดเกมชั่วคราว (Pause Menu) — กด Esc ระหว่างเล่นเพื่อเปิด/ปิด (Option เสริมที่แนะนำเพิ่ม)
    /// เกมยิงแบบนี้เล่นไปนานๆ ควรมีทางหยุดพัก/ปรับเสียง/ออกกลางเกมได้โดยไม่ต้อง Alt+F4
    ///
    /// วิธีติดตั้ง: แปะสคริปต์นี้ที่ Canvas ของฉากเกมจริง (ฉากที่มี Player/Enemy)
    /// สร้าง Panel เมนู Pause ไว้ (เริ่มต้นซ่อนไว้) มีปุ่ม: เล่นต่อ / ตั้งค่า / เล่นด่านนี้ใหม่ / กลับเมนูหลัก / ออกจากเกม
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("── Panels ──")]
        public GameObject pausePanel;
        public GameObject settingsPanel;
        public GameObject quitConfirmPanel;

        [Header("── ชื่อ Scene เมนูหลัก ──")]
        public string mainMenuSceneName = "MainMenu";

        public bool IsPaused { get; private set; }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsPaused) Resume();
                else Pause();
            }
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f; // หยุดเกม (ตัว AI/Physics ทุกอย่างจะหยุดตามไปด้วยเพราะอิง Time.deltaTime)
            if (pausePanel != null) pausePanel.SetActive(true);
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
        }

        public void OpenSettingsFromPause()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        public void CloseSettingsBackToPause()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(true);
        }

        /// <summary>เล่นด่านปัจจุบันใหม่ตั้งแต่ต้น (โหลด Scene เดิมซ้ำ)</summary>
        public void RestartLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void OnQuitButton()
        {
            if (quitConfirmPanel != null) { quitConfirmPanel.SetActive(true); return; }
            QuitGame();
        }

        public void OnQuitConfirmYes() => QuitGame();
        public void OnQuitConfirmNo()
        {
            if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            // กันเผลอค้าง Time.timeScale = 0 ไว้ถ้า Scene ถูกเปลี่ยนไปตอนกำลัง Pause อยู่
            Time.timeScale = 1f;
        }
    }
}
