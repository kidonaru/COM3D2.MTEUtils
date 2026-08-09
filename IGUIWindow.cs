using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// IMGUI ウィンドウの共通インターフェース。
    /// プラグイン側のウィンドウ管理クラスから一括で駆動される
    /// </summary>
    public interface IGUIWindow
    {
        int windowIndex { get; set; }
        bool isShowWnd { get; set; }
        Rect windowRect { get; set; }

        void Init();
        void Update();
        void Close();
        void OnLoad();
        void OnScreenSizeChanged();
        void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode);
        void OnGUI();
    }
}
