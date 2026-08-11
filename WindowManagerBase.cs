using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// IMGUI ウィンドウ群のライフサイクルを束ねる共通基底。
    /// プラグイン固有の処理 (カメラ操作の制御・入力ブロック・配置の保存復元等) は
    /// 派生側のフックへ委ねる
    /// </summary>
    public abstract class WindowManagerBase : IManager
    {
        public readonly List<IGUIWindow> windows = new List<IGUIWindow>();

        // 画面サイズ変化の検知用。Update で毎フレーム比較する
        private int _lastScreenWidth = 0;
        private int _lastScreenHeight = 0;

        /// <summary>サイズ変化後、安定フレームでの確定ディスパッチ (settled=true) が未実施か</summary>
        private bool _screenScalePending = false;

        public virtual void Init()
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        public void AddWindow(IGUIWindow window)
        {
            windows.Add(window);
            window.Init();
        }

        public virtual void PreUpdate()
        {
        }

        public virtual void LateUpdate()
        {
        }

        public virtual void Update()
        {
            CheckScreenSizeChanged();

            foreach (var window in windows)
            {
                window.Update();
            }

            UpdateResizeCursor();

            OnAfterUpdate();
        }

        /// <summary>全ウィンドウの Update 後に呼ばれる。入力ブロックやカメラ制御に使う</summary>
        protected virtual void OnAfterUpdate()
        {
        }

        /// <summary>
        /// 全ウィンドウのリサイズカーソル要求を仲裁して適用する。
        /// カーソルはアプリ全体で 1 つなので、各ウィンドウが直接設定すると毎フレーム
        /// 奪い合ってちらつく。リサイズ中のウィンドウを最優先し、次にカーソルが
        /// つかみ範囲に乗っているウィンドウを採用する
        /// </summary>
        private void UpdateResizeCursor()
        {
            var kind = ResizeCursor.Kind.None;

            foreach (var window in windows)
            {
                var provider = window as IResizeCursorProvider;
                if (provider == null)
                {
                    continue;
                }

                if (provider.isResizing)
                {
                    kind = provider.desiredCursorKind;
                    break;
                }

                if (kind == ResizeCursor.Kind.None)
                {
                    kind = provider.desiredCursorKind;
                }
            }

            ResizeCursor.Set(kind);
        }

        /// <summary>
        /// 画面サイズの変化を検知し、各ウィンドウへ通知する。
        /// プラグイン無効中に変化した場合も、再有効化後の最初の Update で差分が検知される。
        /// OS ウィンドウのドラッグリサイズ中は毎フレーム呼ばれうるが、各ウィンドウは
        /// config の保存値から再計算するため誤差や min クランプの影響は累積しない
        /// </summary>
        private void CheckScreenSizeChanged()
        {
            if (Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight)
            {
                // サイズが安定した最初のフレームで確定処理 (RT 作り直し等) を 1 回だけ行う
                if (_screenScalePending)
                {
                    _screenScalePending = false;
                    DispatchScreenSizeScaled(settled: true);
                }
                return;
            }

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _screenScalePending = true;

            OnBeforeScreenSizeDispatch();

            DispatchScreenSizeScaled(settled: false);
        }

        /// <summary>画面サイズ変化の通知前に呼ばれる。ドラッグ追跡の中断等に使う</summary>
        protected virtual void OnBeforeScreenSizeDispatch()
        {
        }

        /// <summary>画面サイズ変化の通知後に呼ばれる。連結群のクランプ等に使う</summary>
        protected virtual void OnAfterScreenSizeDispatch()
        {
        }

        private void DispatchScreenSizeScaled(bool settled)
        {
            foreach (var window in windows)
            {
                var scalable = window as IScreenScalableWindow;
                if (scalable != null)
                {
                    scalable.OnScreenSizeScaled(settled);
                }
                else
                {
                    // ポップアップ等、スケール非対応の窓は画面内クランプだけ行う
                    window.OnScreenSizeChanged();
                }
            }

            OnAfterScreenSizeDispatch();
        }

        public virtual void OnGUI()
        {
            // 組み込み GUIStyle の複製は OnGUI 内でしか行えないためここで初期化する
            GUIView.InitStyles();

            foreach (var window in windows)
            {
                window.OnGUI();
            }
        }

        public virtual void OnLoad()
        {
            foreach (var window in windows)
            {
                window.OnLoad();
            }
        }

        public virtual void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
            foreach (var window in windows)
            {
                window.OnChangedSceneLevel(scene, sceneMode);
            }
        }

        public virtual void OnPluginDisable()
        {
            OnBeforeCloseWindows();

            foreach (var window in windows)
            {
                window.Close();
            }

            // Update が止まるので、カーソルはここで戻す
            ResizeCursor.Set(ResizeCursor.Kind.None);
        }

        /// <summary>
        /// 無効化時、全ウィンドウを閉じる前に呼ばれる。
        /// 閉じると表示状態が失われるため、配置の保存はここで行う
        /// </summary>
        protected virtual void OnBeforeCloseWindows()
        {
        }
    }
}
