using System;
using System.Reflection;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor プラグインの EditorStateHost へのリフレクションブリッジ。
    /// SceneEditor の有効/無効に自プラグインを追従させるために使う。
    /// SceneEditor が存在しない環境では isAvailable が false になり、
    /// Subscribe 等はすべて無視される (呼び出し側で分岐する必要はない)。
    /// 毎回 MethodInfo.Invoke するとコストが嵩むため、
    /// 初回に一度だけ Delegate.CreateDelegate でキャッシュする
    ///
    /// 契約 (ホスト側 EditorStateHost と同じ):
    /// - 連動は SceneEditor → 外部の一方向のみ。自プラグインの有効/無効は SceneEditor へ反映されない
    /// - SceneEditor 側の連動設定が OFF の間は通知が来ない (購読は維持される)
    /// - Subscribe した時点では通知が来ない。プラグインのロード順は不定なので、
    ///   接続直後に現状へ合わせたい場合は isEditorEnabled を読むこと
    /// - Subscribe したら不要になった時点で必ず Unsubscribe すること
    ///   (ホストは常駐するため、解除を怠るとハンドラが掴んだ参照ごと残る)
    /// </summary>
    public static class EditorStateClient
    {
        private static Action<Action<bool>> _subscribe;
        private static Action<Action<bool>> _unsubscribe;
        private static Func<bool> _isEditorEnabled;
        private static Func<bool> _isLinkEnabled;
        private static bool _initialized;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _subscribe != null;
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
            var type = DockingClient.FindHostType("EditorStateHost");
            if (type == null)
            {
                return;
            }

            // ここから先はホストの型は見つかっている。シグネチャ不一致は
            // バージョン差による恒久的な問題なので、この場合のみ無効へ確定する
            _initialized = true;

            try
            {
                var subscribe = type.GetMethod("Subscribe", BindingFlags.Public | BindingFlags.Static);
                var unsubscribe = type.GetMethod("Unsubscribe", BindingFlags.Public | BindingFlags.Static);
                var isEditorEnabled = type.GetProperty(
                    "isEditorEnabled", BindingFlags.Public | BindingFlags.Static);
                var isLinkEnabled = type.GetProperty(
                    "isLinkEnabled", BindingFlags.Public | BindingFlags.Static);
                if (subscribe == null || unsubscribe == null
                    || isEditorEnabled == null || isLinkEnabled == null)
                {
                    MTEUtils.LogWarning(
                        "EditorStateClient: EditorStateHost にシグネチャの一致するメンバーが見つかりませんでした");
                    return;
                }

                _subscribe = (Action<Action<bool>>)Delegate.CreateDelegate(
                    typeof(Action<Action<bool>>), subscribe);
                _unsubscribe = (Action<Action<bool>>)Delegate.CreateDelegate(
                    typeof(Action<Action<bool>>), unsubscribe);
                _isEditorEnabled = (Func<bool>)Delegate.CreateDelegate(
                    typeof(Func<bool>), isEditorEnabled.GetGetMethod());
                _isLinkEnabled = (Func<bool>)Delegate.CreateDelegate(
                    typeof(Func<bool>), isLinkEnabled.GetGetMethod());
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は連動なしで動作する
                MTEUtils.LogWarning(
                    "EditorStateClient: EditorStateHost との接続に失敗しました: " + e.Message);
                _subscribe = null;
                _unsubscribe = null;
                _isEditorEnabled = null;
                _isLinkEnabled = null;
            }
        }

        /// <summary>SceneEditor の UI が現在有効か。SceneEditor が無い環境では常に false</summary>
        public static bool isEditorEnabled => isAvailable && _isEditorEnabled();

        /// <summary>SceneEditor 側の連動設定が ON か。OFF の間は通知が来ない</summary>
        public static bool isLinkEnabled => isAvailable && _isLinkEnabled();

        /// <summary>
        /// SceneEditor の有効/無効の変化を購読する。引数は変化後の有効状態。
        /// 不要になったら必ず Unsubscribe すること
        /// </summary>
        public static void Subscribe(Action<bool> onChanged)
        {
            if (onChanged != null && isAvailable)
            {
                _subscribe(onChanged);
            }
        }

        public static void Unsubscribe(Action<bool> onChanged)
        {
            if (onChanged != null && isAvailable)
            {
                _unsubscribe(onChanged);
            }
        }
    }
}
