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

        // ── สัญญาณขอความช่วยเหลือเชิงรุก (Call-for-Help) ──
        // ฝ่ายที่ "ถูกคุกคาม" (Healer โดนผู้เล่นเข้าใกล้/มีกระสุน, เพื่อนกำลังหนี) ประกาศเอง
        // แทนที่ Tank จะต้อง "เดา" จากสายตาตัวเอง (แบบเดิม: จำผู้เล่นไม่ได้ = ไม่รู้เรื่องเลย)
        // อ้างอิงมาตรฐานเกม top-down: ศัตรูแบ่งปันภัยคุกคามแบบ structured ไม่ใช่แค่ alert ตำแหน่ง (Killzone squad / AI controller pattern)
        private Transform _distressSource;
        private Vector2 _distressPosition;
        private float _distressTime = float.NegativeInfinity;

        // ตำแหน่งผู้เล่น "ล่าสุดที่ทีมรับรู้" — ใช้เรียงลำดับ chaser เป็นมุมรอบผู้เล่น (ไม่ใช่ลำดับ register)
        private Vector2 _playerHint;
        private bool _playerHintValid;

        // buffer กลางของ GetActiveChaseIndex (ไม่ allocate ทุกเฟรม — single-threaded)
        private static readonly List<Transform> s_chaserBuffer = new List<Transform>(8);
        private static readonly List<float> s_chaserAngleBuffer = new List<float>(8);

        // buffer กลางของ GetSearchSectorIndex — แยกจาก chaser buffer เพราะถูกเรียกคนละที่/คนละเฟรม
        private static readonly List<Transform> s_searchBuffer = new List<Transform>(8);

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
        /// หาลำดับ (index) ของตัวนี้ในกลุ่ม "ที่กำลังรบ/ไล่ล่าอยู่จริงๆ"
        /// (Chase / Combat / Flank — Flanker ต้องนับด้วย ไม่งั้นหลุดจากวงล้อม)
        ///
        /// เรียงตาม "มุมรอบผู้เล่น" แทนลำดับ register: index คงที่ตามเรขาคณิต
        /// ต่อให้ register/unregister กลางเกม บทบาทในวงล้อมจะไม่สลับกันมั่ว
        /// (ถ้าไม่รู้ตำแหน่งผู้เล่น → ใช้ลำดับ register เดิม)
        /// </summary>
        public int GetActiveChaseIndex(Transform self, out int totalActiveChasers)
        {
            s_chaserBuffer.Clear();
            s_chaserAngleBuffer.Clear();

            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;
                if (!enemy.TryGetComponent(out EnemyBrain brain)) continue;

                EnemyState state = brain.GetCurrentState();
                if (state != EnemyState.Chase && state != EnemyState.Combat && state != EnemyState.Flank)
                    continue;

                s_chaserBuffer.Add(enemy);
            }

            totalActiveChasers = s_chaserBuffer.Count;
            if (s_chaserBuffer.Count == 0) return -1;

            // เรียงตามมุมรอบผู้เล่น (atan2) — insertion sort บน buffer เดิม
            // (list เล็ก ~8 ตัว ไม่ใช้ List.Sort กัน delegate allocation)
            if (_playerHintValid)
            {
                s_chaserAngleBuffer.Clear();
                for (int i = 0; i < s_chaserBuffer.Count; i++)
                {
                    Vector2 offset = (Vector2)s_chaserBuffer[i].position - _playerHint;
                    s_chaserAngleBuffer.Add(Mathf.Atan2(offset.y, offset.x));
                }

                for (int i = 1; i < s_chaserBuffer.Count; i++)
                {
                    Transform item = s_chaserBuffer[i];
                    float angle = s_chaserAngleBuffer[i];
                    int j = i - 1;
                    while (j >= 0 && s_chaserAngleBuffer[j] > angle)
                    {
                        s_chaserBuffer[j + 1] = s_chaserBuffer[j];
                        s_chaserAngleBuffer[j + 1] = s_chaserAngleBuffer[j];
                        j--;
                    }
                    s_chaserBuffer[j + 1] = item;
                    s_chaserAngleBuffer[j + 1] = angle;
                }
            }

            return s_chaserBuffer.IndexOf(self);
        }

        /// <summary>
        /// บันทึกตำแหน่งผู้เล่นที่ทีมเพิ่งรับรู้ (เรียกโดย EnemyBrain ที่เห็นผู้เล่น)
        /// ใช้ 2 อย่าง: เรียงลำดับ chaser เป็นมุมรอบผู้เล่น + เป็น "Active Contact" สำหรับระบบไปอ้อมดัก
        /// </summary>
        public void NotePlayerPosition(Vector2 position)
        {
            _playerHint = position;
            _playerHintValid = true;
            _contactPos = position;
            _contactTime = Time.time; // ทุกครั้งที่ตัวไหน "เห็นจริง" = contact ยังสด
        }

        private Vector2 _contactPos;
        private float _contactTime = float.NegativeInfinity;

        /// <summary>
        /// Active Contact = "ตอนนี้มีเพื่อนยังเห็นผู้เล่นอยู่จริงไหม" (อายุ ≤ maxAge วินาที)
        /// ใช้โดย TacticalDecision/ChaseState: ถ้าใช่ → เพื่อนที่มีความจำรีบไป "อ้อมดัก"
        /// แทนการค้นจุดเก่า — ผู้เล่นหนีไม่หลุดเพราะมีคนรออยู่ตามทางหนีแล้ว
        /// </summary>
        public bool TryGetContact(out Vector2 position, float maxAge = 1.5f)
        {
            position = _contactPos;
            return _playerHintValid && Time.time - _contactTime <= maxAge;
        }

        /// <summary>
        /// "ฝ่ายที่ถูกคุกคาม" ประกาศขอความช่วยเหลือ (Call-for-Help)
        /// ต่างจาก BroadcastAlert ตรงที่ไม่ได้บอกตำแหน่งผู้เล่น แต่บอกว่า "ฉัน/เพื่อนกำลังเดือดร้อน"
        /// ให้ Tank ตอบสนองได้โดยไม่ต้องเห็นผู้เล่นด้วยตาตัวเอง
        /// (แก้จุดอ่อนเดิม: HealerThreatened ผูกกับ perception ของ Tank — จำผู้เล่นไม่ได้ = ไม่รู้เรื่องเลย)
        /// </summary>
        /// <param name="source">ตัวที่กำลังเดือดร้อน (healer โดนเข้าใกล้ / เพื่อนกำลังหนี)</param>
        /// <param name="position">ตำแหน่งที่เกิดเหตุ</param>
        public void BroadcastDistress(Transform source, Vector2 position)
        {
            _distressSource = source;
            _distressPosition = position;
            _distressTime = Time.time;
        }

        /// <summary>อ่านสัญญาณขอความช่วยเหลือล่าสุด — คืน false ถ้าหมดอายุ/ตัวส่งตาย/ยังไม่เคยมี</summary>
        /// <param name="maxAge">อายุสูงสุดที่ยังถือว่าใช้ได้ (วินาที)</param>
        public bool TryGetRecentDistress(float maxAge, out Transform source, out Vector2 position)
        {
            source = _distressSource;
            position = _distressPosition;

            if (_distressSource == null) return false;              // ไม่เคยมี หรือตัวส่งถูกทำลาย (Unity null)
            if (Time.time - _distressTime > maxAge) return false;   // หมดอายุ
            return true;
        }

        /// <summary>true ถ้ามี Tank (Defensive) ตัวใดกำลังทำ duty คุ้มกัน/สกัดอยู่ — ใช้ให้ Flanker ได้โบนัส "คุมไฟ-ขยับ" (Leapfrog)</summary>
        public bool HasTankOnDuty()
        {
            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;
                if (!enemy.TryGetComponent(out EnemyBrain brain)) continue;
                if (brain.Role != EnemyRole.Defensive) continue;

                EnemyState state = brain.GetCurrentState();
                if (state == EnemyState.ProtectHealer || state == EnemyState.PeelAlly)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// แบ่ง "โซนค้นหา" ให้ตัวที่กำลัง Search อยู่ — คืน index ของตัวนี้ในกลุ่มคนค้น
        /// ใช้กระจายมุมค้นหาของแต่ละตัว (ไม่ค้นทับกันที่จุดเดิมซ้ำ ๆ) — ดู SearchState.StartSearchPattern
        /// index 0 = ค้นตามทิศทางหนีจริงก่อน, ตัวอื่นกระจายรอบวงตามมุมที่แบ่งไว้
        /// </summary>
        public int GetSearchSectorIndex(Transform self, out int totalSearchers)
        {
            s_searchBuffer.Clear();

            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;
                if (!enemy.TryGetComponent(out EnemyBrain brain)) continue;
                if (brain.GetCurrentState() != EnemyState.Search) continue;
                s_searchBuffer.Add(enemy);
            }

            totalSearchers = s_searchBuffer.Count;
            return s_searchBuffer.IndexOf(self);
        }

        /// <summary>
        /// แบ่งปันจุดที่ "เห็นด้วยตาตอนนี้" ให้เพื่อนในระยะ — รายงานต่อเนื่อง (ทุก ~1 วิ)
        /// ต่างจาก BroadcastAlert ที่ยิงแค่ "ครั้งแรก" — ช่วยให้เพื่อนที่เสียสายตาไปแล้วยังตามต่อได้
        /// (ข้อมูลประกอบจากเพื่อน: ผู้รับได้ความจำ confidence ~0.8 ไม่ใช่ 1.0 เหมือนสายตาตัวเอง)
        /// </summary>
        public void SpotPlayer(Vector2 position, Vector2 direction, Transform source, float radius)
        {
            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null || enemy == source) continue;
                if (Vector2.Distance(position, enemy.position) > radius) continue; // ไกลเกินระยะวิทยุ

                if (enemy.TryGetComponent(out EnemyBrain brain))
                    brain.ReceiveSpotting(position, direction);
            }
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
        ///
        /// Gradient: แต่ละตัวได้รับ "ความมั่นใจ + เวลาหน่วง" ต่างกันตามระยะจากจุดเกิดเสียง
        /// (ใกล้ = รู้ทันทีและมั่นใจ, ไกล = ช้าลงและมั่วน้อยลง) — ดู EnemyBrain.ReceiveNoise
        /// </summary>
        /// <param name="noisePosition">ตำแหน่งที่เกิดเสียง</param>
        /// <param name="noiseRadius">ระยะที่เสียงไปถึง (ยิ่งดัง เช่นเสียงปืน ยิ่งไปไกล — ใช้คำนวณ gradient ด้วย)</param>
        public void EmitNoise(Vector2 noisePosition, float noiseRadius)
        {
            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;
                if (Vector2.Distance(noisePosition, enemy.position) > noiseRadius) continue; // ไกลเกินไป ไม่ได้ยิน

                if (enemy.TryGetComponent(out EnemyBrain brain))
                {
                    brain.ReceiveNoise(noisePosition, noiseRadius);
                }
            }
        }
    }
}
