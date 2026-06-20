using System;
using System.Collections.Generic;
using UnityEngine;

namespace MirageGate.Systems
{
    /// <summary>永続データ（メダル・コレクション）。原作 mg_save（§12）。</summary>
    [Serializable]
    public class MetaSave
    {
        public int medals = 2000;
        public List<string> collectionIds = new List<string>();   // id
        public List<int> collectionCounts = new List<int>();      // 対応枚数
        public List<string> clearedDungeons = new List<string>();
        public List<string> unlockedJobs = new List<string>();
        public int worldFloor;   // ワールド（試練の塔）の到達フロア
    }

    /// <summary>ストーリー進行。原作 mg_story（§12）。forbiddenEverで真エンド判定。</summary>
    [Serializable]
    public class StorySave
    {
        public int chapter, chapter2;
        public bool cleared, cleared2;
        public string storyJob = "ren", story2Job = "ren";
        public int memShards;
        public List<string> bossCards = new List<string>();
        public List<string> titles = new List<string>();
        public bool forbiddenEver;   // 真エンド封印（§10.4）
        public int ver = 1;
    }

    /// <summary>オプション。原作 mg_opt（§12）。</summary>
    [Serializable]
    public class OptionsSave
    {
        public float bgmVol = 0.8f, seVol = 0.8f;
        public string textSpeed = "normal"; // slow/normal/fast/instant
        public bool reduceFx;
    }

    /// <summary>
    /// セーブ管理（§12）。3スロット×（本編/2部）。原作のlocalStorageキー体系を踏襲。
    /// 設計指針：JSON平坦化で破損リスク低減。全read/writeをtry-catch。
    /// </summary>
    public class SaveManager
    {
        // 原作キー（スロット2/3は接尾辞 _2 / _3）
        const string K_Meta = "mg_save", K_Story = "mg_story", K_World = "mg_world";
        const string K_Log = "mg_log", K_Flags = "mg_flags", K_Opt = "mg_opt";
        const string K_OpSeen = "mg_op_seen", K_Slot = "mg_slot";

        public int ActiveSlot = 1;
        string Key(string baseKey) => ActiveSlot == 1 ? baseKey : $"{baseKey}_{ActiveSlot}";

        public bool OpeningSeen
        {
            get => PlayerPrefs.GetString(K_OpSeen, "") == "1";
            set => PlayerPrefs.SetString(K_OpSeen, value ? "1" : "");
        }

        public void SaveMeta(MetaSave m) => WriteJson(Key(K_Meta), m);
        public MetaSave LoadMeta() => ReadJson<MetaSave>(Key(K_Meta));
        public void SaveStory(StorySave s) => WriteJson(Key(K_Story), s);
        public StorySave LoadStory() => ReadJson<StorySave>(Key(K_Story));
        public void SaveOptions(OptionsSave o) => WriteJson(K_Opt, o); // オプションはグローバル

        static void WriteJson<T>(string key, T data)
        {
            try { PlayerPrefs.SetString(key, JsonUtility.ToJson(data)); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning($"[Save] {key} 失敗: {e.Message}"); }
        }

        static T ReadJson<T>(string key) where T : class
        {
            try
            {
                var s = PlayerPrefs.GetString(key, "");
                return string.IsNullOrEmpty(s) ? null : JsonUtility.FromJson<T>(s);
            }
            catch (Exception e) { Debug.LogWarning($"[Save] {key} 読込失敗: {e.Message}"); return null; }
        }
    }
}
