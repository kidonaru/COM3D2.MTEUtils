using System;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>ギズモ表示対象行の設定。状態は持たず、取得・変更はデリゲートで注入する</summary>
    public struct GizmoTargetRowOption
    {
        public float labelWidth;
        /// <summary>行の高さ。0 なら 20</summary>
        public float height;
        /// <summary>ラベルのスタイル。null なら既定</summary>
        public GUIStyle labelStyle;
        public Func<GizmoTargetType> getTargetType;
        public Action<GizmoTargetType> setTargetType;
    }

    /// <summary>
    /// ギズモの表示対象 (すべて/選択中) の切替行。
    /// SceneEditor の Inspector と ModItemExplorer のモデル操作ウィンドウで共通に使う
    /// </summary>
    public static class GizmoTargetRowDrawer
    {
        private static readonly GizmoTargetType[] Types =
            { GizmoTargetType.All, GizmoTargetType.Selected };

        private static readonly string[] Names = { "すべて表示", "選択中" };

        /// <summary>表示対象を選ぶボタンの幅。最長の「すべて表示」が収まる幅にする</summary>
        public static readonly float ButtonWidth = 80f;

        public static void Draw(GUIView view, GizmoTargetRowOption option)
        {
            var height = option.height > 0f ? option.height : 20f;

            view.BeginHorizontal();
            {
                view.DrawLabel("表示対象", option.labelWidth, height, style: option.labelStyle);

                var current = option.getTargetType();
                for (var i = 0; i < Types.Length; i++)
                {
                    var targetType = Types[i];
                    view.DrawToggle(Names[i], current == targetType, ButtonWidth, height,
                        // 選択中の項目を再度押しても解除しない (ギズモ行と同じ規約)
                        on => { if (on) option.setTargetType(targetType); });
                }
            }
            view.EndLayout();
        }
    }
}
