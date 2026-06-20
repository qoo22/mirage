using UnityEngine;
using MirageGate.Data;
using MirageGate.Runtime;
using MirageGate.Systems;

namespace MirageGate.View
{
    /// <summary>
    /// 最小HUD（OnGUI・Canvas不要）。目標WIN/残りF・HP/MP/Lv・所持メダル/配当・手札（§6 HUD）。
    /// ダメージ数字はGameFeelDirector.Popupsをワールド→スクリーン変換して浮遊描画。
    /// プロトタイプ用。本番はuGUI/TMPに置換推奨。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public Camera cam;
        public GameFeelDirector feel;
        public GameDatabase db;
        public EconomyManager Econ;     // GameControllerから注入
        public ShopSystem shop;
        public SlotSystem slot;
        public float tileSize = 1f;

        RunState _run;
        public void SetRun(RunState run) => _run = run;

        // 取引パネルの状態
        enum Mode { None, Shop, Slot, HandFull }
        Mode _mode = Mode.None;
        System.Collections.Generic.List<ShopSlot> _stock;
        Core.SlotMachineType _slotType;
        string _msg = "";
        string _pendingCard = "";

        public void OpenHandFull(string cardId) { _mode = Mode.HandFull; _pendingCard = cardId; _msg = ""; }

        public void OpenShop(FloorItem _)
        {
            if (_mode == Mode.Shop) return;
            _mode = Mode.Shop;
            _stock = shop != null ? shop.RollStock() : null;
            _msg = "";
        }
        public void OpenSlot(FloorItem it)
        {
            _mode = Mode.Slot; _slotType = it.slotType; _msg = "";
        }
        public bool InteractionOpen => _mode != Mode.None;
        void Close() => _mode = Mode.None;

        GUIStyle _box, _pop;

