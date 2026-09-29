using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor の UIScaleHost へのリフレクションブリッジ。連携プラグインの UI 倍率を
    /// SceneEditor にそろえるために使う。SceneEditor が無い・無効・旧版なら hostScale は 0 で、
    /// 呼び出し側は自前の設定を使う (Resolve がその分岐を持つ)
    /// </summary>
    public static class UIScaleClient
    {
        // ホスト探索の再試行間隔と打ち切りまでの時間。これを過ぎても見つからなければ SceneEditor 不在とみなす
        private const float RetryIntervalSeconds = 1f;
        private const float RetryTimeoutSeconds = 30f;

        // 読み取りの例外が何回続いたら連携を止めるか。止めたらそのセッション中は自前の設定を使う (毎フレームのログを避ける)
        private const int MaxReadFailures = 3;

        /// <summary>SceneEditor の倍率に従っている間、各プラグインの設定行に添える案内</summary>
        public const string FollowingHostMessage =
            "SceneEditor の UI 倍率に従っています (SceneEditor の設定ウィンドウ「表示」タブで変更)";

        private static Func<float> _getHostScale;
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
                    }
                    return 0f;
                }
            }
        }

        public static bool isFollowingHost => hostScale > 0f;

        /// <summary>呼び出し側が使う入口。SceneEditor の倍率があればそれ、無ければ ownScale を返す</summary>
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
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("UIScaleClient: UIScaleHost との接続に失敗しました: {0}", e.Message);
                _getHostScale = null;
            }
        }
    }
}
