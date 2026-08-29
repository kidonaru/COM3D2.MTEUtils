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
            /// <summary>表示する先頭タブの index (クランプ・アクティブ追従適用済み)</summary>
            public int firstVisible;
            public int visibleCount;
            /// <summary>タブ列の描画開始 X (タブバー左端からの相対)</summary>
            public float tabsOriginX;
            /// <summary>
            /// タブ列を描いてよい幅 (スクロール時は &lt; &gt; ボタンを除いた領域)。
            /// 描画側はこの幅でクリップし、収まりきらないタブを見切れたまま見せる
            /// </summary>
            public float tabsAreaWidth;
            /// <summary>実際に描くタブの先頭 index (見切れる手前の 1 枚を含む)</summary>
            public int firstDrawIndex;
            /// <summary>実際に描くタブの末尾 index (見切れる先の 1 枚を含む)。-1 なら描画なし</summary>
            public int lastDrawIndex;
            /// <summary>
            /// firstDrawIndex のタブを描き始める X (タブ領域左端からの相対)。
            /// 見切れ描画と右詰めのため負になりうる
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

        public static Result Calc(int count, float availableWidth, int scrollOffset, int activeIndex)
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
                result.tabWidth = shrunkWidth;
                result.visibleCount = count;
                result.tabsAreaWidth = availableWidth;
                result.lastDrawIndex = count - 1;
                return result;
            }

            // スクロールモード: 幅は下限固定、両端のボタン分を除いた領域に入る枚数だけ表示する
            result.scrollable = true;
            result.tabWidth = MIN_TAB_WIDTH;
            result.tabsOriginX = SCROLL_BUTTON_WIDTH + margin;
            var tabsArea = availableWidth - (SCROLL_BUTTON_WIDTH + margin) * 2;
            result.tabsAreaWidth = tabsArea;
            result.visibleCount = Mathf.Max(
                1, Mathf.FloorToInt((tabsArea + margin) / (MIN_TAB_WIDTH + margin)));
            if (result.visibleCount >= count)
            {
                // 防御分岐: shrunkWidth < MIN の時点で数学的にここへは到達しないはずだが、
                // 万一入った場合はボタンなしの非スクロール表示へフォールバックする
                result.scrollable = false;
                result.tabsOriginX = 0f;
                result.tabsAreaWidth = availableWidth;
                result.visibleCount = count;
                result.lastDrawIndex = count - 1;
                return result;
            }

            var maxOffset = count - result.visibleCount;
            var first = Mathf.Clamp(scrollOffset, 0, maxOffset);

            // アクティブタブが常に見えるよう追従する (切替直後に見失わないため)
            if (activeIndex >= 0 && activeIndex < count)
            {
                if (activeIndex < first)
                {
                    first = activeIndex;
                }
                else if (activeIndex > first + result.visibleCount - 1)
                {
                    first = activeIndex - result.visibleCount + 1;
                }
            }

            result.firstVisible = first;

            // 末尾まで来ていたら最後のタブを領域の右端へ揃える (最後のタブが
            // アクティブなときに端数の空きが右に残らないようにする)。
            // 左に空いたぶんは手前のタブを見切れさせて埋める
            var step = result.tabWidth + margin;
            var rowWidth = result.visibleCount * result.tabWidth + (result.visibleCount - 1) * margin;
            var shift = first == maxOffset ? tabsArea - rowWidth : 0f;

            result.firstDrawIndex = first;
            result.drawOriginX = shift;
            if (shift > 0f && first > 0)
            {
                result.firstDrawIndex = first - 1;
                result.drawOriginX = shift - step;
            }
            // 末尾側も 1 枚多く描いて見切れさせる (最後まで来ていれば存在しないので Min で止める)
            result.lastDrawIndex = Mathf.Min(count - 1, first + result.visibleCount);
            return result;
        }
    }
}
