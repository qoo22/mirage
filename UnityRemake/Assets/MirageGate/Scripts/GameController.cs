using UnityEngine;
using MirageGate.Core;
using MirageGate.Data;
using MirageGate.Runtime;
using MirageGate.Systems;
using MirageGate.View;

namespace MirageGate
{
    /// <summary>
    /// 全システムの結線（ブートストラップ）。シーンに1つ置く。
    /// 依存をここで生成・注入し、ゲーム開始まで導く。雛形＝結線の見取り図。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("データ")]
        public GameDatabase db;

        [Header("MonoBehaviour系")]
        public TurnManager turnManager;
        public EnemyAI enemyAI;
        public GameFeelDirector feel;
        public DialogueManager dialogue;
        public TutorialManager tutorial;

        [Header("ビュー層")]
        public GameBoardView board;
        public CameraRig cameraRig;
        public GameInput input;
        public Hud hud;

        [Header("デバッグ（任意）")]
        public bool skipTitle = false;     // trueなら即出撃（先頭の職業/ダンジョン）
        public DungeonData debugDungeon;
        public JobData debugJob;

        // ---- アプリ状態（メタループ §9）----
        public enum AppState { Title, Setup, Playing, Cleared, GameOver, Cutscene, EndingChoice, Opening, World }
        public AppState State { get; private set; } = AppState.Title;
        public int LastReward { get; private set; }
        public int Medals => econ != null ? econ.Medals : 0;
        public System.Collections.Generic.List<JobData> Jobs => db != null ? db.jobs : null;
        public System.Collections.Generic.List<DungeonData> Dungeons => db != null ? db.dungeons : null;

        JobData _job; DungeonData _dungeon; int _seed;
        int _seedCounter = 1;

        // 物語モード
        StoryFile _story; int _storyPart; int _storyChapter; bool _storyMode;
        bool _forbiddenEver;          // 禁忌カードを一度でも使ったか（真エンド判定 §10.4）
        EndingPart _ending;           // 現在の3択エンディング
        Data.MonsterData _pendingBoss; // 物語最終章の固定ボス
        public EndingPart CurrentEnding => _ending;

        // 純Cクラス系
        DamageCalculator dmg;
        ProgressionSystem prog;
        EconomyManager econ;
        StatusEffectManager status;
        CombatResolver combat;
        CardEffectExecutor cardEffect;
        DungeonGenerator generator;
        MovementSystem movement;
        VisionSystem vision;
        ShopSystem shop;
        SlotSystem slot;
        CardDropTable dropTable;
        SaveManager save;

        RunState run;

        void Awake()
        {
            db.BuildIndex();
            var cfg = db.balance;

            save = new SaveManager();
            dmg = new DamageCalculator(cfg);
            prog = new ProgressionSystem(cfg);
            econ = new EconomyManager(cfg, cfg.startingMedals);
            status = new StatusEffectManager(db.statusEffects);
            combat = new CombatResolver(cfg, dmg, prog, econ, feel);
            cardEffect = new CardEffectExecutor(cfg, dmg, prog, status);
            generator = new DungeonGenerator(cfg);
            movement = new MovementSystem();
            vision = new VisionSystem();
            shop = new ShopSystem(cfg, db.cards);
            slot = new SlotSystem(cfg);
            dropTable = new CardDropTable(cfg, db.cards);
            generator.dropTable = dropTable;  // 床アイテムにカード/宝石の中身を入れる
            generator.econ = econ;

            enemyAI.Init(combat, status);

            // セーブ読込（メダル）。無ければ初期値
            var ms = save.LoadMeta();
            if (ms != null) econ.Medals = ms.medals;
        }

        void Start()
        {
            if (skipTitle && Jobs != null && Jobs.Count > 0 && Dungeons != null && Dungeons.Count > 0)
            { StartChosen(debugJob != null ? debugJob : Jobs[0], debugDungeon != null ? debugDungeon : Dungeons[0]); return; }
            if (!save.OpeningSeen) StartOpening();
            else SetState(AppState.Title);
        }

