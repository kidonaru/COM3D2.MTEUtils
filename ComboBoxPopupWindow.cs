using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// コンボボックスのドロップダウンを独立した GUI.Window として描くポップアップ。
    /// ホストウィンドウ内に描く方式だとウィンドウ矩形にクリップされてリストが
    /// 見切れるため、別ウィンドウ化して画面全体を使えるようにする。
    /// 同時に開くコンボは 1 つなのでシングルトンで足りる
    /// </summary>
    public class ComboBoxPopupWindow : IGUIWindow
    {
        public static readonly int WINDOW_ID = 923483;

        /// <summary>開いているコンボ。null なら閉じている</summary>
        private GUIComboBoxBase _comboBox;

        /// <summary>コンボを開いたホストウィンドウ。表示状態の追従に使う</summary>
        private IGUIWindow _host;

        /// <summary>
        /// ボタン座標をスクリーン座標へ直すための、コンボを描いた GUI.Window の矩形取得。
        /// 1 ウィンドウクラスが従属ウィンドウを併せ持つ場合、ホストの windowRect とは
        /// 別の矩形が基準になるため差し替えられるようにしている
        /// </summary>
        private Func<Rect> _hostRectGetter;

        private readonly GUIView _view = new GUIView()
        {
            padding = Vector2.zero,
            margin = 0,
        };

        /// <summary>今フレームの描画で使うポップアップ矩形 (スクリーンGUI座標)</summary>
        private Rect _popupRect;

        public int windowIndex { get; set; }

        /// <summary>
        /// 表示状態はポップアップの開閉と同義。
        /// ウィンドウマネージャの入力ブロック判定がこれと windowRect を見るため、
        /// 独自フラグを持たず開閉状態をそのまま返す。
        /// true の指定は無視する（開くのは ProcessFocus 経由のみ）
        /// </summary>
        public bool isShowWnd
        {
            get => isOpen;
            set
            {
                if (!value)
                {
                    Close();
                }
            }
        }

        public Rect windowRect
        {
            get => _popupRect;
            set => _popupRect = value;
        }

        private static ComboBoxPopupWindow _instance = null;
        public static ComboBoxPopupWindow instance
            => _instance ?? (_instance = new ComboBoxPopupWindow());

        public bool isOpen => _comboBox != null;

        /// <summary>指定ホストが開いたコンボのポップアップが出ているか</summary>
        public bool IsOpenFor(IGUIWindow host) => _comboBox != null && _host == host;

        /// <summary>
        /// ホストの描画末尾から毎フレーム呼ぶ。ボタン押下で rootView へ登録された
        /// フォーカスをポップアップへ引き取る。開いているコンボのボタンを再クリック
        /// した場合はトグルとして閉じる (外側クリック判定はボタン上を除外しているため、
        /// 閉じる操作はここで行う)
        /// </summary>
        public void ProcessFocus(GUIView rootView, IGUIWindow host)
        {
            ProcessFocus(rootView, host, null);
        }

        /// <summary>
        /// コンボを描いた GUI.Window の矩形をホストと別に指定する版。
        /// hostRectGetter が null なら host.windowRect を使う
        /// </summary>
        public void ProcessFocus(GUIView rootView, IGUIWindow host, Func<Rect> hostRectGetter)
        {
            var comboBox = rootView.focusedComboBox;
            if (comboBox == null)
            {
                return;
            }
            rootView.CancelFocusComboBox();

            if (_comboBox == comboBox)
            {
                Close();
                return;
            }

            _comboBox = comboBox;
            _host = host;
            // 既定のクロージャ生成はここまで遅らせる。ProcessFocus は毎フレーム
            // (IMGUI なので 1 フレームに複数回) 呼ばれるため、引数で作ると
            // コンボを開いていない間も delegate を確保し続けてしまう
            _hostRectGetter = hostRectGetter ?? (() => host.windowRect);
        }

        /// <summary>ボタンのスクリーンGUI座標の矩形。トグル判定と外側クリック判定に使う</summary>
        private Rect GetButtonScreenRect()
        {
            var pos = _hostRectGetter().position + _comboBox.buttonPos;
            return new Rect(pos.x, pos.y, _comboBox.buttonSize.x, _comboBox.buttonSize.y);
        }

        private Rect CalcPopupRect()
        {
            var buttonRect = GetButtonScreenRect();
            var size = _comboBox.GetPopupSize();

            // 基本はボタン直下。収まらなければボタンの上へ反転し、それでも画面内へクランプする
            var y = buttonRect.yMax;
            if (y + size.y > Screen.height)
            {
                y = buttonRect.y - size.y;
            }
            y = Mathf.Clamp(y, 0, Mathf.Max(0, Screen.height - size.y));
            var x = Mathf.Clamp(buttonRect.x, 0, Mathf.Max(0, Screen.width - size.x));

            return new Rect(x, y, size.x, size.y);
        }

        public void OnGUI()
        {
            if (_comboBox == null)
            {
                return;
            }

            // メニューバー等からホストが閉じられたらポップアップも畳む。
            // タブグループで別タブへ切り替わった場合もホストが描画されなくなるため同様に畳む
            var tabWindow = _host as ITabVisibleWindow;
            if (_host == null || !_host.isShowWnd ||
                (tabWindow != null && !tabWindow.isTabVisible))
            {
                Close();
                return;
            }

            // ホストのドラッグ移動に追従するよう毎フレーム計算する
            _popupRect = CalcPopupRect();
            GUI.Window(WINDOW_ID, _popupRect, DrawPopup, "", GUIView.gsWin);
            // 他のウィンドウに隠されないよう最前面へ
            GUI.BringWindowToFront(WINDOW_ID);
        }

        private void DrawPopup(int id)
        {
            _view.Init(new Rect(0, 0, _popupRect.width, _popupRect.height));

            if (_comboBox.DrawPopupContent(_view))
            {
                Close();
            }
        }

        public void Update()
        {
            // ポップアップとボタンの外をクリックしたら閉じる。
            // ボタン上は ProcessFocus のトグルに任せる (ここで閉じると再クリックで開き直ってしまう)
            if (_comboBox != null && Input.GetMouseButtonDown(0))
            {
                var pos = MTEUtils.rawGuiPosition;
                if (!CalcPopupRect().Contains(pos) && !GetButtonScreenRect().Contains(pos))
                {
                    Close();
                }
            }
        }

        public void Close()
        {
            _comboBox = null;
            _host = null;
            _hostRectGetter = null;
        }

        public void Init()
        {
        }

        public void OnLoad()
        {
        }

        public void OnScreenSizeChanged()
        {
        }

        public void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
            Close();
        }
    }
}
