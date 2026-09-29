using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>設定画面で選べる UI 倍率の範囲と刻み (GUIScale の許容範囲の内側に置く)</summary>
    public static class UIScaleSetting
    {
        public const float Min = 0.8f;
        public const float Max = 2f;
        public const float Step = 0.05f;

        /// <summary>倍率を刻みへ丸めて範囲へ収める</summary>
        public static float Snap(float value)
        {
            return SliderSnap.Snap(value, Step, Min, Max);
        }
    }

    /// <summary>
    /// 設定画面の「UI 倍率 %」スライダー行。その場で反映すると設定画面自身が拡大縮小し、
    /// ドラッグ中のつまみがカーソルから逃げ、入力欄では打ちかけの値 (「1」→ 下限) が反映されるため、
    /// 操作が終わるまで値を保留する。描画の前に毎回 TryCommit を呼び、確定した値を設定へ書くこと
    /// </summary>
    public class UIScaleSliderRow
    {
        /// <summary>操作中の値 (%)。null は操作なし</summary>
        private float? _pendingPercent;

        /// <summary>保留中の値がマウス操作 (スライダー・ラベルドラッグ・R) 由来か。false は入力欄のキー入力</summary>
        private bool _pendingByMouse;

        /// <summary>
        /// 操作が終わったとみなせるか。マウス操作はボタンを離したとき、
        /// 入力欄は Enter かフォーカスが外れたとき
        /// </summary>
        public static bool ShouldCommit(bool mouseHeld, bool byMouse, bool keyboardFocused, bool isEnter)
        {
            if (mouseHeld)
            {
                return false;
            }
            return byMouse || !keyboardFocused || isEnter;
        }

        public void Draw(GUIView view, string label, float labelWidth, float currentScale, bool enabled)
        {
            view.BeginEnabled(enabled);
            // 値は % で見せる (設定ファイルの uiScale は倍率のまま)
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = label,
                labelWidth = labelWidth,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = UIScaleSetting.Min * 100f,
                max = UIScaleSetting.Max * 100f,
                snapStep = UIScaleSetting.Step * 100f,
                defaultValue = 100f,
                value = _pendingPercent ?? Mathf.Round(currentScale * 100f),
                onChanged = value =>
                {
                    _pendingPercent = value;
                    _pendingByMouse = Event.current.isMouse;
                },
            });
            view.EndEnabled();
        }

        public bool TryCommit(float currentScale, out float newScale)
        {
            newScale = currentScale;
            if (!_pendingPercent.HasValue)
            {
                return false;
            }

            var e = Event.current;
            var isEnter = e != null && e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            if (!ShouldCommit(Input.GetMouseButton(0), _pendingByMouse, GUIUtility.keyboardControl != 0, isEnter))
            {
                return false;
            }
            if (isEnter)
            {
                GUIUtility.keyboardControl = 0;
            }

            newScale = UIScaleSetting.Snap(_pendingPercent.Value / 100f);
            _pendingPercent = null;
            return newScale != currentScale;
        }

        /// <summary>描かれない間は操作の終わりを判定できないため、保留中の値を反映せず捨てる</summary>
        public void Discard()
        {
            _pendingPercent = null;
        }
    }
}
