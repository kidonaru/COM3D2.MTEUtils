using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor の UIScaleHost へのリフレクションブリッジ。連携プラグインの UI 倍率を
    /// SceneEditor にそろえるために使う。SceneEditor が無い・無効・旧版なら hostScale は 0 で、
    /// 呼び出し側は自前の設定を使う (Resolve がその分岐を持つ)。
    /// 従っている間の設定画面の変更は TrySetHostScale で SceneEditor の設定へ書く
    /// </summary>
    public static class UIScaleClient
    {
        // ホスト探索の再試行間隔と打ち切りまでの時間。これを過ぎても見つからなければ SceneEditor 不在とみなす
        private const float RetryIntervalSeconds = 1f;
        private const float RetryTimeoutSeconds = 30f;

        // 読み取りの例外が何回続いたら連携を止めるか。止めたらそのセッション中は自前の設定を使う (毎フレームのログを避ける)
        private const int MaxReadFailures = 3;

        /// <summary>SceneEditor の倍率に従っている間、各プラグインの設定行に添える案内 (followingHostMessage で選ぶ)</summary>
        public const string FollowingHostEditableMessage =
            "SceneEditor の UI 倍率と共通です (変えると SceneEditor の設定も変わります)";
        public const string FollowingHostReadOnlyMessage =
            "SceneEditor の UI 倍率に従っています (SceneEditor の設定ウィンドウ「表示」タブで変更)";

        private static Func<float> _getHostScale;
        private static Func<float, bool> _setHostScale;
        private static bool _initialized;
        private static float _nextRetryTime;
        private static float _retryDeadline = -1f;
        private static int _readFailures;

        /// <summary>
        /// 実際に使う倍率を決める純関数 (Resolve の中身)。SceneEditor の倍率 (0 以下・壊れた値は「無し」扱い) があればそれ、
        /// 無ければ自前の設定。どちらも GUIScale の許容範囲へ収める
        /// </summary>
        public static float ResolveScale(float hostScale, float ownScale)
        {
            if (hostScale > 0f && !float.IsInfinity(hostScale))
            {
                return GUIScale.ClampScale(hostScale);
            }
            return GUIScale.ClampScale(ownScale);
        }

        public static float hostScale
        {
            get
            {
                Initialize();
                if (_getHostScale == null)
                {
                    return 0f;
                }
                try
                {
                    var value = _getHostScale();
                    _readFailures = 0;
                    return value;
                }
                catch (Exception e)
                {
                    // 一時的な失敗は自前の設定で凌ぎ、続くようなら連携を止める
                    MTEUtils.LogWarning("UIScaleClient: UIScaleHost の読み取りに失敗しました: {0}", e.Message);
                    if (++_readFailures >= MaxReadFailures)
                    {
                        MTEUtils.LogWarning("UIScaleClient: 読み取りの失敗が続いたため、以降は自前の UI 倍率を使います");
                        _getHostScale = null;
                        _setHostScale = null;
                    }
                    return 0f;
                }
            }
        }

        public static bool isFollowingHost => hostScale > 0f;

        /// <summary>設定行を操作できるか。自前の設定を使う間か、従っている SceneEditor に書き込み API がある間は操作できる</summary>
        public static bool IsScaleEditable(bool followingHost, bool canSetHostScale)
        {
            return !followingHost || canSetHostScale;
        }

        /// <summary>SceneEditor が倍率の書き込み API (UIScaleHost.SetUIScale) を持つか。旧版には無い</summary>
        public static bool canSetHostScale
        {
            get
            {
                Initialize();
                return _setHostScale != null;
            }
        }

        public static bool isScaleEditable => IsScaleEditable(isFollowingHost, canSetHostScale);

        public static string followingHostMessage =>
            canSetHostScale ? FollowingHostEditableMessage : FollowingHostReadOnlyMessage;

        /// <summary>
        /// 設定行で確定した倍率を、SceneEditor に従っている間は SceneEditor の設定へ書く。
        /// 書けたら true。false なら呼び出し側は自前の設定へ書く
        /// </summary>
        public static bool TrySetHostScale(float scale)
        {
            if (!isFollowingHost || _setHostScale == null)
            {
                return false;
            }
            try
            {
                return _setHostScale(scale);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("UIScaleClient: UIScaleHost への書き込みに失敗しました: {0}", e.Message);
                return false;
            }
        }

        /// <summary>
        /// 呼び出し側が使う入口。SceneEditor の倍率があればそれ、無ければ ownScale を返す。
        /// ホストの設定を直接読むため、TrySetHostScale で書いた値もそのフレームから返る
        /// (設定行の表示・比較の基準にも使う。GUIScale.scale は次の Update まで古い)
        /// </summary>
        public static float Resolve(float ownScale)
        {
            return ResolveScale(hostScale, ownScale);
        }

        private static void Initialize()
        {
            if (_initialized || Time.realtimeSinceStartup < _nextRetryTime)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (_retryDeadline < 0f)
            {
                _retryDeadline = now + RetryTimeoutSeconds;
            }

            var type = DockingClient.FindHostType("UIScaleHost");
            if (type == null)
            {
                // ロード順で見つからないことがあるため、初回の呼び出しから一定時間は間隔を空けて探し直す
                _nextRetryTime = now + RetryIntervalSeconds;
                if (now > _retryDeadline)
                {
                    _initialized = true;
                }
                return;
            }

            _initialized = true;
            try
            {
                var property = type.GetProperty("uiScale", BindingFlags.Public | BindingFlags.Static);
                if (property == null)
                {
                    MTEUtils.LogWarning("UIScaleClient: UIScaleHost.uiScale が見つかりませんでした");
                    return;
                }
                _getHostScale = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), property.GetGetMethod());

                // 書き込みは後発の API。旧版に無ければ読むだけにする (設定行は操作できない)
                var setMethod = type.GetMethod("SetUIScale", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(float) }, null);
                if (setMethod != null && setMethod.ReturnType == typeof(bool))
                {
                    _setHostScale = (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), setMethod);
                }
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("UIScaleClient: UIScaleHost との接続に失敗しました: {0}", e.Message);
                _getHostScale = null;
                _setHostScale = null;
            }
        }
    }
}
