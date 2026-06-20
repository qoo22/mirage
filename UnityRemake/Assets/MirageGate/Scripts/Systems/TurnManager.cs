using System.Collections;
using UnityEngine;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// 1ターン制の進行制御（§2）。AwaitInput → ResolvePlayer → EnemyPhase → Upkeep。
    /// コンボ中(§4)は EnemyPhase をスキップして追加行動。
    /// 撃破(loot/exp/演出)は hp<=0 の敵を combat.KillMonster へスイープして一元処理。
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public enum Phase { AwaitInput, ResolvePlayer, EnemyPhase, Upkeep }
        public Phase Current { get; private set; } = Phase.AwaitInput;
        public bool Busy { get; private set; }
        public bool InputEnabled = true; // Playing状態以外ではfalse（タイトル/クリア等）

        RunState run;
        CombatResolver combat;
        EnemyAI enemyAI;
        StatusEffectManager status;
        MovementSystem movement;
        CardEffectExecutor cardEffect;
        GameFeelDirector feel;
        public VisionSystem vision; // 任意。割り当てると視界(fog)を更新

        public System.Action OnFloorClear;   // ゴール到達
        public System.Action OnGameOver;     // HP0
        public System.Action<FloorItem> OnShop;   // ショップタイル到達
        public System.Action<FloorItem> OnSlot;   // スロットタイル到達
        public System.Action<string> OnPickupCard; // カード拾得（id）通知（UI用）
        public System.Action OnEscape;             // エスケープカードで撤退
        public System.Action<string> OnHandFull;   // 手札満杯で拾えなかったカードid

        public void Init(RunState run, CombatResolver combat, EnemyAI enemyAI,
            StatusEffectManager status, MovementSystem movement, CardEffectExecutor cardEffect, GameFeelDirector feel)
        {
            this.run = run; this.combat = combat; this.enemyAI = enemyAI; this.status = status;
            this.movement = movement; this.cardEffect = cardEffect; this.feel = feel;
        }

        bool CanInput => InputEnabled && Current == Phase.AwaitInput && !Busy && (feel == null || !feel.IsHitstopActive);

        /// <summary>移動入力（8方向）。進行先に敵なら攻撃に切替（§2.2）。</summary>
        public void TryMove(int dx, int dy)
        {
            if (!CanInput) return;
            var res = movement.ResolveMove(run, dx, dy);

            if (res.blockedByMonster)
            {
                run.player.animAtkT = Time.realtimeSinceStartup; // 攻撃アニメ
                combat.PlayerAttack(run.player, res.target, run);
                StartCoroutine(EndPlayerTurn(true));
            }
            else if (res.moved)
            {
                run.player.animMoveT = Time.realtimeSinceStartup; // 歩行アニメ
                PickupAt(run.player.x, run.player.y);
                vision?.Compute(run); // 移動直後に視界更新（描画即時反映）
                if (res.reachedGoal) { Current = Phase.AwaitInput; OnFloorClear?.Invoke(); return; }
                StartCoroutine(EndPlayerTurn(true));
            }
            // 壁＝何も起きない（無反応撲滅のためUI側でSE/小演出を出すのは可）
        }

        /// <summary>カード使用（§4.3）。成立で1ターン消費。</summary>
        public void UseCard(int handIndex, MonsterInstance target)
        {
            if (!CanInput || handIndex < 0 || handIndex >= run.player.hand.Count) return;
            // TODO: id→CardData は GameDatabase 経由。ここでは呼び出し側で解決済みを想定。
        }

        public void UseCard(Data.CardData card, int handIndex, MonsterInstance target)
        {
            if (!CanInput) return;
            var ctx = new CardCastContext { run = run, singleTarget = target };
            if (!cardEffect.TryCast(card, run.player, ctx)) return; // MP不足
            run.player.hand.RemoveAt(handIndex);
            if (run.escaped) { Current = Phase.AwaitInput; OnEscape?.Invoke(); return; } // 撤退
            SweepDead();
            StartCoroutine(EndPlayerTurn(true));
        }

        /// <summary>プレイヤー行動後：撃破スイープ→（コンボ外なら）敵フェーズ→Upkeep。</summary>
        IEnumerator EndPlayerTurn(bool spendsTurn)
        {
            Busy = true;
            Current = Phase.ResolvePlayer;
            SweepDead();

            if (CheckGameOver()) { Busy = false; yield break; }

            bool combo = run.player.Status(Core.StatusType.Combo) > 0;
            if (combo) status.Apply(run.player, Core.StatusType.Combo, run.player.Status(Core.StatusType.Combo) - 1);

            if (spendsTurn && !combo)
            {
                Current = Phase.EnemyPhase;
                yield return StartCoroutine(enemyAI.RunEnemyPhase(run)); // §2.3 揺れ収束待ち
                if (CheckGameOver()) { Busy = false; yield break; }

                Current = Phase.Upkeep;
                status.TickAll(run);   // 毒/状態減衰/MP回復（§2.2）
                SweepDead();
                vision?.Compute(run);  // 敵移動後の視界更新
                if (CheckGameOver()) { Busy = false; yield break; }
            }
            Current = Phase.AwaitInput;
            Busy = false;
        }

        /// <summary>足元のアイテムを拾得（§4.6/§7）。ショップ/スロットはイベント通知。</summary>
        void PickupAt(int x, int y)
        {
            FloorItem found = null;
            foreach (var it in run.items) if (it.x == x && it.y == y) { found = it; break; }
            if (found == null) return;

            switch (found.kind)
            {
                case FloorItem.Kind.Gem:
                    run.player.loot += found.gemValue;
                    run.items.Remove(found);
                    break;
                case FloorItem.Kind.Card:
                    if (string.IsNullOrEmpty(found.cardId)) { run.items.Remove(found); break; }
                    if (run.player.hand.Count < run.player.handMax)
                    {
                        run.player.hand.Add(found.cardId);
                        OnPickupCard?.Invoke(found.cardId);
                        run.items.Remove(found);
                    }
                    else
                    {
                        run.items.Remove(found);     // 床から取り上げ、交換UIへ
                        OnHandFull?.Invoke(found.cardId); // 満杯：交換/破棄を選ばせる（§4.6）
                    }
                    break;
                case FloorItem.Kind.Shop:
                    OnShop?.Invoke(found); // タイルは残置（再訪可）
                    break;
                case FloorItem.Kind.Slot:
                    OnSlot?.Invoke(found);
                    break;
            }
        }

        /// <summary>hp<=0 だが未撃破フラグの敵を正式撃破（loot/exp/演出は KillMonster が担う）。</summary>
        void SweepDead()
        {
            for (int i = 0; i < run.monsters.Count; i++)
            {
                var m = run.monsters[i];
                if (!m.killed && m.hp <= 0) combat.KillMonster(run.player, m, run);
            }
            run.monsters.RemoveAll(m => m.killed);
        }

        bool CheckGameOver()
        {
            if (run.player.hp > 0) return false;
            OnGameOver?.Invoke();
            return true;
        }
    }
}
