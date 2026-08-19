using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// ลอจิกสำหรับสาย Support เพื่อเดินตามหลังพันธมิตรที่เป็นสาย Defensive (Tank)
    /// </summary>
    public class FollowTankState : IAIState
    {
        private EnemyBrain _brain;
        private Transform _tankTarget;
        
        [Tooltip("ระยะห่างที่จะยืนตามหลังแทงก์")]
        private float _followDistance = 2.5f;
        [Tooltip("รัศมีสแกนหาแทงก์ในฉาก")]
        private float _searchRadius = 15f;

        public EnemyState StateType => EnemyState.FollowTank;

        public FollowTankState(EnemyBrain brain)
        {
            _brain = brain;
        }

        public void Enter()
        {
            FindTankLeader();
        }

        public void Tick(float deltaTime)
        {
            if (_tankTarget == null)
            {
                // ถ้าแทงก์ตาย หรือยังหาไม่เจอ ให้สแกนหาใหม่
                FindTankLeader();
                return;
            }

            // --- คำนวณหาตำแหน่ง "ด้านหลัง" ของแทงก์ ---
            Vector3 behindOffset = -_tankTarget.right * _followDistance;
            Vector3 targetPosition = _tankTarget.position + behindOffset;

            // TODO: ส่ง targetPosition ไปให้ระบบ Pathfinding หรือ Steering ของพี
        }

        // --- เพิ่มฟังก์ชันนี้ตามกฎของ IAIState (สำหรับงานฟิสิกส์) ---
        public void FixedTick(float fixedDeltaTime)
        {
            // ถ้ายังไม่มีงานคำนวณฟิสิกส์ ปล่อยว่างไว้ได้เลยครับ
        }

        public void Exit()
        {
            _tankTarget = null;
        }

        private void FindTankLeader()
        {
            // หาศัตรูทั้งหมดในระยะ
            Collider2D[] allies = Physics2D.OverlapCircleAll(_brain.transform.position, _searchRadius);
            float closestDist = float.MaxValue;

            foreach (var ally in allies)
            {
                EnemyBrain allyBrain = ally.GetComponent<EnemyBrain>();
                
                // เช็กว่าเป็นคนละตัวกับเรา และมีสมอง AI
                if (allyBrain != null && allyBrain != _brain)
                {
                    // กรองเอาเฉพาะเพื่อนที่เป็นสาย Defensive (Tank)
                    if (allyBrain.Role == EnemyRole.Defensive)
                    {
                        float dist = Vector2.Distance(_brain.transform.position, allyBrain.transform.position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            _tankTarget = allyBrain.transform;
                        }
                    }
                }
            }
        }
    }
}