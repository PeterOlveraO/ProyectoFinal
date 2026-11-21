using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.Player;
using Game.Rounds;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>Scene-owned handheld score: bar-aligned changes, bounded voices and separate mix buses.</summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        public enum MusicMood { Menu, Journey, Battle, Boss }
        const string MasterKey = "pketer.volumen";
        const string MusicKey = "pketer.musica";
        const string EffectsKey = "pketer.efectos";
        const double BarSeconds = 2; // Synthesized boss score: 120 BPM, 4/4.
        const double LavenderBarSeconds = 114d * 70224d / 4194304d; // Original Game Boy clock.
        const double FadeSeconds = .65;
        const int CombatVoices = 10, UiVoices = 3;
        static GameAudio _instance;
        static float _master = .8f, _music = 1f, _effects = .85f;
        readonly Dictionary<string, AudioClip> _cues = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, AudioClip> _cries = new Dictionary<string, AudioClip>();
        readonly Dictionary<PokemonElement, AudioClip> _shots = new Dictionary<PokemonElement, AudioClip>();
        readonly Dictionary<PokemonElement, AudioClip> _impacts = new Dictionary<PokemonElement, AudioClip>();
        readonly AudioClip[] _scores = new AudioClip[4];
        readonly AudioSource[] _musicSources = new AudioSource[2];
        readonly AudioSource[] _combatSources = new AudioSource[CombatVoices];
        readonly AudioSource[] _uiSources = new AudioSource[UiVoices];
        AudioMixer _mixer;
        AudioClip _lavender;
        AudioSource _crySource;
        GameObject _ownedListener;
        RoundManager _rounds;
        MusicMood _current, _pending;
        int _front, _lastPoints, _lastRound, _variation;
        bool _hasMusic, _switching, _lastOver, _lastRunActive, _started;
        RoundManager.RoundPhase _lastPhase;
        double _epoch, _switchTime, _nextShot, _nextImpact, _duckUntil, _nextCry;
        float _nextStateCheck, _duckDb, _lastHealth = 1, _oldListenerVolume;
        bool _oldListenerPause;
        bool _combatPaused;

        public static float MasterVolume => _master;
        public static float MusicVolume => _music;
        public static float EffectsVolume => _effects;
        public MusicMood CurrentMood => _current;
        public MusicMood RequestedMood { get; private set; }
        public int VoiceCapacity => CombatVoices + UiVoices + 2;
        public bool CombatPaused => _combatPaused;

        public static void EnsureExists(GameObject host)
        {
            if (_instance == null && host != null && host.GetComponent<GameAudio>() == null)
                host.AddComponent<GameAudio>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            _master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, .8f));
            _music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
            _effects = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, .85f));
            _oldListenerVolume = AudioListener.volume;
            _oldListenerPause = AudioListener.pause;
            AudioListener.volume = 1;
            AudioListener.pause = false;
            _mixer = Resources.Load<AudioMixer>("Audio/GameMix");
            if (_mixer == null) { enabled = false; return; }
            bool hasListener = false;
            foreach (var listener in FindObjectsByType<AudioListener>())
                if (listener.isActiveAndEnabled) { hasListener = true; break; }
            if (!hasListener)
            {
                _ownedListener = new GameObject("Audio Listener");
                _ownedListener.transform.SetParent(transform, false);
                _ownedListener.AddComponent<AudioListener>();
            }
            var musicGroup = Group("MusicDuck");
            var combatGroup = Group("Combat");
            var uiGroup = Group("UI");
            for (int i = 0; i < 2; i++)
                _musicSources[i] = Source("Music " + i, musicGroup, true, true);
            for (int i = 0; i < CombatVoices; i++)
                _combatSources[i] = Source("Combat " + i, combatGroup, false, false);
            for (int i = 0; i < UiVoices; i++)
                _uiSources[i] = Source("UI " + i, uiGroup, false, true);
            _crySource = _uiSources[UiVoices - 1]; // Dedicated within the existing 15-voice budget.
            foreach (var cry in Resources.LoadAll<AudioClip>("Audio/Cries"))
                _cries[cry.name] = cry;
            _lavender = Resources.Load<AudioClip>("Audio/Music/LavenderTown");
            foreach (MusicMood mood in Enum.GetValues(typeof(MusicMood)))
                _scores[(int)mood] = mood != MusicMood.Boss && _lavender != null
                    ? _lavender : Resources.Load<AudioClip>("Audio/Music/" + mood);
            foreach (string cue in new[] { "Ui", "Confirm", "Purchase", "Hurt", "Clear", "Defeat" })
                _cues[cue] = Resources.Load<AudioClip>("Audio/Sfx/" + cue);
            foreach (PokemonElement element in Enum.GetValues(typeof(PokemonElement)))
            {
                string family = SoundFamily(element.ToString());
                _shots[element] = Resources.Load<AudioClip>("Audio/Sfx/Shot" + family);
                _impacts[element] = Resources.Load<AudioClip>("Audio/Sfx/Impact" + family);
            }
        }

        void Start()
        {
            if (_mixer == null) return;
            _started = true;
            ApplyVolumes();
            UpdateState();
        }

        AudioMixerGroup Group(string name)
        {
            var groups = _mixer.FindMatchingGroups(name);
            foreach (var group in groups) if (group.name == name) return group;
            return null;
        }

        AudioSource Source(string label, AudioMixerGroup group, bool loop, bool ignorePause)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = group;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.ignoreListenerPause = ignorePause;
            source.priority = loop ? 32 : ignorePause ? 64 : 128;
            return source;
        }

        void Update()
        {
            if (!_started) return;
            if (Time.unscaledTime >= _nextStateCheck)
            {
                _nextStateCheck = Time.unscaledTime + .08f;
                UpdateState();
            }
            UpdateMusic();
            float targetDuck = AudioSettings.dspTime < _duckUntil ? -3 :
                _rounds != null && _rounds.IsRunActive && GameFlow.Blocked ? -2.5f : 0;
            _duckDb = Mathf.MoveTowards(_duckDb, targetDuck, Time.unscaledDeltaTime * 12);
            _mixer.SetFloat("MusicDuckVolume", _duckDb);
        }

        void UpdateState()
        {
            if (_rounds != RoundManager.Instance)
            {
                if (_rounds != null) _rounds.Changed -= OnRoundChanged;
                _rounds = RoundManager.Instance;
                if (_rounds != null) _rounds.Changed += OnRoundChanged;
                _lastRunActive = false;
            }
            OnRoundChanged();
            bool active = _rounds != null && _rounds.IsRunActive;
            // AudioListener.pause freezes dspTime even for sources which ignore its pause.
            // Pause combat voices individually so the soundtrack keeps its musical clock.
            bool pauseCombat = active && GameFlow.Blocked;
            if (pauseCombat != _combatPaused)
            {
                _combatPaused = pauseCombat;
                foreach (var source in _combatSources)
                    if (pauseCombat) source.Pause(); else source.UnPause();
            }
            RequestedMood = ResolveMood(active, _rounds != null ? _rounds.Phase : RoundManager.RoundPhase.Idle,
                _rounds != null && _rounds.IsBossRound, GameFlow.Over);
            RequestMusic(RequestedMood);
            var player = PlayerSphereController.Instance;
            if (player != null)
            {
                float health = player.Health01;
                if (active && !GameFlow.Blocked && health < _lastHealth - .005f)
                    Cue("Hurt", false, .7f);
                _lastHealth = health;
            }
        }

        public static MusicMood ResolveMood(bool active, RoundManager.RoundPhase phase, bool boss, bool over)
        {
            if (over) return MusicMood.Journey;
            if (!active) return MusicMood.Menu;
            if (phase == RoundManager.RoundPhase.Fighting)
                return boss ? MusicMood.Boss : MusicMood.Battle;
            return MusicMood.Journey;
        }

        void OnRoundChanged()
        {
            if (GameFlow.Over && !_lastOver)
            {
                foreach (var source in _combatSources) if (source != null) source.Stop();
                Cue("Defeat", true);
                _duckUntil = AudioSettings.dspTime + 2;
            }
            _lastOver = GameFlow.Over;
            if (_rounds == null) return;
            bool active = _rounds.IsRunActive;
            if (_rounds.Phase == RoundManager.RoundPhase.Clearing && _lastPhase != _rounds.Phase)
            {
                Cue("Clear", true);
                _duckUntil = AudioSettings.dspTime + 1.6;
            }
            if (active && _lastRunActive && _rounds.Round == _lastRound && _rounds.Points < _lastPoints)
                Cue("Purchase", true);
            if (active && _rounds.IsBossRound && _rounds.Phase == RoundManager.RoundPhase.Fighting &&
                _lastPhase != _rounds.Phase)
                PlayCry(_rounds.BossName);
            _lastPhase = _rounds.Phase;
            _lastPoints = _rounds.Points;
            _lastRound = _rounds.Round;
            _lastRunActive = active;
        }

        // Quantization uses DSP time, not scaled gameplay time, so pause cannot desynchronize the score.
        public static double NextBar(double now, double epoch, double barSeconds = BarSeconds) =>
            epoch + Math.Ceiling(Math.Max(0, now + .06 - epoch) / barSeconds) * barSeconds;

        void RequestMusic(MusicMood mood)
        {
            double now = AudioSettings.dspTime;
            if (_switching)
            {
                if (mood == _pending || now >= _switchTime) return;
                _musicSources[1 - _front].Stop();
                _switching = false; // Cancel a queued transition if navigation changed before the bar.
            }
            if (_hasMusic && mood == _current) return;
            AudioClip clip = _scores[(int)mood];
            if (clip == null) return;
            if (_hasMusic && _musicSources[_front].clip == clip)
            {
                // Menu, preparation and normal combat share Lavender Town without restarting it.
                _current = mood;
                return;
            }
            if (!_hasMusic)
            {
                _current = mood;
                _epoch = now + .08;
                _musicSources[_front].clip = clip;
                _musicSources[_front].volume = 1;
                _musicSources[_front].PlayScheduled(_epoch);
                _hasMusic = true;
                return;
            }
            _pending = mood;
            _switchTime = NextBar(now, _epoch,
                _musicSources[_front].clip == _lavender ? LavenderBarSeconds : BarSeconds);
            var next = _musicSources[1 - _front];
            next.clip = clip;
            next.volume = 0;
            next.PlayScheduled(_switchTime);
            _switching = true;
        }

        void UpdateMusic()
        {
            if (!_switching || AudioSettings.dspTime < _switchTime) return;
            float t = Mathf.Clamp01((float)((AudioSettings.dspTime - _switchTime) / FadeSeconds));
            // Linear complementary gains keep the peak sum bounded during the overlap.
            _musicSources[_front].volume = 1 - t;
            _musicSources[1 - _front].volume = t;
            if (t < 1) return;
            _musicSources[_front].Stop();
            _front = 1 - _front;
            _current = _pending;
            _epoch = _switchTime;
            _switching = false;
        }

        public static float SliderDecibels(float value) =>
            value <= 0 ? -80 : Mathf.Max(-80, 20 * Mathf.Log10(Mathf.Clamp01(value) * Mathf.Clamp01(value)));

        public static void SetMasterVolume(float value)
        {
            _master = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterKey, _master);
            if (_instance != null) _instance.ApplyVolumes();
        }
        public static void SetMusicVolume(float value)
        {
            _music = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, _music);
            if (_instance != null) _instance.ApplyVolumes();
        }
        public static void SetEffectsVolume(float value)
        {
            _effects = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(EffectsKey, _effects);
            if (_instance != null) _instance.ApplyVolumes();
        }
        void ApplyVolumes()
        {
            if (!_started || _mixer == null) return;
            _mixer.SetFloat("MasterVolume", SliderDecibels(_master));
            _mixer.SetFloat("MusicVolume", SliderDecibels(_music));
            _mixer.SetFloat("EffectsVolume", SliderDecibels(_effects));
        }

        public static void PlayUi(bool confirm = false)
        {
            if (_instance != null) _instance.Cue(confirm ? "Confirm" : "Ui", true, .65f);
        }
        public static void SyncState()
        {
            if (_instance != null && _instance._mixer != null) _instance.UpdateState();
        }
        public static void PlayCry(string speciesId)
        {
            if (_instance == null || !_instance.isActiveAndEnabled || string.IsNullOrEmpty(speciesId) ||
                AudioSettings.dspTime < _instance._nextCry ||
                !_instance._cries.TryGetValue(speciesId.ToLowerInvariant(), out var clip)) return;
            _instance._nextCry = AudioSettings.dspTime + .25;
            var voice = _instance._crySource;
            voice.Stop(); // Rapid selection replaces the previous cry instead of layering voices.
            voice.clip = clip;
            voice.pitch = 1; // Preserve the actual Pokémon cry.
            voice.volume = .75f;
            voice.Play();
        }
        public static void PlayShot(PokemonElement element)
        {
            if (_instance == null || !GameFlow.Playing || AudioSettings.dspTime < _instance._nextShot) return;
            _instance._nextShot = AudioSettings.dspTime + .045;
            if (_instance._shots.TryGetValue(element, out var clip))
                _instance.Play(clip, false, .35f);
        }
        public static void PlayImpact(PokemonElement element)
        {
            if (_instance == null || !GameFlow.Playing || AudioSettings.dspTime < _instance._nextImpact) return;
            _instance._nextImpact = AudioSettings.dspTime + .06;
            if (_instance._impacts.TryGetValue(element, out var clip))
                _instance.Play(clip, false, .32f);
        }
        void Cue(string name, bool ui, float gain = 1)
        {
            if (_cues.TryGetValue(name, out var clip)) Play(clip, ui, gain);
        }
        void Play(AudioClip clip, bool ui, float gain)
        {
            if (clip == null || !isActiveAndEnabled) return;
            AudioSource[] pool = ui ? _uiSources : _combatSources;
            foreach (var source in pool)
            {
                if (source == null || source == _crySource || source.isPlaying) continue;
                source.clip = clip;
                source.volume = gain;
                source.pitch = ui ? 1 : 1 + ((_variation++ % 5) - 2) * .018f;
                source.Play();
                return;
            }
            // A busy pool drops low-priority sounds instead of allocating or stacking voices.
        }

        static string SoundFamily(string type)
        {
            switch (type)
            {
                case "Fire": case "Electric": case "Water": case "Grass": case "Ice": return type;
                case "Psychic": case "Ghost": case "Dark": case "Fairy": case "Dragon": case "Poison": return "Arcane";
                case "Ground": case "Rock": case "Steel": return "Stone";
                case "Flying": case "Bug": case "Air": return "Air";
                default: return "Normal";
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) ApplyVolumes();
        }
        void OnDestroy()
        {
            if (_rounds != null) _rounds.Changed -= OnRoundChanged;
            if (_instance != this) return;
            _instance = null;
            if (_ownedListener != null) Destroy(_ownedListener);
            AudioListener.volume = _oldListenerVolume;
            AudioListener.pause = _oldListenerPause;
        }
    }
}
