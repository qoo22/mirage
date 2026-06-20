using UnityEngine;
using MirageGate;
using MirageGate.Data;
using MirageGate.Systems;

namespace MirageGate.View
{
    /// <summary>
    /// メタループのオーバーレイUI（OnGUI・Canvas不要）：タイトル/編成/クリア/ゲームオーバー（§9）。
    /// GameController.State を見て描画し、選択結果をGameControllerへ渡す。
    /// </summary>
    public class GameScreens : MonoBehaviour
    {
        public GameController gc;

        int _jobIdx, _dunIdx;
        GUIStyle _title, _box, _btn;
        Vector2 _scrollJob, _scrollDun;
        Texture2D _bg;
        bool _bgTried;

        Texture2D Bg()
        {
            if (!_bgTried) { _bg = Resources.Load<Texture2D>("ui/home"); _bgTried = true; }
            return _bg;
        }

        void Styles()
        {
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _box = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleLeft, richText = true };
                _btn = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            }
        }

        void OnGUI()
        {
            if (gc == null) return;
            Styles();
            switch (gc.State)
            {
                case GameController.AppState.Title: DrawTitle(); break;
                case GameController.AppState.Setup: DrawSetup(); break;
                case GameController.AppState.Cleared: DrawResult("🎉 クリア！", $"獲得メダル +{gc.LastReward}"); break;
                case GameController.AppState.GameOver: DrawResult("💀 ゲームオーバー", "力尽きた…"); break;
                case GameController.AppState.EndingChoice: DrawEndingChoice(); break;
                case GameController.AppState.Opening: DrawOpening(); break;
                case GameController.AppState.World: DrawWorld(); break;
            }
        }

        void Dim(float alpha = 0.72f)
        {
            var prev = GUI.color;
            var bg = Bg();
            if (bg != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bg, ScaleMode.ScaleAndCrop);
                GUI.color = new Color(0, 0, 0, alpha * 0.6f); // 背景の上に薄暗幕
            }
            else GUI.color = new Color(0, 0, 0, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawTitle()
        {
            Dim();
            GUI.Label(new Rect(0, Screen.height * 0.22f, Screen.width, 60), "MIRAGE GATE", _title);
            GUI.Label(new Rect(0, Screen.height * 0.22f + 60, Screen.width, 30),
                "封印カードの迷宮  — Unity Remake", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            float bw = 240, bh = 46, cx = Screen.width / 2 - bw / 2;
            float y0 = Screen.height * 0.45f;
            if (GUI.Button(new Rect(cx, y0, bw, bh), "📖 物語をはじめる", _btn)) gc.StartStory(1);
            if (GUI.Button(new Rect(cx, y0 + (bh + 10), bw, bh), "⚔ フリープレイ", _btn)) gc.GoSetup();
            if (GUI.Button(new Rect(cx, y0 + (bh + 10) * 2, bw, bh), "🏰 ワールド（城攻略）", _btn)) gc.OpenWorld();
            GUI.Label(new Rect(0, y0 + (bh + 10) * 3 + 4, Screen.width, 24),
                $"所持メダル {gc.Medals}", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }

        void DrawSetup()
        {
            Dim();
            var jobs = gc.AvailableJobs; var duns = gc.Dungeons;
            if (jobs == null || duns == null || jobs.Count == 0 || duns.Count == 0)
            {
                GUI.Label(new Rect(0, Screen.height / 2 - 20, Screen.width, 40),
                    "データ未投入。メニュー MirageGate ▸ Import Data ▸ ★Import ALL を実行してください。",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                return;
            }
            _jobIdx = Mathf.Clamp(_jobIdx, 0, jobs.Count - 1);
            _dunIdx = Mathf.Clamp(_dunIdx, 0, duns.Count - 1);

            float colW = 300, h = Screen.height * 0.5f, top = Screen.height * 0.18f;
            // 職業
            GUILayout.BeginArea(new Rect(Screen.width / 2 - colW - 20, top, colW, h + 40), GUI.skin.box);
            GUILayout.Label("<b>職業を選ぶ</b>", _box);
            _scrollJob = GUILayout.BeginScrollView(_scrollJob, GUILayout.Height(h));
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                bool sel = i == _jobIdx;
                if (GUILayout.Toggle(sel, $"{j.emoji} {j.jpName}  HP{j.hp}/ATK{j.atk}/DEF{j.def}", _box) && !sel) _jobIdx = i;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            // ダンジョン
            GUILayout.BeginArea(new Rect(Screen.width / 2 + 20, top, colW, h + 40), GUI.skin.box);
            GUILayout.Label("<b>ダンジョンを選ぶ</b>", _box);
            _scrollDun = GUILayout.BeginScrollView(_scrollDun, GUILayout.Height(h));
            for (int i = 0; i < duns.Count; i++)
            {
                var d = duns[i];
                bool sel = i == _dunIdx;
                if (GUILayout.Toggle(sel, $"★{d.star} {d.jpName}  {d.floors}F  BET{d.bet}/WIN{d.win}", _box) && !sel) _dunIdx = i;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            float bw = 200, bh = 46;
            if (GUI.Button(new Rect(Screen.width / 2 - bw - 10, top + h + 56, bw, bh), "出撃！", _btn))
                gc.StartChosen(jobs[_jobIdx], duns[_dunIdx]);
            if (GUI.Button(new Rect(Screen.width / 2 + 10, top + h + 56, bw, bh), "タイトルへ", _btn))
                gc.GoTitle();
        }

        readonly System.Collections.Generic.Dictionary<string, Texture2D> _opTex = new System.Collections.Generic.Dictionary<string, Texture2D>();
        Texture2D OpTex(string img)
        {
            if (string.IsNullOrEmpty(img)) return null;
            if (!_opTex.TryGetValue(img, out var t)) { t = Resources.Load<Texture2D>("op/" + img); _opTex[img] = t; }
            return t;
        }

        void DrawOpening()
        {
            var sc = gc.CurrentOpScene;
            if (sc == null) { gc.OpSkip(); return; }

            // クリックで送り
            var e = Event.current;
            if (e.type == EventType.MouseDown) { gc.OpAdvance(); e.Use(); return; }

            var tex = OpTex(sc.img);
            if (tex != null) { GUI.color = Color.white; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tex, ScaleMode.ScaleAndCrop); }
            else { GUI.color = new Color(0.03f, 0.04f, 0.07f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); }
            // 下部に暗幕＋字幕
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(0, Screen.height * 0.62f, Screen.width, Screen.height * 0.38f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string body = sc.lines != null ? string.Join("\n", sc.lines) : "";
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true,
                fontSize = sc.title ? 38 : 20, fontStyle = sc.title ? FontStyle.Bold : FontStyle.Normal };
            st.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.66f, Screen.width, Screen.height * 0.3f), body, st);

            if (GUI.Button(new Rect(Screen.width - 110, 20, 90, 32), "スキップ")) gc.OpSkip();
            GUI.Label(new Rect(0, Screen.height - 28, Screen.width, 20), "クリックで進む",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(1, 1, 1, 0.6f) } });
        }

        Vector2 _scrollLord;
        void DrawWorld()
        {
            Dim();
            GUI.Label(new Rect(0, Screen.height * 0.1f, Screen.width, 50), "🏰 城攻略", _title);
            GUI.Label(new Rect(0, Screen.height * 0.1f + 52, Screen.width, 24),
                "城主を倒すとその職業が解放される（覇者は超強敵）",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 });

            var lords = gc.Lords != null ? gc.Lords.lords : null;
            float w = 460, top = Screen.height * 0.22f, h = Screen.height * 0.55f;
            GUILayout.BeginArea(new Rect(Screen.width / 2 - w / 2, top, w, h), GUI.skin.box);
            _scrollLord = GUILayout.BeginScrollView(_scrollLord);
            if (lords != null)
                foreach (var l in lords)
                {
                    bool owned = gc.IsJobUnlocked(l.job);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(owned ? $"<color=#8f8>✔ {l.nm}（攻略済）</color>" : $"<b>{l.nm}</b>  — {l.teaser}", _box, GUILayout.Width(360));
                    GUI.enabled = !owned;
                    if (GUILayout.Button(owned ? "—" : "挑む", GUILayout.Width(70))) gc.StartLordBattle(l.job);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (GUI.Button(new Rect(Screen.width / 2 - 100, top + h + 14, 200, 44), "タイトルへ", _btn)) gc.GoTitle();
        }

        void DrawEndingChoice()
        {
            Dim(0.85f);
            var e = gc.CurrentEnding;
            GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 56), "最後の選択", _title);
            GUI.Label(new Rect(0, Screen.height * 0.18f + 60, Screen.width, 28),
                e != null ? e.sub : "", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, wordWrap = true });
            float bw = 520, bh = 50, y = Screen.height * 0.42f;
            var choices = e != null ? e.choices : null;
            for (int i = 0; i < 3; i++)
            {
                string lab = (choices != null && i < choices.Count) ? choices[i] : ("選択 " + (i + 1));
                if (GUI.Button(new Rect(Screen.width / 2 - bw / 2, y + i * (bh + 12), bw, bh), lab, _btn))
                    gc.ChooseEnding(i);
            }
        }

        void DrawResult(string title, string sub)
        {
            Dim();
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60), title, _title);
            GUI.Label(new Rect(0, Screen.height * 0.3f + 64, Screen.width, 28), sub,
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
            GUI.Label(new Rect(0, Screen.height * 0.3f + 92, Screen.width, 24), $"所持メダル {gc.Medals}",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            float bw = 200, bh = 46, y = Screen.height * 0.56f;
            if (GUI.Button(new Rect(Screen.width / 2 - bw - 10, y, bw, bh), "もう一度", _btn)) gc.Retry();
            if (GUI.Button(new Rect(Screen.width / 2 + 10, y, bw, bh), "タイトルへ", _btn)) gc.GoTitle();
        }
    }
}
