using System.Collections.Generic;
using UnityEngine;

namespace MirageGate.View
{
    /// <summary>
    /// 原作の背景除去済みスプライト（Resources/mon, Resources/hero）をモンスター名/職業idから引く。
    /// 2DプロジェクトではPNGはSprite型で取り込まれるため Resources.Load&lt;Sprite&gt; で読める。
    /// 該当が無ければ null（呼び出し側は色分けにフォールバック）。
    /// </summary>
    public static class SpriteLibrary
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // 原作 MON_SPRITE（日本語名→t_mon_<slug>）
        static readonly Dictionary<string, string> MonSlug = new Dictionary<string, string>
        {
            {"プーニャ緑","slime"},{"プーニャ黄","slime"},{"リザード","lizard"},{"ベノム","venom"},
            {"キラービー","bee"},{"キラービー群","bee"},{"マザービー","queenbee"},{"ケムンパ","caterpillar"},
            {"スピット","spit"},{"ホッピー","hoppy"},{"キラーホッピー","hoppy"},{"スケルトン","skeleton"},
            {"死神","reaper"},{"魔道士","mage"},{"プリースト","priest"},{"ビショップ","priest"},
            {"リスタール","golem"},{"エビルアイ","evileye"},{"ドラゴン","dragon"},{"グランシャーク","shark"},
            {"リスタドラゴン","darkdragon"},{"キングドラゴン","kingdragon"},{"カオス","chaos"},{"ゴブリン","goblin"},
            {"バルチャー","vulture"},{"ターミネーター","robot"},{"マッドブル","madbull"},{"デーモン","demon"},
            {"ミミック","mimic"},{"レイス","wraith"},{"クリスタルゴーレム","crystalgolem"},{"幻影のオーブ","orb"},
        };

        // 城主ボス（名前→職業id・立ち絵はheroを流用）
        static readonly Dictionary<string, string> _lordJob = new Dictionary<string, string>();
        public static void RegisterLord(string monsterName, string jobId) { if (!string.IsNullOrEmpty(monsterName)) _lordJob[monsterName] = jobId; }

        public static Sprite Monster(string jpName)
        {
            if (jpName == null) return null;
            if (_lordJob.TryGetValue(jpName, out var ljob)) return Hero(ljob);
            return MonSlug.TryGetValue(jpName, out var slug) ? Load("mon/" + slug) : null;
        }

        /// <summary>職業id→hero slug（mageのみwizard、他はid＝slug）。</summary>
        public static Sprite Hero(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) return null;
            string slug = jobId == "mage" ? "wizard" : jobId;
            return Load("hero/" + slug);
        }

        /// <summary>方向アニメ（side視点）：state= idle/walk/attack。Resources/anim/&lt;slug&gt;_&lt;state&gt;。無ければnull。</summary>
        public static Sprite MonsterAnim(string jpName, string state)
        {
            if (jpName == null) return null;
            if (_lordJob.TryGetValue(jpName, out var ljob)) return HeroAnim(ljob, state);
            return MonSlug.TryGetValue(jpName, out var slug) ? Load($"anim/{slug}_{state}") : null;
        }

        public static Sprite HeroAnim(string jobId, string state)
        {
            if (string.IsNullOrEmpty(jobId)) return null;
            string slug = jobId == "mage" ? "wizard" : jobId;
            return Load($"anim/{slug}_{state}");
        }

        static Sprite Load(string path)
        {
            if (_cache.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(path);
            _cache[path] = s;
            return s;
        }
    }
}
