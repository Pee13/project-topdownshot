using UnityEngine;
using UnityEngine.SceneManagement;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// ตัวควบคุมหน้าเมนูหลัก (Main Menu) — ปุ่มเล่น / ตั้งค่า / ออกจากเกม
    /// รองรับ 2 รูปแบบ: (1) Settings เป็น Panel ซ้อนอยู่ในฉากเดียวกัน (แนะนำ ง่ายกว่า) หรือ (2) แยกเป็นคนละ Scene
    ///
    /// วิธีติดตั้ง: แปะสคริปต์นี้ที่ GameObject ไหนก็ได้ในฉาก MainMenu (เช่น Canvas)
    /// แล้วลาก Panel/ปุ่มมาผูกใน Inspector ตามคำอธิบายแต่ละช่อง
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("── ชื่อ Scene (ต้องตรงกับที่เพิ่มไว้ใน File > Build Settings ทุกตัวอักษร) ──")]
        public string levelSelectSceneName = "LevelSelect";

        [Header("── Panels ในฉากเดียวกัน (ถ้าทำ Settings เป็น Panel ซ้อน ไม่แยก Scene) ──")]
        public GameObject mainMenuPanel;
        public GameObject settingsPanel;
        public GameObject quitConfirmPanel;
        public GameObject creditsPanel;
        public GameObject controlsPanel;

        private void Start()
        {
            ShowOnly(mainMenuPanel);
        }

        // ── ปุ่มหลัก ──
        public void OnPlayButton()
        {
            SceneManager.LoadScene(levelSelectSceneName);
        }

        public void OnSettingsButton() => ShowOnly(settingsPanel);
        public void OnSettingsBackButton() => ShowOnly(mainMenuPanel);

        public void OnControlsButton() => ShowOnly(controlsPanel);
        public void OnControlsBackButton() => ShowOnly(mainMenuPanel);

        public void OnCreditsButton() => ShowOnly(creditsPanel);
        public void OnCreditsBackButton() => ShowOnly(mainMenuPanel);

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

        /// <summary>โชว์แค่ Panel เดียวที่ระบุ ปิด Panel อื่นทั้งหมด (กันเผลอเปิดซ้อนกันหลายอัน)</summary>
        private void ShowOnly(GameObject panelToShow)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(panelToShow == mainMenuPanel);
            if (settingsPanel != null) settingsPanel.SetActive(panelToShow == settingsPanel);
            if (creditsPanel != null) creditsPanel.SetActive(panelToShow == creditsPanel);
            if (controlsPanel != null) controlsPanel.SetActive(panelToShow == controlsPanel);
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
    }
}
