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

            // プラグインのロード順によっては EditorWindow のアセンブリがまだ AppDomain に
            // 存在せず Type.GetType が null を返すことがある。その場合は _initialized を
            // 立てずに戻り、次回呼び出し (Register / NotifyHeaderMouseDown) で再試行する
            var type = FindDockingHostType();
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
                var notify = type.GetMethod("NotifyHeaderMouseDown", BindingFlags.Public | BindingFlags.Static);
                if (register == null || unregister == null || notify == null)
                {
                    MTEUtils.LogWarning("DockingClient: DockingHost にシグネチャの一致するメソッドが見つかりませんでした");
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

        /// <summary>
        /// DockingHost の型を解決する。通常は Type.GetType で足りるが、
        /// EditorWindow プラグインのロードが自分より後の場合は Type.GetType が
        /// null を返すため、AppDomain の読み込み済みアセンブリからも探す
        /// </summary>
        private static Type FindDockingHostType()
        {
            var type = Type.GetType(
                "COM3D25.EditorWindow.Plugin.DockingHost, COM3D25.EditorWindow.Plugin");
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "COM3D25.EditorWindow.Plugin")
                {
                    continue;
                }
                type = assembly.GetType("COM3D25.EditorWindow.Plugin.DockingHost");
                if (type != null)
                {
                    return type;
                }
            }

            return null;
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
