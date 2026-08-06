using UnityEngine;

/// <summary>
/// เก็บสถานะของผู้เล่นที่ระบบอื่น (เช่น EnemyAI) ต้องอ่านไปใช้ตัดสินใจ
/// EnemyAI.cs อ่านค่า currentMana ไปใช้ในสมการ Utility AI: ถ้า Mana ผู้เล่นต่ำ ศัตรูจะยิ่งกล้าเข้าโจมตี (Aggressive)
///
/// วิธีติดตั้ง: แปะสคริปต์นี้ไว้ที่ GameObject ผู้เล่น (ตัวที่ Tag เป็น "Player")
/// </summary>
public class PlayerStatus : MonoBehaviour
{
    [Header("── Mana ──")]
    public float maxMana = 100f;
    public float currentMana = 100f;
    [Tooltip("อัตราฟื้นฟู Mana ต่อวินาทีตอนไม่ได้ใช้")]
    public float manaRegenPerSecond = 8f;

    [Header("── Health ──")]
    public int maxHealth = 100;
    public int currentHealth = 100;

    private void Update()
    {
        if (currentMana < maxMana)
            currentMana = Mathf.Min(maxMana, currentMana + manaRegenPerSecond * Time.deltaTime);
    }

    public bool HasEnoughMana(float amount) => currentMana >= amount;

    public void ConsumeMana(float amount)
    {
        currentMana = Mathf.Max(0f, currentMana - amount);
    }

    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }
}
