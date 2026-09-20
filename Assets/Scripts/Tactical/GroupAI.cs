using System.Collections.Generic;
using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Player;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// ตัวประสานงานกลุ่ม AI แบบง่าย: เก็บรายชื่อศัตรูที่กำลัง Active
    /// เพื่อใช้แจก Index ให้ Flanking, นับจำนวนพันธมิตรใกล้เคียง และ "เรียกพวก" เมื่อโดนโจมตี
    /// วาง Component นี้ไว้ที่ GameObject กลาง (Manager) เพียงตัวเดียวในฉาก
    /// </summary>
    public class GroupAI : MonoBehaviour
    {
        public static GroupAI Instance { get; private set; }

        private readonly List<Transform> _activeEnemies = new List<Transform>();

        private void Awake()
        {
            // Singleton ปกติ: ถ้ามีตัวอื่นอยู่แล้วให้ทำลายตัวนี้ทิ้ง
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // เคลียร์รายชื่อรอบก่อน (ถ้ามี) ให้ตรงกับฉากปัจจุบัน
            _activeEnemies.Clear();
        }

        private void OnDestroy()
        {
            // สำคัญ: ต้องเคลียร์ static ตอนถูกทำลาย (ตอนออกจาก Play Mode / ปิดฉาก)
            // ไม่งั้นรอบถัดไป GroupAI.Awake จะเห็น Instance ค้างจากรอบก่อน แล้วคิดว่า
            // "มีตัวอื่นอยู่แล้ว" → ทำลายตัวเองทิ้ง → ไม่มีใครลงทะเบียน → ระบบฮีล/ทีมมองไม่เห็นใครเลย
            // (บั๊กที่เจอจริง: Tank กับ Sup ไม่ถูกลงทะเบียน เหลือแค่ Flanker)
            if (Instance == this) Instance = null;
        }

        public void Register(Transform enemy)
        {
            if (!_activeEnemies.Contains(enemy))
                _activeEnemies.Add(enemy);
        }

        public void Unregister(Transform enemy)
        {
            _activeEnemies.Remove(enemy);
        }

        public int GetIndexOf(Transform enemy) => _activeEnemies.IndexOf(enemy);

        public int TotalActive => _activeEnemies.Count;

        /// <summary>
        /// รายชื่อเพื่อนร่วมทีมทั้งหมดที่ยัง Active อยู่ (§20 — ทีมบัส)
        /// ใช้โดย RetreatState (หา healer), HealAllyState (เลือกเป้าฮีล) ฯลฯ
        /// คืนเป็น read-only เพื่อกันภายนอกแก้รายการภายใน
        /// </summary>
        public IReadOnlyList<Transform> ActiveEnemies => _activeEnemies;

        /// <summary>
        /// หา Healer ที่ยังมีชีวิตอยู่ และใกล้ที่สุดจากจุด origin
        /// ใช้โดยตัวที่เลือดน้อยตอนถอยกลับไปหา healer (§18)
        /// </summary>
        /// <param name="origin">ตำแหน่งตัวเอง</param>
        /// <param name="maxRange">ระยะค้นหาสูงสุด (ใส่ float.MaxValue เพื่อค้นทั้งแมพ)</param>
        /// <returns>Transform ของ healer ที่ใกล้ที่สุด หรือ null ถ้าไม่มี healer ที่ยังมีชีวิต</returns>
        public Transform FindNearestHealer(Vector2 origin, float maxRange)
        {
            Transform best = null;
            float bestDistance = maxRange;

            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;
                if (!enemy.TryGetComponent(out EnemyBrain brain)) continue;
                if (brain.Role != EnemyRole.Support) continue;   // เฉพาะบทบาท healer

                if (!enemy.TryGetComponent(out Health health)) continue;
                if (health.IsDead || health.CurrentHP <= 0f) continue; // healer ตายแล้วฮีลไม่ได้

                float distance = Vector2.Distance(origin, enemy.position);
                if (distance > bestDistance) continue;

                bestDistance = distance;
                best = enemy;
            }

            return best;
        }

        public int CountNearby(Vector2 origin, float radius, Transform exclude)
        {
            int count = 0;
            foreach (var enemy in _activeEnemies)
            {
                if (enemy == exclude || enemy == null) continue;
                if (Vector2.Distance(origin, enemy.position) <= radius)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// "ตะโกน" เรียกพวกที่อยู่ในระยะได้ยิน (เช่นตอนโดนโจมตี) ให้รู้ตำแหน่งภัยคุกคามทันที
        /// โดยไม่ต้องเห็นผู้เล่นด้วยตาตัวเอง — เพื่อนที่อยู่ไกลเกินระยะ shoutRadius จะไม่ได้ยินและไม่รู้เรื่อง
        /// </summary>
        /// <param name="origin">ตำแหน่งที่เกิดเหตุ (ตัวที่โดนโจมตี)</param>
        /// <param name="shoutRadius">ระยะที่เสียง/สัญญาณเตือนไปถึง</param>
        /// <param name="threatPosition">ตำแหน่งที่คาดว่าภัยคุกคาม (ผู้เล่น) อยู่ ให้เพื่อนไปตรวจสอบ</param>
        /// <param name="source">ตัวที่ส่งสัญญาณ (ไม่ต้องแจ้งเตือนตัวเอง)</param>
        /// <param name="threatDirection">
        /// ทิศทางที่ผู้เล่นกำลังเคลื่อนที่ ณ ตอนที่แจ้งเตือน (จาก TargetMemory ของตัวที่เห็น)
        /// ส่งต่อให้เพื่อนใช้เดาทิศทางค้นหาต่อได้เลย แทนที่จะได้แค่จุดเดียวโดยไม่รู้ทิศทาง
        /// </param>
        public void BroadcastAlert(Vector2 origin, float shoutRadius, Vector2 threatPosition, Transform source, Vector2 threatDirection = default)
        {
            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null || enemy == source) continue;
                if (Vector2.Distance(origin, enemy.position) > shoutRadius) continue; // ไกลเกินไป ไม่ได้ยิน

                if (enemy.TryGetComponent(out EnemyBrain brain))
                {
                    brain.ReceiveAlert(threatPosition, threatDirection);
                }
            }
        }
        /// <summary>
        /// หาลำดับ (index) ของตัวนี้ในกลุ่ม "ที่กำลังไล่ล่าผู้เล่นอยู่จริงๆ" (Chase หรือ Combat เท่านั้น)
        /// ใช้กำหนดว่าใครควรไล่ตรง ใครควรอ้อมไปดักอีกทาง (ดู Flanking.cs)
        /// index 0 = ตัวหลักที่ไล่ตรง, index อื่นๆ = ให้อ้อมไปทางอื่น
        /// </summary>
        public int GetActiveChaseIndex(Transform self, out int totalActiveChasers)
        {
            int index = -1;
            int count = 0;

            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;
                if (!enemy.TryGetComponent(out EnemyBrain brain)) continue;

                EnemyState state = brain.GetCurrentState();
                if (state != EnemyState.Chase && state != EnemyState.Combat) continue;

                if (enemy == self) index = count;
                count++;
            }

            totalActiveChasers = count;
            return index;
        }

        /// <summary>
        /// คำนวณแรงผลักออกจากพันธมิตรตัวอื่นที่อยู่ใกล้เกินไป (ระยะ personalSpace)
        /// ใช้ผสมกับทิศทางที่ตั้งใจจะเดิน กันไม่ให้ศัตรูหลายตัวเดินซ้อนทับ/ชิดกันเกินไปตอนเข้าหาผู้เล่นพร้อมกัน
        /// </summary>
        public Vector2 ComputeSeparationPush(Vector2 origin, Transform exclude, float personalSpace)
        {
            Vector2 push = Vector2.zero;

            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null || enemy == exclude) continue;

                Vector2 offset = origin - (Vector2)enemy.position;
                float distance = offset.magnitude;

                if (distance < personalSpace && distance > 0.001f)
                {
                    float closeness = 1f - (distance / personalSpace); // ยิ่งใกล้ยิ่งผลักแรง
                    push += offset.normalized * closeness;
                }
            }

            return push;
        }

        /// <summary>
        /// ระบบ "ได้ยินเสียง" (Sound Investigation): ผู้เล่นทำเสียง (เดิน/วิ่ง/ยิงปืน) ที่จุดหนึ่ง
        /// ศัตรูทุกตัวที่อยู่ในระยะได้ยิน (ไม่ว่าจะกำลัง Patrol อยู่เฉยๆ หรือทำอะไรอยู่ก็ตาม) จะไปตรวจสอบจุดนั้น
        /// ต่างจาก BroadcastAlert ตรงที่นี่มาจากการกระทำของ "ผู้เล่นเอง" ไม่ใช่จากเพื่อนศัตรูด้วยกัน
        /// และศัตรูที่ได้ยินไม่รู้ทิศทางที่แน่ชัด (แค่รู้ตำแหน่งที่มาของเสียง) ต่างจากการเห็นตัวจริง
        /// </summary>
        /// <param name="noisePosition">ตำแหน่งที่เกิดเสียง</param>
        /// <param name="noiseRadius">ระยะที่เสียงไปถึง (ยิ่งดัง เช่นเสียงปืน ยิ่งไปไกล)</param>
        public void EmitNoise(Vector2 noisePosition, float noiseRadius)
        {
            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;
                if (Vector2.Distance(noisePosition, enemy.position) > noiseRadius) continue; // ไกลเกินไป ไม่ได้ยิน

                if (enemy.TryGetComponent(out EnemyBrain brain))
                {
                    brain.ReceiveNoise(noisePosition);
                }
            }
        }
    }
}
