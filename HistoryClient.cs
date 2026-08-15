using System;
using System.Reflection;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor プラグインの HistoryAPI へのリフレクションブリッジ。
    /// 自前の操作を SceneEditor の操作履歴へ積み、undo/redo キーや
    /// 履歴ウィンドウから戻せるようにする。
    /// SceneEditor が存在しない環境では isAvailable が false になり、
    /// Register 等はすべて無視される (呼び出し側で分岐する必要はない)。
    /// 毎回 MethodInfo.Invoke するとコストが嵩むため、
    /// 初回に一度だけ Delegate.CreateDelegate でキャッシュする
    ///
    /// 契約 (ホスト側 HistoryAPI と同じ):
    /// - Register は「確定済み」の操作 1 件を登録する。ドラッグ中の連続変更を
    ///   1 件へまとめるのは呼び出し側の責務 (操作確定時に 1 回だけ呼ぶ)
    /// - undo/redo クロージャは冪等であり、他エントリとの順序に依存しないこと
    ///   (履歴ウィンドウのジャンプで連続適用される)
    /// - undo/redo/canApply の中から Register/Undo/Redo を呼び返さないこと
    /// - Subscribe したら不要になった時点で必ず Unsubscribe すること
    /// - シーン遷移で履歴は全クリアされる
    /// </summary>
    public static class HistoryClient
    {
        private static Action<string, Action, Action, Func<bool>> _register;
        private static Action _undo;
        private static Action _redo;
        private static Func<bool> _canUndo;
        private static Func<bool> _canRedo;
        private static Action<Action> _addOnChanged;
        private static Action<Action> _removeOnChanged;
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

            // プラグインのロード順によっては SceneEditor のアセンブリがまだ AppDomain に
            // 存在せず型が見つからないことがある。その場合は _initialized を立てずに戻り、
            // 次回呼び出しで再試行する
            var type = DockingClient.FindHostType("HistoryAPI");
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
                var undo = type.GetMethod("Undo", BindingFlags.Public | BindingFlags.Static);
                var redo = type.GetMethod("Redo", BindingFlags.Public | BindingFlags.Static);
                var canUndo = type.GetProperty("canUndo", BindingFlags.Public | BindingFlags.Static);
                var canRedo = type.GetProperty("canRedo", BindingFlags.Public | BindingFlags.Static);
                if (register == null || undo == null || redo == null
                    || canUndo == null || canRedo == null)
                {
                    MTEUtils.LogWarning(
                        "HistoryClient: HistoryAPI にシグネチャの一致するメンバーが見つかりませんでした");
                    return;
                }

                _register = (Action<string, Action, Action, Func<bool>>)Delegate.CreateDelegate(
                    typeof(Action<string, Action, Action, Func<bool>>), register);
                _undo = (Action)Delegate.CreateDelegate(typeof(Action), undo);
                _redo = (Action)Delegate.CreateDelegate(typeof(Action), redo);
                _canUndo = (Func<bool>)Delegate.CreateDelegate(
                    typeof(Func<bool>), canUndo.GetGetMethod());
                _canRedo = (Func<bool>)Delegate.CreateDelegate(
                    typeof(Func<bool>), canRedo.GetGetMethod());

                // 履歴変化の通知は購読側だけの機能なので、無くても登録・undo/redo は使える
                var onChanged = type.GetEvent("onChanged", BindingFlags.Public | BindingFlags.Static);
                if (onChanged != null)
                {
                    _addOnChanged = (Action<Action>)Delegate.CreateDelegate(
                        typeof(Action<Action>), onChanged.GetAddMethod());
                    _removeOnChanged = (Action<Action>)Delegate.CreateDelegate(
                        typeof(Action<Action>), onChanged.GetRemoveMethod());
                }
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は履歴連携なしで動作する
                MTEUtils.LogWarning("HistoryClient: HistoryAPI との接続に失敗しました: " + e.Message);
                _register = null;
                _undo = null;
                _redo = null;
                _canUndo = null;
                _canRedo = null;
                _addOnChanged = null;
                _removeOnChanged = null;
            }
        }

        /// <summary>
        /// 確定済みの操作を 1 件登録する。SceneEditor が無い環境では何もしない
        /// </summary>
        /// <param name="description">履歴ウィンドウに表示する操作名</param>
        /// <param name="undo">操作前の状態へ書き戻す処理</param>
        /// <param name="redo">操作後の状態へ書き戻す処理</param>
        /// <param name="canApply">対象消滅等で今は適用できないとき false を返す判定。null なら常に適用可</param>
        public static void Register(
            string description, Action undo, Action redo, Func<bool> canApply = null)
        {
            if (isAvailable)
            {
                _register(description, undo, redo, canApply);
            }
        }

        public static void Undo()
        {
            if (isAvailable)
            {
                _undo();
            }
        }

        public static void Redo()
        {
            if (isAvailable)
            {
                _redo();
            }
        }

        public static bool canUndo => isAvailable && _canUndo();
        public static bool canRedo => isAvailable && _canRedo();

        /// <summary>
        /// 履歴の変化 (追加/undo/redo/ジャンプ/クリア) を購読する。
        /// 不要になったら必ず Unsubscribe すること (履歴は常駐するため解除しないと残り続ける)
        /// </summary>
        public static void Subscribe(Action onChanged)
        {
            Initialize();
            if (onChanged != null && _addOnChanged != null)
            {
                _addOnChanged(onChanged);
            }
        }

        public static void Unsubscribe(Action onChanged)
        {
            Initialize();
            if (onChanged != null && _removeOnChanged != null)
            {
                _removeOnChanged(onChanged);
            }
        }
    }
}
