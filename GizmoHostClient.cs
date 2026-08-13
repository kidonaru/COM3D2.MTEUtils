using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// EditorWindow プラグインの GizmoHost へのリフレクションブリッジ。
    /// 登録すると SceneView / GameView の入力・描画ディスパッチに乗り、
    /// 各ビューの RT 座標とカメラでギズモを操作できる。
    /// EditorWindow が不在・旧バージョンの場合は isAvailable が false になり、
    /// 呼び出し側は standalone (Camera.main + Input.mousePosition) で駆動する
    /// </summary>
    public static class GizmoHostClient
    {
        private delegate object RegisterDelegate(
            string name,
            Func<Camera, Vector2, bool> tryBeginDrag,
            Action<Camera, Vector2> updateDrag,
            Action endDrag,
            Func<bool> isDragging,
            Action<Camera> draw);

        private static RegisterDelegate _register;
        private static Action<object> _unregister;
        private static bool _initialized;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _register != null;
            }
        }

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // ロード順によってはホストのアセンブリが未登場のことがあるため、
            // 型が見つかるまでは _initialized を立てずに再試行を続ける
            var type = DockingClient.FindHostType("GizmoHost");
            if (type == null)
            {
                return;
            }

            // ここから先はホストの型は見つかっている。シグネチャ不一致は
            // バージョン差による恒久的な問題なので、この場合のみ standalone へ確定する
            _initialized = true;

            try
            {
                var register = type.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
                var unregister = type.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static);
                if (register == null || unregister == null)
                {
                    MTEUtils.LogWarning("GizmoHostClient: GizmoHost にシグネチャの一致するメソッドが見つかりませんでした");
                    return;
                }

                _register = (RegisterDelegate)Delegate.CreateDelegate(typeof(RegisterDelegate), register);
                _unregister = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), unregister);
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は standalone へフォールバックする
                MTEUtils.LogWarning("GizmoHostClient: GizmoHost との接続に失敗しました: " + e.Message);
                _register = null;
                _unregister = null;
            }
        }

        /// <summary>
        /// ギズモをホストへ登録する。戻り値はハンドル (ホスト不在なら null)。
        /// tryBeginDrag / updateDrag はビューのカメラと RT ピクセル座標で呼ばれる
        /// </summary>
        public static object Register(
            string name,
            Func<Camera, Vector2, bool> tryBeginDrag,
            Action<Camera, Vector2> updateDrag,
            Action endDrag,
            Func<bool> isDragging,
            Action<Camera> draw)
        {
            return isAvailable
                ? _register(name, tryBeginDrag, updateDrag, endDrag, isDragging, draw)
                : null;
        }

        public static void Unregister(object handle)
        {
            if (handle != null && isAvailable)
            {
                _unregister(handle);
            }
        }
    }
}
