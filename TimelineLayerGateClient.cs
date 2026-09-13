using System;
using System.Reflection;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor の TimelineLayerGateHost へリフレクションで接続するクライアント。
    /// 外部プラグインのウィンドウが「タイムライン読込中にレイヤー未登録」の状態を
    /// 問い合わせ、注意文言と追加ボタンを出すために使う。
    ///
    /// 契約 (ホスト側 TimelineLayerGateHost と対):
    /// - GetState は 0=未読込, 1=メイド不在, 2=未登録, 3=登録済み。SceneEditor 不在なら 0
    /// - ホスト型が見つかるまでは呼び出しのたびに探し直す (OnGUI から毎フレーム呼ばれる前提なので
    ///   再試行のタイマーは持たない)。型は見つかったがシグネチャが合わない場合のみ恒久的に無効
    /// </summary>
    public static class TimelineLayerGateClient
    {
        public const int StateNoTimeline = 0;
        public const int StateMaidNotFound = 1;
        public const int StateMissing = 2;
        public const int StateReady = 3;

        private static Func<string, int> _getState;
        private static Func<string, string> _getNoticeText;
        private static Func<string, string> _getAddButtonText;
        private static Action<string> _addLayer;
        private static bool _initialized;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _getState != null;
            }
        }

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // ホストがまだロードされていなければ _initialized を立てずに戻り、次回呼び出しで再試行する
            var type = DockingClient.FindHostType("TimelineLayerGateHost");
            if (type == null)
            {
                return;
            }

            // ここから先はホストの型は見つかっている。シグネチャ不一致は
            // バージョン差による恒久的な問題なので、この場合のみ無効へ確定する
            _initialized = true;

            try
            {
                var flags = BindingFlags.Public | BindingFlags.Static;
                var getState = type.GetMethod("GetState", flags);
                var getNoticeText = type.GetMethod("GetNoticeText", flags);
                var getAddButtonText = type.GetMethod("GetAddButtonText", flags);
                var addLayer = type.GetMethod("AddLayer", flags);
                if (getState == null || getNoticeText == null || getAddButtonText == null || addLayer == null)
                {
                    MTEUtils.LogWarning(
                        "TimelineLayerGateClient: TimelineLayerGateHost にシグネチャの一致するメンバーが見つかりませんでした");
                    return;
                }

                _getState = (Func<string, int>) Delegate.CreateDelegate(typeof(Func<string, int>), getState);
                _getNoticeText = (Func<string, string>) Delegate.CreateDelegate(typeof(Func<string, string>), getNoticeText);
                _getAddButtonText = (Func<string, string>) Delegate.CreateDelegate(typeof(Func<string, string>), getAddButtonText);
                _addLayer = (Action<string>) Delegate.CreateDelegate(typeof(Action<string>), addLayer);
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合はゲート無しで動作する
                MTEUtils.LogWarning(
                    "TimelineLayerGateClient: TimelineLayerGateHost との接続に失敗しました: " + e.Message);
                _getState = null;
                _getNoticeText = null;
                _getAddButtonText = null;
                _addLayer = null;
            }
        }

        /// <summary>レイヤーの登録状態。SceneEditor 不在・タイムライン未読込は StateNoTimeline</summary>
        public static int GetState(string layerName)
        {
            return isAvailable ? _getState(layerName) : StateNoTimeline;
        }

        public static string GetNoticeText(string layerName)
        {
            return isAvailable ? _getNoticeText(layerName) : "";
        }

        public static string GetAddButtonText(string layerName)
        {
            return isAvailable ? _getAddButtonText(layerName) : "";
        }

        /// <summary>レイヤーを追加し SceneEditor 側のアクティブレイヤーも切り替える</summary>
        public static void AddLayer(string layerName)
        {
            if (isAvailable)
            {
                _addLayer(layerName);
            }
        }
    }
}
