using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// 色編集用の共通ウィンドウ。
    /// GUIView.DrawColor の一行表示から開かれ、グラデーションマップとスライダーで色を編集する。
    /// 編集対象の情報は DrawColor から毎フレーム Sync() で渡される
    /// </summary>
    public class ColorPickerWindow : IGUIWindow
    {
        /// <summary>他ウィンドウと衝突する場合はプラグイン側で差し替える</summary>
        public static int windowId = 896431;

        public static readonly int WINDOW_WIDTH = 260;
        public static readonly int HEADER_HEIGHT = 20;
        public static readonly string WINDOW_NAME = "カラー編集";

        private static readonly int SV_MAP_HEIGHT = 120;
        private static readonly int BAR_HEIGHT = 16;
        private static readonly int MARKER_SIZE = 5;
        private static readonly int ANCHOR_MARGIN = 2;

        private static ColorPickerWindow _instance = null;
        public static ColorPickerWindow instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ColorPickerWindow();
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

        // 編集対象。DrawColor から毎フレーム同期される
        private string _targetLabel = null;
        private Color _targetColor = Color.white;
        private Color _defaultColor = Color.white;
        private bool _hasAlpha = false;
        private Action<Color> _onColorChanged = null;
        private int _syncedFrame = -1;
        private int _openedFrame = -1;

        // 呼び出し元ボタンの矩形と、それを元に自動配置した位置（手動で動かされたかの判定用）
        private Rect _anchorRect = Rect.zero;
        private Vector2 _autoPos = Vector2.zero;

        private ColorFieldCache _fieldCache = new ColorFieldCache();

        private enum DragTarget
        {
            None,
            SV,
            Hue,
            Alpha,
        }

        private DragTarget _dragTarget = DragTarget.None;

        private Texture2D _hueTexture = null;
        private Texture2D _svTexture = null;
        private Texture2D _alphaTexture = null;
        private Texture2D _checkerTexture = null;
        private float _svTextureHue = -1f;
        private Color _alphaTextureColor = Color.clear;

        private ColorPickerWindow()
        {
            _windowRect = new Rect(0, 0, WINDOW_WIDTH, _windowHeight);
        }

        /// <summary>
        /// 指定ラベルの色を編集中かどうか
        /// </summary>
        public bool IsEditing(string label)
        {
            return isShowWnd && _targetLabel != null && _targetLabel == label;
        }

        /// <param name="anchorRect">呼び出し元ボタンの矩形（GUI 座標系のスクリーン位置）</param>
        public void Open(
            string label,
            Color color,
            Color defaultColor,
            bool hasAlpha,
            Action<Color> onColorChanged,
            Rect anchorRect)
        {
            _targetLabel = label;
            _onColorChanged = onColorChanged;
            _hasAlpha = hasAlpha;
            _targetColor = color;
            _defaultColor = defaultColor;
            _syncedFrame = Time.frameCount;
            _openedFrame = Time.frameCount;

            _fieldCache.label = label;
            _fieldCache.hasAlpha = hasAlpha;
            _fieldCache.UpdateColor(color, true);
            _fieldCache.UpdateDefaultColor(defaultColor);

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
            Color color,
            Color defaultColor,
            bool hasAlpha,
            Action<Color> onColorChanged)
        {
            if (!IsEditing(label))
            {
                return;
            }

            _targetColor = color;
            _defaultColor = defaultColor;
            _hasAlpha = hasAlpha;
            _onColorChanged = onColorChanged;
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
            _onColorChanged = null;
            _dragTarget = DragTarget.None;
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

            var contentWidth = WINDOW_WIDTH - (int)view.padding.x * 2;

            // ウィンドウ外でボタンを離した場合は MouseUp が届かないため、ここでドラッグ状態を解除する
            if (!Input.GetMouseButton(0))
            {
                _dragTarget = DragTarget.None;
            }

            _fieldCache.label = _targetLabel;
            _fieldCache.hasAlpha = _hasAlpha;
            _fieldCache.UpdateColor(_targetColor, true);
            _fieldCache.UpdateDefaultColor(_defaultColor);

            var hsv = _fieldCache.hsv;

            DrawSVMap(view, contentWidth, ref hsv);
            DrawHueBar(view, contentWidth, ref hsv);

            if (_hasAlpha)
            {
                DrawAlphaBar(view, contentWidth, ref hsv);
            }

            _fieldCache.UpdateHSV(hsv, true);

            view.AddSpace(5);

            // 現在色のプレビューと HEX 入力
            view.BeginHorizontal();
            {
                view.DrawTexture(GUIView.texWhite, 20, 20, _fieldCache.color);

                view.DrawColorFieldCache(null, _fieldCache, contentWidth - 20 * 3 - 15, 20);

                if (view.DrawButton("R", 20, 20))
                {
                    _fieldCache.ResetColor();
                }

                // スライダーの表色系（RGB/HSV）を切り替える。現在の表色系はスライダーのラベルで判る
                if (view.DrawTextureButton(GUIView.option.changeIcon, 20, 20, 0))
                {
                    GUIView.option.useHSVColor = !GUIView.option.useHSVColor;
                }
            }
            view.EndLayout();

            view.DrawHorizontalLine(Color.gray);

            DrawSliders(view, contentWidth);

            view.AddSpace(10);

            _windowHeight = (int)(view.currentPos.y + view.viewRect.y);

            if (_fieldCache.color != _targetColor && _onColorChanged != null)
            {
                _targetColor = _fieldCache.color;
                _onColorChanged(_fieldCache.color);
            }
        }

        private void DrawSliders(GUIView view, float width)
        {
            if (GUIView.option.useHSVColor)
            {
                var hsv = _fieldCache.hsv;
                var defaultHSV = _fieldCache.defaultHSV;

                DrawComponentSlider(view, "H", width, hsv.x, defaultHSV.x, x => hsv.x = x);
                DrawComponentSlider(view, "S", width, hsv.y, defaultHSV.y, y => hsv.y = y);
                DrawComponentSlider(view, "V", width, hsv.z, defaultHSV.z, z => hsv.z = z);

                if (_hasAlpha)
                {
                    DrawComponentSlider(view, "A", width, hsv.w, defaultHSV.w, w => hsv.w = w);
                }

                _fieldCache.UpdateHSV(hsv, true);
            }
            else
            {
                var color = _fieldCache.color;
                var defaultColor = _fieldCache.defaultColor;

                DrawComponentSlider(view, "R", width, color.r, defaultColor.r, r => color.r = r);
                DrawComponentSlider(view, "G", width, color.g, defaultColor.g, g => color.g = g);
                DrawComponentSlider(view, "B", width, color.b, defaultColor.b, b => color.b = b);

                if (_hasAlpha)
                {
                    DrawComponentSlider(view, "A", width, color.a, defaultColor.a, a => color.a = a);
                }

                _fieldCache.UpdateColor(color, true);
            }
        }

        private void DrawComponentSlider(
            GUIView view,
            string label,
            float width,
            float value,
            float defaultValue,
            Action<float> onChanged)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = label,
                labelWidth = 20,
                width = width,
                min = 0f,
                max = 1f,
                step = 0.01f,
                defaultValue = defaultValue,
                value = value,
                onChanged = onChanged,
            });
        }

        /// <summary>
        /// 彩度・明度のグラデーションマップ
        /// </summary>
        private void DrawSVMap(GUIView view, float width, ref Vector4 hsv)
        {
            UpdateSVTexture(hsv.x);

            Vector2 rate;
            var drawRect = DrawGradient(view, _svTexture, width, SV_MAP_HEIGHT, DragTarget.SV, out rate);
            if (rate.x >= 0f)
            {
                hsv.y = rate.x;
                hsv.z = 1f - rate.y;
            }

            DrawMarker(drawRect, hsv.y, 1f - hsv.z);
        }

        /// <summary>
        /// 色相のグラデーションバー
        /// </summary>
        private void DrawHueBar(GUIView view, float width, ref Vector4 hsv)
        {
            UpdateHueTexture();

            Vector2 rate;
            var drawRect = DrawGradient(view, _hueTexture, width, BAR_HEIGHT, DragTarget.Hue, out rate);
            if (rate.x >= 0f)
            {
                hsv.x = rate.x;
            }

            DrawMarker(drawRect, hsv.x, 0.5f);
        }

        /// <summary>
        /// 不透明度のグラデーションバー
        /// </summary>
        private void DrawAlphaBar(GUIView view, float width, ref Vector4 hsv)
        {
            UpdateAlphaTexture(_fieldCache.color);
            UpdateCheckerTexture();

            // 透過が判るように市松模様を下敷きにする
            var backRect = view.GetDrawRect(width, BAR_HEIGHT);
            GUI.DrawTextureWithTexCoords(
                backRect,
                _checkerTexture,
                new Rect(0, 0, backRect.width / _checkerTexture.width, backRect.height / _checkerTexture.height));

            Vector2 rate;
            var drawRect = DrawGradient(view, _alphaTexture, width, BAR_HEIGHT, DragTarget.Alpha, out rate);
            if (rate.x >= 0f)
            {
                hsv.w = rate.x;
            }

            DrawMarker(drawRect, hsv.w, 0.5f);
        }

        /// <summary>
        /// グラデーション画像を描画し、ドラッグ位置を 0～1 の比率で返す。
        /// 未操作時は rate.x に -1 を返す
        /// </summary>
        private Rect DrawGradient(
            GUIView view,
            Texture2D texture,
            float width,
            float height,
            DragTarget target,
            out Vector2 rate)
        {
            var drawRect = view.GetDrawRect(width, height);
            GUI.DrawTexture(drawRect, texture);
            view.NextElement(drawRect);

            rate = new Vector2(-1f, -1f);

            var ev = Event.current;
            if (ev.button != 0)
            {
                return drawRect;
            }

            if (ev.type == EventType.MouseDown && drawRect.Contains(ev.mousePosition))
            {
                _dragTarget = target;
            }

            if (_dragTarget != target)
            {
                return drawRect;
            }

            if (ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag)
            {
                var pos = ev.mousePosition - drawRect.position;
                rate = new Vector2(
                    Mathf.Clamp01(pos.x / drawRect.width),
                    Mathf.Clamp01(pos.y / drawRect.height));

                // ウィンドウのドラッグ移動に取られないように消費する
                ev.Use();
            }

            return drawRect;
        }

        /// <summary>
        /// グラデーション上の現在位置を示すマーカー
        /// </summary>
        private void DrawMarker(Rect drawRect, float rateX, float rateY)
        {
            var markerRect = new Rect(
                drawRect.x + drawRect.width * Mathf.Clamp01(rateX) - MARKER_SIZE * 0.5f,
                drawRect.y + drawRect.height * Mathf.Clamp01(rateY) - MARKER_SIZE * 0.5f,
                MARKER_SIZE,
                MARKER_SIZE);

            var color = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(markerRect, GUIView.texWhite);
            GUI.color = Color.white;
            GUI.DrawTexture(
                new Rect(markerRect.x + 1, markerRect.y + 1, markerRect.width - 2, markerRect.height - 2),
                GUIView.texWhite);
            GUI.color = color;
        }

        private static Texture2D CreateTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            // シーン遷移時の Resources.UnloadUnusedAssets() で破棄されないように保護する
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private void UpdateHueTexture()
        {
            if (_hueTexture != null)
            {
                return;
            }

            var width = 128;
            _hueTexture = CreateTexture(width, 1);

            for (var x = 0; x < width; x++)
            {
                _hueTexture.SetPixel(x, 0, Color.HSVToRGB(x / (float)(width - 1), 1f, 1f));
            }
            _hueTexture.Apply();
        }

        private void UpdateSVTexture(float hue)
        {
            if (_svTexture != null && Mathf.Approximately(_svTextureHue, hue))
            {
                return;
            }

            var size = 64;
            if (_svTexture == null)
            {
                _svTexture = CreateTexture(size, size);
            }
            _svTextureHue = hue;

            for (var y = 0; y < size; y++)
            {
                // GUI.DrawTexture はテクスチャ上端（v=1）を矩形の上端に描くため、明度は上ほど高くなる
                var v = y / (float)(size - 1);
                for (var x = 0; x < size; x++)
                {
                    var s = x / (float)(size - 1);
                    _svTexture.SetPixel(x, y, Color.HSVToRGB(hue, s, v));
                }
            }
            _svTexture.Apply();
        }

        private void UpdateAlphaTexture(Color color)
        {
            color.a = 1f;
            if (_alphaTexture != null && _alphaTextureColor == color)
            {
                return;
            }

            var width = 128;
            if (_alphaTexture == null)
            {
                _alphaTexture = CreateTexture(width, 1);
            }
            _alphaTextureColor = color;

            for (var x = 0; x < width; x++)
            {
                color.a = x / (float)(width - 1);
                _alphaTexture.SetPixel(x, 0, color);
            }
            _alphaTexture.Apply();
        }

        private void UpdateCheckerTexture()
        {
            if (_checkerTexture != null)
            {
                return;
            }

            var size = 16;
            var half = size / 2;
            _checkerTexture = CreateTexture(size, size);
            _checkerTexture.wrapMode = TextureWrapMode.Repeat;
            _checkerTexture.filterMode = FilterMode.Point;

            var colorA = new Color(0.75f, 0.75f, 0.75f, 1f);
            var colorB = new Color(0.5f, 0.5f, 0.5f, 1f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var isEven = (x < half) == (y < half);
                    _checkerTexture.SetPixel(x, y, isEven ? colorA : colorB);
                }
            }
            _checkerTexture.Apply();
        }
    }
}
