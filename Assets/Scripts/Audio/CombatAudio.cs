using UnityEngine;

namespace TopDownTacticalAI.Audio
{
    /// <summary>
    /// เล่นเสียงยิงและเสียงรีโหลด
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CombatAudio : MonoBehaviour
    {
        public AudioClip ShootClip;
        public AudioClip ReloadClip;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
        }

        public void PlayShoot()
        {
            if (ShootClip != null)
                _source.PlayOneShot(ShootClip);
        }

        public void PlayReload()
        {
            if (ReloadClip != null)
                _source.PlayOneShot(ReloadClip);
        }
    }
}
