using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// EditorWindow プラグインの InspectorHost へのリフレクションブリッジ。
    /// 登録すると、EW Inspector で対象オブジェクト選択時に内容描画が丸ごと委譲される。
    /// EditorWindow が不在・旧バージョンの場合は isAvailable が false になり、
    /// 呼び出し側は登録しない (EW Inspector は従来描画のまま)
    /// </summary>
    public static class InspectorHostClient
    {
        private delegate object RegisterDelegate(
            string name,
            Func<GameObject, bool> canDraw,
            Action<GameObject, Rect> draw);

        private static RegisterDelegate _register;
        private static Action<object> _unregister;
        private static Func<Rect> _getWindowRect;
        private static Func<bool> _isWindowVisible;
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
        /// ホストのウィンドウ状態を取得できるか。
        /// 取得できない旧バージョンの EditorWindow では、委譲先はドロップダウンを
        /// 出さずに済む UI (前後送りボタン等) へ倒す
        /// </summary>
        public static bool isWindowStateAvailable
        {
            get
            {
                Initialize();
                return _getWindowRect != null;
            }
        }

        /// <summary>ホストのウィンドウのスクリーン矩形。取得できなければ zero</summary>
        public static Rect hostWindowRect
            => isWindowStateAvailable ? _getWindowRect() : new Rect();

        /// <summary>ホストのウィンドウが描画されているか。取得できなければ false</summary>
        public static bool isHostWindowVisible
            => isWindowStateAvailable && _isWindowVisible();

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // ロード順によってはホストのアセンブリが未登場のことがあるため、
            // 型が見つかるまでは _initialized を立てずに再試行を続ける
            var type = DockingClient.FindHostType("InspectorHost");
            if (type == null)
            {
                return;
            }

            // ここから先はホストの型は見つかっている。シグネチャ不一致は
            // バージョン差による恒久的な問題なので、この場合のみ無効へ確定する
            _initialized = true;

            try
            {
                var register = type.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
                var unregister = type.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static);
                if (register == null || unregister == null)
                {
                    MTEUtils.LogWarning("InspectorHostClient: InspectorHost にシグネチャの一致するメソッドが見つかりませんでした");
                    return;
                }

                _register = (RegisterDelegate)Delegate.CreateDelegate(typeof(RegisterDelegate), register);
                _unregister = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), unregister);

                // ウィンドウ状態は後から足した API なので、無くても登録自体は成立させる
                var getWindowRect = type.GetMethod("GetWindowRect", BindingFlags.Public | BindingFlags.Static);
                var isWindowVisible = type.GetMethod("IsWindowVisible", BindingFlags.Public | BindingFlags.Static);
                if (getWindowRect != null && isWindowVisible != null)
                {
                    _getWindowRect = (Func<Rect>)Delegate.CreateDelegate(typeof(Func<Rect>), getWindowRect);
                    _isWindowVisible = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), isWindowVisible);
                }
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は登録を無効化する
                MTEUtils.LogWarning("InspectorHostClient: InspectorHost との接続に失敗しました: " + e.Message);
                _register = null;
                _unregister = null;
                _getWindowRect = null;
                _isWindowVisible = null;
            }
        }

        /// <summary>Inspector 描画をホストへ登録する。戻り値はハンドル (ホスト不在なら null)</summary>
        public static object Register(
            string name,
            Func<GameObject, bool> canDraw,
            Action<GameObject, Rect> draw)
        {
            if (!isAvailable)
            {
                return null;
            }

            try
            {
                return _register(name, canDraw, draw);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("InspectorHostClient: InspectorHost への登録に失敗しました: " + e.Message);
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
                MTEUtils.LogWarning("InspectorHostClient: InspectorHost からの登録解除に失敗しました: " + e.Message);
            }
        }
    }
}
