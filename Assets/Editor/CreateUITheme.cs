using UnityEditor;
using UnityEngine;
using TopDownTacticalAI.UI.Themes;

namespace TopDownTacticalAI.Editor
{
    /// <summary>
    /// เครื่องมือช่วยสร้างไฟล์ UITheme พร้อมค่าสีเริ่มต้นที่ดูดีในตัว ผ่านเมนู Unity โดยตรง
    /// ไม่ต้องคลิกขวา > Create > ... เอง ทำให้เร็วขึ้น
    /// </summary>
    public static class CreateUITheme
    {
        [MenuItem("TopDownTacticalAI/Create Default UI Theme")]
        public static void CreateDefaultTheme()
        {
            // ต้องใช้ ScriptableObject.CreateInstance<T>() เสมอ (เขียน CreateInstance เฉยๆ ไม่ได้
            // เพราะเมธอดนี้เป็น Static ของคลาส ScriptableObject ไม่ใช่ของคลาสนี้ - นี่คือจุดที่ Error CS0103)
            UITheme theme = ScriptableObject.CreateInstance<UITheme>();

            // ตั้งชื่อไฟล์ กันชื่อซ้ำถ้ากดสร้างหลายรอบ
            string path = "Assets/MainTheme.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(theme, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // เลือกไฟล์ที่สร้างเสร็จให้อัตโนมัติ เห็นผลทันทีใน Inspector
            Selection.activeObject = theme;
            EditorUtility.FocusProjectWindow();

            Debug.Log($"[CreateUITheme] สร้างไฟล์ธีมเสร็จแล้วที่: {path}");
        }
    }
}
