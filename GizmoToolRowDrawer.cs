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
        /// <summary>
        /// 3 値の座標系 (Local / Global / Camera) の取得・変更。両方設定されていれば軸空間ボタンは
        /// 3 値の巡回になり、getUseLocalSpace / setUseLocalSpace は使わない。
        /// 設定しない利用側 (ModItemExplorer 等) は従来の Local / Global トグルのまま
        /// </summary>
        public Func<GizmoSpace> getSpace;
        public Action<GizmoSpace> setSpace;
        /// <summary>軸空間ボタンのワールドアイコン。null ならテキスト表示にフォールバックする</summary>
        public Texture2D globalIcon;
        /// <summary>Camera のときのアイコン。null なら globalIcon を点灯表示する</summary>
        public Texture2D cameraIcon;
    }

    /// <summary>
    /// ギズモの操作種別 (なし/移動/回転/拡縮) と軸空間 (Local/Global、3 値の口があれば Camera も) の切替行。
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
        /// 軸空間の切替ボタン 1 個。3 値の口 (getSpace / setSpace) があれば Local → Global → Camera の巡回、
        /// 無ければ Local / Global のトグル。ワールドアイコンのトグルで表し、Local 以外のとき点灯させる。
        /// SceneView のツールバーからも単体で使う。アイコンを読み込めなかったときはテキスト表示にフォールバックする
        /// </summary>
        public static void DrawSpaceButton(GUIView view, GizmoToolRowOption option, float height)
        {
            if (option.getSpace != null && option.setSpace != null)
            {
                DrawSpaceCycleButton(view, option, height);
                return;
            }

            var useLocalSpace = option.getUseLocalSpace();

            if (option.globalIcon != null)
            {
                view.DrawToggle(option.globalIcon, !useLocalSpace, height, height,
                    on => option.setUseLocalSpace(!on), SpaceIconOffset,
                    useLocalSpace ? "座標系: Local" : "座標系: Global");
            }
            else if (view.DrawButton(useLocalSpace ? "Local" : "Global", SpaceButtonWidth, height))
            {
                option.setUseLocalSpace(!useLocalSpace);
            }
        }

        /// <summary>
        /// 押すたびに Local → Global → Camera → Local と巡回させる。
        /// Local 以外を点灯し、Camera はアイコンを替えて Global と見分ける
        /// </summary>
        private static void DrawSpaceCycleButton(GUIView view, GizmoToolRowOption option, float height)
        {
            var space = option.getSpace();
            var next = NextSpace(space);

            if (option.globalIcon != null)
            {
                var icon = space == GizmoSpace.Camera && option.cameraIcon != null
                    ? option.cameraIcon
                    : option.globalIcon;
                // トグルはクリックのたびに値が反転して onChanged が呼ばれる。
                // 反転後の値は使わず、次の座標系へ進める
                view.DrawToggle(icon, space != GizmoSpace.Local, height, height,
                    _ => option.setSpace(next), SpaceIconOffset, "座標系: " + GetSpaceName(space));
            }
            else if (view.DrawButton(GetSpaceName(space), SpaceButtonWidth, height))
            {
                option.setSpace(next);
            }
        }

        /// <summary>軸空間ボタンを押したときの次の座標系</summary>
        public static GizmoSpace NextSpace(GizmoSpace space)
        {
            switch (space)
            {
                case GizmoSpace.Local: return GizmoSpace.Global;
                case GizmoSpace.Global: return GizmoSpace.Camera;
                default: return GizmoSpace.Local;
            }
        }

        /// <summary>ボタンとツールチップに出す座標系の名前</summary>
        public static string GetSpaceName(GizmoSpace space)
        {
            switch (space)
            {
                case GizmoSpace.Local: return "Local";
                case GizmoSpace.Global: return "Global";
                default: return "Camera";
            }
        }
    }
}
