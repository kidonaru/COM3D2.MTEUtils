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

        // スナップ/コネクト系 (ホストが旧バージョンだと存在しない)。
        // 1 つでも欠けていたら機能ごと無効にする一括検出
        // (「スナップは効くがボタンは出ない」等の中間状態を作らない)
        private static Action<object> _enableConnect;
        private static Action<object> _notifyDragMouseDown;
        private static Func<object, bool> _isSnapDragging;
        private static Func<object, bool> _hasAdjacent;
        private static Func<object, bool> _isConnected;
        private static Action<object> _toggleConnect;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _register != null;
            }
        }

        /// <summary>スナップ/コネクト連携が使えるか (ホストが対応バージョンか)</summary>
        public static bool isConnectAvailable
        {
            get
            {
                Initialize();
                return _enableConnect != null;
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

                // スナップ/コネクト系は後発 API のため任意。旧ホストでは見つからないが
                // タブドッキングは従来通り使えるので警告は出さない
                var enableConnect = type.GetMethod("EnableConnect", BindingFlags.Public | BindingFlags.Static);
                var notifyDrag = type.GetMethod("NotifyDragMouseDown", BindingFlags.Public | BindingFlags.Static);
                var isSnapDragging = type.GetMethod("IsSnapDragging", BindingFlags.Public | BindingFlags.Static);
                var hasAdjacent = type.GetMethod("HasAdjacent", BindingFlags.Public | BindingFlags.Static);
                var isConnected = type.GetMethod("IsConnected", BindingFlags.Public | BindingFlags.Static);
                var toggleConnect = type.GetMethod("ToggleConnect", BindingFlags.Public | BindingFlags.Static);
                if (enableConnect != null && notifyDrag != null && isSnapDragging != null &&
                    hasAdjacent != null && isConnected != null && toggleConnect != null)
                {
                    _enableConnect = (Action<object>)Delegate.CreateDelegate(
                        typeof(Action<object>), enableConnect);
                    _notifyDragMouseDown = (Action<object>)Delegate.CreateDelegate(
                        typeof(Action<object>), notifyDrag);
                    _isSnapDragging = (Func<object, bool>)Delegate.CreateDelegate(
                        typeof(Func<object, bool>), isSnapDragging);
                    _hasAdjacent = (Func<object, bool>)Delegate.CreateDelegate(
                        typeof(Func<object, bool>), hasAdjacent);
                    _isConnected = (Func<object, bool>)Delegate.CreateDelegate(
                        typeof(Func<object, bool>), isConnected);
                    _toggleConnect = (Action<object>)Delegate.CreateDelegate(
                        typeof(Action<object>), toggleConnect);
                }
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は standalone へフォールバックする
                MTEUtils.LogWarning("DockingClient: DockingHost との接続に失敗しました: " + e.Message);
                _register = null;
                _unregister = null;
                _notifyHeaderMouseDown = null;
                _enableConnect = null;
                _notifyDragMouseDown = null;
                _isSnapDragging = null;
                _hasAdjacent = null;
                _isConnected = null;
                _toggleConnect = null;
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

        public static void EnableConnect(object handle)
        {
            if (handle != null && isConnectAvailable)
            {
                _enableConnect(handle);
            }
        }

        public static void NotifyDragMouseDown(object handle)
        {
            if (handle != null && isConnectAvailable)
            {
                _notifyDragMouseDown(handle);
            }
        }

        public static bool IsSnapDragging(object handle)
        {
            return handle != null && isConnectAvailable && _isSnapDragging(handle);
        }

        public static bool HasAdjacent(object handle)
        {
            return handle != null && isConnectAvailable && _hasAdjacent(handle);
        }

        public static bool IsConnected(object handle)
        {
            return handle != null && isConnectAvailable && _isConnected(handle);
        }

        public static void ToggleConnect(object handle)
        {
            if (handle != null && isConnectAvailable)
            {
                _toggleConnect(handle);
            }
        }
    }
}
