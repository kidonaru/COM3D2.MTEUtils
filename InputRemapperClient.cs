using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// EditorWindow プラグインの InputRemapper へのリフレクションブリッジ。
    /// InputRemapper は GameView の描画領域内で Input.mousePosition を RT 座標へ書き換えるため、
    /// スクリーン座標前提の窓上判定 (MTEUtils.IsMouseOverWindowRect 等) が変換済み座標を
    /// 読んでしまうと GameView 上なのに「ウィンドウ上」と誤判定し、入力ブロックや
    /// ギズモ抑止が誤発動する。ここで MTEUtils.mousePositionGetter を生座標へ差し替える。
    /// EditorWindow が存在しない環境では何もしない (既定の Input.mousePosition のまま)
    /// </summary>
    public static class InputRemapperClient
    {
        // 再試行間隔 (フレーム)。アセンブリ走査は毎フレーム行うほど安くはない
        private const int RETRY_INTERVAL_FRAMES = 60;

        private static bool _resolved;
        private static int _lastAttemptFrame = int.MinValue;

        /// <summary>
        /// InputRemapper を探して mousePositionGetter を差し替える。
        /// 解決済みなら何もしない。毎フレーム呼んでよい
        /// </summary>
        public static void Update()
        {
            if (_resolved)
            {
                return;
            }

            var frame = Time.frameCount;
            if (frame - _lastAttemptFrame < RETRY_INTERVAL_FRAMES)
            {
                return;
            }
            _lastAttemptFrame = frame;

            var type = DockingClient.FindHostType("InputRemapper");
            if (type == null)
            {
                // EditorWindow 未ロード。後からロードされる可能性があるので再試行を続ける
                return;
            }

            // 型は見つかった。ここからの失敗はバージョン差による恒久的な問題なので
            // 成否に関わらず再試行を打ち切る
            _resolved = true;

            try
            {
                var property = type.GetProperty(
                    "rawMousePosition", BindingFlags.Public | BindingFlags.Static);
                if (property == null)
                {
                    MTEUtils.LogWarning(
                        "InputRemapperClient: rawMousePosition が見つかりませんでした");
                    return;
                }

                var getter = (Func<Vector3>)Delegate.CreateDelegate(
                    typeof(Func<Vector3>), property.GetGetMethod());
                MTEUtils.mousePositionGetter = getter;
                MTEUtils.LogDebug("InputRemapperClient: マウス座標を生座標へ差し替えました");
            }
            catch (Exception e)
            {
                // 差し替えに失敗しても既定の Input.mousePosition で動作は継続できる
                MTEUtils.LogWarning(
                    "InputRemapperClient: InputRemapper との接続に失敗しました: " + e.Message);
            }
        }

    }
}
