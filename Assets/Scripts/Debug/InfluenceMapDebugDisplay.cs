using System.Collections.Generic;
using UnityEngine;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Vision;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// วาดตาราง Influence Map ทับฉากเกมแบบเรียลไทม์ (ตรงกับบทที่ 3.4.2 และรูปที่ 3.4.2 ในเอกสาร)
    /// สีแดง = อันตราย (Danger Score สูง), สีเขียว = ปลอดภัย (Safety Score สูง)
    /// จุดสีน้ำเงิน = ช่องเป้าหมายที่คะแนนดีที่สุด (Best Target Cell)
    ///
    /// วิธีใช้: แปะสคริปต์นี้ไว้ที่ตัวศัตรูตัวที่อยากดู (ต้องมี EnemyBrain อยู่ด้วย) แล้วกด Play
    /// เปิด/ปิดได้ด้วย Show Heatmap ใน Inspector — แนะนำเปิดดูทีละตัวตอน Debug เท่านั้น
    /// เพราะการวาดกริดจำนวนมากพร้อมกันหลายตัวจะกิน Performance ของ Editor
    /// </summary>
    public class InfluenceMapDebugDisplay : MonoBehaviour
    {
        [Header("Toggle")]
        public bool ShowHeatmap = true;

        [Header("ค่าที่ใช้คำนวณ (ควรตรงกับค่าที่ EnemyBrain ใช้จริง)")]
        public float SearchRadius = 6f;
        public float CellSize = 1f;
        public LayerMask ObstacleMask;
        public LayerMask BulletMask;
        public float DangerDetectRadius = 8f;

        [Header("สี")]
        public Color DangerColor = new Color(1f, 0f, 0f, 0.5f);
        public Color SafetyColor = new Color(0f, 1f, 0f, 0.35f);
        public Color NeutralColor = new Color(1f, 1f, 1f, 0.05f);
        public Color BestTargetColor = new Color(0.2f, 0.4f, 1f, 0.9f);

        private Transform _player;

        private void OnDrawGizmos()
        {
            if (!ShowHeatmap || !Application.isPlaying) return;
            if (_player == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj == null) return;
                _player = playerObj.transform;
            }

            // เก็บตำแหน่งกระสุนที่อยู่ในระยะเป็นแหล่งอันตราย (Danger Source) เหมือนที่ EnemyBrain ใช้จริง
            var dangerSources = new List<InfluenceMap.DangerSource>();
            Collider2D[] bullets = Physics2D.OverlapCircleAll(transform.position, DangerDetectRadius, BulletMask);
            foreach (var b in bullets)
            {
                dangerSources.Add(new InfluenceMap.DangerSource(b.transform.position, 3f, 100f));
            }

            Vector2 origin = transform.position;
            int steps = Mathf.Max(1, Mathf.RoundToInt(SearchRadius / CellSize));

            Vector2 bestCell = Vector2.zero;
            float bestScore = float.MinValue;

            for (int gx = -steps; gx <= steps; gx++)
            {
                for (int gy = -steps; gy <= steps; gy++)
                {
                    Vector2 cell = origin + new Vector2(gx * CellSize, gy * CellSize);
                    if (Vector2.Distance(origin, cell) > SearchRadius) continue;

                    float dangerScore = 0f;
                    foreach (var d in dangerSources)
                    {
                        float dist = Vector2.Distance(cell, d.Position);
                        if (dist < d.Radius)
                            dangerScore += d.Intensity * (1f - dist / d.Radius);
                    }

                    bool blocksLoS = _player != null && !RaycastDetector.HasLineOfSight(cell, _player.position, ObstacleMask, Vector2.Distance(cell, _player.position) + 1f);
                    float safetyScore = blocksLoS ? 50f : 0f;
                    float total = safetyScore - dangerScore;

                    Color cellColor;
                    if (dangerScore > 10f)
                        cellColor = Color.Lerp(NeutralColor, DangerColor, Mathf.Clamp01(dangerScore / 100f));
                    else if (safetyScore > 0f)
                        cellColor = SafetyColor;
                    else
                        cellColor = NeutralColor;

                    Gizmos.color = cellColor;
                    Gizmos.DrawCube(cell, Vector3.one * (CellSize * 0.9f));

                    if (total > bestScore)
                    {
                        bestScore = total;
                        bestCell = cell;
                    }
                }
            }

            // ไฮไลต์ช่องเป้าหมายที่ดีที่สุดด้วยสีน้ำเงิน ตรงกับ "Best Target Cell" ในเอกสาร
            Gizmos.color = BestTargetColor;
            Gizmos.DrawSphere(bestCell, CellSize * 0.3f);
        }
    }
}
