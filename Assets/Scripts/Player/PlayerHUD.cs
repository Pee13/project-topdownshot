using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using TopDownTacticalAI.Player;

namespace TopDownTacticalAI.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Player References")]
        public Health PlayerHealth;
        public PlayerMana PlayerManaRef;

        [Header("UI Bars")]
        public Slider HealthSlider;
        public Slider ManaSlider;
        public TextMeshProUGUI HealthText;
        public TextMeshProUGUI ManaText;

        [Header("Enemy Tracker")]
        public TextMeshProUGUI EnemyCountText;

        [Header("End Game Panels")]
        public GameObject GameOverPanel;
        public GameObject VictoryPanel;

        private int _lastEnemyCount = -1;
        private float _enemyCheckTimer = 0.2f;
        private bool _playerInitialized = false;
        private bool _isGameOver = false;

        private void Start()
        {
            // ค้นหาผู้เล่นอัตโนมัติ
            if (PlayerHealth == null)
            {
                GameObject player = GameObject.Find("Player 1");
                if (player == null) player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    PlayerHealth = player.GetComponent<Health>();
                    PlayerManaRef = player.GetComponent<PlayerMana>();
                }
            }

            if (PlayerHealth != null)
            {
                _playerInitialized = true;
                PlayerHealth.OnDeath.AddListener(OnPlayerDead);
            }

            if (GameOverPanel != null) GameOverPanel.SetActive(false);
            if (VictoryPanel != null) VictoryPanel.SetActive(false);

            UpdateEnemyCount();
        }

        private void Update()
        {
            // ตรวจสอบการตาย: ถ้าเคยมีผู้เล่นแล้วจู่ๆ หายไป (โดน Destroy) หรือเลือดเหลือ 0
            if (!_isGameOver && _playerInitialized)
            {
                if (PlayerHealth == null || PlayerHealth.CurrentHP <= 0f)
                {
                    OnPlayerDead();
                }
            }

            UpdateBars();

            _enemyCheckTimer -= Time.deltaTime;
            if (_enemyCheckTimer <= 0f)
            {
                UpdateEnemyCount();
                _enemyCheckTimer = 0.2f;
            }
        }

        private void UpdateBars()
        {
            if (PlayerHealth != null && HealthSlider != null)
            {
                HealthSlider.maxValue = PlayerHealth.MaxHP;
                HealthSlider.value = Mathf.Max(0f, PlayerHealth.CurrentHP);
                if (HealthText != null)
                    HealthText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, PlayerHealth.CurrentHP))} / {PlayerHealth.MaxHP}";
            }

            if (PlayerManaRef != null && ManaSlider != null)
            {
                ManaSlider.maxValue = PlayerManaRef.MaxMana;
                ManaSlider.value = PlayerManaRef.CurrentMana;
                if (ManaText != null)
                    ManaText.text = $"{Mathf.CeilToInt(PlayerManaRef.CurrentMana)} / {PlayerManaRef.MaxMana}";
            }
        }

        private void UpdateEnemyCount()
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            int aliveCount = 0;

            foreach (var e in enemies)
            {
                if (e == null) continue;
                Health h = e.GetComponent<Health>();
                if (h != null && !h.IsDead && h.CurrentHP > 0f)
                {
                    aliveCount++;
                }
            }

            if (aliveCount != _lastEnemyCount)
            {
                _lastEnemyCount = aliveCount;
                if (EnemyCountText != null)
                {
                    EnemyCountText.text = $"Enemies: {aliveCount}";
                }

                if (aliveCount == 0 && enemies.Length > 0 && VictoryPanel != null && !VictoryPanel.activeSelf && !_isGameOver)
                {
                    VictoryPanel.SetActive(true);
                }
            }
        }

        private void OnPlayerDead()
        {
            if (_isGameOver) return;
            _isGameOver = true;

            Debug.Log("[PlayerHUD] ตรวจพบผู้เล่นเสียชีวิต กำลังเปิด GameOverPanel!");

            if (HealthSlider != null) HealthSlider.value = 0f; // ปรับหลอดเลือดให้หมดหลอดทันที

            if (GameOverPanel != null)
            {
                GameOverPanel.SetActive(true);
            }
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}