using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// タブ列のレイアウト計算 (純粋関数)。描画・入力判定は TabBarDrawer が行う。
    /// GUI に依存させないのはユニットテストのため
    /// </summary>
    public static class TabBarLayout
    {
        /// <summary>タブ幅の下限。これを割る場合は縮小せずスクロールモードへ入る</summary>
        public const float MIN_TAB_WIDTH = 60f;
        /// <summary>スクロールモードで両端に出す &lt; &gt; ボタンの幅</summary>
        public const float SCROLL_BUTTON_WIDTH = 16f;

        public struct Result
        {
            public float tabWidth;
            /// <summary>全タブが収まらずスクロールボタンを出すか</summary>
            public bool scrollable;
            /// <summary>タブ列の描画開始 X (タブバー左端からの相対)</summary>
            public float tabsOriginX;
            /// <summary>
            /// タブ列を描いてよい幅 (スクロール時は &lt; &gt; ボタンを除いた領域)。
            /// 描画側はこの幅でクリップし、収まりきらないタブを見切れたまま見せる
            /// </summary>
            public float tabsAreaWidth;
            /// <summary>クランプ済みのスクロール位置 (px)。呼び出し元はこの値を保持し直す</summary>
            public float scrollX;
            /// <summary>スクロール位置の上限 (px)。これ以上送るとタブ列の右端が浮く</summary>
            public float maxScrollX;
            /// <summary>実際に描くタブの先頭 index (左で見切れる 1 枚を含む)</summary>
            public int firstDrawIndex;
            /// <summary>実際に描くタブの末尾 index (右で見切れる 1 枚を含む)。-1 なら描画なし</summary>
            public int lastDrawIndex;
            /// <summary>
            /// firstDrawIndex のタブを描き始める X (タブ領域左端からの相対)。
            /// 左端のタブを見切れさせるため負になりうる
            /// </summary>
            public float drawOriginX;
        }

        /// <summary>
        /// ヘッダー幅からタブ列の利用可能幅を求める。
        /// 左右フレームと右側の閉じる + ロックボタン領域を除いた値。
        /// 描画側 (EditorSubWindow / DockableWindowBase) と並び替え (TabGroupManager) で共有する
        /// </summary>
        public static float CalcAvailableWidth(float headerWidth)
        {
            return headerWidth - DockableWindowBase.FRAME * 2
                - (DockableWindowBase.CLOSE_BUTTON_WIDTH + DockableWindowBase.CLOSE_BUTTON_MARGIN * 2)
                - (DockableWindowBase.LOCK_BUTTON_WIDTH + DockableWindowBase.CLOSE_BUTTON_MARGIN);
        }

        /// <summary>
        /// index のタブが全部見えるところまでスクロール位置を動かして返す。
        /// 既に収まっていれば scrollX をそのまま返す (むやみに位置を変えない)。
        /// 描画中の自動追従はしない代わりに、タブがアクティブになった時だけ呼ぶ想定
        /// </summary>
        public static float ScrollToShow(int count, float availableWidth, float scrollX, int index)
        {
            var layout = Calc(count, availableWidth, scrollX);
            if (!layout.scrollable || index < 0 || index >= count)
            {
                return layout.scrollX;
            }

            var step = layout.tabWidth + TabBarDrawer.TAB_MARGIN;
            var left = index * step;
            var right = left + layout.tabWidth;

            var result = layout.scrollX;
            if (left < result)
            {
                // 左へ隠れている: 左端を合わせる
                result = left;
            }
            else if (right > result + layout.tabsAreaWidth)
            {
                // 右へはみ出している: 右端を合わせる
                result = right - layout.tabsAreaWidth;
            }
            return Mathf.Clamp(result, 0f, layout.maxScrollX);
        }

        /// <summary>
        /// タブ列のレイアウトを求める。scrollX はタブ列を左へ送った量 (px)。
        /// タブ単位ではなく px 単位で送るので、端のタブは途中で切れて見える。
        /// 描画中にアクティブタブを追従はしない (追い出したい場合は ScrollToShow を使う)
        /// </summary>
        public static Result Calc(int count, float availableWidth, float scrollX)
        {
            var result = new Result();
            result.lastDrawIndex = -1;
            if (count <= 0)
            {
                return result;
            }

            var margin = TabBarDrawer.TAB_MARGIN;
            var shrunkWidth = Mathf.Min(
                TabBarDrawer.TAB_WIDTH,
                (availableWidth - margin * (count - 1)) / count);

            if (shrunkWidth >= MIN_TAB_WIDTH)
            {
                // 全部収まるので縮小するだけ。スクロールもクリップも要らない
                result.tabWidth = shrunkWidth;
                result.tabsAreaWidth = availableWidth;
                result.lastDrawIndex = count - 1;
                return result;
            }

            // スクロールモード: 幅は下限固定、両端のボタン分を除いた領域をクリップ窓にする
            result.scrollable = true;
            result.tabWidth = MIN_TAB_WIDTH;
            result.tabsOriginX = SCROLL_BUTTON_WIDTH + margin;
            // 描画側が GUI.BeginGroup の幅に使うため負値を渡さない
            result.tabsAreaWidth = Mathf.Max(
                0f, availableWidth - (SCROLL_BUTTON_WIDTH + margin) * 2);

            var step = MIN_TAB_WIDTH + margin;
            var contentWidth = count * MIN_TAB_WIDTH + (count - 1) * margin;
            // 右端まで送ったら最後のタブがクリップ窓の右端へ揃う (端数の空きを残さない)
            result.maxScrollX = Mathf.Max(0f, contentWidth - result.tabsAreaWidth);
            result.scrollX = Mathf.Clamp(scrollX, 0f, result.maxScrollX);

            // クリップ窓 [scrollX, scrollX + tabsAreaWidth] に掛かるタブを描く。
            // 端は途中で切れるので、境界に跨る 1 枚も範囲へ含める
            result.firstDrawIndex = Mathf.Clamp(
                Mathf.FloorToInt(result.scrollX / step), 0, count - 1);
            result.lastDrawIndex = Mathf.Clamp(
                Mathf.FloorToInt((result.scrollX + result.tabsAreaWidth) / step), 0, count - 1);
            result.drawOriginX = result.firstDrawIndex * step - result.scrollX;
            return result;
        }
    }
}
