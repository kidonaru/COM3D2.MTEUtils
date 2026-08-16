using System;
using System.Collections.Generic;
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
    /// ゲスト側は Subscribe / Unsubscribe の 2 つだけ呼べばよい
    /// (自動再試行・初期同期の詳細は Subscribe のコメントを参照)
    ///
    /// 契約 (ホスト側 EditorStateHost と同じ):
    /// - 連動は SceneEditor → 外部の一方向のみ。自プラグインの有効/無効は SceneEditor へ反映されない
    /// - SceneEditor 側の連動設定が OFF の間は通知が来ない (購読は維持される)
    /// - Subscribe したら不要になった時点で必ず Unsubscribe すること
    ///   (ホストは常駐するため、解除を怠るとハンドラが掴んだ参照ごと残る)
    /// </summary>
    public static class EditorStateClient
    {
        // ホスト探索の再試行間隔と打ち切りまでの時間。
        // BepInEx のプラグインロードは起動直後に終わるため、これを過ぎても
        // 見つからなければ SceneEditor 不在の環境とみなす
        private const float RetryIntervalSeconds = 1f;
        private const float RetryTimeoutSeconds = 30f;

        private static Action<Action<bool>> _subscribe;
        private static Action<Action<bool>> _unsubscribe;
        private static Func<bool> _isEditorEnabled;
        private static Func<bool> _isLinkEnabled;
        private static bool _initialized;

        // ホストへ渡す中継は Relay の 1 本だけ。ゲストの購読はこちらで保持する
        private static readonly List<Action<bool>> _subscribers = new List<Action<bool>>();
        private static bool _relayRegistered;

        // まだ初期同期を受けていない購読者。接続が確立した経路 (Subscribe / 再試行) に
        // かかわらずここを捌くことで、接続待ちの購読者へのプッシュ漏れを防ぐ
        private static readonly List<Action<bool>> _pendingSync = new List<Action<bool>>();

        private static bool _retryScheduled;
        private static float _nextRetryTime;
        private static float _retryDeadline;

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
        /// 接続済みなら現在の有効状態がその場で 1 回渡る (連動設定が OFF の間は渡らない)。
        /// SceneEditor が未ロードでも購読は保持され、接続できた時点で同じく現在値が渡る。
        /// 不要になったら必ず Unsubscribe すること
        /// </summary>
        public static void Subscribe(Action<bool> onChanged)
        {
            if (onChanged == null || _subscribers.Contains(onChanged))
            {
                return;
            }

            _subscribers.Add(onChanged);
            _pendingSync.Add(onChanged);

            if (TryConnect())
            {
                FlushPendingSync();
            }
            else
            {
                ScheduleRetry();
            }
        }

        public static void Unsubscribe(Action<bool> onChanged)
        {
            if (onChanged != null)
            {
                _subscribers.Remove(onChanged);
                _pendingSync.Remove(onChanged);
            }
        }

        /// <summary>
        /// ホストへ中継を登録する。接続済み (または今回接続できた) なら true
        /// </summary>
        private static bool TryConnect()
        {
            if (!isAvailable)
            {
                return false;
            }

            if (!_relayRegistered)
            {
                _subscribe(Relay);
                _relayRegistered = true;
            }

            return true;
        }

        /// <summary>
        /// ホストからの通知をゲストへ配る。
        /// ホスト側は中継 1 本しか見ていないため、購読者ごとの例外はここで握り潰す
        /// (1 つのハンドラの例外で他のハンドラが呼ばれなくなるのを防ぐ)
        /// </summary>
        private static void Relay(bool enabled)
        {
            // 通知中に Subscribe / Unsubscribe されてもコレクションが壊れないよう複製して回す
            var subscribers = _subscribers.ToArray();
            foreach (var subscriber in subscribers)
            {
                SafeInvoke(subscriber, enabled);
            }
        }

        /// <summary>
        /// 接続待ちだった購読者へ現在の有効状態を配る (初期同期)。
        /// 連動設定が OFF の間は配らずゲストを現状維持にする。
        /// この場合も保留は解消してよい (OFF → ON の切り替え時はホストが全員へ流すため)
        /// </summary>
        private static void FlushPendingSync()
        {
            if (_pendingSync.Count == 0)
            {
                return;
            }

            var pending = _pendingSync.ToArray();
            _pendingSync.Clear();

            if (!isLinkEnabled)
            {
                return;
            }

            var enabled = isEditorEnabled;
            foreach (var subscriber in pending)
            {
                SafeInvoke(subscriber, enabled);
            }
        }

        /// <summary>デリゲートの例外がホストや他の購読者へ波及しないよう握り潰して呼ぶ</summary>
        private static void SafeInvoke(Action<bool> onChanged, bool enabled)
        {
            try
            {
                onChanged(enabled);
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
            }
        }

        /// <summary>
        /// ホストの型は見つかったがシグネチャが合わず、接続不能が確定している状態。
        /// _initialized はホストの型を発見できるまで false のままなので、
        /// これが true = バージョン差による恒久的な失敗を意味する
        /// </summary>
        private static bool isPermanentlyUnavailable => _initialized && _subscribe == null;

        /// <summary>
        /// SceneEditor が自分より後にロードされる場合に備え、接続できるまで再試行する。
        /// static クラスでコルーチンを持てないため、MainThreadDispatcher への
        /// 自己再登録でポーリングする
        /// </summary>
        private static void ScheduleRetry()
        {
            if (_retryScheduled || isPermanentlyUnavailable)
            {
                return;
            }

            _retryScheduled = true;
            var now = UnityEngine.Time.realtimeSinceStartup;
            _nextRetryTime = now + RetryIntervalSeconds;
            _retryDeadline = now + RetryTimeoutSeconds;
            MainThreadDispatcher.Enqueue(RetryConnect);
        }

        private static void RetryConnect()
        {
            // 接続済み (別経路の Subscribe で確立した場合も含む) / 購読者ゼロ /
            // 恒久的な失敗確定 のいずれかなら再試行を続ける意味がない
            if (_relayRegistered || _subscribers.Count == 0 || isPermanentlyUnavailable)
            {
                _retryScheduled = false;
                FlushPendingSync();
                return;
            }

            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _nextRetryTime)
            {
                MainThreadDispatcher.Enqueue(RetryConnect);
                return;
            }

            if (TryConnect())
            {
                _retryScheduled = false;
                FlushPendingSync();
                return;
            }

            if (now >= _retryDeadline)
            {
                _retryScheduled = false;
                MTEUtils.LogDebug("EditorStateClient: SceneEditor が見つからないため連動を無効にします");
                return;
            }

            _nextRetryTime = now + RetryIntervalSeconds;
            MainThreadDispatcher.Enqueue(RetryConnect);
        }
    }
}
