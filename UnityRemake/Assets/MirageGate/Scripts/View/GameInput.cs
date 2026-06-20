using UnityEngine;
using UnityEngine.InputSystem;
using MirageGate.Runtime;
using MirageGate.Systems;

namespace MirageGate.View
{
    /// <summary>
    /// キーボード入力 → TurnManager（§2）。新Input System（com.unity.inputsystem）対応。
    /// 8方向移動＋カード使用。矢印/WASD=直交、QEZC=斜め、数字1-9/0=手札のカード使用（対象＝最寄りの敵）。
    /// </summary>
    public class GameInput : MonoBehaviour
    {
        public TurnManager turnManager;
        public GameDatabase db;
        public Hud hud;
        RunState _run;

        public void SetRun(RunState run) => _run = run;

        void Update()
        {
            if (turnManager == null || _run == null) return;
            if (hud != null && hud.InteractionOpen) return; // ショップ/スロット中は移動を止める
            var kb = Keyboard.current;
            if (kb == null) return;

            int dx = 0, dy = 0;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) dy = -1;
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) dy = 1;
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dx = -1;
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dx = 1;
            else if (kb.qKey.wasPressedThisFrame) { dx = -1; dy = -1; }
            else if (kb.eKey.wasPressedThisFrame) { dx = 1; dy = -1; }
            else if (kb.zKey.wasPressedThisFrame) { dx = -1; dy = 1; }
            else if (kb.cKey.wasPressedThisFrame) { dx = 1; dy = 1; }

            if (dx != 0 || dy != 0) { turnManager.TryMove(dx, dy); return; }

            // カード使用 1..9, 0
            var digits = new[] {
                kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key,
                kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key, kb.digit0Key
            };
            for (int i = 0; i < digits.Length; i++)
                if (digits[i].wasPressedThisFrame) { UseCard(i); return; }
        }

        void UseCard(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _run.player.hand.Count) return;
            var card = db != null ? db.Card(_run.player.hand[handIndex]) : null;
            if (card == null) return;
            turnManager.UseCard(card, handIndex, NearestEnemy());
        }

        MonsterInstance NearestEnemy()
        {
            MonsterInstance best = null; int bestD = int.MaxValue;
            foreach (var m in _run.monsters)
            {
                if (m.killed) continue;
                int d = GridMap.Chebyshev(m.x, m.y, _run.player.x, _run.player.y);
                if (d < bestD) { bestD = d; best = m; }
            }
            return best;
        }
    }
}
