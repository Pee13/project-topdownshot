using UnityEngine;
using UnityEngine.Events;

namespace TopDownTacticalAI.Player
{
    /// <summary>
    /// ระบบ Mana/พลังงานของผู้เล่น ตามสเปคในเอกสารบทที่ 3.3.1.1 และ 3.4.1.1
    /// ผู้เล่นใช้ Mana ในการยิง (แทน/เสริมระบบกระสุนเดิม) และค่านี้เป็นตัวแปรสำคัญ (Input Parameter)
    /// ที่ AI ฝั่งศัตรูใช้คำนวณผ่านทฤษฎีอรรถประโยชน์ (Utility Theory) เพื่อตัดสินใจว่าจะ
    /// "ระมัดระวัง" (Mana ผู้เล่นสูง → เน้นความปลอดภัย) หรือ "รุกราน" (Mana ผู้เล่นต่ำ → เน้นโอกาสโจมตี)
    /// </summary>
    public class PlayerMana : MonoBehaviour
    {
        [Header("ค่า Mana")]
        public float MaxMana = 100f;
        public float CurrentMana { get; private set; }

        [Header("อัตราใช้/ฟื้นฟู")]
        [Tooltip("Mana ที่ใช้ต่อการยิง 1 นัด")]
        public float ManaCostPerShot = 8f;
        [Tooltip("อัตราการฟื้นฟู Mana ต่อวินาทีตอนไม่ได้ยิง")]
        public float ManaRegenPerSecond = 12f;
        [Tooltip("หน่วงเวลา (วินาที) หลังยิงนัดล่าสุด ก่อนที่ Mana จะเริ่มฟื้นฟูอีกครั้ง")]
        public float RegenDelay = 0.6f;

        [Header("เกณฑ์ Mana ต่ำ (ใช้โดย AI ฝั่งศัตรูตัดสินใจ)")]
        [Range(0f, 1f)]
        [Tooltip("ถ้า Mana เหลือต่ำกว่าสัดส่วนนี้ ถือว่า 'Mana ต่ำ' — AI จะรู้และเข้าสู่โหมดรุกรานมากขึ้น")]
        public float LowManaThreshold = 0.25f;

        /// <summary>true ถ้า Mana ปัจจุบันต่ำกว่าเกณฑ์ที่กำหนด — ใช้เป็น Input ให้ระบบ Utility AI ฝั่งศัตรู</summary>
        public bool IsLow => CurrentMana <= MaxMana * LowManaThreshold;

        /// <summary>สัดส่วน Mana ปัจจุบัน (0-1) สำหรับแสดงผลแถบ Mana บน HUD</summary>
        public float Fraction01 => MaxMana <= 0f ? 0f : Mathf.Clamp01(CurrentMana / MaxMana);

        public UnityEvent<float> OnManaChanged;

        private float _timeSinceLastShot;

        private void Awake()
        {
            CurrentMana = MaxMana;
            _timeSinceLastShot = RegenDelay;
        }

        private void Update()
        {
            _timeSinceLastShot += Time.deltaTime;

            if (_timeSinceLastShot >= RegenDelay && CurrentMana < MaxMana)
            {
                CurrentMana = Mathf.Min(MaxMana, CurrentMana + ManaRegenPerSecond * Time.deltaTime);
                OnManaChanged?.Invoke(CurrentMana);
            }
        }

        /// <summary>เช็คว่ามี Mana พอสำหรับยิงไหม โดยไม่หัก (ใช้เช็คก่อนอนุญาตให้ยิง)</summary>
        public bool HasEnoughMana() => CurrentMana >= ManaCostPerShot;

        /// <summary>หัก Mana สำหรับการยิง 1 นัด เรียกทุกครั้งที่ยิงสำเร็จ</summary>
        public void ConsumeForShot()
        {
            CurrentMana = Mathf.Max(0f, CurrentMana - ManaCostPerShot);
            _timeSinceLastShot = 0f;
            OnManaChanged?.Invoke(CurrentMana);
        }
    }
}
