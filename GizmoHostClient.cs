using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor プラグインの GizmoHost へのリフレクションブリッジ。
    /// 登録すると SceneView / GameView の入力・描画ディスパッチに乗り、
    /// 各ビューの RT 座標とカメラでギズモを操作できる。
    /// SceneEditor が不在・旧バージョンの場合は isAvailable が false になり、
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
        private static Func<bool> _isViewActive;
        private static bool _viewActiveFailed;
        private static bool _initialized;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _register != null;
            }
        }

        /// <summary>
        /// ホストに外部ギズモを描画・操作できるビューが稼働しているか。
        /// SceneEditor が入っていても GameView が window mode でなく SceneView も
        /// 非表示なら、ホストからは描画も入力も届かない。呼び出し側はこの間だけ
        /// standalone (Camera.main + Input.mousePosition) で駆動する。
        /// IsViewActive を持たない旧ホストでは常に稼働扱いにする (従来動作を保つ)
        /// </summary>
        public static bool isViewActive
        {
            get
            {
                if (!isAvailable || _viewActiveFailed)
                {
                    return false;
                }
                if (_isViewActive == null)
                {
                    return true;
                }

                try
                {
                    return _isViewActive();
                }
                catch (Exception e)
                {
                    // 毎フレーム問い合わせる経路なので、一度失敗したら以後は standalone に倒して
                    // ログを溢れさせない
                    MTEUtils.LogWarning("GizmoHostClient: GizmoHost の稼働状態の取得に失敗しました: " + e.Message);
                    _viewActiveFailed = true;
                    return false;
                }
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

                // ビュー稼働状態の問い合わせは後発 API のため任意。
                // 旧ホストでは見つからないが登録自体は従来通り機能するので警告は出さない
                var isViewActive = type.GetMethod("IsViewActive", BindingFlags.Public | BindingFlags.Static);
                if (isViewActive != null)
                {
                    _isViewActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), isViewActive);
                }
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は standalone へフォールバックする
                MTEUtils.LogWarning("GizmoHostClient: GizmoHost との接続に失敗しました: " + e.Message);
                _register = null;
                _unregister = null;
                _isViewActive = null;
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
            if (!isAvailable)
            {
                return null;
            }

            // ホスト側で失敗した場合はハンドルなし (= standalone) で続行する
            try
            {
                return _register(name, tryBeginDrag, updateDrag, endDrag, isDragging, draw);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("GizmoHostClient: GizmoHost への登録に失敗しました: " + e.Message);
                return null;
            }
        }

        public static void Unregister(object handle)
        {
            if (handle == null || !isAvailable)
            {
                return;
            }

            try
            {
                _unregister(handle);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("GizmoHostClient: GizmoHost からの登録解除に失敗しました: " + e.Message);
            }
        }
    }
}
