using UnityEngine;

namespace MirageGate.View
{
    /// <summary>
    /// BGM（手続き合成・音源ファイル不要）。穏やかなマイナー調のパッド＋アルペジオの8秒ループ。
    /// 原作のWebAudio合成BGMの思想を踏襲。音量はInspectorで調整可、不要ならcomponentを無効化。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        const int SR = 44100;
        [Range(0f, 1f)] public float volume = 0.32f;
        public bool playOnStart = true;
        AudioSource _src;

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.loop = true; _src.playOnAwake = false; _src.volume = volume;
            _src.clip = BuildLoop();
        }

        void Start() { if (playOnStart) _src.Play(); }

        public void SetVolume(float v) { volume = v; if (_src != null) _src.volume = v; }

        /// <summary>Am→F→C→G の各2秒・パッド＋やわらかアルペジオ。端をクロスフェードしてシームレスループ。</summary>
        AudioClip BuildLoop()
        {
            float dur = 8f;
            int n = (int)(SR * dur);
            var data = new float[n];
            float[][] chords = {
                new[] { 220f, 261.63f, 329.63f },   // Am
                new[] { 174.61f, 220f, 261.63f },   // F
                new[] { 261.63f, 329.63f, 392f },   // C
                new[] { 196f, 246.94f, 392f },      // G
            };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                int ci = (int)(t / 2f) % 4;
                var ch = chords[ci];
                float s = 0f;
                for (int k = 0; k < ch.Length; k++) s += Mathf.Sin(2f * Mathf.PI * ch[k] * t) * 0.11f; // パッド
                float arpF = ch[(int)(t * 2f) % ch.Length] * 2f;                                       // アルペジオ(1オクターブ上)
                s += Mathf.Sin(2f * Mathf.PI * arpF * t) * 0.05f * (0.5f + 0.5f * Mathf.Sin(t * 5.5f));
                s += Mathf.Sin(2f * Mathf.PI * ch[0] * 0.5f * t) * 0.06f;                               // 低音ドローン
                data[i] = Mathf.Clamp(s, -1f, 1f) * 0.7f;
            }
            // ループ継ぎ目をなめらかに（先頭/末尾0.15秒をクロスフェード）
            int fade = (int)(SR * 0.15f);
            for (int i = 0; i < fade; i++)
            {
                float a = (float)i / fade;
                data[i] = data[i] * a + data[n - fade + i] * (1f - a);
            }
            var clip = AudioClip.Create("bgm_loop", n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
