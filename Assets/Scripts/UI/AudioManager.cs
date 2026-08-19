using UnityEngine;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// ตัวจัดการเสียงกลางของเกม แยกระดับเสียง Music/SFX ออกจากกัน (นอกเหนือจาก Master ที่คุม AudioListener.volume โดยตรง)
    /// เป็น Singleton อยู่ข้ามฉาก (DontDestroyOnLoad) ให้เพลงเล่นต่อเนื่องตอนเปลี่ยน Scene ระหว่างเมนู/ด่าน
    ///
    /// วิธีติดตั้ง: สร้าง Empty GameObject ชื่อ "AudioManager" ในฉาก MainMenu (แค่ฉากเดียว จะข้ามไปทุกฉากเอง)
    /// แปะสคริปต์นี้ แล้วลาก AudioSource 2 ตัว (Music กับ SFX) มาใส่ในช่อง Inspector
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("── Audio Sources ──")]
        [Tooltip("AudioSource สำหรับเพลงพื้นหลัง (ตั้ง Loop = true ไว้ล่วงหน้า)")]
        public AudioSource musicSource;
        [Tooltip("AudioSource สำหรับเสียงเอฟเฟกต์ทั่วไป (ปุ่มกด ฯลฯ) ใช้ PlayOneShot")]
        public AudioSource sfxSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // ถอดตัวเองออกจากพ่อแม่ก่อนเสมอ (เช่นถ้าเผลอวางเป็นลูกของ Canvas) เพราะ DontDestroyOnLoad
            // ใช้ได้เฉพาะกับ GameObject ที่เป็น "ราก" (Root) เท่านั้น ไม่งั้นจะขึ้น Warning และไม่ทำงานจริง
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        public void SetMusicVolume(float volume01)
        {
            if (musicSource != null) musicSource.volume = Mathf.Clamp01(volume01);
        }

        public void SetSFXVolume(float volume01)
        {
            if (sfxSource != null) sfxSource.volume = Mathf.Clamp01(volume01);
        }

        /// <summary>เล่นเสียงเอฟเฟกต์สั้นๆ เช่น เสียงคลิกปุ่มในเมนู</summary>
        public void PlaySFX(AudioClip clip)
        {
            if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (musicSource == null || clip == null) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return; // เล่นเพลงเดิมอยู่แล้ว ไม่ต้องเริ่มใหม่
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.Play();
        }
    }
}
