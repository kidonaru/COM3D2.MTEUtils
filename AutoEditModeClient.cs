using System;
using System.Reflection;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor の AutoEditModeHost へリフレクションで接続するクライアント。
    /// 外部プラグインのウィンドウで値を書く直前に呼び、SceneEditor 側の編集モードへ入れる。
    /// 編集モード外はタイムラインのレイヤーが毎フレーム再生値を書き戻すため、
    /// 先に入っておかないと変更が巻き戻る。
    ///
    /// 契約 (ホスト側 AutoEditModeHost と対):
    /// - Enter は値を書く「直前」に呼ぶ。既に編集モードならホスト側で無視される
    /// - layerName は SceneEditor 側レイヤーのクラス名 (例: "PostEffectTimelineLayer")。
    ///   ホストはこれを「触ったレイヤー」として控え、編集確定時にアクティブへ切り替える
    /// - SceneEditor 不在なら何もしない。ホスト型が見つかるまでは呼び出しのたびに探し直す
    ///   (OnGUI から呼ばれる前提なので再試行のタイマーは持たない)。
    ///   型は見つかったがシグネチャが合わない場合のみ恒久的に無効
    /// </summary>
    public static class AutoEditModeClient
    {
        private static Action<string> _enter;
        private static bool _initialized;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _enter != null;
            }
        }

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // ホストがまだロードされていなければ _initialized を立てずに戻り、次回呼び出しで再試行する
            var type = DockingClient.FindHostType("AutoEditModeHost");
            if (type == null)
            {
                return;
            }

            // ここから先はホストの型は見つかっている。シグネチャ不一致は
            // バージョン差による恒久的な問題なので、この場合のみ無効へ確定する
            _initialized = true;

            try
            {
                var enter = type.GetMethod("Enter", BindingFlags.Public | BindingFlags.Static);
                if (enter == null)
                {
                    MTEUtils.LogWarning(
                        "AutoEditModeClient: AutoEditModeHost にシグネチャの一致するメンバーが見つかりませんでした");
                    return;
                }

                _enter = (Action<string>) Delegate.CreateDelegate(typeof(Action<string>), enter);
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は連動なしで動作する
                MTEUtils.LogWarning(
                    "AutoEditModeClient: AutoEditModeHost との接続に失敗しました: " + e.Message);
                _enter = null;
            }
        }

        /// <summary>値を書く直前に呼ぶ。SceneEditor 不在なら何もしない</summary>
        public static void Enter(string layerName)
        {
            if (isAvailable)
            {
                _enter(layerName);
            }
        }
    }
}
