using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// EditorWindow プラグインの DockingHost へのリフレクションブリッジ。
    /// EditorWindow が存在しない環境では isAvailable が false になり、
    /// 呼び出し側 (DockableWindowBase) は独立ウィンドウとして振る舞う。
    /// 毎フレーム MethodInfo.Invoke するとコストが嵩むため、
    /// 起動時に一度だけ Delegate.CreateDelegate でキャッシュする
    /// </summary>
    public static class DockingClient
    {
        private delegate object RegisterDelegate(
            int windowId, string title,
            Func<Rect> getRect, Action<Rect> setRect,
            Func<bool> isVisible, Action<bool> setTabVisible);

        private static RegisterDelegate _register;
        private static Action<object> _unregister;
        private static Action<object> _notifyHeaderMouseDown;
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
            _initialized = true;

            try
            {
                var type = Type.GetType(
                    "COM3D25.EditorWindow.Plugin.DockingHost, COM3D25.EditorWindow.Plugin");
                if (type == null)
                {
                    return;
                }

                var register = type.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
                var unregister = type.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static);
                var notify = type.GetMethod("NotifyHeaderMouseDown", BindingFlags.Public | BindingFlags.Static);
                if (register == null || unregister == null || notify == null)
                {
                    return;
                }

                _register = (RegisterDelegate)Delegate.CreateDelegate(
                    typeof(RegisterDelegate), register);
                _unregister = (Action<object>)Delegate.CreateDelegate(
                    typeof(Action<object>), unregister);
                _notifyHeaderMouseDown = (Action<object>)Delegate.CreateDelegate(
                    typeof(Action<object>), notify);
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は standalone へフォールバックする
                MTEUtils.LogWarning("DockingClient: DockingHost との接続に失敗しました: " + e.Message);
                _register = null;
                _unregister = null;
                _notifyHeaderMouseDown = null;
            }
        }

        public static object Register(
            int windowId, string title,
            Func<Rect> getRect, Action<Rect> setRect,
            Func<bool> isVisible, Action<bool> setTabVisible)
        {
            return isAvailable
                ? _register(windowId, title, getRect, setRect, isVisible, setTabVisible)
                : null;
        }

        public static void Unregister(object handle)
        {
            if (handle != null && isAvailable)
            {
                _unregister(handle);
            }
        }

        public static void NotifyHeaderMouseDown(object handle)
        {
            if (handle != null && isAvailable)
            {
                _notifyHeaderMouseDown(handle);
            }
        }
    }
}
