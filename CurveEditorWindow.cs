using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// カーブ編集用の共通ウィンドウ。
    /// GUIView.DrawCurve の一行表示から開かれ、プロット上のキー操作でカーブを編集する。
    /// - 左ドラッグ: キーの移動（空白クリックでキー追加）
    /// - 右クリック: キーの削除
    /// 編集対象の情報は DrawCurve から毎フレーム Sync() で渡される
    /// </summary>
    public class CurveEditorWindow : IGUIWindow
    {
        /// <summary>他ウィンドウと衝突する場合はプラグイン側で差し替える</summary>
        public static int windowId = 896432;

        public static readonly int WINDOW_WIDTH = 280;
        public static readonly int HEADER_HEIGHT = 20;
        public static readonly string WINDOW_NAME = "カーブ編集";

        private static readonly int PLOT_HEIGHT = 180;
        private static readonly int PLOT_TEX_WIDTH = 256;
        private static readonly int PLOT_TEX_HEIGHT = 192;
        private static readonly int MARKER_SIZE = 7;
        private static readonly float HIT_RADIUS = 8f;
        private static readonly float MIN_KEY_TIME_GAP = 0.001f;
        private static readonly int ANCHOR_MARGIN = 2;

        private static CurveEditorWindow _instance = null;
        public static CurveEditorWindow instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CurveEditorWindow();
                }
                return _instance;
            }
        }

        public int windowIndex { get; set; }
        public bool isShowWnd { get; set; }

        private Rect _windowRect;
        public Rect windowRect
        {
            get => _windowRect;
            set => _windowRect = value;
        }

        private int _windowHeight = 320;
        private bool _initializedGUI = false;

        private GUIView _rootView = new GUIView();
        private GUIView _headerView = new GUIView();
        private GUIView _contentView = new GUIView();

        // 編集対象。DrawCurve から毎フレーム同期される
        private string _targetLabel = null;
        private CurveData _curve = null;
        private Color _curveColor = Color.white;
        private Action _onChanged = null;
        private int _syncedFrame = -1;
        private int _openedFrame = -1;

        // 呼び出し元ボタンの矩形と、それを元に自動配置した位置（手動で動かされたかの判定用）
        private Rect _anchorRect = Rect.zero;
        private Vector2 _autoPos = Vector2.zero;

        private int _selectedKeyIndex = -1;
        private bool _dragging = false;

        private Texture2D _plotTexture = null;
        private int _plotVersion = -1;
        private Color _plotColor = Color.clear;

        private CurveEditorWindow()
        {
            _windowRect = new Rect(0, 0, WINDOW_WIDTH, _windowHeight);
        }

        /// <summary>
        /// 指定ラベルのカーブを編集中かどうか
        /// </summary>
        public bool IsEditing(string label)
        {
            return isShowWnd && _targetLabel != null && _targetLabel == label;
        }

        /// <param name="anchorRect">呼び出し元ボタンの矩形（GUI 座標系のスクリーン位置）</param>
        public void Open(
            string label,
            CurveData curve,
            Color curveColor,
            Action onChanged,
            Rect anchorRect)
        {
            _targetLabel = label;
            _curve = curve;
            _curveColor = curveColor;
            _onChanged = onChanged;
            _syncedFrame = Time.frameCount;
            _openedFrame = Time.frameCount;
            _selectedKeyIndex = -1;
            _dragging = false;

            isShowWnd = true;
            _anchorRect = anchorRect;
            ApplyAnchorPosition();
        }

        /// <summary>
        /// 編集対象の最新状態を反映する。
        /// 呼び出し元の描画が止まった場合はウィンドウを閉じる判定にも使う
        /// </summary>
        public void Sync(
            string label,
            CurveData curve,
            Color curveColor,
            Action onChanged)
        {
            if (!IsEditing(label))
            {
                return;
            }

            _curve = curve;
            _curveColor = curveColor;
            _onChanged = onChanged;
            _syncedFrame = Time.frameCount;
        }

        /// <summary>
        /// 呼び出し元ボタンに被らない位置へ移動する。
        /// 下側に収まらない場合は上側へ表示する
        /// </summary>
        private void ApplyAnchorPosition()
        {
            var x = _anchorRect.x;
            if (x + WINDOW_WIDTH > Screen.width)
            {
                x = Screen.width - WINDOW_WIDTH;
            }

            var y = _anchorRect.yMax + ANCHOR_MARGIN;
            if (y + _windowHeight > Screen.height)
            {
                y = _anchorRect.y - _windowHeight - ANCHOR_MARGIN;
            }

            _windowRect.x = Mathf.Max(x, 0);
            _windowRect.y = Mathf.Max(y, 0);
            _autoPos = _windowRect.position;
        }

        public void Init()
        {
        }

        public void Update()
        {
            // 呼び出し元が描画されなくなった（タブ切り替え・ウィンドウを閉じた等）場合は追従して閉じる
            if (isShowWnd && _syncedFrame < Time.frameCount - 1)
            {
                Close();
            }
        }

        public void Close()
        {
            isShowWnd = false;
            _targetLabel = null;
            _curve = null;
            _onChanged = null;
            _selectedKeyIndex = -1;
            _dragging = false;
        }

        public void OnLoad()
        {
        }

        public void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
            Close();
        }

        public void OnScreenSizeChanged()
        {
            MTEUtils.AdjustWindowPosition(ref _windowRect);
        }

        public void InitView()
        {
            _rootView.Init(0, 0, WINDOW_WIDTH, _windowHeight);
            _headerView.Init(0, 0, WINDOW_WIDTH, HEADER_HEIGHT);
            _contentView.Init(0, HEADER_HEIGHT, WINDOW_WIDTH, _windowHeight - HEADER_HEIGHT);

            _headerView.parent = _rootView;
            _contentView.parent = _rootView;
        }

        public void InitGUI()
        {
            if (_initializedGUI)
            {
                return;
            }
            _initializedGUI = true;

            InitView();
            MTEUtils.AdjustWindowPosition(ref _windowRect);
        }

        public void OnGUI()
        {
            if (!isShowWnd)
            {
                return;
            }

            InitGUI();

            if (CloseOnClickOutside())
            {
                return;
            }

            if (_windowHeight != _windowRect.height)
            {
                _windowRect.height = _windowHeight;
                InitView();

                // 手動で移動されていなければ、高さの変化に合わせて配置し直す
                if (_windowRect.position == _autoPos)
                {
                    ApplyAnchorPosition();
                }
            }

            var title = string.IsNullOrEmpty(_targetLabel) ? WINDOW_NAME : WINDOW_NAME + ": " + _targetLabel;
            _windowRect = GUI.Window(windowId, _windowRect, DrawWindow, title, GUIView.gsWin);
            MTEUtils.ResetInputOnScroll(_windowRect);
        }

        /// <summary>
        /// ウィンドウ外がクリックされたら閉じる。
        /// 他のコントロールにイベントを消費された後でも判定できるよう Input を直接見る。
        /// 判定はフレーム内で最後に流れる Repaint で行い、同フレームに開かれた場合は無視する
        /// </summary>
        private bool CloseOnClickOutside()
        {
            if (Event.current.type != EventType.Repaint) return false;
            if (_openedFrame == Time.frameCount) return false;
            if (!Input.GetMouseButtonDown(0)) return false;
            if (MTEUtils.IsMouseOverWindowRect(_windowRect)) return false;

            Close();
            return true;
        }

        private void DrawWindow(int id)
        {
            _rootView.ResetLayout();

            DrawHeader();
            DrawContent();

            GUI.DragWindow();
        }

        private void DrawHeader()
        {
            var view = _headerView;
            view.ResetLayout();

            view.padding = Vector2.zero;
            view.currentPos.x = WINDOW_WIDTH - 20;

            if (view.DrawButton("x", 20, 20))
            {
                Close();
            }
        }

        private void DrawContent()
        {
            var view = _contentView;
            view.ResetLayout();

            if (_curve == null)
            {
                return;
            }

            var contentWidth = WINDOW_WIDTH - (int)view.padding.x * 2;

            // ウィンドウ外でボタンを離した場合は MouseUp が届かないため、ここでドラッグ状態を解除する
            if (!Input.GetMouseButton(0))
            {
                _dragging = false;
            }

            DrawPlot(view, contentWidth);

            view.AddSpace(5);

            DrawSelectedKey(view, contentWidth);

            view.DrawHorizontalLine(Color.gray);

            view.BeginHorizontal();
            {
                if (view.DrawButton("直線に戻す", 100, 20))
                {
                    _curve.CopyFrom(CurveData.Linear());
                    _selectedKeyIndex = -1;
                    NotifyChanged();
                }

                view.DrawLabel("空白: 追加 / 右クリック: 削除", -1, 20);
            }
            view.EndLayout();

            view.AddSpace(10);

            _windowHeight = (int)(view.currentPos.y + view.viewRect.y);
        }

        /// <summary>
        /// カーブのプロット領域を描画し、キーのマウス操作を処理する
        /// </summary>
        private void DrawPlot(GUIView view, float width)
        {
            UpdatePlotTexture();

            var drawRect = view.GetDrawRect(width, PLOT_HEIGHT);
            GUI.DrawTexture(drawRect, _plotTexture);
            view.NextElement(drawRect);

            for (var i = 0; i < _curve.keys.Count; i++)
            {
                DrawKeyMarker(drawRect, _curve.keys[i], i == _selectedKeyIndex);
            }

            HandlePlotEvents(drawRect);
        }

        private Vector2 KeyToPlotPos(Rect drawRect, CurveKeyData key)
        {
            return new Vector2(
                drawRect.x + drawRect.width * Mathf.Clamp01(key.time),
                drawRect.y + drawRect.height * (1f - Mathf.Clamp01(key.value)));
        }

        /// <summary>
        /// プロット上のマウス座標を time (x) / value (y) へ変換する。KeyToPlotPos の逆変換
        /// </summary>
        private Vector2 PlotPosToTimeValue(Rect drawRect, Vector2 mousePos)
        {
            return new Vector2(
                Mathf.Clamp01((mousePos.x - drawRect.x) / drawRect.width),
                Mathf.Clamp01(1f - (mousePos.y - drawRect.y) / drawRect.height));
        }

        /// <summary>
        /// 前後のキーを跨がないように時間を制限する。
        /// prevIndex / nextIndex は範囲外なら 0〜1 の端として扱う。
        /// 隣接キーが密集して有効な範囲が無い場合は、並び順を保てる中間位置へ寄せる
        /// </summary>
        private float ClampTimeBetween(int prevIndex, int nextIndex, float time)
        {
            var keys = _curve.keys;
            var min = prevIndex >= 0 ? keys[prevIndex].time + MIN_KEY_TIME_GAP : 0f;
            var max = nextIndex < keys.Count ? keys[nextIndex].time - MIN_KEY_TIME_GAP : 1f;
            if (min > max)
            {
                var prevTime = prevIndex >= 0 ? keys[prevIndex].time : 0f;
                var nextTime = nextIndex < keys.Count ? keys[nextIndex].time : 1f;
                return (prevTime + nextTime) * 0.5f;
            }
            return Mathf.Clamp(time, min, max);
        }

        /// <summary>
        /// キーを削除して選択を解除する。キーが 2 個以下のときは何もしない
        /// </summary>
        private void DeleteKey(int index)
        {
            if (index < 0 || index >= _curve.keys.Count || _curve.keys.Count <= 2)
            {
                return;
            }

            _curve.keys.RemoveAt(index);
            _selectedKeyIndex = -1;
            SmoothAndNotify();
        }

        private void DrawKeyMarker(Rect drawRect, CurveKeyData key, bool selected)
        {
            var pos = KeyToPlotPos(drawRect, key);
            var markerRect = new Rect(
                pos.x - MARKER_SIZE * 0.5f,
                pos.y - MARKER_SIZE * 0.5f,
                MARKER_SIZE,
                MARKER_SIZE);

            var color = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(markerRect, GUIView.texWhite);
            GUI.color = selected ? Color.yellow : Color.white;
            GUI.DrawTexture(
                new Rect(markerRect.x + 1, markerRect.y + 1, markerRect.width - 2, markerRect.height - 2),
                GUIView.texWhite);
            GUI.color = color;
        }

        private int FindKeyAt(Rect drawRect, Vector2 mousePos)
        {
            var bestIndex = -1;
            var bestDist = HIT_RADIUS;
            for (var i = 0; i < _curve.keys.Count; i++)
            {
                var dist = Vector2.Distance(KeyToPlotPos(drawRect, _curve.keys[i]), mousePos);
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private void HandlePlotEvents(Rect drawRect)
        {
            var ev = Event.current;

            // キーの削除（右クリック）
            if (ev.type == EventType.MouseDown && ev.button == 1 && drawRect.Contains(ev.mousePosition))
            {
                DeleteKey(FindKeyAt(drawRect, ev.mousePosition));
                ev.Use();
                return;
            }

            if (ev.button != 0)
            {
                return;
            }

            if (ev.type == EventType.MouseDown && drawRect.Contains(ev.mousePosition))
            {
                var index = FindKeyAt(drawRect, ev.mousePosition);
                if (index < 0)
                {
                    // 空白クリックはその位置にキーを追加してそのまま掴む
                    index = AddKeyAt(drawRect, ev.mousePosition);
                }

                _selectedKeyIndex = index;
                _dragging = index >= 0;

                // ウィンドウのドラッグ移動に取られないように消費する
                ev.Use();
                return;
            }

            if (ev.type == EventType.MouseDrag && _dragging && _selectedKeyIndex >= 0)
            {
                MoveSelectedKey(drawRect, ev.mousePosition);
                ev.Use();
            }
        }

        private int AddKeyAt(Rect drawRect, Vector2 mousePos)
        {
            var timeValue = PlotPosToTimeValue(drawRect, mousePos);

            // 挿入位置（time 順を維持する）
            var index = 0;
            while (index < _curve.keys.Count && _curve.keys[index].time < timeValue.x)
            {
                index++;
            }

            // 既存キーと time が重複するとタンジェント計算が破綻するため、
            // 隙間が無い位置には追加せず近い方の既存キーを選択するに留める
            var min = index > 0 ? _curve.keys[index - 1].time + MIN_KEY_TIME_GAP : 0f;
            var max = index < _curve.keys.Count ? _curve.keys[index].time - MIN_KEY_TIME_GAP : 1f;
            if (min > max)
            {
                return index > 0 ? index - 1 : (_curve.keys.Count > 0 ? 0 : -1);
            }

            _curve.keys.Insert(index, new CurveKeyData
            {
                time = Mathf.Clamp(timeValue.x, min, max),
                value = timeValue.y,
            });
            SmoothAndNotify();
            return index;
        }

        private void MoveSelectedKey(Rect drawRect, Vector2 mousePos)
        {
            var key = _curve.keys[_selectedKeyIndex];
            var timeValue = PlotPosToTimeValue(drawRect, mousePos);

            key.time = ClampTimeBetween(_selectedKeyIndex - 1, _selectedKeyIndex + 1, timeValue.x);
            key.value = timeValue.y;
            SmoothAndNotify();
        }

        private void DrawSelectedKey(GUIView view, float width)
        {
            var keys = _curve.keys;
            if (_selectedKeyIndex < 0 || _selectedKeyIndex >= keys.Count)
            {
                view.DrawLabel("キー未選択", -1, 20);
                return;
            }

            var key = keys[_selectedKeyIndex];

            view.BeginHorizontal();
            {
                view.DrawLabel("キー " + (_selectedKeyIndex + 1) + "/" + keys.Count, 90, 20);

                if (keys.Count > 2 && view.DrawButton("削除", 45, 20))
                {
                    DeleteKey(_selectedKeyIndex);
                }
            }
            view.EndLayout();

            if (_selectedKeyIndex < 0)
            {
                return;
            }

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "時間",
                labelWidth = 40,
                width = width,
                min = 0f,
                max = 1f,
                step = 0.001f,
                defaultValue = key.time,
                value = key.time,
                onChanged = time =>
                {
                    key.time = ClampTimeBetween(_selectedKeyIndex - 1, _selectedKeyIndex + 1, Mathf.Clamp01(time));
                    SmoothAndNotify();
                },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "値",
                labelWidth = 40,
                width = width,
                min = 0f,
                max = 1f,
                step = 0.001f,
                defaultValue = key.value,
                value = key.value,
                onChanged = value =>
                {
                    key.value = Mathf.Clamp01(value);
                    SmoothAndNotify();
                },
            });
        }

        /// <summary>
        /// キー編集後の共通処理。全キーのタンジェントを滑らかに再計算して変更を通知する
        /// </summary>
        private void SmoothAndNotify()
        {
            // キーが 2 個未満だと ToAnimationCurve() が直線へフォールバックし、
            // 書き戻しで編集内容が消えてしまうため往復させない
            if (_curve.keys.Count >= 2)
            {
                var curve = _curve.ToAnimationCurve();
                for (var i = 0; i < curve.length; i++)
                {
                    curve.SmoothTangents(i, 0f);
                }
                _curve.FromAnimationCurve(curve);
            }
            else
            {
                _curve.version++;
            }
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            _onChanged?.Invoke();
        }

        private void UpdatePlotTexture()
        {
            if (_plotTexture == null)
            {
                _plotTexture = new Texture2D(PLOT_TEX_WIDTH, PLOT_TEX_HEIGHT, TextureFormat.ARGB32, false);
                // シーン遷移時の Resources.UnloadUnusedAssets() で破棄されないように保護する
                _plotTexture.hideFlags = HideFlags.HideAndDontSave;
                _plotTexture.wrapMode = TextureWrapMode.Clamp;
            }

            if (_plotVersion == _curve.version && _plotColor == _curveColor)
            {
                return;
            }
            _plotVersion = _curve.version;
            _plotColor = _curveColor;

            CurveTextureUtils.RenderCurve(_plotTexture, _curve.ToAnimationCurve(), _curveColor, drawGrid: true);
        }
    }
}
