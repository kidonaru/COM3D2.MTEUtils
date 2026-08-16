using System;
using System.Reflection;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor プラグインの GizmoRenderer が持つギズモ操作設定
    /// (操作種別 / Local・Global) へのリフレクションブリッジ。
    /// enum はアセンブリ間で別型になるため int 経由で授受する。
    /// SceneEditor が不在・シグネチャ不一致の場合は isAvailable が false になり、
    /// 呼び出し側は同期しない
    /// </summary>
    public static class GizmoToolClient
    {
        private static PropertyInfo _toolProp;
        private static PropertyInfo _useLocalSpaceProp;
        private static Type _hostToolType;
        private static bool _initialized;
        private static bool _failed;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _toolProp != null && !_failed;
            }
        }

        /// <summary>
        /// SceneEditor 側のギズモ操作種別。取得失敗時は None (最も無害な「操作なし」へ倒す)。
        /// 失敗時は isAvailable も false になるため、呼び出し側は読み出し後に
        /// isAvailable を確認して既定値を現在値と取り違えないようにする
        /// </summary>
        public static GizmoTool tool
        {
            get
            {
                if (!isAvailable)
                {
                    return GizmoTool.None;
                }

                try
                {
                    return (GizmoTool)Convert.ToInt32(_toolProp.GetValue(null, null));
                }
                catch (Exception e)
                {
                    Fail("操作種別の取得に失敗しました", e);
                    return GizmoTool.None;
                }
            }
            set
            {
                if (!isAvailable)
                {
                    return;
                }

                try
                {
                    _toolProp.SetValue(null, Enum.ToObject(_hostToolType, (int)value), null);
                }
                catch (Exception e)
                {
                    Fail("操作種別の設定に失敗しました", e);
                }
            }
        }

        /// <summary>
        /// SceneEditor 側のギズモ軸空間 (true = Local)。取得失敗時は SceneEditor の既定と同じ true。
        /// 失敗時の扱いは tool と同じ (isAvailable で判別する)
        /// </summary>
        public static bool useLocalSpace
        {
            get
            {
                if (!isAvailable)
                {
                    return true;
                }

                try
                {
                    return (bool)_useLocalSpaceProp.GetValue(null, null);
                }
                catch (Exception e)
                {
                    Fail("軸空間の取得に失敗しました", e);
                    return true;
                }
            }
            set
            {
                if (!isAvailable)
                {
                    return;
                }

                try
                {
                    _useLocalSpaceProp.SetValue(null, value, null);
                }
                catch (Exception e)
                {
                    Fail("軸空間の設定に失敗しました", e);
                }
            }
        }

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // ロード順によってはホストのアセンブリが未登場のことがあるため、
            // 型が見つかるまでは _initialized を立てずに再試行を続ける
            var type = DockingClient.FindHostType("GizmoRenderer");
            if (type == null)
            {
                return;
            }

            // ここから先はホストの型は見つかっている。シグネチャ不一致は
            // バージョン差による恒久的な問題なので、この場合のみ無効へ確定する
            _initialized = true;

            var toolProp = type.GetProperty("currentTool", BindingFlags.Public | BindingFlags.Static);
            var spaceProp = type.GetProperty("useLocalSpace", BindingFlags.Public | BindingFlags.Static);
            if (toolProp == null || spaceProp == null ||
                !toolProp.PropertyType.IsEnum || spaceProp.PropertyType != typeof(bool) ||
                !toolProp.CanWrite || !spaceProp.CanWrite)
            {
                MTEUtils.LogWarning("GizmoToolClient: GizmoRenderer にシグネチャの一致するプロパティが見つかりませんでした");
                return;
            }

            _toolProp = toolProp;
            _useLocalSpaceProp = spaceProp;
            _hostToolType = toolProp.PropertyType;
        }

        /// <summary>
        /// 毎フレーム呼ばれる経路なので、一度失敗したら以後は同期を止めてログを溢れさせない
        /// </summary>
        private static void Fail(string message, Exception e)
        {
            MTEUtils.LogWarning("GizmoToolClient: " + message + ": " + e.Message);
            _failed = true;
        }
    }
}
