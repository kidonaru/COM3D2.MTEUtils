using System;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// IMGUI ウィンドウの 4 辺 + 4 隅リサイズ。
    /// GameViewWindow と EditorSubWindow は継承関係にないため、委譲で共有する。
    /// ウィンドウ矩形は保持側が持ち、ここへは都度渡す
    /// </summary>
    public class WindowResizeController
    {
        /// <summary>
        /// つかんでいる辺。ビット値は DLL 間 (DockingHost.SnapResize) の契約でもあるため変更禁止
        /// </summary>
        [Flags]
        public enum ResizeEdge
        {
            None = 0,
            Left = 1,
            Right = 2,
            Top = 4,
            Bottom = 8,
        }

        // リサイズのつかみ幅。角は掴みやすいよう RESIZE_CORNER 四方まで広げる
        public static readonly int RESIZE_BORDER = 6;
        public static readonly int RESIZE_CORNER = 20;

        /// <summary>
        /// つかんでいる辺のスナップ吸着。保持側が設定する (null なら吸着なし)。
        /// 引数は吸着前の矩形とつかんでいる辺、戻り値は吸着後の矩形。
        /// 吸着位置は他ウィンドウの配置に依存するため、判断はウィンドウ管理側へ委ねる
        /// </summary>
        public Func<Rect, ResizeEdge, Rect> snapper;

        private ResizeEdge _edge = ResizeEdge.None;
        // リサイズ開始時のウィンドウ矩形とカーソル位置。
        // 差分で計算することで、辺のどこを掴んでも位置が飛ばない
        private Rect _startRect;
        private Vector2 _startMousePos;

        public bool isResizing => _edge != ResizeEdge.None;

        /// <summary>
        /// つかみ範囲ならリサイズを開始して true。
        /// localPos は GUI.Window 内のローカル座標
        /// </summary>
        public bool TryBegin(Rect windowRect, Vector2 localPos)
        {
            var edge = GetEdge(windowRect, localPos);
            if (edge == ResizeEdge.None)
            {
                return false;
            }

            _edge = edge;
            _startRect = windowRect;
            // UpdateResize と同じ座標系で差分を取るため、ここでも生座標から求める
            _startMousePos = MTEUtils.rawGuiPosition;
            return true;
        }

        /// <summary>
        /// リサイズ中ならウィンドウ矩形を更新する。
        /// このフレームでリサイズが確定した (ボタンを離した) 場合に true を返す
        /// </summary>
        public bool UpdateResize(ref Rect windowRect, int minWidth, int minHeight)
        {
            if (_edge == ResizeEdge.None)
            {
                return false;
            }

            // つかみ範囲は描画領域と重なるため、生座標で読む必要がある
            var delta = MTEUtils.rawGuiPosition - _startMousePos;
            var rect = _startRect;

            // 上限は画面サイズ (画面外ドラッグで巨大なウィンドウになるのを防ぐ)。
            // 左辺・上辺は原点が動くので、反対側の辺を固定したまま位置と大きさを同時に更新する
            if ((_edge & ResizeEdge.Left) != 0)
            {
                var xMax = rect.xMax;
                var x = Mathf.Clamp(rect.x + delta.x, xMax - Screen.width, xMax - minWidth);
                rect.x = x;
                rect.width = xMax - x;
            }
            if ((_edge & ResizeEdge.Right) != 0)
            {
                rect.width = Mathf.Clamp(rect.width + delta.x, minWidth, Screen.width);
            }
            if ((_edge & ResizeEdge.Top) != 0)
            {
                var yMax = rect.yMax;
                // ヘッダーが画面上端より外へ出るとウィンドウを掴めなくなるため 0 で止める
                var y = Mathf.Clamp(rect.y + delta.y, Mathf.Max(0f, yMax - Screen.height), yMax - minHeight);
                rect.y = y;
                rect.height = yMax - y;
            }
            if ((_edge & ResizeEdge.Bottom) != 0)
            {
                rect.height = Mathf.Clamp(rect.height + delta.y, minHeight, Screen.height);
            }

            windowRect = ApplySnap(rect, minWidth, minHeight);

            if (Input.GetMouseButton(0))
            {
                return false;
            }

            _edge = ResizeEdge.None;
            return true;
        }

        /// <summary>
        /// 吸着を反映した矩形を返す。吸着で最小サイズを割り込む軸は吸着しない
        /// (最小サイズへ丸めると狙った辺とは別の位置へ貼り付いて見えるため)
        /// </summary>
        private Rect ApplySnap(Rect rect, int minWidth, int minHeight)
        {
            if (snapper == null)
            {
                return rect;
            }

            var snapped = snapper(rect, _edge);
            if (snapped.width >= minWidth)
            {
                rect.x = snapped.x;
                rect.width = snapped.width;
            }
            if (snapped.height >= minHeight)
            {
                rect.y = snapped.y;
                rect.height = snapped.height;
            }
            return rect;
        }

        /// <summary>
        /// ドラッグ中に無効化された場合、次に有効化した最初のフレームで
        /// 無関係なカーソル位置を基準にサイズが飛ぶのを防ぐ
        /// </summary>
        public void Cancel()
        {
            _edge = ResizeEdge.None;
        }

        /// <summary>
        /// スクリーンGUI座標がリサイズのつかみ範囲上にあるか。
        /// この範囲は内容の描画領域と重なるため、ここでのクリックは内容側へ通さない
        /// </summary>
        public bool IsOverHandle(Rect windowRect, Vector2 guiPos)
        {
            return GetEdge(windowRect, ToLocalPos(windowRect, guiPos)) != ResizeEdge.None;
        }

        /// <summary>
        /// 出したいリサイズカーソル。ドラッグ中は範囲外へ出ても掴んでいる辺の向きを維持する。
        /// 実際の適用は WindowManager が全ウィンドウ分を仲裁して行う。
        /// hoverEnabled が false の間はホバー判定を行わない (ウィンドウ非表示時など)
        /// </summary>
        public ResizeCursor.Kind GetCursorKind(Rect windowRect, bool hoverEnabled, int selfWindowId)
        {
            var edge = _edge;
            if (edge == ResizeEdge.None && hoverEnabled)
            {
                var guiPos = MTEUtils.rawGuiPosition;
                if (!MTEUtils.isOverOtherWindowChecker(selfWindowId, guiPos))
                {
                    edge = GetEdge(windowRect, ToLocalPos(windowRect, guiPos));
                }
            }
            return ToCursorKind(edge);
        }

        private static Vector2 ToLocalPos(Rect windowRect, Vector2 guiPos)
        {
            return new Vector2(guiPos.x - windowRect.x, guiPos.y - windowRect.y);
        }

        /// <summary>
        /// ウィンドウローカル座標がどの辺・角のつかみ範囲にあるかを返す。
        /// 縦横どちらの角範囲にも入っていれば角として扱い、RESIZE_CORNER 四方すべてを有効にする
        /// </summary>
        private static ResizeEdge GetEdge(Rect windowRect, Vector2 localPos)
        {
            var width = windowRect.width;
            var height = windowRect.height;
            if (localPos.x < 0f || localPos.x > width || localPos.y < 0f || localPos.y > height)
            {
                return ResizeEdge.None;
            }

            var nearLeft = localPos.x <= RESIZE_CORNER;
            var nearRight = localPos.x >= width - RESIZE_CORNER;
            var nearTop = localPos.y <= RESIZE_CORNER;
            var nearBottom = localPos.y >= height - RESIZE_CORNER;
            var isCorner = (nearLeft || nearRight) && (nearTop || nearBottom);

            var edge = ResizeEdge.None;
            if (localPos.x <= RESIZE_BORDER || (isCorner && nearLeft)) edge |= ResizeEdge.Left;
            if (localPos.x >= width - RESIZE_BORDER || (isCorner && nearRight)) edge |= ResizeEdge.Right;
            if (localPos.y <= RESIZE_BORDER || (isCorner && nearTop)) edge |= ResizeEdge.Top;
            if (localPos.y >= height - RESIZE_BORDER || (isCorner && nearBottom)) edge |= ResizeEdge.Bottom;
            return edge;
        }

        private static ResizeCursor.Kind ToCursorKind(ResizeEdge edge)
        {
            var isHorizontal = (edge & (ResizeEdge.Left | ResizeEdge.Right)) != 0;
            var isVertical = (edge & (ResizeEdge.Top | ResizeEdge.Bottom)) != 0;

            if (isHorizontal && isVertical)
            {
                // 左上と右下が同じ向き、右上と左下が同じ向きになる
                var isLeft = (edge & ResizeEdge.Left) != 0;
                var isTop = (edge & ResizeEdge.Top) != 0;
                return isLeft == isTop ? ResizeCursor.Kind.DiagonalDown : ResizeCursor.Kind.DiagonalUp;
            }
            if (isHorizontal) return ResizeCursor.Kind.Horizontal;
            if (isVertical) return ResizeCursor.Kind.Vertical;
            return ResizeCursor.Kind.None;
        }
    }
}