        // ---- オープニング映像（§10.1）----
        OpeningFile _opening; int _opIdx;
        public OpeningScene CurrentOpScene => (_opening != null && _opIdx < _opening.scenes.Count) ? _opening.scenes[_opIdx] : null;
        void StartOpening()
        {
            _opening = StoryData.LoadOpening(); _opIdx = 0;
            if (_opening == null || _opening.scenes.Count == 0) { SetState(AppState.Title); return; }
            SetState(AppState.Opening);
        }
        public void OpAdvance() { _opIdx++; if (_opening == null || _opIdx >= _opening.scenes.Count) OpEnd(); }
        public void OpSkip() => OpEnd();
        void OpEnd() { save.OpeningSeen = true; SetState(AppState.Title); }

        void SetState(AppState s)
        {
            State = s;
            if (turnManager != null) turnManager.InputEnabled = (s == AppState.Playing);
            if (tutorial != null) tutorial.Enabled = (s == AppState.Playing);
        }

        System.Collections.Generic.List<JobData> _availableJobs;
        public System.Collections.Generic.List<JobData> AvailableJobs => _availableJobs ?? Jobs;

        public void GoSetup()
        {
            _availableJobs = new System.Collections.Generic.List<JobData>();
            var meta = save.LoadMeta();
            foreach (var j in db.jobs)
                if (j.id != "ren" && (!j.hidden || (meta != null && meta.unlockedJobs.Contains(j.id))))
                    _availableJobs.Add(j);
            SetState(AppState.Setup);
        }
        public void GoTitle() { _storyMode = false; SetState(AppState.Title); }

        void OnEscapeRun()
        {
            econ.Medals += run.player.loot; LastReward = run.player.loot; SaveMeta();
            if (_storyMode) GoTitle(); else SetState(AppState.Cleared);
        }

        // ---- ワールド（城主戦・原作CB_SCENE準拠）----
        LordFile _lords; string _worldLordJob;
        public LordFile Lords => _lords ?? (_lords = StoryData.LoadLords());
        public bool IsJobUnlocked(string job)
        {
            var m = save.LoadMeta();
            return m != null && m.unlockedJobs.Contains(job);
        }
        public void OpenWorld() { _ = Lords; SetState(AppState.World); }

        /// <summary>城主に挑戦：登場会話→城（高難度ダンジョン＋城主ボス）→撃破で職解放。</summary>
        public void StartLordBattle(string job)
        {
            var lord = Lords.lords.Find(l => l.job == job);
            if (lord == null) return;
            var jd = db.Job(job);
            // 城主ボス（覇者）の合成MonsterData：hp=420+k*120 / atk=22+k*4.2 / def=8+k*1.4（k=max(4,star)）
            int star = 7, k = Mathf.Max(4, star);
            var boss = ScriptableObject.CreateInstance<Data.MonsterData>();
            boss.monsterName = "覇者 " + (jd != null ? jd.jpName : job);
            boss.hp = Mathf.RoundToInt(420 + k * 120);
            boss.atk = Mathf.RoundToInt(22 + k * 4.2f);
            boss.def = Mathf.RoundToInt(8 + k * 1.4f);
            boss.minDmg = Mathf.RoundToInt(10 + k);
            boss.pay = 50; boss.role = Core.MonsterRole.Melee; boss.cap = new[] { 200, 200, 200 };
            boss.isBoss = true;
            View.SpriteLibrary.RegisterLord(boss.monsterName, job); // 立ち絵=hero流用

            _worldLordJob = job;
            _pendingBoss = boss;
            _dungeon = db.Dungeon(lord.tex) ?? db.Dungeon("hard") ?? FirstRealDungeon();
            _job = db.Job("warrior") ?? (Jobs.Count > 0 ? Jobs[0] : null);
            _storyMode = false;
            SetState(AppState.Cutscene);
            dialogue.Play(lord.intro, () => { _seed = NextSeed(); StartRun(_dungeon, _job, GameMode.World, _seed); }, SetFlag);
        }

        DungeonData FirstRealDungeon()
        {
            foreach (var d in db.dungeons) if (!d.isTutorial) return d;
            return Dungeons.Count > 0 ? Dungeons[0] : null;
        }

        void OnLordDefeated()
        {
            var lord = Lords.lords.Find(l => l.job == _worldLordJob);
            // 城主撃破で職業解放
            var meta = save.LoadMeta() ?? new MetaSave();
            if (!meta.unlockedJobs.Contains(_worldLordJob)) meta.unlockedJobs.Add(_worldLordJob);
            save.SaveMeta(meta);
            string job = _worldLordJob; _worldLordJob = null;
            SetState(AppState.Cutscene);
            dialogue.Play(lord != null ? lord.defeat : new System.Collections.Generic.List<StoryLine>(),
                () => GoTitle(), SetFlag);
        }

