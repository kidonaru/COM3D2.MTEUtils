using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// アイコンボタン等のツールチップ。
    /// GUI.Window の中身は窓矩形にクリップされ、他の窓に隠されもするため、
    /// 各コントロールはホバー中の文言を登録するだけにして、
    /// 全ウィンドウ描画後に最前面の専用ウィンドウとして 1 か所で描く
    /// </summary>
    public static class TooltipDrawer
    {
        private const int WINDOW_ID = 8903395;
        /// <summary>ホバーしてから表示するまでの秒数</summary>
        private const float SHOW_DELAY = 0.5f;
        /// <summary>カーソルに被らないよう右下へずらす量</summary>
        private static readonly Vector2 CursorOffset = new Vector2(12f, 20f);
        private static readonly Vector2 Padding = new Vector2(8f, 4f);

        private static string _text;
        private static Rect _sourceScreenRect;
        private static int _registeredFrame = -1;
        private static float _hoverStartTime;
        private static Rect _windowRect;

        /// <summary>
        /// ホバー中のコントロールを登録する。Repaint で毎フレーム呼ぶこと。
        /// 呼ばれなくなったフレームで自動的に閉じる。
        /// localRect は現在の GUI 座標系 (窓内・スクロール内) の矩形
        /// </summary>
        public static void Register(Rect localRect, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // GUIUtility.GUIToScreenRect は Unity 5.6 に無いため 2 隅を個別に変換する
            var min = GUIUtility.GUIToScreenPoint(new Vector2(localRect.xMin, localRect.yMin));
            var max = GUIUtility.GUIToScreenPoint(new Vector2(localRect.xMax, localRect.yMax));
            var screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

            // 別のコントロールへ移ったら遅延を取り直す
            if (text != _text || screenRect != _sourceScreenRect)
            {
                _text = text;
                _sourceScreenRect = screenRect;
                _hoverStartTime = Time.realtimeSinceStartup;
            }
            _registeredFrame = Time.frameCount;
        }

        /// <summary>
        /// Repaint 中にカーソルが rect 上にあれば登録する。
        /// GUIView を通さず GUI.Button を直接描く箇所 (ウィンドウヘッダー等) から使う
        /// </summary>
        public static void RegisterIfHovered(Rect localRect, string text)
        {
            if (Event.current.type == EventType.Repaint && localRect.Contains(Event.current.mousePosition))
            {
                Register(localRect, text);
            }
        }

        /// <summary>全ウィンドウの描画後に呼ぶ</summary>
        public static void DrawWindow()
        {
            // Register は Repaint でしか呼ばれないので、直前フレームの登録も生きているとみなす
            if (_text == null || Time.frameCount - _registeredFrame > 1)
            {
                _text = null;
                return;
            }

            // クリックしたら閉じる (押した直後にトグルの説明が残り続けないように)
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                _text = null;
                return;
            }

            if (Time.realtimeSinceStartup - _hoverStartTime < SHOW_DELAY)
            {
                return;
            }

            var size = GUIView.CalcSize(GUIView.gsLabel, _text) + Padding * 2f;
            var pos = MTEUtils.rawGuiPosition + CursorOffset;
            // 画面外へはみ出すなら内側へ寄せる
            pos.x = Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, Screen.width - size.x));
            pos.y = Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, Screen.height - size.y));
            _windowRect = new Rect(pos, size);

            GUI.Window(WINDOW_ID, _windowRect, DrawContents, "", GUIStyle.none);
            // 他のウィンドウに隠されないよう最前面へ
            GUI.BringWindowToFront(WINDOW_ID);
        }

        private static void DrawContents(int id)
        {
            if (_text == null)
            {
                return;
            }
            GUI.Box(new Rect(0f, 0f, _windowRect.width, _windowRect.height), GUIContent.none, GUIView.gsBox);
            GUI.Label(new Rect(Padding.x, Padding.y,
                _windowRect.width - Padding.x * 2f, _windowRect.height - Padding.y * 2f), _text, GUIView.gsLabel);
        }
    }
}
