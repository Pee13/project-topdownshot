using UnityEngine;

namespace TopDownTacticalAI.Audio
{
    /// <summary>
    /// เล่นเสียงฝีเท้าเป็นจังหวะขณะเคลื่อนที่
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FootstepAudio : MonoBehaviour
    {
        public AudioClip[] FootstepClips;
        public float StepInterval = 0.4f;

        private AudioSource _source;
        private float _timer;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
        }

        /// <summary>เรียกทุกเฟรมพร้อมส่งค่าว่ากำลังเคลื่อนที่อยู่หรือไม่</summary>
        public void Tick(bool isMoving, float deltaTime)
        {
            if (!isMoving || FootstepClips == null || FootstepClips.Length == 0)
            {
                _timer = 0f;
                return;
            }

            _timer += deltaTime;
            if (_timer >= StepInterval)
            {
                _timer = 0f;
                AudioClip clip = FootstepClips[Random.Range(0, FootstepClips.Length)];
                _source.PlayOneShot(clip);
            }
        }
    }
}