        /// <summary>ダンジョンクリアで隠し職を1つ解放（§3.1）。</summary>
        void UnlockNextJob()
        {
            var meta = save.LoadMeta() ?? new MetaSave();
            foreach (var j in db.jobs)
                if (j.hidden && j.id != "ren" && !meta.unlockedJobs.Contains(j.id))
                { meta.unlockedJobs.Add(j.id); save.SaveMeta(meta); return; }
        }

        int NextSeed() => (System.Environment.TickCount ^ (_seedCounter++ * 2654435761u).GetHashCode()) & 0x7fffffff;

        /// <summary>編成画面で職業・ダンジョンを選んで出撃（フリープレイ）。</summary>
        public void StartChosen(JobData job, DungeonData dungeon)
        {
            if (job == null || dungeon == null) return;
            _storyMode = false; _job = job; _dungeon = dungeon; _seed = NextSeed();
            StartRun(dungeon, job, GameMode.FreePlay, _seed);
        }

        public void Retry()
        {
            if (_dungeon == null || _job == null) return;
            _seed = NextSeed();
            StartRun(_dungeon, _job, _storyMode ? GameMode.Story : GameMode.FreePlay, _seed);
        }

        // ---- 物語モード（§10）----
        public void StartStory(int part)
        {
            _story = StoryData.Load(part); _storyPart = part;
            var ss = save.LoadStory();
            _storyChapter = ss != null ? (part == 1 ? ss.chapter : ss.chapter2) : 0;
            _forbiddenEver = ss != null && ss.forbiddenEver;
            if (_storyChapter < 0 || _storyChapter >= _story.chapters.Count) _storyChapter = 0;
            _storyMode = true;
            EnterStoryChapter();
        }

        void EnterStoryChapter()
        {
            if (_story == null || _storyChapter >= _story.chapters.Count) { GoTitle(); return; } // 全章クリア
            var ch = _story.chapters[_storyChapter];
            _dungeon = db.Dungeon(ch.dungeon) ?? (Dungeons.Count > 0 ? Dungeons[0] : null);
            _job = db.Job("ren") ?? (Jobs.Count > 0 ? Jobs[0] : null);
            bool isFinal = ch.isFinal || _storyChapter >= _story.chapters.Count - 1;
            _pendingBoss = isFinal ? db.Monster("幻影のオーブ") : null; // 終章はオーブ戦
            SetState(AppState.Cutscene);
            dialogue.Play(ch.intro, () => { _seed = NextSeed(); StartRun(_dungeon, _job, GameMode.Story, _seed); }, SetFlag);
        }

        void SetFlag(string flag) { /* TODO: mg_flags 記録（エンディング分岐用） */ }

        void SaveStoryProgress()
        {
            var ss = save.LoadStory() ?? new StorySave();
            if (_storyPart == 1) ss.chapter = _storyChapter; else ss.chapter2 = _storyChapter;
            ss.forbiddenEver = _forbiddenEver;
            save.SaveStory(ss);
        }

        // ---- エンディング（§10.4）----
        void ShowEndingChoice()
        {
            _ending = StoryData.LoadEnding(_storyPart);
            if (_ending == null) { MarkStoryCleared(); GoTitle(); return; }
            SetState(AppState.EndingChoice);
        }

        /// <summary>3択：0=bad, 1=normal, 2=真エンド挑戦（禁忌使用なら濁った手）。</summary>
        public void ChooseEnding(int idx)
        {
            if (_ending == null) { GoTitle(); return; }
            var lines = idx == 0 ? _ending.bad
                      : idx == 1 ? _ending.normal
                      : (_forbiddenEver ? _ending.tainted : _ending.trueEnd);
            SetState(AppState.Cutscene);
            dialogue.Play(lines, () => { MarkStoryCleared(); GoTitle(); }, SetFlag);
        }

        void MarkStoryCleared()
        {
            var ss = save.LoadStory() ?? new StorySave();
            if (_storyPart == 1) ss.cleared = true; else ss.cleared2 = true;
            ss.forbiddenEver = _forbiddenEver;
            save.SaveStory(ss);
            _storyMode = false; _ending = null;
        }

