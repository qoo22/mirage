using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using MirageGate.Core;
using MirageGate.Data;
using MirageGate.Systems;

namespace MirageGate.EditorTools
{
    /// <summary>
    /// 設計図の表(CSV) → ScriptableObjectアセットを一括生成するエディタ拡張。
    /// メニュー "MirageGate/Import Data" 配下。CSVは Assets/MirageGate/EditorData/ に置く。
    /// 既存アセットがあれば値を更新（id/名前で照合）、無ければ新規作成。
    /// </summary>
    public static class DataImporter
    {
        const string CsvDir = "Assets/MirageGate/EditorData";
        const string SoRoot = "Assets/MirageGate/ScriptableObjects";

        [MenuItem("MirageGate/Import Data/① Monsters", priority = 1)]
        public static void ImportMonsters()
        {
            int n = 0;
            foreach (var row in Load("monsters.csv"))
            {
                var name = row["monsterName"];
                var so = GetOrCreate<MonsterData>($"{SoRoot}/Monsters/Monster_{Safe(name)}.asset");
                so.monsterName = name;
                so.emoji = Get(row, "emoji");
                so.hp = Int(row, "hp"); so.atk = Int(row, "atk"); so.def = Int(row, "def");
                so.role = Enum<MonsterRole>(Get(row, "role"));
                so.pay = Int(row, "pay");
                so.cap = new[] { Int(row, "cap0"), Int(row, "cap1"), Int(row, "cap2") };
                so.minDmg = row.ContainsKey("minDmg") && !string.IsNullOrEmpty(row["minDmg"]) ? Int(row, "minDmg") : -1;
                so.isBoss = Bool(row, "isBoss");
                EditorUtility.SetDirty(so); n++;
            }
            Finish($"Monsters: {n}体");
        }

        [MenuItem("MirageGate/Import Data/② Cards", priority = 2)]
        public static void ImportCards()
        {
            int n = 0;
            foreach (var row in Load("cards.csv"))
            {
                var id = row["id"];
                var so = GetOrCreate<CardData>($"{SoRoot}/Cards/Card_{Safe(id)}.asset");
                so.id = id;
                so.cardName = Get(row, "name");
                so.icon = Get(row, "icon");
                so.category = Enum<CardCategory>(Get(row, "category"));
                so.desc = Get(row, "desc");
                so.cost = Int(row, "cost");
                so.mp = Int(row, "mp");
                ApplyEffects(so, Get(row, "effects")); // セミコロン区切り key:value をリフレクションで適用
                so.target = GuessTarget(so);
                EditorUtility.SetDirty(so); n++;
            }
            Finish($"Cards: {n}枚");
        }

        [MenuItem("MirageGate/Import Data/③ Dungeons", priority = 3)]
        public static void ImportDungeons()
        {
            var monsters = IndexByName<MonsterData>();
            int n = 0;
            foreach (var row in Load("dungeons.csv"))
            {
                var id = row["id"];
                var so = GetOrCreate<DungeonData>($"{SoRoot}/Dungeons/Dungeon_{Safe(id)}.asset");
                so.id = id;
                so.jpName = Get(row, "jpName");
                so.star = Int(row, "star"); so.floors = Int(row, "floors");
                so.size = Int(row, "size"); so.rooms = Int(row, "rooms");
                so.dense = Float(row, "dense"); so.spawn = Float(row, "spawn");
                so.bet = Int(row, "bet"); so.win = Int(row, "win");
                so.isTutorial = Bool(row, "isTutorial");
                so.hidden = Bool(row, "hidden");
                so.bagLimit = Int(row, "bagLimit");
                so.autoDeepen = Bool(row, "autoDeepen");
                so.gimIce = Float(row, "gimIce"); so.gimPoison = Float(row, "gimPoison");
                so.gimFire = Float(row, "gimFire"); so.gimCurse = Float(row, "gimCurse");

                // bands: フロアを '|'、敵を ',' で区切り。名前→MonsterData参照を解決
                so.bands = new List<FloorBand>();
                var bandsRaw = Get(row, "bands");
                if (!string.IsNullOrEmpty(bandsRaw))
                {
                    foreach (var floor in bandsRaw.Split('|'))
                    {
                        var band = new FloorBand();
                        foreach (var mn in floor.Split(','))
                        {
                            var key = mn.Trim();
                            if (key.Length == 0) continue;
                            if (monsters.TryGetValue(key, out var md)) band.monsters.Add(md);
                            else Debug.LogWarning($"[Import] Dungeon {id}: モンスター '{key}' が見つかりません（先にMonstersを取り込んで下さい）");
                        }
                        so.bands.Add(band);
                    }
                }
                EditorUtility.SetDirty(so); n++;
            }
            Finish($"Dungeons: {n}個");
        }

