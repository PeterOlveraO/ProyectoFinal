using System.Collections;
using System.Reflection;
using Game.Audio;
using Game.Combat;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class GameAudioTests
    {
        GameObject _host;
        bool _blocked, _over;
        readonly string[] _keys = { "pketer.volumen", "pketer.musica", "pketer.efectos" };
        readonly bool[] _hadKeys = new bool[3];
        readonly float[] _values = new float[3];

        [SetUp]
        public void SetUp()
        {
            _blocked = GameFlow.Blocked; _over = GameFlow.Over;
            for (int i = 0; i < _keys.Length; i++)
            { _hadKeys[i] = PlayerPrefs.HasKey(_keys[i]); _values[i] = PlayerPrefs.GetFloat(_keys[i], 1); }
            GameFlow.Blocked = GameFlow.Over = false;
            _host = new GameObject("Audio integration test");
            GameAudio.EnsureExists(_host);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameAudio.SetMasterVolume(_values[0]);
            GameAudio.SetMusicVolume(_values[1]);
            GameAudio.SetEffectsVolume(_values[2]);
            Object.Destroy(_host);
            yield return null;
            for (int i = 0; i < _keys.Length; i++)
                if (!_hadKeys[i]) PlayerPrefs.DeleteKey(_keys[i]);
            GameFlow.Blocked = _blocked; GameFlow.Over = _over;
        }

        [UnityTest]
        public IEnumerator AllVoicesAreRoutedAndBurstFireDoesNotAllocateMoreSources()
        {
            yield return null;
            var voices = _host.GetComponentsInChildren<AudioSource>();
            Assert.AreEqual(15, voices.Length);
            foreach (var voice in voices) Assert.IsNotNull(voice.outputAudioMixerGroup, voice.name);
            foreach (var voice in voices)
                if (voice.name.StartsWith("Music")) Assert.IsTrue(voice.ignoreListenerPause);
            for (int i = 0; i < 100; i++)
            { GameAudio.PlayShot(PokemonElement.Fire); GameAudio.PlayImpact(PokemonElement.Electric); }
            yield return null;
            Assert.AreEqual(15, _host.GetComponentsInChildren<AudioSource>().Length);
            Assert.IsNotNull(Resources.Load<AudioClip>("Audio/Music/Boss"));
            Assert.IsNotNull(Resources.Load<AudioClip>("Audio/Music/LavenderTown"));
            Assert.IsNotNull(Resources.Load<AudioClip>("Audio/Sfx/ShotFire"));
        }

        [UnityTest]
        public IEnumerator MusicMuteIsIndependentAndQueuedTransitionCanBeCancelled()
        {
            yield return null;
            var mix = Resources.Load<AudioMixer>("Audio/GameMix");
            GameAudio.SetMusicVolume(0);
            GameAudio.SetEffectsVolume(1);
            Assert.IsTrue(mix.GetFloat("MusicVolume", out float music));
            Assert.IsTrue(mix.GetFloat("EffectsVolume", out float effects));
            Assert.AreEqual(-80, music);
            Assert.AreEqual(0, effects);
            var audio = _host.GetComponent<GameAudio>();
            var request = typeof(GameAudio).GetMethod("RequestMusic", BindingFlags.Instance | BindingFlags.NonPublic);
            request.Invoke(audio, new object[] { GameAudio.MusicMood.Boss });
            request.Invoke(audio, new object[] { GameAudio.MusicMood.Menu });
            Assert.IsFalse((bool)typeof(GameAudio).GetField("_switching", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio),
                "Returning to the menu before the next bar must cancel the queued boss music.");
            Assert.AreEqual(GameAudio.MusicMood.Menu, audio.CurrentMood);
        }

        [UnityTest]
        public IEnumerator LavenderContinuesAcrossNormalStatesAndEverySpeciesHasItsOriginalCry()
        {
            yield return null;
            var audio = _host.GetComponent<GameAudio>();
            var request = typeof(GameAudio).GetMethod("RequestMusic", BindingFlags.Instance | BindingFlags.NonPublic);
            var sources = (AudioSource[])typeof(GameAudio).GetField("_musicSources",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            var clip = Resources.Load<AudioClip>("Audio/Music/LavenderTown");
            Assert.AreSame(clip, sources[0].clip);
            var epoch = typeof(GameAudio).GetField("_epoch", BindingFlags.Instance | BindingFlags.NonPublic);
            double start = (double)epoch.GetValue(audio);
            request.Invoke(audio, new object[] { GameAudio.MusicMood.Journey });
            request.Invoke(audio, new object[] { GameAudio.MusicMood.Battle });
            Assert.AreSame(clip, sources[0].clip);
            Assert.AreEqual(start, epoch.GetValue(audio), "Normal state changes must not restart Lavender Town.");
            Assert.IsFalse((bool)typeof(GameAudio).GetField("_switching",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio));
            var catalog = JsonUtility.FromJson<Game.Pokemon.Progression.SpeciesCatalog>(
                Resources.Load<TextAsset>("PokemonProgression/catalog").text);
            foreach (var species in catalog.species)
                Assert.IsNotNull(Resources.Load<AudioClip>("Audio/Cries/" + species.id), species.id);
            GameAudio.PlayCry("jolteon");
            var cry = (AudioSource)typeof(GameAudio).GetField("_crySource",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            Assert.AreEqual("jolteon", cry.clip.name);
            Assert.AreEqual(1f, cry.pitch);
            Assert.IsTrue(cry.ignoreListenerPause, "Acquisition and inventory work while gameplay is paused.");
            Assert.AreEqual(15, _host.GetComponentsInChildren<AudioSource>().Length);
        }
    }
}
