using System;
using System.Collections.Generic;
using UnityEngine;

namespace MirageGate.Systems
{
    /// <summary>会話の選択肢（原作 choices: [{label,set}]）。</summary>
    [Serializable] public class StoryChoice { public string label; public string set; }

    /// <summary>会話1行（原作 [who,text,face,opts]）。</summary>
    [Serializable]
    public class StoryLine
    {
        public string who;
        public string text;
        public string face;
        public string effect;     // shake/tremor/red/slow/silent/noflow
        public bool classic;      // ナレーション/システム表示
        public bool star;         // 重要台詞
        public List<StoryChoice> choices = new List<StoryChoice>();
    }

    /// <summary>1章（intro→dungeon→boss/outro）。原作 STORY/STORY2 の要素。</summary>
    [Serializable]
    public class StoryChapter
    {
        public string key;
        public string title;
        public string dungeon;
        public string bossNm;
        public bool isFinal;
        public List<StoryLine> intro = new List<StoryLine>();
        public List<StoryLine> boss = new List<StoryLine>();
        public List<StoryLine> outro = new List<StoryLine>();
    }

    [Serializable]
    public class StoryFile
    {
        public List<StoryChapter> chapters = new List<StoryChapter>();
    }

    /// <summary>エンディング1部ぶん（3択＋各結末＋濁った手, §10.4）。</summary>
    [Serializable]
    public class EndingPart
    {
        public int part;
        public string sub;
        public List<string> choices = new List<string>();
        public List<StoryLine> bad = new List<StoryLine>();
        public List<StoryLine> normal = new List<StoryLine>();
        public List<StoryLine> trueEnd = new List<StoryLine>();
        public List<StoryLine> tainted = new List<StoryLine>();
    }

    [Serializable] public class EndingFile { public List<EndingPart> parts = new List<EndingPart>(); }

    /// <summary>オープニング1シーン（§10.1 OP_SCENES）。</summary>
    [Serializable] public class OpeningScene { public string img; public List<string> lines = new List<string>(); public bool title; }
    [Serializable] public class OpeningFile { public List<OpeningScene> scenes = new List<OpeningScene>(); }

    /// <summary>城主（隠しキャラ）。撃破で職業解放（原作CB_SCENE）。</summary>
    [Serializable]
    public class LordData
    {
        public string job;    // 解放される職業id
        public string nm;     // 城主名（覇者名ではなく登場名 例:剣豪 ムサシ）
        public string teaser;
        public string tex;
        public List<StoryLine> intro = new List<StoryLine>();
        public List<StoryLine> defeat = new List<StoryLine>();
    }
    [Serializable] public class LordFile { public List<LordData> lords = new List<LordData>(); }

    /// <summary>Resources/story/story{part}.json（原作STORY/STORY2を抽出したもの）を読む。</summary>
    public static class StoryData
    {
        public static StoryFile Load(int part)
        {
            var ta = Resources.Load<TextAsset>($"story/story{part}");
            if (ta == null) { Debug.LogWarning($"[Story] story{part}.json が見つかりません"); return new StoryFile(); }
            return JsonUtility.FromJson<StoryFile>(ta.text) ?? new StoryFile();
        }

        public static EndingPart LoadEnding(int part)
        {
            var ta = Resources.Load<TextAsset>("story/endings");
            if (ta == null) return null;
            var f = JsonUtility.FromJson<EndingFile>(ta.text);
            if (f == null) return null;
            foreach (var p in f.parts) if (p.part == part) return p;
            return null;
        }

        public static OpeningFile LoadOpening()
        {
            var ta = Resources.Load<TextAsset>("story/opening");
            return ta != null ? JsonUtility.FromJson<OpeningFile>(ta.text) : null;
        }

        public static LordFile LoadLords()
        {
            var ta = Resources.Load<TextAsset>("story/lords");
            return ta != null ? JsonUtility.FromJson<LordFile>(ta.text) : new LordFile();
        }
    }
}
