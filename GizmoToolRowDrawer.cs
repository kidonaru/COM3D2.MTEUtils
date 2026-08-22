using System;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>ギズモツール行の設定。状態は持たず、取得・変更はデリゲートで注入する</summary>
    public struct GizmoToolRowOption
    {
        public float labelWidth;
        /// <summary>行の高さ。0 なら 20</summary>
        public float height;
        /// <summary>ラベルのスタイル。null なら既定</summary>
        public GUIStyle labelStyle;
        public Func<GizmoTool> getTool;
        public Action<GizmoTool> setTool;
        public Func<bool> getUseLocalSpace;
        public Action<bool> setUseLocalSpace;
        /// <summary>軸空間ボタンのワールドアイコン。null ならテキスト表示にフォールバックする</summary>
        public Texture2D globalIcon;
    }

    /// <summary>
    /// ギズモの操作種別 (なし/移動/回転/拡縮) と軸空間 (Local/Global) の切替行。
    /// SceneEditor の Inspector と MTE のモデル操作ウィンドウで共通に使う
    /// </summary>
    public static class GizmoToolRowDrawer
    {
        private static readonly GizmoTool[] Tools =
            { GizmoTool.None, GizmoTool.Move, GizmoTool.Rotate, GizmoTool.Scale };
        private static readonly string[] ToolNames = { "なし", "移動", "回転", "拡縮" };

        public static readonly float ToolButtonWidth = 44f;
        public static readonly float SpaceButtonWidth = 54f;
        // アイコンをボタン枠より少し小さく描くための余白 (両側合計)
        private const float SpaceIconOffset = 4f;

        /// <summary>軸空間ボタンの幅。アイコンなら正方形、テキストフォールバックなら固定幅</summary>
        public static float GetSpaceButtonWidth(GizmoToolRowOption option, float height)
        {
            return option.globalIcon != null ? height : SpaceButtonWidth;
        }

        public static void Draw(GUIView view, GizmoToolRowOption option)
        {
            var height = option.height > 0f ? option.height : 20f;

            view.BeginHorizontal();
            {
                view.DrawLabel("ギズモ", option.labelWidth, height, style: option.labelStyle);

                var current = option.getTool();
                for (var i = 0; i < Tools.Length; i++)
                {
                    var tool = Tools[i];
                    view.DrawToggle(ToolNames[i], current == tool,
                        ToolButtonWidth, height,
                        // 選択中の項目を再度押しても解除しない (解除は「なし」で行う)
                        on => { if (on) option.setTool(tool); });
                }

                DrawSpaceButton(view, option, height);
            }
            view.EndLayout();
        }

        /// <summary>
        /// 軸空間 (Local/Global) の切替ボタン 1 個。ワールドアイコンのトグルで表し、
        /// Global のときだけ点灯させる。SceneView のツールバーからも単体で使う。
        /// アイコンを読み込めなかったときはテキスト表示にフォールバックする
        /// </summary>
        public static void DrawSpaceButton(GUIView view, GizmoToolRowOption option, float height)
        {
            var useLocalSpace = option.getUseLocalSpace();

            if (option.globalIcon != null)
            {
                view.DrawToggle(option.globalIcon, !useLocalSpace, height, height,
                    on => option.setUseLocalSpace(!on), SpaceIconOffset);
            }
            else if (view.DrawButton(useLocalSpace ? "Local" : "Global", SpaceButtonWidth, height))
            {
                option.setUseLocalSpace(!useLocalSpace);
            }
        }
    }
}
