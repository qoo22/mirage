using System.Collections.Generic;
using UnityEngine;
using MirageGate.Core;
using MirageGate.Runtime;
using MirageGate.Systems;

namespace MirageGate.View
{
    /// <summary>
    /// 盤面の描画（プロトタイプ用・アート素材不要）。
    /// 1x1白スプライトを実行時生成し、色分けでタイル/敵/プレイヤーを表示する。
    /// 後でSpriteRenderer.sprite を本素材に差し替えれば見た目を強化できる（Visual Onlyなのでロジック不変）。
    /// グリッド(x,y)→ワールド(x, -y)。yは下方向。
    /// </summary>
    public class GameBoardView : MonoBehaviour
    {
        [Header("タイル色")]
        public Color cWall = new Color(0.08f, 0.08f, 0.12f);
        public Color cRoom = new Color(0.22f, 0.22f, 0.28f);
        public Color cCorridor = new Color(0.16f, 0.16f, 0.20f);
        public Color cPoison = new Color(0.20f, 0.55f, 0.25f);
        public Color cFire = new Color(0.65f, 0.25f, 0.12f);
        public Color cCurse = new Color(0.35f, 0.12f, 0.45f);
        public Color cIce = new Color(0.55f, 0.75f, 0.95f);
        public Color cGoalStairs = new Color(0.30f, 0.50f, 0.95f);
        public Color cGoalCrystal = new Color(0.98f, 0.85f, 0.30f);
        public Color cFog = new Color(0.03f, 0.03f, 0.05f);

        [Header("エンティティ色")]
        public Color cPlayer = new Color(0.35f, 0.9f, 0.95f);
        public Color cMonster = new Color(0.9f, 0.35f, 0.32f);
        public Color cBoss = new Color(0.95f, 0.3f, 0.85f);
        public Color cItem = new Color(0.5f, 0.9f, 0.5f);   // カード
        public Color cGem = new Color(0.55f, 0.85f, 1f);    // 宝石
        public Color cShop = new Color(1f, 0.8f, 0.3f);     // ショップ
        public Color cSlot = new Color(1f, 0.5f, 0.85f);    // スロット
        public float tileSize = 1f;
        public bool useFog = true;  // 視界(fog)：未踏破=黒・既踏破=暗・視界内=明
        [Range(0f, 1f)] public float seenDim = 0.4f; // 既踏破だが視界外の暗さ
        public bool useTileArt = true; // 床/壁テクスチャ（Resources/tile）
        public GameFeelDirector feel; // 任意。けぞり/フラッシュ/撃破演出の描画に使用

        Sprite _floorSpr, _wallSpr; bool _tileTried;
        Sprite _gemSpr, _shopSpr, _slotSpr; bool _itemTried;
        void LoadItemArt()
        {
            if (_itemTried) return; _itemTried = true;
            _gemSpr = Resources.Load<Sprite>("item/gem");
            _shopSpr = Resources.Load<Sprite>("item/shop");
            _slotSpr = Resources.Load<Sprite>("item/slot");
        }
        bool LoadTileArt()
        {
            if (!_tileTried) { _tileTried = true; _floorSpr = Resources.Load<Sprite>("tile/floor"); _wallSpr = Resources.Load<Sprite>("tile/wall"); }
            return _floorSpr != null || _wallSpr != null;
        }

        Sprite _sq;
        RunState _run;
        SpriteRenderer[,] _tiles;
        Transform _tileRoot, _entRoot;
        readonly List<SpriteRenderer> _entPool = new List<SpriteRenderer>();
        public Transform PlayerTransform { get; private set; }

        void Awake()
        {
            _sq = MakeSquare();
            _tileRoot = new GameObject("Tiles").transform; _tileRoot.SetParent(transform);
            _entRoot = new GameObject("Entities").transform; _entRoot.SetParent(transform);
        }

        static Sprite MakeSquare()
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, Color.white); t.filterMode = FilterMode.Point; t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        Vector3 World(int x, int y, float z = 0) => new Vector3(x * tileSize, -y * tileSize, z);

        public void SetRun(RunState run)
        {
            _run = run;
            BuildTiles();
            RefreshEntities(); // PlayerTransform を即時生成（カメラ追従先のため）
        }

        void BuildTiles()
        {
            for (int i = _tileRoot.childCount - 1; i >= 0; i--) Destroy(_tileRoot.GetChild(i).gameObject);
            var map = _run.map;
            _tiles = new SpriteRenderer[map.width, map.height];
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var go = new GameObject($"t{x}_{y}");
                    go.transform.SetParent(_tileRoot);
                    go.transform.position = World(x, y, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = _sq; sr.sortingOrder = 0;
                    _tiles[x, y] = sr;
                }
        }

