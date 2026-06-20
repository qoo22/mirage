using System.Collections.Generic;
using UnityEngine;

namespace MirageGate.View
{
    /// <summary>
    /// 斬撃FX5種（§8.1 arc/thrust/sweep/double/chop）。LineRendererで procedural に描く（素材不要）。
    /// 会心は金色＆長め(520ms)・通常は白(380ms)。realtime駆動でヒットストップ中も再生。
    /// </summary>
    public class SlashFxPlayer : MonoBehaviour
    {
        public float tileSize = 1f;
        public Color normalColor = new Color(1f, 1f, 1f, 0.95f);
        public Color critColor = new Color(1f, 0.92f, 0.45f, 1f);
        public float baseWidth = 0.14f;

        Material _mat;
        readonly List<Stroke> _pool = new List<Stroke>();

        class Stroke
        {
            public LineRenderer lr;
            public float start, dur;
            public Color color;
            public Vector3[] pts;
            public bool active;
        }

        void Awake()
        {
            var sh = Shader.Find("Sprites/Default");
            _mat = new Material(sh != null ? sh : Shader.Find("Unlit/Color"));
        }

        Vector3 World(float gx, float gy) => new Vector3(gx * tileSize, -gy * tileSize, -2f);

        /// <summary>type: 0=arc 1=thrust 2=sweep 3=double 4=chop。</summary>
        public void Play(int gridX, int gridY, float dirX, float dirY, bool crit, int type)
        {
            float ang = Mathf.Atan2(-dirY, dirX); // グリッドy下→ワールドy反転
            Vector3 center = World(gridX, gridY);
            Color col = crit ? critColor : normalColor;
            float dur = (crit ? 520f : 380f) / 1000f;

            if (type == 3) // double = X字に2本
            {
                Spawn(BuildStroke(0, ang + 0.5f), center, col, dur);
                Spawn(BuildStroke(0, ang - 0.5f), center, col, dur);
            }
            else
            {
                Spawn(BuildStroke(type, ang), center, col, dur);
            }
        }

        /// <summary>ローカル形状を生成（+X=攻撃方向）し、angで回転した点列を返す。</summary>
        Vector3[] BuildStroke(int type, float ang)
        {
            var local = new List<Vector2>();
            switch (type)
            {
                case 1: // thrust（突き）：直線
                    local.Add(new Vector2(-0.1f, 0)); local.Add(new Vector2(0.85f, 0));
                    break;
                case 2: // sweep（横薙ぎ）：広い弧 ±80°
                    Arc(local, 0.6f, -1.4f, 1.4f, 10);
                    break;
                case 4: // chop（振り下ろし）：上→下の直線
                    local.Add(new Vector2(0.1f, 0.7f)); local.Add(new Vector2(0.1f, -0.7f));
                    break;
                default: // arc（弧斬り）：中程度の弧 ±60°
                    Arc(local, 0.5f, -1.0f, 1.0f, 9);
                    break;
            }
            float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
            var pts = new Vector3[local.Count];
            for (int i = 0; i < local.Count; i++)
            {
                var p = local[i];
                pts[i] = new Vector3(p.x * c - p.y * s, p.x * s + p.y * c, 0);
            }
            return pts;
        }

        static void Arc(List<Vector2> list, float r, float a0, float a1, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)(n - 1));
                list.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
        }

        void Spawn(Vector3[] localPts, Vector3 center, Color col, float dur)
        {
            var st = Rent();
            st.pts = localPts;
            st.start = Time.realtimeSinceStartup;
            st.dur = dur; st.color = col; st.active = true;
            st.lr.positionCount = localPts.Length;
            for (int i = 0; i < localPts.Length; i++) st.lr.SetPosition(i, center + localPts[i]);
            st.lr.enabled = true;
        }

        Stroke Rent()
        {
            foreach (var s in _pool) if (!s.active) return s;
            var go = new GameObject("slash");
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = _mat; lr.useWorldSpace = true;
            lr.numCapVertices = 2; lr.numCornerVertices = 2;
            lr.sortingOrder = 5; lr.textureMode = LineTextureMode.Stretch;
            var st = new Stroke { lr = lr };
            _pool.Add(st);
            return st;
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var s in _pool)
            {
                if (!s.active) continue;
                float t = (now - s.start) / s.dur;
                if (t >= 1f) { s.active = false; s.lr.enabled = false; continue; }
                // 幅：細→太→0／アルファ：1→0
                float w = baseWidth * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * 1.4f;
                s.lr.startWidth = w; s.lr.endWidth = w * 0.4f;
                var c = s.color; c.a = s.color.a * (1f - t);
                s.lr.startColor = c; s.lr.endColor = new Color(c.r, c.g, c.b, 0f);
            }
        }
    }
}
