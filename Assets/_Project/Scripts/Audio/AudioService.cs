using System.Collections.Generic;
using RiskOfMagic.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace RiskOfMagic.Audio
{
    /// <summary>
    /// Local playback (every peer plays its own sounds; nothing is replicated): pooled one-shot sources,
    /// music with crossfade (scene music + override for events), volumes from GameSettings or the mixer.
    /// </summary>
    public static class AudioService
    {
        private static AudioRunner runner;
        private static AudioConfig config;

        public static AudioConfig Config
        {
            get
            {
                if (config == null)
                    config = Resources.Load<AudioConfig>(AudioConfig.ResourcePath);
                return config;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            runner = null;
            config = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var go = new GameObject("AudioService");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<AudioRunner>();
            go.AddComponent<GameAudioEvents>();
        }

        /// <summary>3D at a position (or 2D if the sound is not spatial).</summary>
        public static void PlayAt(SoundEvent sound, Vector3 position)
        {
            if (runner != null)
                runner.Play(sound, position);
        }

        /// <summary>2D (UI, local feedback).</summary>
        public static void Play2D(SoundEvent sound)
        {
            if (runner != null)
                runner.Play(sound, null);
        }

        /// <summary>Base music of the current scene (SceneMusic).</summary>
        public static void SetSceneMusic(MusicTrack track)
        {
            if (runner != null)
                runner.SetSceneMusic(track);
        }

        /// <summary>Temporary music on top of the scene music (boss event, game over); null to clear.</summary>
        public static void SetOverrideMusic(MusicTrack track)
        {
            if (runner != null)
                runner.SetOverrideMusic(track);
        }

        /// <summary>The override if it has clips, otherwise the fallback (empty generated slots stay "unset").</summary>
        public static SoundEvent Pick(SoundEvent preferred, SoundEvent fallback) =>
            preferred != null && preferred.HasClips ? preferred : fallback;

        internal static AudioMixerGroup GroupFor(SoundCategory category)
        {
            var cfg = Config;
            if (cfg == null || cfg.mixer == null)
                return null;
            return category switch
            {
                SoundCategory.UI => cfg.uiGroup != null ? cfg.uiGroup : cfg.effectsGroup,
                SoundCategory.Music => cfg.musicGroup,
                _ => cfg.effectsGroup
            };
        }

        /// <summary>Volume multiplier applied on the source (1 when the mixer handles volumes).</summary>
        internal static float CategoryVolume(SoundCategory category)
        {
            var cfg = Config;
            if (cfg != null && cfg.mixer != null)
                return 1f;
            return category == SoundCategory.Music ? GameSettings.MusicVolume : GameSettings.EffectsVolume;
        }

        internal static void ApplyMixerVolumes()
        {
            var cfg = Config;
            if (cfg == null || cfg.mixer == null)
                return;
            if (!string.IsNullOrEmpty(cfg.musicVolumeParameter))
                cfg.mixer.SetFloat(cfg.musicVolumeParameter, ToDecibels(GameSettings.MusicVolume));
            if (!string.IsNullOrEmpty(cfg.effectsVolumeParameter))
                cfg.mixer.SetFloat(cfg.effectsVolumeParameter, ToDecibels(GameSettings.EffectsVolume));
        }

        private static float ToDecibels(float linear) => Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f;
    }

    internal sealed class AudioRunner : MonoBehaviour
    {
        private const int PoolSize = 32;
        private const float MusicFadeDuration = 1.5f;

        private readonly List<AudioSource> pool = new();
        private readonly Dictionary<SoundEvent, float> lastPlayed = new();
        private readonly Dictionary<AudioSource, SoundEvent> playing = new();
        private AudioSource[] musicSources;
        private int activeMusic;
        private MusicTrack sceneMusic;
        private MusicTrack overrideMusic;
        private MusicTrack currentMusic;
        private float musicFade = 1f;
        private float previousMusicVolume;

        private void Awake()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject($"Voice_{i}");
                child.transform.SetParent(transform);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                pool.Add(source);
            }

            musicSources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.outputAudioMixerGroup = AudioService.GroupFor(SoundCategory.Music);
                musicSources[i] = source;
            }

        }

        // Start: after every BeforeSceneLoad initializer (GameSettings clears its event there); mixer SetFloat is ignored in Awake.
        private void Start()
        {
            GameSettings.Changed += OnSettingsChanged;
            AudioService.ApplyMixerVolumes();
        }

        private void OnDestroy() => GameSettings.Changed -= OnSettingsChanged;

        private void OnSettingsChanged() => AudioService.ApplyMixerVolumes();

        public void Play(SoundEvent sound, Vector3? position)
        {
            if (sound == null || !sound.HasClips)
                return;
            float now = Time.unscaledTime;
            if (sound.cooldown > 0f && lastPlayed.TryGetValue(sound, out float last) && now - last < sound.cooldown)
                return;
            if (sound.maxSimultaneous > 0 && CountPlaying(sound) >= sound.maxSimultaneous)
                return;

            var source = GetFreeSource();
            bool spatial = sound.spatial && position.HasValue;
            source.transform.position = position ?? Vector3.zero;
            source.clip = sound.PickClip();
            source.spatialBlend = spatial ? 1f : 0f;
            source.minDistance = sound.minDistance;
            source.maxDistance = sound.maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.volume = Random.Range(sound.volume.x, sound.volume.y) * AudioService.CategoryVolume(sound.category);
            source.pitch = Random.Range(sound.pitch.x, sound.pitch.y);
            source.outputAudioMixerGroup = AudioService.GroupFor(sound.category);
            source.Play();

            playing[source] = sound;
            lastPlayed[sound] = now;
        }

        private int CountPlaying(SoundEvent sound)
        {
            int count = 0;
            foreach (var pair in playing)
                if (pair.Value == sound && pair.Key.isPlaying)
                    count++;
            return count;
        }

        private AudioSource GetFreeSource()
        {
            AudioSource oldest = pool[0];
            foreach (var source in pool)
            {
                if (!source.isPlaying)
                    return source;
                if (source.time > oldest.time)
                    oldest = source;
            }
            oldest.Stop(); // voice stealing
            return oldest;
        }

        public void SetSceneMusic(MusicTrack track)
        {
            sceneMusic = track;
            RefreshMusic();
        }

        public void SetOverrideMusic(MusicTrack track)
        {
            overrideMusic = track;
            RefreshMusic();
        }

        private void RefreshMusic()
        {
            var wanted = overrideMusic != null && overrideMusic.clip != null ? overrideMusic : sceneMusic;
            if (wanted == currentMusic || (wanted != null && currentMusic != null && wanted.clip == currentMusic.clip))
                return;
            currentMusic = wanted;
            previousMusicVolume = musicSources[activeMusic].volume;
            activeMusic = 1 - activeMusic;
            var source = musicSources[activeMusic];
            source.Stop();
            if (wanted != null && wanted.clip != null)
            {
                source.clip = wanted.clip;
                source.loop = wanted.loop;
                source.volume = 0f;
                source.Play();
            }
            musicFade = 0f;
        }

        private void Update()
        {
            musicFade = Mathf.MoveTowards(musicFade, 1f, Time.unscaledDeltaTime / MusicFadeDuration);
            float categoryVolume = AudioService.CategoryVolume(SoundCategory.Music);
            float target = currentMusic != null ? currentMusic.volume * categoryVolume : 0f;
            musicSources[activeMusic].volume = target * musicFade;
            var previous = musicSources[1 - activeMusic];
            previous.volume = previousMusicVolume * (1f - musicFade);
            if (musicFade >= 1f && previous.isPlaying)
                previous.Stop();
        }
    }
}
