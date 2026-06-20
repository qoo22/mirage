using System.Collections.Generic;
using UnityEngine;
using MirageGate.Runtime;
using MirageGate.View;

namespace MirageGate.Systems
{
    /// <summary>浮遊ダメージ数字（§8.3）。</summary>
    public class DamagePopup
    {
        public float worldX, worldY;
        public string text;
        public Color color;
        public bool big;
        public float startRealtimeMs;
        public float DurMs => big ? 760f : 620f;
    }

    /// <summary>
    /// 手応え演出の司令塔（§8）— 本作の生命線。ヒットストップ・画面揺れ・ダメージ数字を司る。
    /// 全て「Visual Only」で論理座標に干渉しない（§1/§8.5）。
    /// ヒットストップは Time.timeScale=0 ではなく「ゲーム内時計の凍結」で実装し、
    /// SE/揺れ/FXは realtime（unscaled）で駆動＝凍結中も鳴り・揺れる。
    /// </summary>
    public class GameFeelDirector : MonoBehaviour
    {
        [Header("ヒットストップ ms（§8.3）")]
        public int hsHitNormal = 120, hsHitCrit = 260;
        public int hsKillBoss = 320, hsKillMob = 150;
        public int hsHurtMelee = 160, hsHurtRanged = 280, hsAllyHurt = 120;

        [Header("画面揺れ amp（§8.3）")]
        public float ampNormalHit = 0.5f, ampCritHit = 1.0f;
        public float ampKillMob = 1.0f, ampKillBoss = 2.0f, ampHurt = 0.5f;
        [Tooltip("hold = max(0,(amp-0.5)*holdPerAmp) ms")] public float holdPerAmp = 30f;
        [Tooltip("decay = decayBase + amp*decayPerAmp ms")] public float decayBase = 200f, decayPerAmp = 80f;
        [Tooltip("揺れ最大幅（ワールド単位/amp）。タイル1.0に対する割合の目安")] public float shakeWorldPerAmp = 0.08f;

        [Header("ダメージ数字色")]
        public Color colCrit = new Color(1f, 0.95f, 0.63f);
        public Color colNormal = Color.white;
        public Color colHurt = new Color(1f, 0.48f, 0.42f);
        public Color colHeal = new Color(0.6f, 0.94f, 0.69f);

        // ---- ゲーム内時計（凍結対応, §8.3）----
        float frozenAt, offset, hsEnd;
        public float Now() { float t = RT(); return t < hsEnd ? frozenAt : t - offset; }
        public bool IsHitstopActive => RT() < hsEnd;
        static float RT() => Time.realtimeSinceStartup * 1000f;

        public void Hitstop(int ms)
        {
            float t = RT();
            if (t < hsEnd) { offset += (t + ms) - hsEnd; hsEnd = t + ms; } // 延長
            else { frozenAt = t - offset; offset += ms; hsEnd = t + ms; }
        }

        // ---- 画面揺れ ----
        float shakeStart = -100000f, shakeAmp;
        public void Shake(float amp)
        {
            // 既存揺れが残っていれば強い方を採用（重ねがけで弱くならない）
            if (RT() < shakeStart + ShakeDurMs(shakeAmp) && amp < shakeAmp) return;
            shakeStart = RT(); shakeAmp = amp;
        }

        float ShakeDurMs(float amp) => Mathf.Max(0, (amp - 0.5f) * holdPerAmp) + (decayBase + amp * decayPerAmp);

        /// <summary>CameraRigが毎フレーム参照する揺れオフセット（ワールド単位）。</summary>
        public Vector2 ShakeOffset()
        {
            float se = RT() - shakeStart;
            float hold = Mathf.Max(0, (shakeAmp - 0.5f) * holdPerAmp);
            float decay = decayBase + shakeAmp * decayPerAmp;
            if (se < 0 || se >= hold + decay) return Vector2.zero;
            float k = se < hold ? 1f : 1f - (se - hold) / decay;
            float mag = shakeAmp * shakeWorldPerAmp * k;
            return new Vector2(Random.Range(-1f, 1f) * mag, Random.Range(-1f, 1f) * mag);
        }

        [Header("敵けぞり hitOff（§8.3）")]
        [Tooltip("最大ノックバック幅（ワールド単位/mag）")] public float recoilWorldPerMag = 0.16f;
        [Tooltip("減衰振動の往復回数")] public float recoilCycles = 4.2f;
        [Tooltip("白フラッシュの持続割合（被弾直後）")] public float flashFrac = 0.35f;

        [Header("外部参照")]
        public SfxPlayer sfx;          // 任意。割り当てがあればSEを鳴らす
        public SlashFxPlayer slashFx;  // 任意。割り当てがあれば斬撃FXを出す

        // 被弾リアクション（敵・プレイヤー共通モデル）
        class HitReaction { public float start, dirX, dirY, mag, durMs; }
        readonly Dictionary<MonsterInstance, HitReaction> _hits = new Dictionary<MonsterInstance, HitReaction>();
        HitReaction _playerHit; // プレイヤーのけぞり（単一）

        /// <summary>撃破フラッシュ＋崩壊（§8.3 killflash/fxDie）。</summary>
        public class KillFlash { public float worldX, worldY, start, durMs; public bool big; public string monsterName; public int faceX; }
        public readonly List<KillFlash> KillFlashes = new List<KillFlash>();

