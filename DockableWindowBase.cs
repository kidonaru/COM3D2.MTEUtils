using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// タブドッキング対応の汎用ウィンドウ基底。
    /// EditorWindow プラグインが存在すれば DockingClient 経由でドッキングへ参加し、
    /// 存在しない環境では独立ウィンドウとして完結して動作する。
    /// ヘッダー高さ等の寸法はホスト (EditorSubWindow) と揃えること
    /// (グループ加入中はホストが push したタブ状態を自前ヘッダーへ描くため、
    /// 内部窓とタブ列の見た目・位置が揃っている必要がある)。
    /// 注意: ドッキング参加中は windowRect がホスト都合 (タブ同期・連結クランプ等) で
    /// setRect デリゲート経由で書き換わりうる契約である
    /// </summary>
    public abstract class DockableWindowBase : IGUIWindow, IResizeCursorProvider, ITabVisibleWindow
    {
        public static readonly int HEADER_HEIGHT = 26;
        public static readonly int FRAME = 4;
        public static readonly int CLOSE_BUTTON_WIDTH = 20;
        public static readonly int CLOSE_BUTTON_HEIGHT = 16;
        public static readonly int CLOSE_BUTTON_MARGIN = 2;
        public static readonly int CONNECT_BUTTON_WIDTH = 20;
        /// <summary>連結中表示のアクセント色。ホスト (EditorSubWindow.ACCENT_COLOR) と揃えること</summary>
        public static readonly Color CONNECT_ACCENT_COLOR = Color.cyan;

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

        /// <summary>ホストから push されたタブバー状態。null はグループ非加入</summary>
        private string[] _tabTitles;
        private int _tabActiveIndex = -1;

        /// <summary>ドッキング中に非アクティブタブとして畳まれていないか (従属ポップアップの追従判定用)</summary>
        public bool isTabVisible => !_dockTabHidden;

        private readonly WindowResizeController _resize = new WindowResizeController();

        /// <summary>移動の永続化検知用。前フレームの矩形</summary>
        private Rect _lastStoredRect;

        /// <summary>OnSizeChanged 通知用。前フレームの実寸</summary>
        private int _lastWidth;
        private int _lastHeight;

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
            _lastWidth = (int)_windowRect.width;
            _lastHeight = (int)_windowRect.height;
        }

        /// <summary>
        /// ウィンドウ矩形を画面内へ収める。
        /// windowRect はプロパティで ref 渡しできないため、派生側の重複を避けてここに置く
        /// </summary>
        protected void AdjustPosition()
        {
            MTEUtils.AdjustWindowPosition(ref _windowRect);
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

            // この基底はスナップ/コネクト協調 (DragWindow 抑止・個別クランプ抑止) を実装済みのため、
            // コネクト候補になることをホストへ宣言する
            DockingClient.EnableConnect(_dockHandle);

            // タブグループ加入中は自前ヘッダーへタブバーを描くため、状態 push を受け取る
            DockingClient.EnableTabBar(_dockHandle, (titles, activeIndex) =>
            {
                _tabTitles = titles;
                _tabActiveIndex = activeIndex;
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
            _tabTitles = null;
            _tabActiveIndex = -1;
        }

        public virtual void OnGUI()
        {
            if (!_isShowWnd || _dockTabHidden)
            {
                return;
            }

            // グループ加入中はタブバーを自前描画するのでタイトルは空にする
            var title = _tabTitles != null ? "" : windowTitle;
            _windowRect = GUI.Window(windowId, _windowRect, DrawWindowInternal, title, GUIView.gsWin);

            // 画面外へ出ないようクランプ。
            // 連結中はメンバー間のオフセットを壊さないよう個別クランプせず、
            // ホスト側が群のバウンディングボックスでクランプする (内部窓と同じ流儀)
            if (!DockingClient.IsConnected(_dockHandle))
            {
                _windowRect.x = Mathf.Clamp(_windowRect.x, -_windowRect.width + 100, Screen.width - 100);
                _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - HEADER_HEIGHT);
            }

            // ウィンドウ上のホイール操作をゲーム側へ流さない
            MTEUtils.ResetInputOnScroll(_windowRect);
        }

        private void DrawWindowInternal(int id)
        {
            // ボタンの有無でタブ列の使える幅が変わるため、描画前に一度だけ判定する
            var isConnected = DockingClient.IsConnected(_dockHandle);
            var showConnectButton = DockingClient.isConnectAvailable &&
                (isConnected || DockingClient.HasAdjacent(_dockHandle));

            DrawContent();

            if (_tabTitles != null)
            {
                DrawTabBar(showConnectButton);
            }

            // 閉じるボタン (ヘッダー右端)
            var closeRect = new Rect(
                _windowRect.width - CLOSE_BUTTON_WIDTH - CLOSE_BUTTON_MARGIN * 2,
                (HEADER_HEIGHT - CLOSE_BUTTON_HEIGHT) * 0.5f,
                CLOSE_BUTTON_WIDTH,
                CLOSE_BUTTON_HEIGHT);
            if (!DrawHeaderButtons(closeRect, showConnectButton, isConnected))
            {
                // 閉じられたウィンドウには以降の入力判定を走らせない
                return;
            }

            HandleDragInput(closeRect);
        }

        /// <summary>グループ時のタブ列。構成・見た目は内部窓 (EditorSubWindow.DrawTabBar) と揃える</summary>
        private void DrawTabBar(bool showConnectButton)
        {
            // タブ列がヘッダー右のボタンへ食い込まないよう、利用可能幅を先に確定する
            var available = _windowRect.width - FRAME * 2
                - (CLOSE_BUTTON_WIDTH + CLOSE_BUTTON_MARGIN * 2);
            if (showConnectButton)
            {
                available -= CONNECT_BUTTON_WIDTH + CLOSE_BUTTON_MARGIN;
            }

            TabBarDrawer.Draw(
                _tabTitles, _tabActiveIndex,
                FRAME, (HEADER_HEIGHT - TabBarDrawer.TAB_HEIGHT) * 0.5f, available,
                (index, pos) => DockingClient.NotifyTabMouseDown(_dockHandle, index, pos.x, pos.y));
        }

        /// <summary>
        /// ヘッダー右のボタン列を描く。閉じるボタンが押されたら false を返す。
        /// 構成・見た目は内部窓 (EditorSubWindow.DrawHeaderButtons) と揃える
        /// </summary>
        private bool DrawHeaderButtons(Rect closeRect, bool showConnectButton, bool isConnected)
        {
            if (GUI.Button(closeRect, "x"))
            {
                Close();
                return false;
            }

            // コネクトボタン (閉じるボタンの左隣)。見た目・条件は内部窓と揃える
            if (showConnectButton)
            {
                var connectRect = new Rect(
                    closeRect.x - CONNECT_BUTTON_WIDTH - CLOSE_BUTTON_MARGIN,
                    closeRect.y,
                    CONNECT_BUTTON_WIDTH,
                    CLOSE_BUTTON_HEIGHT);

                var oldColor = GUI.color;
                // 連結中はアクセントカラーで塗って状態を示す
                GUI.color = isConnected ? CONNECT_ACCENT_COLOR : Color.white;
                if (GUI.Button(connectRect, isConnected ? "◆" : "◇"))
                {
                    DockingClient.ToggleConnect(_dockHandle);
                }
                GUI.color = oldColor;
            }

            return true;
        }

        /// <summary>リサイズ開始判定・ドラッグ起点の通知・ウィンドウ移動ドラッグを処理する</summary>
        private void HandleDragInput(Rect closeRect)
        {
            var e = Event.current;

            // リサイズ開始判定 (4辺+4隅)。開始したらイベントを消費して移動と競合させない
            if (e.type == EventType.MouseDown && e.button == 0 &&
                _resize.TryBegin(_windowRect, e.mousePosition))
            {
                e.Use();
            }

            // ヘッダー左押下をドッキング判定とドラッグスナップ追跡の起点としてホストへ通知する。
            // イベントは消費せず、そのまま GUI.DragWindow の移動に使わせる
            if (e.type == EventType.MouseDown && e.button == 0 &&
                e.mousePosition.y <= HEADER_HEIGHT && !closeRect.Contains(e.mousePosition))
            {
                DockingClient.NotifyHeaderMouseDown(_dockHandle);
                DockingClient.NotifyDragMouseDown(_dockHandle);
            }

            // コントロールが押下を処理すると e.Use() で消費されるため、
            // この時点で未消費の MouseDown は「空き領域」への押下。
            // 空き領域ドラッグもドラッグスナップの起点にする (内部窓と同じ流儀)
            if (isWholeWindowDraggable &&
                e.type == EventType.MouseDown && e.button == 0 &&
                e.mousePosition.y > HEADER_HEIGHT)
            {
                DockingClient.NotifyDragMouseDown(_dockHandle);
            }

            if (_resize.isResizing)
            {
                return;
            }

            // 吸着中はマウス追従位置と吸着位置がフレームごとに行き来してばたつくため、
            // GUI.DragWindow を呼ばずホストの絶対配置だけに任せる (内部窓と同じ流儀)
            if (DockingClient.IsSnapDragging(_dockHandle))
            {
                return;
            }

            if (isWholeWindowDraggable)
            {
                GUI.DragWindow();
            }
            else
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

            // リサイズやドッキングのタブ同期で実寸が変わったら派生側へ通知する
            if (_lastWidth != (int)_windowRect.width || _lastHeight != (int)_windowRect.height)
            {
                _lastWidth = (int)_windowRect.width;
                _lastHeight = (int)_windowRect.height;
                OnSizeChanged(_lastWidth, _lastHeight);
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

        /// <summary>ウィンドウの実寸が変わったときに呼ばれる。ビュー再構築に使う</summary>
        protected virtual void OnSizeChanged(int width, int height)
        {
        }

        /// <summary>
        /// ヘッダー以外の余白を掴んでもウィンドウを動かせるようにするか。
        /// ドッキング判定の起点はヘッダー押下なので、true でもタブ統合・分離の操作は変わらない。
        /// 内容側に独自のドラッグ操作 (カラーピッカー等) を持つ窓は、
        /// そのドラッグ中だけ false を返してウィンドウ移動と競合させないこと
        /// </summary>
        protected virtual bool isWholeWindowDraggable => true;

        public virtual void Close()
        {
            _resize.Cancel();
            isShowWnd = false;
        }

        public virtual void OnLoad()
        {
            AdjustPosition();
        }

        public virtual void OnScreenSizeChanged()
        {
            AdjustPosition();
        }

        public virtual void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
        }
    }
}
