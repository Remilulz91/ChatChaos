ChatChaos — sounds folder
=========================

Put the Thanos snap audio here, named exactly:

    thanos_snap.mp3      (or thanos_snap.ogg / thanos_snap.wav)

The mod loads the first file named "thanos_snap" with one of those extensions and
plays it when the "Claquement de doigts" (Snap) event triggers, just before the
players/enemies are disintegrated.

Format note:
- .ogg or .wav load the most reliably at runtime in Unity.
- .mp3 usually works too, but on some setups Unity fails to decode it at runtime.
  If you see "[ChatChaos][SnapSound] failed to load ..." in the BepInEx log, convert
  the file to .ogg or .wav (free tools / Audacity) and drop that in instead.

This whole folder is packaged next to the DLL by the GitHub build, so it ends up at:
    BepInEx/plugins/ChatChaos/sounds/thanos_snap.mp3

Timing: the delay between the sound starting and the disintegration is configurable
in the mod's .cfg -> [Events] SnapSoundDelay (default 1.5s). Tune it so the effect
lands right on the "snap" in your audio file.
