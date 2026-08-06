using UnityEngine;

namespace TopDownTacticalAI.Audio
{
    /// <summary>
    /// เล่นเสียงแจ้งเตือนเมื่อ AI พบผู้เล่นครั้งแรก (State เปลี่ยนจาก Patrol เป็น Chase)
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AlertAudio : MonoBehaviour
    {
        public AudioClip AlertClip;
        public AudioClip LostTargetClip;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
        }

        public void PlayAlert()
        {
            if (AlertClip != null)
                _source.PlayOneShot(AlertClip);
        }

        public void PlayLostTarget()
        {
            if (LostTargetClip != null)
                _source.PlayOneShot(LostTargetClip);
        }
    }
}