        [MenuItem("MirageGate/Import Data/④ Jobs", priority = 4)]
        public static void ImportJobs()
        {
            int n = 0;
            foreach (var row in Load("jobs.csv"))
            {
                var id = row["id"];
                var so = GetOrCreate<JobData>($"{SoRoot}/Jobs/Job_{Safe(id)}.asset");
                so.id = id;
                so.jpName = Get(row, "jpName"); so.emoji = Get(row, "emoji");
                so.hp = Int(row, "hp"); so.mp = Int(row, "mp");
                so.atk = Int(row, "atk"); so.def = Int(row, "def"); so.sight = Int(row, "sight");
                so.capIndex = Int(row, "capIndex");
                so.potBonus = Float(row, "potBonus"); so.magBonus = Float(row, "magBonus");
                so.crit = Float(row, "crit"); so.luck = Bool(row, "luck"); so.mpHalf = Bool(row, "mpHalf");
                so.hidden = Bool(row, "hidden"); so.description = Get(row, "description");
                EditorUtility.SetDirty(so); n++;
            }
            Finish($"Jobs: {n}職");
        }

        [MenuItem("MirageGate/Import Data/★ Import ALL", priority = 0)]
        public static void ImportAll()
        {
            ImportMonsters(); // dungeons が参照するので先
            ImportCards();
            ImportDungeons();
            ImportJobs();
            RebuildDatabase();
            Debug.Log("[MirageGate] 全データの取り込み完了。");
        }

        [MenuItem("MirageGate/Rebuild GameDatabase", priority = 20)]
        public static void RebuildDatabase()
        {
            var db = GetOrCreate<GameDatabase>($"{SoRoot}/GameDatabase.asset");
            db.balance = FindFirst<GameBalanceConfig>() ?? db.balance;
            db.jobs = FindAll<JobData>();
            db.cards = FindAll<CardData>();
            db.monsters = FindAll<MonsterData>();
            db.dungeons = FindAll<DungeonData>();
            db.statusEffects = FindAll<StatusEffectData>();
            db.campaigns = FindAll<CampaignData>();
            EditorUtility.SetDirty(db);
            // balance未作成なら自動生成
            if (db.balance == null)
            {
                var cfg = GetOrCreate<GameBalanceConfig>($"{SoRoot}/GameBalanceConfig.asset");
                db.balance = cfg; EditorUtility.SetDirty(db);
            }
            Finish("GameDatabase 再構築");
        }

        // ---------- 効果フィールドの汎用適用（リフレクション）----------
        static void ApplyEffects(CardData card, string effects)
        {
            if (string.IsNullOrEmpty(effects)) return;
            foreach (var tokenRaw in effects.Split(';'))
            {
                var token = tokenRaw.Trim();
                if (token.Length == 0) continue;
                string key, val;
                int colon = token.IndexOf(':');
                if (colon >= 0) { key = token.Substring(0, colon).Trim(); val = token.Substring(colon + 1).Trim(); }
                else { key = token; val = "true"; } // bool フラグ

                var f = typeof(CardData).GetField(key, BindingFlags.Public | BindingFlags.Instance);
                if (f == null) { Debug.LogWarning($"[Import] Card {card.id}: 不明な効果フィールド '{key}'"); continue; }
                try { f.SetValue(card, Convert(f.FieldType, val)); }
                catch (Exception e) { Debug.LogWarning($"[Import] Card {card.id}: '{key}={val}' 変換失敗: {e.Message}"); }
            }
        }

        static object Convert(Type t, string v)
        {
            if (t == typeof(int)) return int.Parse(v, CultureInfo.InvariantCulture);
            if (t == typeof(float)) return float.Parse(v, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return v == "1" || v.ToLower() == "true";
            return v; // string
        }

        static TargetKind GuessTarget(CardData c)
        {
            if (c.multi) return TargetKind.AllVisible;
            if (c.spear > 0) return TargetKind.Line;
            if (c.category == CardCategory.Attack) return TargetKind.SingleEnemy;
            if (c.bright || c.map || c.search || c.silent) return TargetKind.Floor;
            return TargetKind.Self;
        }

        // ---------- ユーティリティ ----------
        static List<Dictionary<string, string>> Load(string file)
        {
            var path = Path.Combine(Application.dataPath, "MirageGate/EditorData", file);
            if (!File.Exists(path)) { Debug.LogError($"[Import] CSVが見つかりません: {path}"); return new List<Dictionary<string, string>>(); }
            return CsvUtil.ParseWithHeader(File.ReadAllText(path));
        }

        static T GetOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var so = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (so == null) { so = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(so, assetPath); }
            return so;
        }

        static Dictionary<string, T> IndexByName<T>() where T : MonsterData
        {
            var dict = new Dictionary<string, T>();
            foreach (var a in FindAll<T>()) dict[a.monsterName] = a;
            return dict;
        }

        static List<T> FindAll<T>() where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            return list;
        }

        static T FindFirst<T>() where T : ScriptableObject
        {
            var all = FindAll<T>();
            return all.Count > 0 ? all[0] : null;
        }

        static void Finish(string msg)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MirageGate] {msg} を取り込みました。");
        }

        static string Get(Dictionary<string, string> r, string k) => r.TryGetValue(k, out var v) ? v : "";
        static int Int(Dictionary<string, string> r, string k)
            => r.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : 0;
        static float Float(Dictionary<string, string> r, string k)
            => r.TryGetValue(k, out var v) && float.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0f;
        static bool Bool(Dictionary<string, string> r, string k)
            => r.TryGetValue(k, out var v) && (v == "1" || v.ToLower() == "true");
        static TEnum Enum<TEnum>(string v) where TEnum : struct
            => System.Enum.TryParse<TEnum>(v, true, out var e) ? e : default;
        static string Safe(string s) => s.Replace("/", "_").Replace(" ", "_");
    }
}