        /// <summary>出撃（§9 startRun 相当）。BET支払い→プレイヤー初期化→フロア生成→ループ。</summary>
        public void StartRun(DungeonData dungeon, JobData job, GameMode mode, int seed)
        {
            econ.PayBet(dungeon); // メダル不足でも続行（プロト）。TODO: 不足モーダル

            run = new RunState { dungeon = dungeon, mode = mode, floor = 1 };
            run.player = CreatePlayer(job);
            run.bossOverride = _pendingBoss; _pendingBoss = null;

            turnManager.Init(run, combat, enemyAI, status, movement, cardEffect, feel);
            turnManager.vision = vision;
            turnManager.OnFloorClear = () => DescendOrClear(seed);
            turnManager.OnGameOver = OnGameOver;
            if (hud != null)
            {
                turnManager.OnShop = it => hud.OpenShop(it);
                turnManager.OnSlot = it => hud.OpenSlot(it);
                turnManager.OnHandFull = id => hud.OpenHandFull(id);
            }
            turnManager.OnEscape = OnEscapeRun;

            // ビュー層の結線
            if (board != null) board.feel = feel;
            if (hud != null) { hud.Econ = econ; hud.shop = shop; hud.slot = slot; hud.SetRun(run); }
            if (input != null) { input.SetRun(run); input.hud = hud; }
            if (tutorial != null) tutorial.SetRun(run);

            EnterFloor(seed);
            SetState(AppState.Playing);
        }

        void OnGameOver()
        {
            SaveMeta();
            SetState(AppState.GameOver);
        }

        void SaveMeta()
        {
            var ms = save.LoadMeta() ?? new MetaSave();
            ms.medals = econ.Medals;
            save.SaveMeta(ms);
        }

        /// <summary>現在フロアを生成し、プレイヤーをスタートへ。ビューを再構築。</summary>
        void EnterFloor(int seed)
        {
            run.monsters.Clear(); run.allies.Clear(); run.items.Clear();
            var map = generator.Build(run, seed + run.floor * 1000); // フロアごとに別Seed
            run.player.x = map.start.x; run.player.y = map.start.y;
            vision?.Compute(run); // 初期視界

            if (board != null)
            {
                board.SetRun(run);
                if (cameraRig != null) cameraRig.target = board.PlayerTransform;
            }
        }

        /// <summary>ゴール到達：最終Fならクリア、通常Fなら次フロアへ（§9）。</summary>
        void DescendOrClear(int seed)
        {
            if (run.IsFinalFloor)
            {
                LastReward = econ.Settle(run); // win+loot をメダルへ
                SaveMeta();
                UnlockNextJob(); // クリアで隠し職を1つ解放
                if (run.mode == GameMode.World) { OnLordDefeated(); return; }
                if (_storyMode && _story != null && _storyChapter < _story.chapters.Count)
                {
                    if (run.player.forbiddenUsed) _forbiddenEver = true;
                    // ボス会話＋章末会話 → 次章へ（§10.3）／最終章ならエンディング3択
                    var ch = _story.chapters[_storyChapter];
                    var seq = new System.Collections.Generic.List<StoryLine>(ch.boss);
                    seq.AddRange(ch.outro);
                    bool isFinal = ch.isFinal || _storyChapter >= _story.chapters.Count - 1;
                    SetState(AppState.Cutscene);
                    dialogue.Play(seq, () =>
                    {
                        if (isFinal) ShowEndingChoice();
                        else { _storyChapter++; SaveStoryProgress(); EnterStoryChapter(); }
                    }, SetFlag);
                }
                else SetState(AppState.Cleared);
            }
            else { run.floor++; EnterFloor(seed); }
        }

        PlayerState CreatePlayer(JobData job)
        {
            var p = new PlayerState
            {
                job = job,
                maxHp = job.hp, hp = job.hp,
                maxMp = job.mp, mp = job.mp,
                atk = job.atk, def = job.def,
                handMax = db.balance.defaultHandMax,
            };
            if (job.mpHalf) { p.maxMp = Mathf.RoundToInt(p.maxHp / 2f); p.mp = p.maxMp; }

            // デバッグ用スターター手札（存在するidのみ・実機では編成画面から）
            string[] starter = { "fire", "pot40", "lock", "bright", "mfire" };
            foreach (var id in starter)
                if (p.hand.Count < p.handMax && db.Card(id) != null) p.hand.Add(id);
            return p;
        }
    }
}
