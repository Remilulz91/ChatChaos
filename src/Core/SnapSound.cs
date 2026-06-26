using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatChaos.Core
{
    /// <summary>
    /// Loads and plays the "Thanos snap" sound used by the Claquement de doigts event.
    ///
    /// The audio file is shipped next to the DLL in a "sounds" folder
    /// (BepInEx/plugins/ChatChaos/sounds/thanos_snap.mp3|ogg|wav) and loaded once at
    /// startup. It plays in 2D so every player hears it at the same volume regardless of
    /// where they are. Each machine plays its OWN local copy (the networker just tells
    /// everyone to play at the same moment).
    /// </summary>
    public class SnapSound : MonoBehaviour
    {
        public static SnapSound? Instance { get; private set; }

        private AudioClip? _clip;
        private AudioSource _source = null!;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("ChatChaos_SnapSound");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            Instance = go.AddComponent<SnapSound>();
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;     // 2D
            _source.playOnAwake = false;
            _source.volume = 1f;
            StartCoroutine(LoadClip());
        }

        /// <summary>Plays the snap sound once (no-op if it hasn't loaded).</summary>
        public void Play()
        {
            if (_clip == null || _source == null) return;
            _source.PlayOneShot(_clip);
        }

        private IEnumerator LoadClip()
        {
            string? path = FindSoundFile();
            if (path == null)
            {
                Plugin.Log.LogWarning("[ChatChaos][SnapSound] no 'thanos_snap' file found in the " +
                                      "plugin's 'sounds' folder; the snap will run without sound.");
                yield break;
            }

            var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, TypeFromExt(path));
            ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;  // fully load, safe to dispose
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Plugin.Log.LogWarning($"[ChatChaos][SnapSound] failed to load '{path}': {req.error} " +
                                      "(if it's an .mp3 that won't decode, try converting it to .ogg or .wav).");
                req.Dispose();
                yield break;
            }

            _clip = DownloadHandlerAudioClip.GetContent(req);
            if (_clip != null) _clip.name = "ChatChaos_ThanosSnap";
            req.Dispose();
            Plugin.Log.LogInfo($"[ChatChaos][SnapSound] loaded '{Path.GetFileName(path)}'.");
        }

        private static string? FindSoundFile()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string soundsDir = Path.Combine(dir, "sounds");
                if (!Directory.Exists(soundsDir)) return null;

                foreach (var f in Directory.GetFiles(soundsDir))
                {
                    string name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (name == "thanos_snap" && (ext == ".mp3" || ext == ".ogg" || ext == ".wav"))
                        return f;
                }
            }
            catch { }
            return null;
        }

        private static AudioType TypeFromExt(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".ogg": return AudioType.OGGVORBIS;
                case ".wav": return AudioType.WAV;
                default:     return AudioType.MPEG;   // .mp3
            }
        }
    }
}
