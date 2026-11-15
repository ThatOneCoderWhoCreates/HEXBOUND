using HEXBOUND.Core;
using HEXBOUND.Simulation;
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
        /// <summary>In a match: quiet ambience for the map (lava and fire, or a stream and distant birds) that never distracts the fighters.</summary>
        Match,
    }

    /// <summary>
    /// Music and ambience, crossfaded when the screen changes. Clips are loaded from Resources/Audio/Music
    /// by file name: intro, loading, flowinglava, fire. Celestial Sanctuary ambience is in Resources/Audio/Ambience:
    /// stream (a soft water loop) and bird_1 to bird_5 (short distant calls played now and then). A missing file is skipped silently.
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
        private const float StreamMatchVolume = 0.14f;
        private const float BirdMinVolume = 0.06f;
        private const float BirdMaxVolume = 0.11f;
        private const float BirdMinGap = 14f;
        private const float BirdMaxGap = 40f;
        private const float FadeSpeed = 0.6f;

        private readonly Layer _intro;
        private readonly Layer _loading;
        private readonly Layer _lava;
        private readonly Layer _fire;
        private readonly Layer _stream;
        private readonly Layer[] _layers;
        private readonly AudioClip[] _birds;
        private readonly AudioSource _birdSource;
        private float _birdTimer = 8f;
        private float _secondCallTimer = -1f;
        private int _map = Arena.MapLava;

        public MusicMood Mood { get; private set; } = MusicMood.None;

        public MusicPlayer(Transform parent)
        {
            var go = new GameObject("Music");
            go.transform.SetParent(parent, false);
            _intro = CreateLayer(go, "intro");
            _loading = CreateLayer(go, "loading");
            _lava = CreateLayer(go, "flowinglava");
            _fire = CreateLayer(go, "fire");
            _stream = CreateLayer(go, "stream", "Audio/Ambience/");
            _layers = new[] { _intro, _loading, _lava, _fire, _stream };

            var birds = new System.Collections.Generic.List<AudioClip>();
            for (int i = 1; i <= 5; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>("Audio/Ambience/bird_" + i);
                if (clip != null) birds.Add(clip);
            }
            _birds = birds.ToArray();
            _birdSource = go.AddComponent<AudioSource>();
            _birdSource.playOnAwake = false;
            _birdSource.spatialBlend = 0f;
            _birdSource.priority = 200;
        }

        private static Layer CreateLayer(GameObject host, string clipName, string folder = "Audio/Music/")
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>(folder + clipName);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 32;
            return new Layer { Source = source, Target = 0f };
        }

        /// <summary>The arena on screen; it picks the match ambience.</summary>
        public void SetMap(int map)
        {
            if (map == _map) return;
            _map = map;
            if (Mood == MusicMood.Match) ApplyTargets();
        }

        public void SetMood(MusicMood mood)
        {
            if (mood == Mood) return;
            Mood = mood;
            ApplyTargets();
        }

        private void ApplyTargets()
        {
            bool match = Mood == MusicMood.Match;
            bool sanctuary = _map == Arena.MapSanctuary;
            _intro.Target = Mood == MusicMood.Menu ? MenuVolume : 0f;
            _loading.Target = Mood == MusicMood.Lobby ? LobbyVolume : 0f;
            _lava.Target = match && !sanctuary ? LavaMatchVolume : 0f;
            _fire.Target = match && !sanctuary ? FireMatchVolume : 0f;
            _stream.Target = match && sanctuary ? StreamMatchVolume : 0f;

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
            UpdateBirds(deltaTime, master);
        }

        /// <summary>Now and then, a quiet bird call from somewhere off to the side (Celestial Sanctuary matches only).</summary>
        private void UpdateBirds(float deltaTime, float master)
        {
            if (_birds.Length == 0 || Mood != MusicMood.Match || _map != Arena.MapSanctuary) return;
            if (_secondCallTimer > 0f)
            {
                _secondCallTimer -= deltaTime;
                if (_secondCallTimer <= 0f) PlayBird(master);
            }
            _birdTimer -= deltaTime;
            if (_birdTimer > 0f) return;
            _birdTimer = Random.Range(BirdMinGap, BirdMaxGap);
            PlayBird(master);
            // Sometimes the bird calls twice.
            if (Random.value < 0.3f) _secondCallTimer = Random.Range(0.6f, 1.4f);
        }

        private void PlayBird(float master)
        {
            AudioClip clip = _birds[Random.Range(0, _birds.Length)];
            _birdSource.pitch = Random.Range(0.92f, 1.08f);
            _birdSource.panStereo = Random.Range(-0.6f, 0.6f);
            _birdSource.PlayOneShot(clip, Random.Range(BirdMinVolume, BirdMaxVolume) * master);
        }
    }
}
