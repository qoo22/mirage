using System;
using System.Collections.Generic;
using UnityEngine;

namespace MirageGate.Systems
{
    /// <summary>
    /// VN会話の進行・表示（§10.2）。原作 playCutscene 相当。OnGUIで会話ボックス＋立ち絵チップ＋選択肢。
    /// クリック/Space/Zで送り、文字送り中はクリックで全文表示。選択肢はフラグに記録。
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public float charsPerSec = 42f;

        // 話者キー→表示名（CHAR_DESC, §10.2）
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            {"hero","レン"},{"pico","ピコ"},{"lily","リリィ"},{"mia","ミア"},{"mina","ミア"},
            {"noa","ノア"},{"garm","ガルム"},{"vail","ヴェイル"},{"narr",""},{"boss","？？？"},
            {"mob","村人"},{"kyle","カイル"},{"mel","メル"},{"sigma","シグマ"},{"crow","クロウ"},
            {"arzen","アルゼン"},{"leon","レオン"},{"iris","イリス"},{"gram","グラム"},
            {"setc","セト"},{"set","セト"},{"riine","リーネ"},{"elder","長老"},{"mom","母"},{"doc","医者"},
        };

        public bool IsPlaying { get; private set; }
        List<StoryLine> _lines;
        int _idx;
        float _startT;
        Action _onDone;
        Action<string> _onFlag;   // 選択肢のset通知
        GUIStyle _name, _text, _narr, _btn;

        public void Play(List<StoryLine> lines, Action onDone, Action<string> onFlag = null)
        {
            _lines = lines ?? new List<StoryLine>();
            _idx = 0; _onDone = onDone; _onFlag = onFlag;
            IsPlaying = _lines.Count > 0;
            _startT = Time.realtimeSinceStartup;
            if (!IsPlaying) onDone?.Invoke();
        }

        StoryLine Cur => (_lines != null && _idx < _lines.Count) ? _lines[_idx] : null;

        void Advance()
        {
            int full = Cur != null ? Cur.text.Length : 0;
            int shown = Mathf.FloorToInt((Time.realtimeSinceStartup - _startT) * charsPerSec);
            if (shown < full) { _startT = Time.realtimeSinceStartup - full / charsPerSec; return; } // 一気に全表示
            _idx++;
            _startT = Time.realtimeSinceStartup;
            if (_idx >= _lines.Count) { IsPlaying = false; var d = _onDone; _onDone = null; d?.Invoke(); }
        }

        void Styles()
        {
            if (_text != null) return;
            _name = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, richText = true };
            _name.normal.textColor = new Color(1f, 0.86f, 0.45f);
            _text = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = true };
            _text.normal.textColor = Color.white;
            _narr = new GUIStyle(_text) { fontStyle = FontStyle.Italic };
            _narr.normal.textColor = new Color(0.8f, 0.85f, 0.95f);
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }

        void OnGUI()
        {
            if (!IsPlaying || Cur == null) return;
            Styles();
            var ln = Cur;

            // 入力で送り（選択肢が無いとき）
            if (ln.choices == null || ln.choices.Count == 0)
            {
                var e = Event.current;
                if (e.type == EventType.MouseDown ||
                    (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Z || e.keyCode == KeyCode.Return)))
                { Advance(); e.Use(); }
            }

            float h = 170, m = 24;
            var box = new Rect(m, Screen.height - h - m, Screen.width - m * 2, h);
            GUI.color = new Color(0.04f, 0.05f, 0.09f, 0.92f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            string speaker = Names.TryGetValue(ln.who ?? "", out var nm) ? nm : ln.who;
            // 文字送り
            int full = ln.text.Length;
            int shown = Mathf.Clamp(Mathf.FloorToInt((Time.realtimeSinceStartup - _startT) * charsPerSec), 0, full);
            string shownText = ln.text.Substring(0, shown);

            var inner = new Rect(box.x + 18, box.y + 12, box.width - 36, box.height - 24);
            if (ln.classic || string.IsNullOrEmpty(speaker))
            {
                GUI.Label(inner, shownText, _narr);
            }
            else
            {
                GUI.Label(new Rect(inner.x, inner.y, inner.width, 22), (ln.star ? "★ " : "") + speaker, _name);
                GUI.Label(new Rect(inner.x, inner.y + 26, inner.width, inner.height - 26), shownText, _text);
            }

            // 選択肢
            if (ln.choices != null && ln.choices.Count > 0 && shown >= full)
            {
                float bw = 360, bh = 30, by = box.y - ln.choices.Count * (bh + 6) - 8;
                for (int i = 0; i < ln.choices.Count; i++)
                {
                    var c = ln.choices[i];
                    if (GUI.Button(new Rect(Screen.width / 2 - bw / 2, by + i * (bh + 6), bw, bh), c.label, _btn))
                    {
                        if (!string.IsNullOrEmpty(c.set)) _onFlag?.Invoke(c.set);
                        Advance();
                    }
                }
            }
            else if (shown >= full)
            {
                GUI.Label(new Rect(box.xMax - 90, box.yMax - 26, 80, 20), "▼ Space",
                    new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1, 1, 1, 0.6f) } });
            }
        }
    }
}