        void LateUpdate()
        {
            if (_run == null || _run.map == null) return;
            RefreshTiles();
            RefreshEntities();
        }

        void RefreshTiles()
        {
            var map = _run.map;
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var sr = _tiles[x, y];
                    if (useFog && !map.seen[x, y]) { SetTile(sr, _sq, cFog); continue; }

                    bool special = (x == map.goal.x && y == map.goal.y) || map.gimmicks[x, y] != GimmickType.None;
                    Color c;
                    Sprite spr = _sq;
                    if (x == map.goal.x && y == map.goal.y) c = map.goalIsCrystal ? cGoalCrystal : cGoalStairs;
                    else switch (map.gimmicks[x, y])
                        {
                            case GimmickType.Poison: c = cPoison; break;
                            case GimmickType.Fire: c = cFire; break;
                            case GimmickType.Curse: c = cCurse; break;
                            case GimmickType.Ice: c = cIce; break;
                            default:
                                bool isWall = map.tiles[x, y] == TileKind.Wall;
                                if (useTileArt && LoadTileArt() && (isWall ? _wallSpr : _floorSpr) != null)
                                {
                                    spr = isWall ? _wallSpr : _floorSpr;
                                    c = isWall ? new Color(0.7f, 0.7f, 0.78f)
                                       : map.tiles[x, y] == TileKind.Corridor ? new Color(0.78f, 0.78f, 0.82f) : Color.white;
                                }
                                else c = isWall ? cWall : map.tiles[x, y] == TileKind.Corridor ? cCorridor : cRoom;
                                break;
                        }
                    // 既踏破だが視界外は暗く
                    if (useFog && !map.lit[x, y]) c = Color.Lerp(special ? cFog : Color.black, c, seenDim);
                    SetTile(sr, spr, c);
                }
        }

        /// <summary>歩行/攻撃アニメの状態（realtime比較）。attack優先→walk→idle。</summary>
        static string AnimState(float moveT, float atkT)
        {
            float now = Time.realtimeSinceStartup;
            if (now - atkT < 0.32f) return "attack";
            if (now - moveT < 0.26f) return "walk";
            return "idle";
        }

        /// <summary>状態に応じたスプライト選択。walkは walk1-3 を時間で循環。無ければidle→単体絵。</summary>
        Sprite PickAnim(bool isHero, string key, string state)
        {
            string[] cands;
            if (state == "walk")
            {
                int f = 1 + (int)(Time.realtimeSinceStartup * 8f) % 3; // walk1/2/3 循環
                cands = new[] { "walk" + f, "walk", "idle" };
            }
            else if (state == "attack") cands = new[] { "attack", "idle" };
            else cands = new[] { "idle" };

            foreach (var c in cands)
            {
                var s = isHero ? SpriteLibrary.HeroAnim(key, c) : SpriteLibrary.MonsterAnim(key, c);
                if (s != null) return s;
            }
            return isHero ? SpriteLibrary.Hero(key) : SpriteLibrary.Monster(key);
        }

        void SetTile(SpriteRenderer sr, Sprite spr, Color c)
        {
            sr.sprite = spr;
            var sz = spr.bounds.size; float md = Mathf.Max(sz.x, sz.y); if (md <= 0.0001f) md = 1f;
            sr.transform.localScale = Vector3.one * (tileSize / md);
            sr.color = c;
        }

        void RefreshEntities()
        {
            int n = 0;
            // 0: プレイヤー（方向アニメ＋けぞり＋被弾白フラッシュ §8.3）
            var p = Ent(n++);
            string jobId = _run.player.job != null ? _run.player.job.id : "";
            string pState = AnimState(_run.player.animMoveT, _run.player.animAtkT);
            var heroArt = PickAnim(true, jobId, pState);
            Color pc = heroArt != null ? Color.white : cPlayer;
            Vector3 pOff = Vector3.zero;
            if (feel != null)
            {
                var po = feel.PlayerHitOffset(); pOff = new Vector3(po.x, po.y, 0);
                if (heroArt == null) pc = Color.Lerp(pc, Color.white, feel.PlayerFlashAlpha());
            }
            ApplyArt(p, heroArt, pc, 0.92f);
            if (heroArt != null) p.flipX = _run.player.faceX < 0; // 進行方向に向く
            p.transform.position = World(_run.player.x, _run.player.y, -1f) + pOff;
            PlayerTransform = p.transform;

            // アイテム（種別で色分け・視界外は非表示）
            foreach (var it in _run.items)
            {
                if (useFog && !_run.map.lit[it.x, it.y]) continue;
                var e = Ent(n++);
                LoadItemArt();
                Sprite ispr = null; Color icol;
                switch (it.kind)
                {
                    case FloorItem.Kind.Gem: ispr = _gemSpr; icol = cGem; break;
                    case FloorItem.Kind.Shop: ispr = _shopSpr; icol = cShop; break;
                    case FloorItem.Kind.Slot: ispr = _slotSpr; icol = cSlot; break;
                    default: icol = cItem; break;
                }
                ApplyArt(e, ispr, ispr != null ? Color.white : icol,
                    it.kind == FloorItem.Kind.Shop || it.kind == FloorItem.Kind.Slot ? 0.85f : 0.55f);
                e.transform.position = World(it.x, it.y, -0.5f);
            }
            // 敵（けぞりオフセット＋被弾白フラッシュ・§8.3・視界外は非表示）
            foreach (var m in _run.monsters)
            {
                if (m.killed) continue;
                if (useFog && !_run.map.lit[m.x, m.y]) continue;
                var e = Ent(n++);
                string mName = m.data != null ? m.data.monsterName : null;
                string mState = AnimState(m.animMoveT, m.animAtkT);
                var art = PickAnim(false, mName, mState);
                Color baseC = art != null ? Color.white : (m.IsBoss ? cBoss : cMonster);
                Vector3 off = Vector3.zero;
                if (feel != null)
                {
                    var ho = feel.HitOffset(m); off = new Vector3(ho.x, ho.y, 0);
                    if (art == null) baseC = Color.Lerp(baseC, Color.white, feel.HitFlashAlpha(m));
                }
                ApplyArt(e, art, baseC, m.IsBoss ? 1.1f : 0.85f);
                if (art != null) e.flipX = _run.player.x < m.x; // プレイヤーの方を向く
                e.transform.position = World(m.x, m.y, -1f) + off;
            }
            // 撃破フラッシュ（§8.3 killflash）
            if (feel != null)
            {
                float now = Time.realtimeSinceStartup * 1000f;
                foreach (var kf in feel.KillFlashes)
                {
                    float t = (now - kf.start) / kf.durMs;
                    if (t < 0 || t > 1) continue;
                    var e = Ent(n++);
                    var spr = SpriteLibrary.Monster(kf.monsterName);
                    if (spr != null)
                    {
                        // 崩壊：縮みながら回転・フェード（fxDie）
                        float fade = 1f - t;
                        var c = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.4f), t); c.a = fade; e.color = c;
                        ApplyArt(e, spr, c, (kf.big ? 1.1f : 0.85f) * (1f - 0.6f * t));
                        e.transform.position = World((int)kf.worldX, (int)kf.worldY, -1.5f) + new Vector3(0, -t * 0.25f, 0);
                        e.transform.rotation = Quaternion.Euler(0, 0, t * (kf.big ? 40f : 25f));
                    }
                    else
                    {
                        var c = Color.white; c.a = 1f - t; e.color = c;
                        e.transform.position = World((int)kf.worldX, (int)kf.worldY, -1.5f);
                        e.transform.localScale = Vector3.one * (kf.big ? 1.3f : 1.0f) * (1f + t * 0.6f);
                    }
                }
            }
            // 余ったプール非表示
            for (int i = n; i < _entPool.Count; i++) _entPool[i].gameObject.SetActive(false);
        }

        SpriteRenderer Ent(int i)
        {
            while (_entPool.Count <= i)
            {
                var go = new GameObject("ent");
                go.transform.SetParent(_entRoot);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sq; sr.sortingOrder = 2;
                _entPool.Add(sr);
            }
            var e = _entPool[i];
            e.gameObject.SetActive(true);
            e.sprite = _sq;                 // プール再利用時に前フレームの絵をリセット
            e.flipX = false;
            e.transform.rotation = Quaternion.identity;
            e.transform.localScale = Vector3.one * 0.8f;
            return e;
        }

        /// <summary>スプライト（無ければ白四角）を設定し、tileSizeにフィットするよう拡縮。</summary>
        void ApplyArt(SpriteRenderer e, Sprite art, Color color, float frac)
        {
            e.sprite = art != null ? art : _sq;
            e.color = color;
            var sz = e.sprite.bounds.size;
            float maxDim = Mathf.Max(sz.x, sz.y);
            if (maxDim <= 0.0001f) maxDim = 1f;
            e.transform.localScale = Vector3.one * (tileSize * frac / maxDim);
        }
    }
}
