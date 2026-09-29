using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// IMGUI ウィンドウ全体の表示倍率。GUI.Window の直前に窓の左上を中心とした拡大行列を掛け、
    /// 文字・行の高さ・幅をまとめて拡大縮小する。
    /// 「窓矩形」は位置がスクリーン座標、サイズが倍率を掛ける前の論理サイズの矩形で、GUI.Window へ渡す値そのもの。
    /// 窓の中の描画コードは論理サイズだけを見ればよく、窓の外 (スクリーン座標) とまたぐ値だけここで換算する。
    /// 既定の 1 では何もしないため、倍率を設定しないプラグインの挙動は変わらない
    /// </summary>
    public static class GUIScale
    {
        public const float MinScale = 0.5f;
        public const float MaxScale = 3f;

        private static float _scale = 1f;

        public static float scale
        {
            get => _scale;
            set => _scale = ClampScale(value);
        }

        public static bool isScaled => _scale != 1f;

        /// <summary>倍率を有効範囲へ収める。設定ファイルの手編集などで壊れた値は 1 に戻す</summary>
        public static float ClampScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 1f;
            }
            return Mathf.Clamp(value, MinScale, MaxScale);
        }

        /// <summary>窓矩形 → 実矩形。位置はそのままでサイズに倍率を掛ける</summary>
        public static Rect ToScreenRect(Rect windowRect, float scale)
        {
            return new Rect(windowRect.x, windowRect.y, windowRect.width * scale, windowRect.height * scale);
        }

        public static Rect ToScreenRect(Rect windowRect)
        {
            return ToScreenRect(windowRect, _scale);
        }

        /// <summary>実矩形 → 窓矩形。位置はそのままでサイズを倍率で割る</summary>
        public static Rect ToWindowRect(Rect screenRect, float scale)
        {
            return new Rect(screenRect.x, screenRect.y, screenRect.width / scale, screenRect.height / scale);
        }

        public static Rect ToWindowRect(Rect screenRect)
        {
            return ToWindowRect(screenRect, _scale);
        }

        /// <summary>窓内のローカル座標 → スクリーン座標。windowPos は窓の左上のスクリーン座標</summary>
        public static Vector2 LocalToScreen(Vector2 windowPos, Vector2 local, float scale)
        {
            return windowPos + local * scale;
        }

        public static Vector2 LocalToScreen(Vector2 windowPos, Vector2 local)
        {
            return LocalToScreen(windowPos, local, _scale);
        }

        public static Rect LocalToScreen(Vector2 windowPos, Rect local, float scale)
        {
            return new Rect(LocalToScreen(windowPos, local.position, scale), local.size * scale);
        }

        public static Rect LocalToScreen(Vector2 windowPos, Rect local)
        {
            return LocalToScreen(windowPos, local, _scale);
        }

        /// <summary>スクリーン座標 → 窓内のローカル座標</summary>
        public static Vector2 ScreenToLocal(Vector2 windowPos, Vector2 screen, float scale)
        {
            return (screen - windowPos) / scale;
        }

        public static Vector2 ScreenToLocal(Vector2 windowPos, Vector2 screen)
        {
            return ScreenToLocal(windowPos, screen, _scale);
        }

        public static Rect ScreenToLocal(Vector2 windowPos, Rect screen, float scale)
        {
            return new Rect(ScreenToLocal(windowPos, screen.position, scale), screen.size / scale);
        }

        public static Rect ScreenToLocal(Vector2 windowPos, Rect screen)
        {
            return ScreenToLocal(windowPos, screen, _scale);
        }

        /// <summary>スクリーン上の移動量 (マウス座標の差分) → 窓内の論理座標での移動量</summary>
        public static Vector2 ScreenDeltaToLocal(Vector2 screenDelta, float scale)
        {
            return screenDelta / scale;
        }

        public static Vector2 ScreenDeltaToLocal(Vector2 screenDelta)
        {
            return ScreenDeltaToLocal(screenDelta, _scale);
        }

        /// <summary>
        /// pivot を中心に scale 倍する行列。Matrix4x4.TRS はネイティブ呼び出しで
        /// テストから使えないため成分で組む
        /// </summary>
        public static Matrix4x4 PivotScaleMatrix(Vector2 pivot, float scale)
        {
            var m = Matrix4x4.identity;
            m.m00 = scale;
            m.m11 = scale;
            m.m03 = pivot.x * (1f - scale);
            m.m13 = pivot.y * (1f - scale);
            return m;
        }

        /// <summary>
        /// 拡大行列の下で GUI.Window が返した位置をスクリーン座標へ戻す。
        /// ドラッグの移動量は論理座標で返るため倍率を掛ける。
        /// 動いていなければ pivot をそのまま返し、往復の丸め誤差を毎フレーム溜めない。
        /// 動いたときは整数ピクセルへ丸め、保存時の (int) 切り捨てで 1px 落ちるのを防ぐ
        /// </summary>
        public static Vector2 ResolveWindowPosition(Vector2 pivot, Vector2 resultPos, float scale)
        {
            if (resultPos == pivot)
            {
                return pivot;
            }
            var pos = pivot + (resultPos - pivot) * scale;
            return new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
        }

        /// <summary>
        /// 窓矩形の実矩形が画面に収まるよう位置だけ直す。
        /// MTEUtils.AdjustWindowPosition と同じ順序 (負の座標を先に直し、そのあと右下へ寄せる) にして、
        /// 倍率 1 で従来と同じ結果にする
        /// </summary>
        public static Rect ClampToScreen(Rect windowRect, float scale, float screenWidth, float screenHeight)
        {
            var width = windowRect.width * scale;
            var height = windowRect.height * scale;
            if (windowRect.x < 0f) windowRect.x = 0f;
            if (windowRect.y < 0f) windowRect.y = 0f;
            if (windowRect.x + width > screenWidth) windowRect.x = screenWidth - width;
            if (windowRect.y + height > screenHeight) windowRect.y = screenHeight - height;
            return windowRect;
        }

        public static Rect ClampToScreen(Rect windowRect)
        {
            return ClampToScreen(windowRect, _scale, Screen.width, Screen.height);
        }

        /// <summary>画面中央へ置く窓矩形。中央へ寄せる計算は倍率を掛けた実サイズで行い、サイズは論理サイズのまま返す</summary>
        public static Rect CenterOnScreen(float width, float height, float scale, float screenWidth, float screenHeight)
        {
            return new Rect(
                (screenWidth - width * scale) / 2f, (screenHeight - height * scale) / 2f, width, height);
        }

        public static Rect CenterOnScreen(float width, float height)
        {
            return CenterOnScreen(width, height, _scale, Screen.width, Screen.height);
        }

        /// <summary>
        /// 倍率付きの GUI.Window。windowRect は窓矩形で、移動後の窓矩形を返す。
        /// 行列はトップレベル (GUI.matrix が単位行列) から呼ぶ前提で、窓の左上を中心に拡大する。
        /// 行列は GUI.Window の時点で窓に記録され、遅延実行されるコールバックにも効く
        /// </summary>
        public static Rect Window(int id, Rect windowRect, GUI.WindowFunction func, string text, GUIStyle style)
        {
            if (!isScaled)
            {
                return GUI.Window(id, windowRect, func, text, style);
            }

            var saved = GUI.matrix;
            var pivot = windowRect.position;
            GUI.matrix = PivotScaleMatrix(pivot, _scale) * saved;
            try
            {
                var result = GUI.Window(id, windowRect, func, text, style);
                result.position = ResolveWindowPosition(pivot, result.position, _scale);
                return result;
            }
            finally
            {
                GUI.matrix = saved;
            }
        }

        /// <summary>倍率付きの GUI.ModalWindow (前提と戻り値は Window と同じ)</summary>
        public static Rect ModalWindow(int id, Rect windowRect, GUI.WindowFunction func, string text, GUIStyle style)
        {
            if (!isScaled)
            {
                return GUI.ModalWindow(id, windowRect, func, text, style);
            }

            var saved = GUI.matrix;
            var pivot = windowRect.position;
            GUI.matrix = PivotScaleMatrix(pivot, _scale) * saved;
            try
            {
                var result = GUI.ModalWindow(id, windowRect, func, text, style);
                result.position = ResolveWindowPosition(pivot, result.position, _scale);
                return result;
            }
            finally
            {
                GUI.matrix = saved;
            }
        }

        /// <summary>
        /// 現在の GUI 座標 → スクリーン座標。GUIUtility.GUIToScreenPoint は窓の中で拡大行列が
        /// 掛かっていると、窓のずれを足す前に行列を掛けてしまい誤った値を返す (実測)。
        /// 行列を一時的に外してクリップのずれだけを足し、そのあと行列を掛ける
        /// </summary>
        public static Vector2 GUIToScreenPoint(Vector2 guiPoint)
        {
            var m = GUI.matrix;
            if (m == Matrix4x4.identity)
            {
                return GUIUtility.GUIToScreenPoint(guiPoint);
            }

            Vector2 unclipped;
            GUI.matrix = Matrix4x4.identity;
            try
            {
                unclipped = GUIUtility.GUIToScreenPoint(guiPoint);
            }
            finally
            {
                GUI.matrix = m;
            }
            return m.MultiplyPoint3x4(unclipped);
        }

        /// <summary>GUI 座標の矩形 → スクリーン座標の矩形 (GUIUtility.GUIToScreenRect は Unity 5.6 に無い)</summary>
        public static Rect GUIToScreenRect(Rect guiRect)
        {
            var min = GUIToScreenPoint(new Vector2(guiRect.xMin, guiRect.yMin));
            var max = GUIToScreenPoint(new Vector2(guiRect.xMax, guiRect.yMax));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
