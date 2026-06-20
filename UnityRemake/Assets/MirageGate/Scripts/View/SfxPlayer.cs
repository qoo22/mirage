using UnityEngine;

namespace MirageGate.View
{
    /// <summary>
    /// 効果音（§8.3）。原作WebAudioの合成SEを踏襲し、波形を実行時生成（音源ファイル不要）。
    /// GameFeelDirector.sfx に割り当てると命中/撃破/被弾で鳴る。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SfxPlayer : MonoBehaviour
    {
        const int SR = 44100;
        AudioSource _src;
        AudioClip _hit, _crit, _kill, _killBoss, _hurt, _step;

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.playOnAwake = false;
            // 命中：高めの短い斬撃音（800→220Hz・軽いノイズ）
            _hit = Sweep("sfx_hit", 800, 220, 0.10f, noise: 0.25f, decay: 22f);
            // 会心：明るく長め（1300→320Hz）
            _crit = Sweep("sfx_crit", 1300, 320, 0.16f, noise: 0.30f, decay: 16f);
            // 撃破：低い「ドンッ」（240→70Hz）＋ノイズ
            _kill = Sweep("sfx_kill", 240, 70, 0.18f, noise: 0.45f, decay: 14f);
            _killBoss = Sweep("sfx_killboss", 180, 50, 0.30f, noise: 0.55f, decay: 9f);
            // 被弾：下降トーン（420→120Hz）
            _hurt = Sweep("sfx_hurt", 420, 120, 0.14f, noise: 0.20f, decay: 18f);
            // 歩行：ごく短いクリック
            _step = Sweep("sfx_step", 200, 160, 0.04f, noise: 0.15f, decay: 40f);
        }

        public void Hit(bool crit) => Play(crit ? _crit : _hit, crit ? 0.7f : 0.55f);
        public void Kill(bool boss) => Play(boss ? _killBoss : _kill, 0.8f);
        public void Hurt() => Play(_hurt, 0.6f);
        public void Step() => Play(_step, 0.25f);

        void Play(AudioClip c, float vol)
        {
            if (c != null && _src != null) _src.PlayOneShot(c, vol);
        }

        /// <summary>周波数スイープ＋ノイズ＋指数減衰の短いSEを合成。</summary>
        static AudioClip Sweep(string name, float f0, float f1, float dur, float noise, float decay)
        {
            int n = Mathf.Max(1, (int)(SR * dur));
            var data = new float[n];
            float phase = 0f;
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;                 // 0..1
                float f = Mathf.Lerp(f0, f1, u);
                phase += 2f * Mathf.PI * f / SR;
                float env = Mathf.Exp(-decay * u);      // 指数減衰
                float tone = Mathf.Sin(phase);
                float nz = (float)(rng.NextDouble() * 2.0 - 1.0) * noise;
                data[i] = Mathf.Clamp((tone + nz) * env * 0.7f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
