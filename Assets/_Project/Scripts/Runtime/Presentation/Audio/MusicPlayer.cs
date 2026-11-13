using HEXBOUND.Core;
using UnityEngine;

namespace HEXBOUND.Presentation
{
    public enum MusicMood
    {
        None,
        /// <summary>Main menu: intro track.</summary>
        Menu,
        /// <summary>Character select and online lobby: loading track.</summary>
        Lobby,
        /// <summary>In a match: quiet lava and fire ambience so it never distracts the fighters.</summary>
        Match,
    }

    /// <summary>
    /// Music and ambience, crossfaded when the screen changes. Clips are loaded from Resources/Audio/Music
    /// by file name: intro, loading, flowinglava, fire. A missing file is skipped silently.
    /// </summary>
    public sealed class MusicPlayer
    {
        private sealed class Layer
        {
            public AudioSource Source;
            public float Target;
        }

        // Volumes before the master volume setting. Match ambience is deliberately very low.
        // The source files differ in loudness (fire.mp3 is about 24 dB quieter than flowinglava.mp3),
        // so these values balance them: both sit far below the fight sounds, fire is the softest layer.
        private const float MenuVolume = 0.5f;
        private const float LobbyVolume = 0.55f;
        private const float LavaMatchVolume = 0.06f;
        private const float FireMatchVolume = 0.35f;
        private const float FadeSpeed = 0.6f;

        private readonly Layer _intro;
        private readonly Layer _loading;
        private readonly Layer _lava;
        private readonly Layer _fire;
        private readonly Layer[] _layers;

        public MusicMood Mood { get; private set; } = MusicMood.None;

        public MusicPlayer(Transform parent)
        {
            var go = new GameObject("Music");
            go.transform.SetParent(parent, false);
            _intro = CreateLayer(go, "intro");
            _loading = CreateLayer(go, "loading");
            _lava = CreateLayer(go, "flowinglava");
            _fire = CreateLayer(go, "fire");
            _layers = new[] { _intro, _loading, _lava, _fire };
        }

        private static Layer CreateLayer(GameObject host, string clipName)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>("Audio/Music/" + clipName);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 32;
            return new Layer { Source = source, Target = 0f };
        }

        public void SetMood(MusicMood mood)
        {
            if (mood == Mood) return;
            Mood = mood;
            _intro.Target = mood == MusicMood.Menu ? MenuVolume : 0f;
            _loading.Target = mood == MusicMood.Lobby ? LobbyVolume : 0f;
            _lava.Target = mood == MusicMood.Match ? LavaMatchVolume : 0f;
            _fire.Target = mood == MusicMood.Match ? FireMatchVolume : 0f;

            // Restart a track from the beginning when it fades in from silence.
            foreach (Layer layer in _layers)
            {
                if (layer.Source.clip == null || layer.Target <= 0f) continue;
                if (!layer.Source.isPlaying)
                {
                    layer.Source.time = 0f;
                    layer.Source.Play();
                }
            }
        }

        public void Update(float deltaTime)
        {
            float master = GameSettings.Volume;
            foreach (Layer layer in _layers)
            {
                if (layer.Source.clip == null) continue;
                float target = layer.Target * master;
                layer.Source.volume = Mathf.MoveTowards(layer.Source.volume, target, FadeSpeed * deltaTime);
                if (layer.Target <= 0f && layer.Source.volume <= 0.001f && layer.Source.isPlaying) layer.Source.Stop();
            }
        }
    }
}
