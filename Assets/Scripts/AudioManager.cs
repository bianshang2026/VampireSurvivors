using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 音效系统 — BGM循环播放 + SFX音效播放。
    /// 挂在场景中持久物体上(如HUD_Canvas)，Singleton模式。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("BGM")]
        [Tooltip("BGM音频片段")]
        public AudioClip bgmClip;
        [Range(0f, 1f)] public float bgmVolume = 0.4f;
        public bool bgmLoop = true;
        public bool playOnAwake = true;

        [Header("SFX")]
        [Range(0f, 1f)] public float sfxVolume = 0.6f;
        public int sfxChannels = 8;

        private AudioSource _bgmSource;
        private AudioSource[] _sfxSources;
        private int _sfxIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // BGM AudioSource
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = bgmLoop;
            _bgmSource.volume = bgmVolume;
            _bgmSource.priority = 0;
            _bgmSource.playOnAwake = false;

            // SFX AudioSource pool
            _sfxSources = new AudioSource[sfxChannels];
            for (int i = 0; i < sfxChannels; i++)
            {
                _sfxSources[i] = gameObject.AddComponent<AudioSource>();
                _sfxSources[i].loop = false;
                _sfxSources[i].volume = sfxVolume;
                _sfxSources[i].playOnAwake = false;
            }

            // Load BGM from Resources if not assigned
            if (bgmClip == null)
            {
                bgmClip = Resources.Load<AudioClip>("BGM_Cultivation");
            }

            // Try loading from known path
            if (bgmClip == null)
            {
                bgmClip = Resources.Load<AudioClip>("Music_20260818_223939");
            }

            if (bgmClip != null)
            {
                _bgmSource.clip = bgmClip;
                if (playOnAwake)
                    _bgmSource.Play();
            }
            else
            {
                Debug.LogWarning("[AudioManager] BGM clip not found. Assign bgmClip in Inspector or place in Resources/");
            }
        }

        /// <summary>播放BGM</summary>
        public void PlayBGM()
        {
            if (_bgmSource != null && bgmClip != null && !_bgmSource.isPlaying)
                _bgmSource.Play();
        }

        /// <summary>停止BGM</summary>
        public void StopBGM()
        {
            if (_bgmSource != null)
                _bgmSource.Stop();
        }

        /// <summary>设置BGM音量</summary>
        public void SetBGMVolume(float vol)
        {
            bgmVolume = Mathf.Clamp01(vol);
            if (_bgmSource != null)
                _bgmSource.volume = bgmVolume;
        }

        /// <summary>播放SFX音效</summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            AudioSource src = _sfxSources[_sfxIndex];
            src.volume = sfxVolume * volumeScale;
            src.PlayOneShot(clip);
            _sfxIndex = (_sfxIndex + 1) % sfxChannels;
        }

        /// <summary>播放SFX by Resources path</summary>
        public void PlaySFXByName(string resourcePath, float volumeScale = 1f)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip != null)
                PlaySFX(clip, volumeScale);
        }

        /// <summary>设置SFX音量</summary>
        public void SetSFXVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
        }

        // ── 游戏 SFX 事件助手 (静态, 自动缓存clip, 失败静默) ──
        private static AudioClip _levelUpClip;
        private static AudioClip _enemyHitClip;
        private static AudioClip _enemyDeathClip;
        private static AudioClip _bossRoarClip;
        private static float _lastHitSfxTime = -1f;

        /// <summary>升级音效</summary>
        public static void PlayLevelUp()
        {
            if (Instance == null) return;
            if (_levelUpClip == null) _levelUpClip = Resources.Load<AudioClip>("Audio/LevelUp");
            if (_levelUpClip != null) Instance.PlaySFX(_levelUpClip, 1f);
        }

        /// <summary>敌人受击音效 (限流: 同0.08秒内只播一次, 避免密集伤害刷屏)</summary>
        public static void PlayEnemyHit()
        {
            if (Instance == null) return;
            if (Time.unscaledTime - _lastHitSfxTime < 0.08f) return;
            _lastHitSfxTime = Time.unscaledTime;
            if (_enemyHitClip == null) _enemyHitClip = Resources.Load<AudioClip>("Audio/EnemyHit");
            if (_enemyHitClip != null) Instance.PlaySFX(_enemyHitClip, 0.7f);
        }

        /// <summary>敌人死亡音效 (限流: 同0.15秒内只播一次)</summary>
        private static float _lastDeathSfxTime = -1f;
        public static void PlayEnemyDeath()
        {
            if (Instance == null) return;
            if (Time.unscaledTime - _lastDeathSfxTime < 0.15f) return;
            _lastDeathSfxTime = Time.unscaledTime;
            if (_enemyDeathClip == null) _enemyDeathClip = Resources.Load<AudioClip>("Audio/EnemyDeath");
            if (_enemyDeathClip != null) Instance.PlaySFX(_enemyDeathClip, 0.8f);
        }

        /// <summary>Boss吼叫音效</summary>
        public static void PlayBossRoar()
        {
            if (Instance == null) return;
            if (_bossRoarClip == null) _bossRoarClip = Resources.Load<AudioClip>("Audio/BossRoar");
            if (_bossRoarClip != null) Instance.PlaySFX(_bossRoarClip, 1f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