        // ---- 演出トリガ（CombatResolverから呼ばれる）----
        /// <summary>(dirX,dirY)=被弾方向（敵 - 攻撃者）。</summary>
        public void OnPlayerHit(MonsterInstance m, bool crit, float dirX, float dirY)
        {
            Hitstop(crit ? hsHitCrit : hsHitNormal);
            Shake(crit ? ampCritHit : ampNormalHit);
            float mag = (crit ? 1.7f : 1.0f) * (m.IsBoss ? 0.7f : 1.0f); // 重い敵ほど小さく
            float len = Mathf.Sqrt(dirX * dirX + dirY * dirY); if (len < 0.001f) { dirX = 0; dirY = 1; len = 1; }
            _hits[m] = new HitReaction
            {
                start = RT(), dirX = dirX / len, dirY = dirY / len, mag = mag,
                durMs = Mathf.Min(1100f, 400f + 400f * Mathf.Max(0, mag - 0.6f)) * 1f
            };
            if (sfx != null) sfx.Hit(crit);
            // 斬撃FX5種からランダム（§8.1 arc/thrust/sweep/double/chop）
            if (slashFx != null) slashFx.Play(m.x, m.y, dirX, dirY, crit, Random.Range(0, 5));
        }
        public void OnKill(MonsterInstance m)
        {
            Hitstop(m.IsBoss ? hsKillBoss : hsKillMob);
            Shake(m.IsBoss ? ampKillBoss : ampKillMob);
            KillFlashes.Add(new KillFlash { worldX = m.x, worldY = m.y, start = RT(), durMs = m.IsBoss ? 620 : 420, big = m.IsBoss,
                monsterName = m.data != null ? m.data.monsterName : null, faceX = 1 });
            _hits.Remove(m);
            if (sfx != null) sfx.Kill(m.IsBoss);
        }
        /// <summary>(dirX,dirY)=被弾方向（プレイヤー - 敵）。dmg量でけぞり振幅段階化（§8.3）。</summary>
        public void OnPlayerDamaged(int dmg, bool ranged, float dirX = 0, float dirY = 1)
        {
            Hitstop(ranged ? hsHurtRanged : hsHurtMelee);
            Shake(ampHurt);
            if (sfx != null) sfx.Hurt();
            float ampScale = dmg <= 10 ? 0.5f : dmg <= 20 ? 0.667f : 1.0f;
            float len = Mathf.Sqrt(dirX * dirX + dirY * dirY); if (len < 0.001f) { dirX = 0; dirY = 1; len = 1; }
            _playerHit = new HitReaction
            {
                start = RT(), dirX = dirX / len, dirY = dirY / len, mag = ampScale,
                durMs = 360f + 240f * ampScale
            };
        }

        /// <summary>プレイヤースプライトの被弾オフセット（§8.3）。</summary>
        public Vector2 PlayerHitOffset()
        {
            if (_playerHit == null) return Vector2.zero;
            float t = (RT() - _playerHit.start) / _playerHit.durMs;
            if (t < 0 || t >= 1) return Vector2.zero;
            float amp = recoilWorldPerMag * 1.3f * _playerHit.mag;
            float osc = Mathf.Sin(2f * Mathf.PI * recoilCycles * t) * Mathf.Exp(-3.2f * t);
            float d = amp * osc;
            return new Vector2(_playerHit.dirX * d, -_playerHit.dirY * d);
        }

        public float PlayerFlashAlpha()
        {
            if (_playerHit == null) return 0f;
            float t = (RT() - _playerHit.start) / _playerHit.durMs;
            if (t < 0 || t >= flashFrac) return 0f;
            return 1f - t / flashFrac;
        }

        /// <summary>敵スプライトの被弾オフセット（減衰振動・ワールド単位, §8.3 hitOff）。</summary>
        public Vector2 HitOffset(MonsterInstance m)
        {
            if (!_hits.TryGetValue(m, out var h)) return Vector2.zero;
            float t = (RT() - h.start) / h.durMs;
            if (t < 0 || t >= 1) return Vector2.zero;
            float amp = recoilWorldPerMag * (0.48f + 0.68f * Mathf.Min(1.7f, h.mag));
            float osc = Mathf.Sin(2f * Mathf.PI * recoilCycles * t) * Mathf.Exp(-3.2f * t);
            float d = amp * osc;
            return new Vector2(h.dirX * d, -h.dirY * d); // y下方向グリッド→ワールドyは反転
        }

        /// <summary>敵の白フラッシュ強度 0..1（被弾直後, §8.3）。</summary>
        public float HitFlashAlpha(MonsterInstance m)
        {
            if (!_hits.TryGetValue(m, out var h)) return 0f;
            float t = (RT() - h.start) / h.durMs;
            if (t < 0 || t >= flashFrac) return 0f;
            return 1f - t / flashFrac;
        }

        // ---- ダメージ数字 ----
        public readonly List<DamagePopup> Popups = new List<DamagePopup>();
        public void PopDamage(int gridX, int gridY, int dmg, bool crit, bool heal = false)
        {
            Popups.Add(new DamagePopup
            {
                worldX = gridX, worldY = gridY,
                text = (heal ? "+" : "-") + Mathf.Abs(dmg),
                color = heal ? colHeal : crit ? colCrit : dmg < 0 ? colHurt : colNormal,
                big = crit, startRealtimeMs = RT()
            });
        }

        void Update()
        {
            float now = RT();
            for (int i = Popups.Count - 1; i >= 0; i--)
                if (now - Popups[i].startRealtimeMs >= Popups[i].DurMs) Popups.RemoveAt(i);
            for (int i = KillFlashes.Count - 1; i >= 0; i--)
                if (now - KillFlashes[i].start >= KillFlashes[i].durMs) KillFlashes.RemoveAt(i);
            // 期限切れのけぞりを掃除
            if (_hits.Count > 0)
            {
                _expired.Clear();
                foreach (var kv in _hits) if (now - kv.Value.start >= kv.Value.durMs) _expired.Add(kv.Key);
                foreach (var k in _expired) _hits.Remove(k);
            }
        }
        readonly List<MonsterInstance> _expired = new List<MonsterInstance>();
    }
}
