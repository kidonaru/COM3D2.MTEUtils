using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// タブドッキング対応の汎用ウィンドウ基底。
    /// EditorWindow プラグインが存在すれば DockingClient 経由でドッキングへ参加し、
    /// 存在しない環境では独立ウィンドウとして完結して動作する。
    /// ヘッダー高さ等の寸法はホスト (EditorSubWindow) と揃えること
    /// (ホスト描画のタブバーがヘッダーへぴったり被さる前提のため)。
    /// 注意: ドッキング参加中は windowRect がホスト都合 (タブ同期・連結クランプ等) で
    /// setRect デリゲート経由で書き換わりうる契約である
    /// </summary>
    public abstract class DockableWindowBase : IGUIWindow, IResizeCursorProvider
    {
        public static readonly int HEADER_HEIGHT = 26;
        public static readonly int FRAME = 4;
        public static readonly int CLOSE_BUTTON_WIDTH = 20;
        public static readonly int CLOSE_BUTTON_HEIGHT = 16;
        public static readonly int CLOSE_BUTTON_MARGIN = 2;

        protected abstract int windowId { get; }
        protected abstract string windowTitle { get; }
        protected virtual int minWidth => 200;
        protected virtual int minHeight => 160;

        /// <summary>ヘッダー下の内容を描画する。座標はウィンドウローカル</summary>
        protected abstract void DrawContent();

        /// <summary>配置の復元。座標が負なら未初期化 (画面中央へ配置する)。既定では永続化しない</summary>
        protected virtual void LoadPlacement(out int x, out int y, out int width, out int height)
        {
            x = -1;
            y = -1;
            width = minWidth;
            height = minHeight;
        }

        /// <summary>配置の保存。既定では何もしない</summary>
        protected virtual void StorePlacement(int x, int y, int width, int height)
        {
        }

        /// <summary>タブの表示状態 (アクティブ⇔非アクティブ) が変わったときに呼ばれる</summary>
        protected virtual void OnTabVisibleChanged(bool visible)
        {
        }

        public int windowIndex { get; set; }

        private bool _isShowWnd;
        public bool isShowWnd
        {
            get => _isShowWnd;
            set
            {
                if (_isShowWnd == value)
                {
                    return;
                }
                _isShowWnd = value;
                if (value)
                {
                    RegisterDocking();
                }
                else
                {
                    UnregisterDocking();
                }
            }
        }

        private Rect _windowRect;
        public Rect windowRect
        {
            get => _windowRect;
            set => _windowRect = value;
        }

        /// <summary>ドッキングホストのハンドル。null なら standalone</summary>
        private object _dockHandle;

        /// <summary>非アクティブタブとしてホストから描画停止を指示されているか</summary>
        private bool _dockTabHidden;

        private readonly WindowResizeController _resize = new WindowResizeController();

        /// <summary>移動の永続化検知用。前フレームの矩形</summary>
        private Rect _lastStoredRect;

        public bool isResizing => _resize.isResizing;

        /// <summary>ホバー中の望ましいカーソル種別。ウィンドウ管理側が仲裁して適用する</summary>
        public ResizeCursor.Kind desiredCursorKind =>
            _resize.GetCursorKind(_windowRect, _isShowWnd && !_dockTabHidden, windowId);

        public Rect contentRect => new Rect(
            FRAME, HEADER_HEIGHT,
            _windowRect.width - FRAME * 2,
            _windowRect.height - HEADER_HEIGHT - FRAME);

        public virtual void Init()
        {
            int x, y, width, height;
            LoadPlacement(out x, out y, out width, out height);
            width = Mathf.Max(width, minWidth);
            height = Mathf.Max(height, minHeight);
            _windowRect = new Rect(
                x >= 0 ? x : (Screen.width - width) / 2,
                y >= 0 ? y : (Screen.height - height) / 2,
                width,
                height);
            _lastStoredRect = _windowRect;
        }

        private void RegisterDocking()
        {
            if (_dockHandle != null || !DockingClient.isAvailable)
            {
                return;
            }
            _dockHandle = DockingClient.Register(
                windowId,
                windowTitle,
                () => _windowRect,
                rect => _windowRect = rect,
                () => _isShowWnd,
                visible =>
                {
                    _dockTabHidden = !visible;
                    OnTabVisibleChanged(visible);
                });
        }

        private void UnregisterDocking()
        {
            if (_dockHandle == null)
            {
                return;
            }
            DockingClient.Unregister(_dockHandle);
            _dockHandle = null;
            _dockTabHidden = false;
        }

        public virtual void OnGUI()
        {
            if (!_isShowWnd || _dockTabHidden)
            {
                return;
            }

            // グループ加入中はホストのオーバーレイがタブバーを被せるためタイトルを空にしたいが、
            // 加入状態はホスト側にしかない。オーバーレイが不透明に被さるため空にしなくても実害はない
            _windowRect = GUI.Window(windowId, _windowRect, DrawWindowInternal, windowTitle, GUIView.gsWin);

            _windowRect.x = Mathf.Clamp(_windowRect.x, -_windowRect.width + 100, Screen.width - 100);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - HEADER_HEIGHT);
        }

        private void DrawWindowInternal(int id)
        {
            DrawContent();

            // 閉じるボタン (ヘッダー右端)
            var closeRect = new Rect(
                _windowRect.width - CLOSE_BUTTON_WIDTH - CLOSE_BUTTON_MARGIN * 2,
                (HEADER_HEIGHT - CLOSE_BUTTON_HEIGHT) * 0.5f,
                CLOSE_BUTTON_WIDTH,
                CLOSE_BUTTON_HEIGHT);
            if (GUI.Button(closeRect, "x"))
            {
                Close();
                return;
            }

            var e = Event.current;

            // リサイズ開始判定 (4辺+4隅)。開始したらイベントを消費して移動と競合させない
            if (e.type == EventType.MouseDown && e.button == 0 &&
                _resize.TryBegin(_windowRect, e.mousePosition))
            {
                e.Use();
            }

            // ヘッダー左押下をドッキング判定の起点としてホストへ通知する。
            // イベントは消費せず、そのまま GUI.DragWindow の移動に使わせる
            if (e.type == EventType.MouseDown && e.button == 0 &&
                e.mousePosition.y <= HEADER_HEIGHT && !closeRect.Contains(e.mousePosition))
            {
                DockingClient.NotifyHeaderMouseDown(_dockHandle);
            }

            if (!_resize.isResizing)
            {
                GUI.DragWindow(new Rect(0, 0, _windowRect.width, HEADER_HEIGHT));
            }
        }

        public virtual void Update()
        {
            if (_resize.UpdateResize(ref _windowRect, minWidth, minHeight))
            {
                OnResizeEnd();
                StorePlacementInternal();
            }

            // 移動でも配置を永続化する。config への書き込みと dirty 設定だけで、
            // ファイル保存は ConfigManager 側 (マウスアップ時) に委ねられる
            if (_windowRect != _lastStoredRect)
            {
                StorePlacementInternal();
            }
        }

        private void StorePlacementInternal()
        {
            _lastStoredRect = _windowRect;
            StorePlacement(
                (int)_windowRect.x, (int)_windowRect.y,
                (int)_windowRect.width, (int)_windowRect.height);
        }

        /// <summary>リサイズ確定 (マウスアップ) 時に呼ばれる。ビュー再構築などに使う</summary>
        protected virtual void OnResizeEnd()
        {
        }

        public virtual void Close()
        {
            _resize.Cancel();
            isShowWnd = false;
        }

        public virtual void OnLoad()
        {
        }

        public virtual void OnScreenSizeChanged()
        {
        }

        public virtual void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
        }
    }
}
