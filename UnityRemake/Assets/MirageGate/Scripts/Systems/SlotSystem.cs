using UnityEngine;
using MirageGate.Core;
using MirageGate.Data;

namespace MirageGate.Systems
{
    /// <summary>スロット結果。</summary>
    public struct SlotResult
    {
        public int coins;
        public CardData card;     // 当たりカード（卵=モンスター/SPECIAL=禁忌）
        public Rarity rarity;
        public bool isJackpot;    // SSR以上演出
    }

    /// <summary>
    /// スロット/カジノ（§7.5）。台種で掛金倍率と確率が変わる。卵はUR45%。
    /// SLOT_BET: item×1 / gambler×2 / monster×3。bet = line*mult*betMul。
    /// </summary>
    public class SlotSystem
    {
        readonly GameBalanceConfig cfg;
        public SlotSystem(GameBalanceConfig cfg) { this.cfg = cfg; }

        public int BetMultiplier(SlotMachineType t)
            => t == SlotMachineType.Monster ? 3 : t == SlotMachineType.Gambler ? 2 : 1;

        public int BetCost(SlotMachineType t, int line, int mult) => line * mult * BetMultiplier(t);

        /// <summary>1回転の抽選（§7.5）。Diamond1.5% / 卵13%(monster) / SPECIAL6%(gambler)。</summary>
        public SlotResult Spin(SlotMachineType t, int line, int mult, System.Func<float> rnd01 = null)
        {
            rnd01 ??= () => Random.value;
            // TODO: 絵柄リール抽選 SLOT_SYMS。Diamond(wild500)/Ruby200/Aqua120/Sapphire80/Pearl40/Topaz30/Sword20/Heart20/Egg/Special/?8
            // monster台：13%で卵 → 45%でUR、残りSSR/UR（slotRare）
            // gambler台：6%でSPECIAL → 禁忌カード
            return new SlotResult();
        }

        /// <summary>お姉さんセリフ（§7.5）：所持メダル/手札数で分岐。</summary>
        public string SlotLadyLine(int medals, int handCount)
        {
            // TODO: medals<10「軍資金が…」/ hand>=8「手札パンパンね」/ medals>=500「景気がいいわね」/ …
            return "冒険に必要なカードは自らの手で";
        }
    }
}
