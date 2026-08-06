using UnityEngine;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Search
{
    /// <summary>
    /// รูปแบบการค้นหาแบบ "สำรวจทีละเส้นทางแล้วย้อนกลับ" (คล้ายคนจริงๆ ค้นหา ไม่ใช่สุ่มมั่วๆ):
    /// มองซ้าย-ขวาที่จุดล่าสุดก่อน -> เดินไปสำรวจจุดที่คาดว่าน่าจะมีคนหลบ (เอนเอียงตามทิศที่ผู้เล่นวิ่งหนี)
    /// -> มองซ้าย-ขวาที่จุดนั้น -> ถ้าไม่เจอ เดินย้อนกลับมาจุดศูนย์กลางก่อน -> แล้วค่อยไปสำรวจเส้นทางถัดไป
    /// วนแบบนี้ไปเรื่อยๆ จนครบจำนวนจุดที่กำหนด ถ้ายังไม่เจอ ถือว่า IsFinished = true ให้กลับไป Patrol ตามปกติ
    /// (การเดินกลับจุดศูนย์กลางก่อนทุกครั้ง ทำให้ดูเหมือนกำลัง "สำรวจเป็นเส้นทางแยกๆ" จริงๆ ไม่ใช่กระโดดไปมา)
    /// </summary>
    public class SearchPattern
    {
        private enum Phase { LookLeft, LookRight, WanderPoint, LookAtPoint, ReturnToCenter, Done }

        private Phase _phase = Phase.LookLeft;
        private readonly CountdownTimer _lookTimer = new CountdownTimer();
        private readonly float _lookDuration;
        private Vector2 _wanderTarget;
        private readonly Vector2 _center;
        private readonly float _baseWanderRadius;
        private readonly LayerMask _obstacleMask;
        private readonly Vector2 _biasDirection;
        private int _wanderCount;
        private readonly int _maxWanderPoints;

        public bool IsFinished => _phase == Phase.Done;
        public float CurrentLookAngleOffset { get; private set; }

        /// <param name="biasDirection">
        /// ทิศทางการเคลื่อนที่ล่าสุดของผู้เล่นก่อนหายไป (เช่นจาก TargetMemory.LastVelocity)
        /// ใช้เดาว่าผู้เล่นน่าจะวิ่งหนีต่อไปทางไหน จุดค้นหาแรกๆ จะเอนเอียงไปทิศนี้ก่อน
        /// ส่ง Vector2.zero ถ้าไม่รู้ทิศทาง จะสุ่มรอบทิศทางปกติแทน
        /// </param>
        public SearchPattern(Vector2 center, float wanderRadius, LayerMask obstacleMask, Vector2 biasDirection = default, float lookDuration = 1.2f, int maxWanderPoints = 6)
        {
            _center = center;
            _baseWanderRadius = wanderRadius;
            _obstacleMask = obstacleMask;
            _biasDirection = biasDirection;
            _lookDuration = lookDuration;
            _maxWanderPoints = maxWanderPoints;
            _lookTimer.Start(_lookDuration);
        }

        public void Tick(float deltaTime)
        {
            switch (_phase)
            {
                case Phase.LookLeft:
                    _lookTimer.Tick(deltaTime);
                    CurrentLookAngleOffset = Mathf.Lerp(0f, 60f, _lookTimer.Progress01);
                    if (_lookTimer.IsFinished)
                    {
                        _phase = Phase.LookRight;
                        _lookTimer.Start(_lookDuration);
                    }
                    break;

                case Phase.LookRight:
                    _lookTimer.Tick(deltaTime);
                    CurrentLookAngleOffset = Mathf.Lerp(60f, -60f, _lookTimer.Progress01);
                    if (_lookTimer.IsFinished)
                    {
                        _phase = Phase.WanderPoint;
                        PickWanderPoint();
                    }
                    break;

                case Phase.WanderPoint:
                    // การเช็คว่าถึงจุดหรือยังทำใน SearchState (ต้องรู้ตำแหน่งปัจจุบันของตัวศัตรู)
                    break;

                case Phase.LookAtPoint:
                    _lookTimer.Tick(deltaTime);
                    CurrentLookAngleOffset = Mathf.PingPong(_lookTimer.Progress01 * 2f, 1f) * 50f - 25f;
                    if (_lookTimer.IsFinished)
                    {
                        _wanderCount++;
                        if (_wanderCount >= _maxWanderPoints)
                            _phase = Phase.Done;
                        else
                            _phase = Phase.ReturnToCenter; // ไม่เจอที่จุดนี้ -> เดินย้อนกลับจุดศูนย์กลางก่อนไปเส้นทางถัดไป
                    }
                    break;

                case Phase.ReturnToCenter:
                    // การเช็คว่าถึงจุดศูนย์กลางหรือยังทำใน SearchState เหมือนกัน
                    break;

                case Phase.Done:
                    break;
            }
        }

        /// <summary>เรียกจาก SearchState เมื่อเดินถึงจุดสำรวจแล้ว (หรือติดทางตัน) เพื่อเข้าสู่ช่วงมองซ้าย-ขวาสั้นๆ ที่จุดนั้น</summary>
        public void NotifyReachedWanderPoint()
        {
            _phase = Phase.LookAtPoint;
            _lookTimer.Start(_lookDuration * 0.8f);
        }

        /// <summary>เรียกจาก SearchState เมื่อเดินย้อนกลับมาถึงจุดศูนย์กลางแล้ว เพื่อเลือกเส้นทางถัดไปสำรวจต่อ</summary>
        public void NotifyReturnedToCenter()
        {
            PickWanderPoint();
            _phase = Phase.WanderPoint;
        }

        private void PickWanderPoint()
        {
            // ขยายรัศมีค้นหาออกไปเรื่อยๆ ทุกจุด เหมือนวงค้นหาที่ขยายกว้างขึ้นตามเวลาที่ผ่านไป (Alert Search)
            float radiusThisStep = _baseWanderRadius * (1f + _wanderCount * 0.4f);

            // จุดค้นหา 2 จุดแรกให้เอนเอียงไปทาง "ทิศที่ผู้เล่นวิ่งหนีล่าสุด" ก่อน (โอกาสเจอสูงกว่าสุ่มมั่ว)
            // จุดหลังๆ ค่อยสุ่มรอบทิศทางปกติ เผื่อผู้เล่นหักเลี้ยวไปทางอื่น
            Vector2 biasForThisPoint = _wanderCount < 2 ? _biasDirection : Vector2.zero;

            if (PhysicsUtility.TryFindDirectionalPoint(_center, radiusThisStep, biasForThisPoint, _obstacleMask, out Vector2 point))
                _wanderTarget = point;
            else
                _wanderTarget = _center;
        }

        public Vector2 WanderTarget => _wanderTarget;
        public Vector2 CenterPosition => _center;
        public bool IsWandering => _phase == Phase.WanderPoint;
        public bool IsReturningToCenter => _phase == Phase.ReturnToCenter;
    }
}
