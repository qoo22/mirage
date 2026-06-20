using UnityEngine;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// チュートリアル（§11）。原作 tutTick 相当。遊びの中で出る文脈ヒントをトースト表示。
    /// 各ヒントはセッション中1回だけ。Enabled（Playing中のみ）でON。
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public bool Enabled;
        RunState _run;
        GUIStyle _toast;

        bool _hMove, _hEnemy, _hCard, _hGoal;
        string _msg = "";
        float _msgUntil;

        public void SetRun(RunState run)
        {
            _run = run;
            // 入場ヒント（最初のフロアで一度）
            if (run != null && !_hMove) { _hMove = true; Show("矢印キー / WASD で移動。金色のマス（出口）を目指そう"); }
        }

        void Show(string m) { _msg = m; _msgUntil = Time.realtimeSinceStartup + 5f; }

        void Update()
        {
            if (!Enabled || _run == null || _run.map == null) return;
            var p = _run.player;

            if (!_hEnemy)
                foreach (var m in _run.monsters)
                    if (!m.killed && _run.map.lit[m.x, m.y]) { _hEnemy = true; Show("敵だ！ 敵に向かって移動すると攻撃。隣で当たる"); break; }

            if (!_hCard && p != null && p.hand.Count > 0) { _hCard = true; Show("手札がある。1〜5キーでカード使用（対象は最寄りの敵）"); }

            if (!_hGoal && _run.map.lit[_run.map.goal.x, _run.map.goal.y]
                && GridMap.Chebyshev(p.x, p.y, _run.map.goal.x, _run.map.goal.y) <= 4)
            { _hGoal = true; Show(_run.map.goalIsCrystal ? "光るクリスタルがゴール！ 触れてクリア" : "下り階段が見えた。乗ると次のフロアへ"); }
        }

        void OnGUI()
        {
            if (!Enabled || string.IsNullOrEmpty(_msg) || Time.realtimeSinceStartup > _msgUntil) return;
            if (_toast == null)
            {
                _toast = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _toast.normal.textColor = Color.white;
            }
            float w = 520, h = 44;
            GUI.color = new Color(0.1f, 0.12f, 0.2f, 0.95f);
            var r = new Rect(Screen.width / 2 - w / 2, 110, w, h);
            GUI.Box(r, "💡 " + _msg, _toast);
            GUI.color = Color.white;
        }
    }
}