        void EnsureStyles()
        {
            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true };
                _box.normal.textColor = Color.white;
            }
            if (_pop == null)
            {
                _pop = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            }
        }

        void OnGUI()
        {
            if (_run == null) return;
            EnsureStyles();
            DrawStatus();
            DrawHand();
            DrawPopups();
            if (_mode == Mode.Shop) DrawShop();
            else if (_mode == Mode.Slot) DrawSlot();
            else if (_mode == Mode.HandFull) DrawHandFull();
        }

        void DrawHandFull()
        {
            var pend = db != null ? db.Card(_pendingCard) : null;
            var area = new Rect(Screen.width / 2 - 210, 60, 420, Mathf.Min(Screen.height - 120, 420));
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"<b>手札が満杯</b>  拾った: {(pend != null ? pend.cardName : _pendingCard)}", _box);
            GUILayout.Label("交換するカードを選ぶ（または捨てる）", _box);
            for (int i = 0; i < _run.player.hand.Count; i++)
            {
                var c = db != null ? db.Card(_run.player.hand[i]) : null;
                if (GUILayout.Button($"[{(i + 1) % 10}] {(c != null ? c.cardName : _run.player.hand[i])} と交換"))
                { _run.player.hand[i] = _pendingCard; Close(); return; }
            }
            GUILayout.Space(6);
            if (GUILayout.Button("拾わず捨てる")) Close();
            GUILayout.EndArea();
        }

        void DrawShop()
        {
            var area = new Rect(Screen.width / 2 - 200, 60, 400, Mathf.Min(Screen.height - 120, 440));
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"<b>🛒 ショップ</b>   所持メダル {(Econ != null ? Econ.Medals : 0)}", _box);
            if (_stock != null)
                foreach (var s in _stock)
                {
                    GUILayout.BeginHorizontal();
                    string label = s.sold ? $"<color=#888>{s.card.cardName}（売切）</color>"
                                          : $"{s.card.cardName}  {s.card.cost}メダル";
                    GUILayout.Label(label, _box, GUILayout.Width(290));
                    GUI.enabled = !s.sold && Econ != null && Econ.Medals >= s.card.cost
                                  && _run.player.hand.Count < _run.player.handMax;
                    if (GUILayout.Button("買う", GUILayout.Width(70)))
                    {
                        if (shop.TryBuy(s, Econ)) { _run.player.hand.Add(s.card.id); _msg = $"{s.card.cardName} を購入"; }
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            GUILayout.Space(6);
            GUILayout.Label(_msg, _box);
            if (GUILayout.Button("閉じる（離れる）")) Close();
            GUILayout.EndArea();
        }

        void DrawSlot()
        {
            int bet = slot != null ? slot.BetCost(_slotType, 1, 1) : 1;
            var area = new Rect(Screen.width / 2 - 160, 80, 320, 200);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"<b>🎰 スロット（{_slotType}）</b>", _box);
            GUILayout.Label($"掛金 {bet}メダル   所持 {(Econ != null ? Econ.Medals : 0)}", _box);
            GUI.enabled = Econ != null && Econ.Medals >= bet;
            if (GUILayout.Button("スピン！"))
            {
                Econ.Medals -= bet;
                var r = slot.Spin(_slotType, 1, 1);
                if (r.coins > 0) { _run.player.loot += r.coins; }
                if (r.card != null && _run.player.hand.Count < _run.player.handMax) _run.player.hand.Add(r.card.id);
                _msg = r.coins > 0 ? $"+{r.coins}コイン{(r.isJackpot ? " 大当たり！" : "")}" : "ハズレ…";
            }
            GUI.enabled = true;
            GUILayout.Space(6);
            GUILayout.Label(_msg, _box);
            if (GUILayout.Button("閉じる（離れる）")) Close();
            GUILayout.EndArea();
        }

        void DrawStatus()
        {
            var p = _run.player;
            int medals = Econ != null ? Econ.Medals : 0;
            string s =
                $"<b>{_run.dungeon.jpName}</b>  F{_run.floor}/{_run.EffectiveFloors}   目標WIN {_run.dungeon.win}\n" +
                $"Lv {p.lvl}   HP {Mathf.Max(0, p.hp)}/{p.maxHp}   MP {p.mp}/{p.maxMp}\n" +
                $"ATK {p.atk}   DEF {p.def}(+{p.shieldDef})\n" +
                $"メダル {medals}   配当 {p.loot}";
            string eq = EquipLine(p);
            if (eq.Length > 0) s += "\n" + eq;
            GUI.Box(new Rect(8, 8, 300, eq.Length > 0 ? 110 : 92), s, _box);
        }

        static string EquipLine(PlayerState p)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (p.swordMul > 1f) parts.Add($"⚔×{p.swordMul:0.0}");
            if (p.swordAtk > 0) parts.Add($"⚔+{p.swordAtk}");
            if (p.shieldDef > 0) parts.Add($"🛡+{p.shieldDef}");
            if (p.goldSword) parts.Add("💰");
            if (p.drain) parts.Add("🩸");
            if (p.reviveCharge) parts.Add("💍復活");
            foreach (var r in p.rings) parts.Add("💍" + r);
            return parts.Count > 0 ? "装備 " + string.Join(" ", parts) : "";
        }

        void DrawHand()
        {
            var p = _run.player;
            float y = Screen.height - 28;
            float x = 8;
            for (int i = 0; i < p.hand.Count; i++)
            {
                var c = db != null ? db.Card(p.hand[i]) : null;
                string label = c != null ? $"[{(i + 1) % 10}] {c.icon}{c.cardName}({c.mp})" : $"[{(i + 1) % 10}] {p.hand[i]}";
                var size = _box.CalcSize(new GUIContent(label));
                GUI.Box(new Rect(x, y, size.x + 10, 24), label, _box);
                x += size.x + 16;
                if (x > Screen.width - 80) break;
            }
        }

        void DrawPopups()
        {
            if (feel == null || cam == null) return;
            float now = Time.realtimeSinceStartup * 1000f;
            foreach (var pop in feel.Popups)
            {
                float t = (now - pop.startRealtimeMs) / pop.DurMs;
                if (t < 0 || t > 1) continue;
                Vector3 world = new Vector3(pop.worldX * tileSize, -pop.worldY * tileSize, 0);
                Vector3 sp = cam.WorldToScreenPoint(world);
                float gx = sp.x, gy = Screen.height - sp.y - t * 34f; // 上へ浮遊
                var col = pop.color; col.a = 1f - t;
                var prev = GUI.color; GUI.color = col;
                _pop.fontSize = pop.big ? 24 : 18;
                GUI.Label(new Rect(gx - 40, gy - 12, 80, 24), pop.text, _pop);
                GUI.color = prev;
            }
        }
    }
}
