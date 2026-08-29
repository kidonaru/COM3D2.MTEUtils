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
                return result;
            }

            // スクロールモード: 幅は下限固定、両端のボタン分を除いた領域に入る枚数だけ表示する
            result.scrollable = true;
            result.tabWidth = MIN_TAB_WIDTH;
            result.tabsOriginX = SCROLL_BUTTON_WIDTH + margin;
            var tabsArea = availableWidth - (SCROLL_BUTTON_WIDTH + margin) * 2;
            result.visibleCount = Mathf.Max(
                1, Mathf.FloorToInt((tabsArea + margin) / (MIN_TAB_WIDTH + margin)));
            if (result.visibleCount >= count)
            {
                // 防御分岐: shrunkWidth < MIN の時点で数学的にここへは到達しないはずだが、
                // 万一入った場合はボタンなしの非スクロール表示へフォールバックする
                result.scrollable = false;
                result.tabsOriginX = 0f;
                result.visibleCount = count;
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
            return result;
        }
    }
}
