using System.Collections.Generic;
using UnityEngine;
using MirageGate.Data;

namespace MirageGate.Systems
{
    /// <summary>
    /// 全SOアセットの参照ハブ（id→データの引き）。Resources or Addressables からロード。
    /// 原作が文字列idで参照していたのを、ここで一元解決する。
    /// </summary>
    [CreateAssetMenu(menuName = "MirageGate/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public GameBalanceConfig balance;
        public List<JobData> jobs = new List<JobData>();
        public List<CardData> cards = new List<CardData>();
        public List<MonsterData> monsters = new List<MonsterData>();
        public List<DungeonData> dungeons = new List<DungeonData>();
        public List<StatusEffectData> statusEffects = new List<StatusEffectData>();
        public List<CampaignData> campaigns = new List<CampaignData>();

        Dictionary<string, CardData> _cards;
        Dictionary<string, JobData> _jobs;
        Dictionary<string, DungeonData> _dungeons;
        Dictionary<string, MonsterData> _monsters;

        public void BuildIndex()
        {
            _cards = new Dictionary<string, CardData>();
            foreach (var c in cards) _cards[c.id] = c;
            _jobs = new Dictionary<string, JobData>();
            foreach (var j in jobs) _jobs[j.id] = j;
            _dungeons = new Dictionary<string, DungeonData>();
            foreach (var d in dungeons) _dungeons[d.id] = d;
            _monsters = new Dictionary<string, MonsterData>();
            foreach (var m in monsters) _monsters[m.monsterName] = m;
        }

        public CardData Card(string id) => _cards != null && _cards.TryGetValue(id, out var c) ? c : null;
        public JobData Job(string id) => _jobs != null && _jobs.TryGetValue(id, out var j) ? j : null;
        public DungeonData Dungeon(string id) => _dungeons != null && _dungeons.TryGetValue(id, out var d) ? d : null;
        public MonsterData Monster(string name) => _monsters != null && _monsters.TryGetValue(name, out var m) ? m : null;
    }
}
