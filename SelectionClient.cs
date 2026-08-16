using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor プラグインの SelectionManager へのリフレクションブリッジ。
    /// showGizmo = false で選択すると SceneEditor の Inspector には選択が反映されるが
    /// SceneEditor 側ギズモは表示されない（呼び出し側が自前ギズモを持つケース用）。
    /// SceneEditor が不在・旧バージョン（2 引数 Select が無い）の場合は
    /// isAvailable が false になり、呼び出し側は同期しない
    /// </summary>
    public static class SelectionClient
    {
        private static Action<GameObject, bool> _select;
        private static Func<GameObject> _getSelectedObject;
        private static EventInfo _selectionChangedEvent;
        private static object _instance;
        private static bool _initialized;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _select != null;
            }
        }

        /// <summary>SceneEditor 側の現在の選択オブジェクト。SceneEditor 不在・取得失敗時は null</summary>
        public static GameObject selectedObject
        {
            get
            {
                if (!isAvailable)
                {
                    return null;
                }

                try
                {
                    return _getSelectedObject();
                }
                catch (Exception e)
                {
                    MTEUtils.LogWarning("SelectionClient: 選択オブジェクトの取得に失敗しました: " + e.Message);
                    return null;
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
            var type = DockingClient.FindHostType("SelectionManager");
            if (type == null)
            {
                return;
            }

            // ここから先はホストの型は見つかっている。シグネチャ不一致は
            // バージョン差による恒久的な問題なので、この場合のみ無効へ確定する
            _initialized = true;

            try
            {
                var instanceProp = type.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                // 2 引数オーバーロードを明示指定する。旧バージョンの SceneEditor（1 引数のみ）では null になり
                // 同期自体を無効化する（1 引数へ落とすとギズモが二重表示されるため）
                var select = type.GetMethod("Select", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(GameObject), typeof(bool) }, null);
                var selectedProp = type.GetProperty("selectedObject", BindingFlags.Public | BindingFlags.Instance);
                var changedEvent = type.GetEvent("onSelectionChanged", BindingFlags.Public | BindingFlags.Instance);
                if (instanceProp == null || select == null || selectedProp == null || changedEvent == null)
                {
                    MTEUtils.LogWarning("SelectionClient: SelectionManager にシグネチャの一致するメンバーが見つかりませんでした");
                    return;
                }

                var instance = instanceProp.GetValue(null, null);
                if (instance == null)
                {
                    MTEUtils.LogWarning("SelectionClient: SelectionManager のインスタンスを取得できませんでした");
                    return;
                }

                _instance = instance;
                _selectionChangedEvent = changedEvent;
                _select = (Action<GameObject, bool>)Delegate.CreateDelegate(
                    typeof(Action<GameObject, bool>), instance, select);
                _getSelectedObject = (Func<GameObject>)Delegate.CreateDelegate(
                    typeof(Func<GameObject>), instance, selectedProp.GetGetMethod());
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は同期を無効化する
                MTEUtils.LogWarning("SelectionClient: SelectionManager との接続に失敗しました: " + e.Message);
                _instance = null;
                _selectionChangedEvent = null;
                _select = null;
                _getSelectedObject = null;
            }
        }

        /// <summary>
        /// SceneEditor 側の選択を設定する。go = null で選択解除。
        /// showGizmo = false なら SceneEditor 側ギズモを抑止する
        /// </summary>
        public static void Select(GameObject go, bool showGizmo)
        {
            if (!isAvailable)
            {
                return;
            }

            try
            {
                _select(go, showGizmo);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("SelectionClient: 選択の設定に失敗しました: " + e.Message);
            }
        }

        /// <summary>
        /// SceneEditor 側の選択変更イベントを購読する。登録できたら true。
        /// SceneEditor 不在時は false を返すので、呼び出し側は true になるまで再試行してよい
        /// </summary>
        public static bool AddSelectionChangedHandler(Action<GameObject> handler)
        {
            if (!isAvailable || handler == null)
            {
                return false;
            }

            try
            {
                _selectionChangedEvent.AddEventHandler(_instance, handler);
                return true;
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("SelectionClient: 選択変更イベントの購読に失敗しました: " + e.Message);
                return false;
            }
        }
    }
}
