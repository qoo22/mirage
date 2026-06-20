using System;
using System.Collections.Generic;
using UnityEngine;

namespace MirageGate.Data
{
    /// <summary>会話演出フラグ（原作 effect: shake/tremor/red/slow/silent/noflow）。</summary>
    public enum DialogueEffect { None, Shake, Tremor, Red, Slow, Silent, NoFlow }

    /// <summary>1つの選択肢。set=フラグ名, gotoの分岐はDialogueManagerが解決。</summary>
    [Serializable]
    public class DialogueChoice
    {
        public string label;
        public string setFlag;   // mg_flags に記録
        public string gotoId;    // 分岐先（任意）
        public bool star;        // ★重要選択
    }

    /// <summary>1行の会話。原作 [who, text, expr?, opts?] / {who,text,face,effect,choices}。</summary>
    [Serializable]
    public class DialogueLine
    {
        public string who = "hero";       // 話者キー（CHAR_DESC, §10.2）
        [TextArea] public string text;
        public string face = "";           // 表情差分
        public DialogueEffect effect = DialogueEffect.None;
        public bool classic = false;       // ナレーション/システム（顔チップのみ）
        public bool star = false;          // 会話ログに重要マーク
        public List<DialogueChoice> choices = new List<DialogueChoice>();
    }

    /// <summary>会話シーケンス（カットシーン1本）。playCutscene の lines に対応（§10.2）。</summary>
    [CreateAssetMenu(menuName = "MirageGate/Dialogue", fileName = "Dialogue_")]
    public class DialogueData : ScriptableObject
    {
        public string id;
        public string backgroundKey = "";
        public List<DialogueLine> lines = new List<DialogueLine>();
    }
}
