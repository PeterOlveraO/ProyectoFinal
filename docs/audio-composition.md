# Game audio

The final soundtrack uses **the original Lavender Town theme from Pokémon Red** as
its predominant score, plus the original cries for all 28 species. Source attribution,
hashes and reproducible import are documented in [pokemon-audio.md](pokemon-audio.md).

The four previously composed tracks and 24 elemental/UI cues are synthesized specifically
for this project. Their reproducible source is `Tools/Audio/generate_audio.py`, using
Python's standard library and FFmpeg. The Boss composition remains the exceptional
combat transition; Menu/Journey/Battle are retained as fallbacks.

## Synthesized music and fallbacks

| Clip | Intent and arrangement | Format / duration | Measured loudness |
|---|---|---|---|
| `Music/Menu.ogg` | Warm, sparse C/G/Am/F loop; rounded pulse melody, soft broken-chord bed, no full drum kit. Relaxed and welcoming. | Mono, 44.1 kHz, 16 bars / 32.0 s | -19.0 LUFS-I, -7.7 dBFS peak |
| `Music/Journey.ogg` | Brighter G/D/Em/C progression; lilting stepwise melody, soft kick and restrained hat. | Mono, 44.1 kHz, 16 bars / 32.0 s | -19.0 LUFS-I, -8.1 dBFS peak |
| `Music/Battle.ogg` | Minor-key Am/F/C/G cycle; syncopated short lead notes, firm triangle bass, quiet kick/snare/noise hats. | Mono, 44.1 kHz, 16 bars / 32.0 s | -18.9 LUFS-I, -6.1 dBFS peak |
| `Music/Boss.ogg` | Tighter Am/G/F/E7 harmony; faster-feeling subdivisions, high-register motif and slightly denser percussion, with a leading-tone cadence. | Mono, 44.1 kHz, 16 bars / 32.0 s | -19.0 LUFS-I, -6.3 dBFS peak |

Each loop is 120 BPM, 4/4 (a 2-second bar), with 4-bar A and B phrases, a varied A
return and a B turnaround. The same pulse/triangle/sine/noise instrument family and C-based
pitch vocabulary keep transitions related while harmony, register, melodic rhythm and drum
density distinguish the scenes. A short edge taper helps avoid playback/codec clicks. The
identical tempo and bar duration let `GameAudio` schedule changes on the next 2-second
bar boundary, with a 0.65-second complementary crossfade.

## Sound effects

All effects are mono 44.1 kHz, 16-bit PCM WAV. Peaks below are measured sample peaks with
FFmpeg `volumedetect`; every clip stays at or below -10 dBFS. UI sounds are deliberately
quieter. The elemental shot/impact pairs use distinct sweeps, pitch registers, pulse/noise
textures and decays, rather than the same beep with a new filename. Repeated combat effects
use bounded runtime pitch variation (±3.6%).

| Exact filename | Duration | Peak |
|---|---:|---:|
| `Ui.wav` | 0.10 s | -16.0 dBFS |
| `Confirm.wav` | 0.28 s | -14.0 dBFS |
| `Purchase.wav` | 0.52 s | -14.0 dBFS |
| `Hurt.wav` | 0.34 s | -12.0 dBFS |
| `Clear.wav` | 1.48 s | -12.0 dBFS |
| `Defeat.wav` | 1.65 s | -12.2 dBFS |
| `ShotFire.wav` / `ImpactFire.wav` | 0.34 / 0.27 s | -12.1 / -11.0 dBFS |
| `ShotElectric.wav` / `ImpactElectric.wav` | 0.34 / 0.27 s | -13.0 / -11.0 dBFS |
| `ShotWater.wav` / `ImpactWater.wav` | 0.34 / 0.27 s | -12.9 / -11.0 dBFS |
| `ShotGrass.wav` / `ImpactGrass.wav` | 0.34 / 0.27 s | -11.0 / -12.4 dBFS |
| `ShotIce.wav` / `ImpactIce.wav` | 0.34 / 0.27 s | -13.7 / -11.2 dBFS |
| `ShotArcane.wav` / `ImpactArcane.wav` | 0.34 / 0.27 s | -13.5 / -12.3 dBFS |
| `ShotStone.wav` / `ImpactStone.wav` | 0.34 / 0.27 s | -11.0 / -11.0 dBFS |
| `ShotAir.wav` / `ImpactAir.wav` | 0.34 / 0.27 s | -11.0 / -12.8 dBFS |
| `ShotNormal.wav` / `ImpactNormal.wav` | 0.34 / 0.27 s | -12.1 / -12.8 dBFS |

## Rebuild and measurement

From the repository root:

```sh
python3 Tools/Audio/generate_audio.py
```

The script creates four temporary 44.1 kHz PCM music renders, normalizes/encodes them as
Vorbis OGG with FFmpeg, and writes the named SFX WAVs. It uses fixed seeds for every noise
source. To recheck music loudness and true peak:

```sh
ffmpeg -hide_banner -nostats -i Assets/Resources/Audio/Music/Menu.ogg \
  -af ebur128=peak=true:framelog=quiet -f null -
```

The supplied OGGs measured -19.0 to -18.9 LUFS integrated and -8.1 to -6.1 dBFS peak
with FFmpeg 9.0.1. These peaks leave extra headroom below the requested -3 dBFS ceiling.
SFX values above were measured with FFmpeg 9.0.1 `volumedetect`.

## Runtime integration and verification

- `GameAudio` keeps Lavender Town playing through menu, preparation, ordinary combat and
  defeat, without restarting it when moving between screens. Only boss combat switches
  to the synthesized Boss score. Transitions use each outgoing score's bar duration and
  a 0.65-second crossfade; pending changes can be cancelled.
- `GameMix.mixer` separates Master, Music/MusicDuck, and Effects/Combat/UI. The three sliders
  persist under `pketer.volumen`, `pketer.musica` and `pketer.efectos`.
- Pausing stops combat voices individually. `AudioListener.pause` remains false so the DSP
  clock, score and UI audio continue; pause and end-of-round cues gently duck the music.
- The scene has one enabled listener and at most 15 sources: two score voices, ten combat
  voices, two UI voices and one reserved Pokémon-cry voice. Selection replaces the prior cry
  rather than stacking voices; original cries retain their pitch and timing.
- `GameAudioImport` imports the scores as streaming Vorbis and short effects/cries as
  decompressed PCM. Its rules apply only to `Assets/Resources/Audio/`.
- Final runtime checks confirmed all mixer routes, independent music mute, transition
  cancellation, Boss selection, combat-only pause and all 18 original cries.
- A 120-second recording of the final Editor speaker output, with Lavender Town and the
  Pokémon cries, measured **−22.2 LUFS-I / −9.3 dBTP**, without clipping.
