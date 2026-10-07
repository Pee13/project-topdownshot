using UnityEditor;
using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Player;

/// <summary>
/// เครื่องมืออัปเดตค่า AI ในฉากปัจจุบันให้ตรงกับค่าแนะนำใหม่
/// (เมนู: Tools > AI > Apply Recommended Values (Open Scene))
///
/// ทำไมต้องมี: ค่า default ในโค้ด (MemoryDuration 30, AlertShoutRadius 60, HealRange 40, ...)
/// "ไม่ทับ" ค่าที่ serialize ไว้ใน scene แล้ว — ศัตรูที่วางไว้ในฉากจะยังคาค่าเดิม (20/8/25/3)
/// เครื่องมือนี้จะอัปเดตเฉพาะค่าที่ "ยังเท่ากับค่า default เก่าเป๊ะ" (ไม่เคยถูกปรับเอง)
/// ถ้าค่าไหนเคยปรับด้วยมือแล้ว จะไม่แตะ
///
/// วิธีใช้: เปิดฉากเป้าหมาย → เมนูนี้ → ยืนยัน → Save Scene
/// </summary>
public static class AIValuesPatcher
{
    // ค่า default เก่าที่ scene อาจยังคาอยู่ (ตรงเป๊ะ = ยังไม่เคยปรับเอง = ปลอดภัยที่จะอัปเดต)
    private const float OldMemoryDuration = 20f;
    private const float OldAlertShoutRadius = 8f;
    private const float OldHealRange = 25f;
    private const float OldSearchPursuitOvershoot = 3f;
    private const float OldGunshotNoiseRadius = 14f;
    private const float OldHealWorthThreshold = 0.80f;

    // ค่าแนะนำใหม่
    private const float NewMemoryDuration = 30f;
    private const float NewAlertShoutRadius = 60f;
    private const float NewHealRange = 40f;
    private const float NewSearchPursuitOvershoot = 5f;
    private const float NewGunshotNoiseRadius = 60f;
    private const float NewHealWorthThreshold = 0.99f; // ฮีลใครก็ได้ที่ยังไม่เต็ม 100% (จัดลำดับด้วย HP% ต่ำสุดก่อน)

    [MenuItem("Tools/AI/Apply Recommended Values (Open Scene)")]
    public static void Apply()
    {
        if (!EditorUtility.DisplayDialog(
                "Apply AI Recommended Values",
                "จะอัปเดตเฉพาะค่าที่ยังเท่ากับค่า default เก่าเป๊ะเท่านั้น\n" +
                "(ค่าที่เคยปรับเองด้วยมือจะไม่ถูกแตะ)\n\n" +
                $"EnemyBrain: MemoryDuration {OldMemoryDuration}→{NewMemoryDuration}, " +
                $"AlertShoutRadius {OldAlertShoutRadius}→{NewAlertShoutRadius}, " +
                $"HealRange {OldHealRange}→{NewHealRange}, " +
                $"SearchPursuitOvershoot {OldSearchPursuitOvershoot}→{NewSearchPursuitOvershoot}\n" +
                $"PlayerShoot: GunshotNoiseRadius {OldGunshotNoiseRadius}→{NewGunshotNoiseRadius}\n\n" +
                "บันทึกฉากหลังทำเองด้วย (Ctrl+S)",
                "Apply", "Cancel"))
            return;

        int brainsPatched = 0;
        foreach (var brain in Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool changed = false;

            if (Mathf.Approximately(brain.MemoryDuration, OldMemoryDuration))
            { brain.MemoryDuration = NewMemoryDuration; changed = true; }

            if (Mathf.Approximately(brain.AlertShoutRadius, OldAlertShoutRadius))
            { brain.AlertShoutRadius = NewAlertShoutRadius; changed = true; }

            if (Mathf.Approximately(brain.HealRange, OldHealRange))
            { brain.HealRange = NewHealRange; changed = true; }

            if (Mathf.Approximately(brain.SearchPursuitOvershoot, OldSearchPursuitOvershoot))
            { brain.SearchPursuitOvershoot = NewSearchPursuitOvershoot; changed = true; }

            if (Mathf.Approximately(brain.HealWorthThreshold, OldHealWorthThreshold))
            { brain.HealWorthThreshold = NewHealWorthThreshold; changed = true; }

            if (changed)
            {
                EditorUtility.SetDirty(brain);
                brainsPatched++;
            }
        }

        int shootersPatched = 0;
        foreach (var shoot in Object.FindObjectsByType<PlayerShoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Mathf.Approximately(shoot.GunshotNoiseRadius, OldGunshotNoiseRadius))
            {
                shoot.GunshotNoiseRadius = NewGunshotNoiseRadius;
                EditorUtility.SetDirty(shoot);
                shootersPatched++;
            }
        }

        if (brainsPatched > 0 || shootersPatched > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[AIValuesPatcher] อัปเดตแล้ว: EnemyBrain {brainsPatched} ตัว, PlayerShoot {shootersPatched} ตัว " +
                  $"(ค่าที่เคยปรับเองถูกข้าม) — กด Ctrl+S เพื่อบันทึกฉาก");
    }
}
