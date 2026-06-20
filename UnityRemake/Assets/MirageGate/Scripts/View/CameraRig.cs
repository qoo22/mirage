using UnityEngine;
using MirageGate.Systems;

namespace MirageGate.View
{
    /// <summary>
    /// プレイヤー追従カメラ。GameFeelDirectorの揺れオフセットを毎フレーム加算（§8.5）。
    /// 揺れはunscaled（realtime）で計算されるのでヒットストップ中も効く。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public Transform target;          // 追従先（GameBoardView.PlayerTransform）
        public GameFeelDirector feel;
        public float followLerp = 12f;
        public float orthoSize = 6f;
        public float z = -10f;

        Vector3 _base;
        bool _init;

        void Start()
        {
            var cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
        }

        void LateUpdate()
        {
            if (target)
            {
                var goal = new Vector3(target.position.x, target.position.y, z);
                if (!_init) { _base = goal; _init = true; } // 初回はスナップ
                else _base = Vector3.Lerp(_base, goal, 1f - Mathf.Exp(-followLerp * Time.unscaledDeltaTime));
            }
            Vector3 shake = Vector3.zero;
            if (feel != null) { var s = feel.ShakeOffset(); shake = new Vector3(s.x, s.y, 0); }
            transform.position = _base + shake;
        }
    }
}
