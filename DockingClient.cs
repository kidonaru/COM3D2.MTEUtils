using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor プラグインの DockingHost へのリフレクションブリッジ。
    /// SceneEditor が存在しない環境では isAvailable が false になり、
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

        // リサイズ吸着 (ホストが旧バージョンだと存在しない)。
        // 単独で完結する機能なのでスナップ/コネクト系とは別に検出し、
        // 旧ホストではリサイズ吸着だけが無効になるようにする
        private static Func<object, Rect, int, Rect> _snapResize;

        // タブのアクティブ化 (ホストが旧バージョンだと存在しない)。
        // 単独で完結する機能なのでスナップ/コネクト系とは別に検出する
        private static Action<object> _activateTab;

        // タブバー描画系 (ホストが旧バージョンだと存在しない)。ペアで一括検出する
        private static Action<object, Action<string[], int>> _enableTabBar;
        private static Action<object, int, float, float> _notifyTabMouseDown;

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

        /// <summary>タブバーのゲスト描画連携が使えるか (ホストが対応バージョンか)</summary>
        public static bool isTabBarAvailable
        {
            get
            {
                Initialize();
                return _enableTabBar != null;
            }
        }

        /// <summary>タブのアクティブ化が使えるか (ホストが対応バージョンか)</summary>
        public static bool isActivateTabAvailable
        {
            get
            {
                Initialize();
                return _activateTab != null;
            }
        }

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // プラグインのロード順によっては SceneEditor のアセンブリがまだ AppDomain に
            // 存在せず Type.GetType が null を返すことがある。その場合は _initialized を
            // 立てずに戻り、次回呼び出し (Register / NotifyHeaderMouseDown) で再試行する
            var type = FindHostType("DockingHost");
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

                // リサイズ吸着も後発 API のため任意
                var snapResize = type.GetMethod("SnapResize", BindingFlags.Public | BindingFlags.Static);
                if (snapResize != null)
                {
                    _snapResize = (Func<object, Rect, int, Rect>)Delegate.CreateDelegate(
                        typeof(Func<object, Rect, int, Rect>), snapResize);
                }

                // タブバー描画系も後発 API のため任意。旧ホストでは見つからないが
                // タブドッキングは従来通り使えるので警告は出さない
                var enableTabBar = type.GetMethod("EnableTabBar", BindingFlags.Public | BindingFlags.Static);
                var notifyTab = type.GetMethod("NotifyTabMouseDown", BindingFlags.Public | BindingFlags.Static);
                if (enableTabBar != null && notifyTab != null)
                {
                    _enableTabBar = (Action<object, Action<string[], int>>)Delegate.CreateDelegate(
                        typeof(Action<object, Action<string[], int>>), enableTabBar);
                    _notifyTabMouseDown = (Action<object, int, float, float>)Delegate.CreateDelegate(
                        typeof(Action<object, int, float, float>), notifyTab);
                }

                // タブのアクティブ化も後発 API のため任意
                var activateTab = type.GetMethod("ActivateTab", BindingFlags.Public | BindingFlags.Static);
                if (activateTab != null)
                {
                    _activateTab = (Action<object>)Delegate.CreateDelegate(
                        typeof(Action<object>), activateTab);
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
                _snapResize = null;
                _enableTabBar = null;
                _notifyTabMouseDown = null;
                _activateTab = null;
            }
        }

        // SceneEditor プラグインのアセンブリ名 (名前空間も同名)
        private const string HostAssemblyName = "COM3D2.SceneEditor.Plugin";

        /// <summary>
        /// SceneEditor プラグイン内の型を解決する。通常は Type.GetType で足りるが、
        /// SceneEditor プラグインのロードが自分より後の場合は Type.GetType が
        /// null を返すため、AppDomain の読み込み済みアセンブリからも探す
        /// </summary>
        /// <param name="typeName">名前空間を除いた型名 (例: "DockingHost")</param>
        internal static Type FindHostType(string typeName)
        {
            var fullName = HostAssemblyName + "." + typeName;

            var type = Type.GetType(fullName + ", " + HostAssemblyName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != HostAssemblyName)
                {
                    continue;
                }
                type = assembly.GetType(fullName);
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

        /// <summary>
        /// リサイズ中の矩形へ辺スナップを適用して返す。
        /// 未対応ホスト・未登録なら素通しする (吸着なしで従来通り動く)。
        /// edges は WindowResizeController.ResizeEdge のビット
        /// </summary>
        public static Rect SnapResize(object handle, Rect rect, int edges)
        {
            Initialize();
            if (handle == null || _snapResize == null)
            {
                return rect;
            }
            return _snapResize(handle, rect, edges);
        }

        public static void EnableTabBar(object handle, Action<string[], int> onTabBarChanged)
        {
            if (handle != null && isTabBarAvailable)
            {
                _enableTabBar(handle, onTabBarChanged);
            }
        }

        public static void NotifyTabMouseDown(object handle, int tabIndex, float x, float y)
        {
            if (handle != null && isTabBarAvailable)
            {
                _notifyTabMouseDown(handle, tabIndex, x, y);
            }
        }

        /// <summary>
        /// 自窓のタブをアクティブへ切り替える。未対応ホスト・未登録なら何もしない。
        /// 押下由来の NotifyTabMouseDown と違い、つまみドラッグ候補は記録されない
        /// </summary>
        public static void ActivateTab(object handle)
        {
            if (handle != null && isActivateTabAvailable)
            {
                _activateTab(handle);
            }
        }
    }
}
