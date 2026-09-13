using UnityEngine;

namespace TopDownTacticalAI.Player
{
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerAimLine : MonoBehaviour
    {
        public float MaxAimDistance = 50f;
        public LayerMask ObstacleMask; // เลือกติ๊ก Wall

        private LineRenderer _line;
        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
            _line = GetComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.startWidth = 0.08f;
            _line.endWidth = 0.08f;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = new Color(1f, 0.2f, 0.2f, 0.6f); // สีแดงโปร่งแสง
            _line.endColor = new Color(1f, 0.2f, 0.2f, 0.1f);
            _line.sortingOrder = 30;
        }

        private void Update()
        {
            Vector3 mouseWorldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;

            Vector2 aimDir = (mouseWorldPos - transform.position).normalized;
            Vector2 startPos = transform.position;

            // ยิง Raycast ตรวจจับว่าเส้นเล็งไปชนกำแพงหรือไม่
            RaycastHit2D hit = Physics2D.Raycast(startPos, aimDir, MaxAimDistance, ObstacleMask);
            Vector2 endPos = hit.collider != null ? hit.point : startPos + (aimDir * MaxAimDistance);

            _line.SetPosition(0, startPos);
            _line.SetPosition(1, endPos);
        }
    }
}