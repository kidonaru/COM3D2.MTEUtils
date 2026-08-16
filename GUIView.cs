using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    public interface IGUIOption
    {
        float keyRepeatTimeFirst { get; }
        float keyRepeatTime { get; }
        bool useHSVColor { get; set; }
        Color windowHoverColor { get; }
        Color accentColor { get; }
        Texture2D changeIcon { get; }
        Texture2D favoriteOffIcon { get; }
        Texture2D favoriteOnIcon { get; }
    }

    public class GUIOptionBase : IGUIOption
    {
        public virtual float keyRepeatTimeFirst { get; } = 0.15f;
        public virtual float keyRepeatTime { get; } = 1f / 30f;
        public virtual bool useHSVColor { get; set; } = false;
        public virtual Color windowHoverColor { get; } = new Color(48 / 255f, 48 / 255f, 48 / 255f, 224 / 255f);
        // トグル・ボタン等の有効状態を示すアクセント色 (従来の Color.green 相当)
        public virtual Color accentColor { get; } = Color.green;
        public virtual Texture2D changeIcon => GUIView.texWhite;
        public virtual Texture2D favoriteOffIcon { get; }
        public virtual Texture2D favoriteOnIcon { get; }
    }

    public interface ITileViewContent
    {
        string name { get; }
        string tag { get; }
        Color tagColor { get; }
        float nameHeight { get; set; }
        Texture2D thum { get; }
        bool isDir { get; }
        bool isSelected { get; }
        bool canDelete { get; }
        bool isFavorite { get; set; }
        bool canFavorite { get; set; }
        List<ITileViewContent> children { get; }
        ITileViewContent parent { get; set; }

        int GetFileCount(bool recursive);
        int GetDirCount(bool recursive);
        void AddChild(ITileViewContent child);
        void RemoveChild(ITileViewContent child);
        void RemoveAllChildren();
        void RemoveFromParent();
        void GetAllChildren(List<ITileViewContent> result);
        void GetAllFiles(List<ITileViewContent> result);
    }

    public class TileViewContentBase : ITileViewContent
    {
        public virtual string name { get; set; }
        public virtual string setumei { get; set; }
        public virtual string tag { get; set; }
        public virtual Color tagColor { get; set; }
        public virtual float nameHeight { get; set; } = -1f;

        protected Texture2D _thum;
        public virtual Texture2D thum
        {
            get
            {
                if (children != null && children.Count > 0)
                {
                    return children[0].thum;
                }

                return _thum;
            }
            set
            {
                if (_thum != null)
                {
                    UnityEngine.Object.Destroy(_thum);
                }
                _thum = value;
            }
        }

        public virtual bool isDir { get; set; }
        public virtual bool isSelected { get; set; }
        public virtual bool canDelete { get; set; }
        public virtual bool isFavorite { get; set; }
        public virtual bool canFavorite { get; set; }
        public virtual List<ITileViewContent> children { get; set; }
        public virtual ITileViewContent parent { get; set; }

        public virtual int GetFileCount(bool recursive)
        {
            if (!isDir || children == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var child in children)
            {
                if (child == null) continue;

                if (!child.isDir)
                {
                    count++;
                }
                else if (recursive)
                {
                    count += child.GetFileCount(true);
                }
            }

            return count;
        }

        public virtual int GetDirCount(bool recursive)
        {
            if (!isDir || children == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var child in children)
            {
                if (child == null) continue;

                if (child.isDir)
                {
                    count++;
                    if (recursive)
                    {
                        count += child.GetDirCount(true);
                    }
                }
            }

            return count;
        }

        public virtual void AddChild(ITileViewContent child)
        {
            if (children == null)
            {
                children = new List<ITileViewContent>(16);
            }

            if (child.parent == this)
            {
                return;
            }

            if (child.parent != null)
            {
                child.parent.RemoveChild(child);
            }

            children.Add(child);
            child.parent = this;
        }

        public virtual void RemoveChild(ITileViewContent child)
        {
            if (children != null)
            {
                children.Remove(child);
            }

            child.parent = null;
        }

        public virtual void RemoveAllChildren()
        {
            if (children != null)
            {
                foreach (var child in children)
                {
                    child.parent = null;
                }
                children.Clear();
            }
        }

        public virtual void RemoveFromParent()
        {
            parent?.RemoveChild(this);
        }

        public virtual void GetAllChildren(List<ITileViewContent> result)
        {
            if (children == null)
            {
                return;
            }

            foreach (var child in children)
            {
                result.Add(child);
                child.GetAllChildren(result);
            }
        }

        public virtual void GetAllFiles(List<ITileViewContent> result)
        {
            if (children == null)
            {
                return;
            }

            foreach (var child in children)
            {
                if (!child.isDir)
                {
                    result.Add(child);
                }
                else
                {
                    child.GetAllFiles(result);
                }
            }
        }
    }

    /// <summary>
    /// 一時的なタイルビュー用のコンテンツ
    /// 子どもの親を操作しない
    /// </summary>
    public class TempTileViewContent : TileViewContentBase
    {
        public override void AddChild(ITileViewContent child)
        {
            if (children == null)
            {
                children = new List<ITileViewContent>(16);
            }

            children.Add(child);
        }

        public override void RemoveChild(ITileViewContent child)
        {
            if (children != null)
            {
                children.Remove(child);
            }
        }

        public override void RemoveAllChildren()
        {
            if (children != null)
            {
                children.Clear();
            }
        }
    }

    public class GUIView
    {
        private GUIView _parent = null;
        public GUIView parent
        {
            get => _parent;
            set
            {
                _parent = value;

                if (_parent != null)
                {
                    SetEnabled(_parent.guiEnabled);
                }
            }
        }

        public Vector2 currentPos;
        private LayoutDirection layoutDirection;
        public Vector2 padding = defaultPadding;

        private Rect _viewRect;
        public Rect viewRect
        {
            get
            {
                if (isScrollViewEnabled)
                {
                    return scrollViewContentRect;
                }
                return _viewRect;
            }
        }

        public Rect scrollViewContentRect;
        public Rect scrollViewRect;
        public Vector2 scrollPosition;

        public bool isScrollViewEnabled;
        public float labelWidth = 100;
        public Vector2 layoutMaxPos;
        public float margin = defaultMargin;
        public Color defaultColor = Color.white;
        public bool guiEnabled = true;

        public class RepeatButtonInfo
        {
            public int lastPressFrame;
            public float startTime;
            public float lastInvokeTime;
        }

        private RepeatButtonInfo _repeatButtonInfo = new RepeatButtonInfo();
        public RepeatButtonInfo repeatButtonInfo
        {
            get
            {
                if (parent != null)
                {
                    return parent.repeatButtonInfo;
                }
                return _repeatButtonInfo;
            }
            set
            {
                if (parent != null)
                {
                    parent.repeatButtonInfo = value;
                }
                else
                {
                    _repeatButtonInfo = value;
                }
            }
        }

        private GUIComboBoxBase _focusedComboBox;
        public GUIComboBoxBase focusedComboBox
        {
            get
            {
                if (parent != null)
                {
                    return parent.focusedComboBox;
                }
                return _focusedComboBox;
            }
            set
            {
                if (parent != null)
                {
                    parent.focusedComboBox = value;
                }
                else
                {
                    _focusedComboBox = value;
                }
            }
        }

        public GUIView topView
        {
            get
            {
                if (parent != null)
                {
                    return parent.topView;
                }
                return this;
            }
        }

        /// <summary>
        /// 自身と祖先のスクロールビューによる表示位置のずれの合計。
        /// スクロールビュー内では描画座標がコンテンツ基準になるため、
        /// ウィンドウ基準の座標が要る箇所 (ComboBox のポップアップ位置など) で足して使う。
        /// </summary>
        public Vector2 scrollOffset
        {
            get
            {
                var offset = Vector2.zero;
                for (var view = this; view != null; view = view.parent)
                {
                    if (view.isScrollViewEnabled)
                    {
                        offset += view.scrollViewRect.position - view.scrollPosition;
                    }
                }
                return offset;
            }
        }

        private List<FloatFieldCache> _fieldCaches = new List<FloatFieldCache>();
        private int _fieldCacheIndex = 0;

        private List<IntFieldCache> _intFieldCaches = new List<IntFieldCache>();
        private int _intFieldCacheIndex = 0;

        private List<TransformCache> _transformCaches = new List<TransformCache>();
        private int _transformCacheIndex = 0;

        private List<ColorFieldCache> _colorFieldCaches = new List<ColorFieldCache>();
        private int _colorFieldCacheIndex = 0;

        // 組み込み GUIStyle ("button" 等) の複製は GUISkin.current が有効な OnGUI 内でしか行えない。
        // OnGUI 外で複製すると Unity は空の StyleNotFoundError を返し、背景・border・padding を
        // 失った見た目になる。Unity 5.6 (COM3D2) では OnGUI 後も GUISkin.current が保持されたため
        // 表面化しなかったが、Unity 2022 (COM3D2.5) では毎回 null になるため必ず遅延生成する。
        private static bool _stylesInitialized = false;

        private static GUIStyle _gsWin = null;
        private static GUIStyle _gsLabel = null;
        private static GUIStyle _gsLabelRight = null;
        private static GUIStyle _gsButton = null;
        private static GUIStyle _gsSelectedButton = null;
        private static GUIStyle _gsToggle = null;
        private static GUIStyle _gsTextField = null;
        private static GUIStyle _gsTextArea = null;
        private static GUIStyle _gsTile = null;
        private static GUIStyle _gsTileLabel = null;
        private static GUIStyle _gsTagLabel = null;
        private static GUIStyle _gsTagBackground = null;
        private static GUIStyle _gsMask = null;
        private static GUIStyle _gsBox = null;

        public static GUIStyle gsWin { get { InitStyles(); return _gsWin; } }
        public static GUIStyle gsLabel { get { InitStyles(); return _gsLabel; } }
        public static GUIStyle gsLabelRight { get { InitStyles(); return _gsLabelRight; } }
        public static GUIStyle gsButton { get { InitStyles(); return _gsButton; } }
        public static GUIStyle gsSelectedButton { get { InitStyles(); return _gsSelectedButton; } }
        public static GUIStyle gsToggle { get { InitStyles(); return _gsToggle; } }
        public static GUIStyle gsTextField { get { InitStyles(); return _gsTextField; } }
        public static GUIStyle gsTextArea { get { InitStyles(); return _gsTextArea; } }
        public static GUIStyle gsTile { get { InitStyles(); return _gsTile; } }
        public static GUIStyle gsTileLabel { get { InitStyles(); return _gsTileLabel; } }
        public static GUIStyle gsTagLabel { get { InitStyles(); return _gsTagLabel; } }
        public static GUIStyle gsTagBackground { get { InitStyles(); return _gsTagBackground; } }
        public static GUIStyle gsMask { get { InitStyles(); return _gsMask; } }
        public static GUIStyle gsBox { get { InitStyles(); return _gsBox; } }

        /// <summary>
        /// 組み込みスタイル由来の GUIStyle を生成する。必ず OnGUI 内から呼ぶこと。
        /// </summary>
        public static void InitStyles()
        {
            if (_stylesInitialized)
            {
                return;
            }

            // Event.current が null = OnGUI 外。組み込みスタイルを取得できないので次フレームに委ねる
            if (Event.current == null)
            {
                return;
            }

            // 失敗しても再試行しない。OnGUI 冒頭で呼ばれるため、例外を投げ続けると
            // 全ウィンドウが描画されないまま毎フレーム同じ例外とログを繰り返すことになる
            _stylesInitialized = true;

            try
            {
                BuildStyles();
            }
            catch (System.Exception e)
            {
                MTEUtils.LogException(e);
            }
        }

        /// <summary>
        /// 新規に GUIStyle を追加したらここにも追加すること。
        /// </summary>
        private static void BuildStyles()
        {
            _gsWin = new GUIStyle("box")
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
            };
            var hoverTex = CreateColorTexture(option.windowHoverColor);
            _gsWin.onHover.background = hoverTex;
            _gsWin.hover.background = hoverTex;
            _gsWin.onFocused.background = hoverTex;
            _gsWin.focused.background = hoverTex;

            // 組み込み box のフォーカス/ホバー状態は文字色が黒のため、
            // タイトルが暗い背景に埋もれる。通常状態と同じ文字色にそろえる
            var winTextColor = _gsWin.normal.textColor;
            _gsWin.onNormal.textColor = winTextColor;
            _gsWin.hover.textColor = winTextColor;
            _gsWin.onHover.textColor = winTextColor;
            _gsWin.focused.textColor = winTextColor;
            _gsWin.onFocused.textColor = winTextColor;
            _gsWin.active.textColor = winTextColor;
            _gsWin.onActive.textColor = winTextColor;

            _gsLabel = new GUIStyle("label")
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
            };
            _gsLabelRight = new GUIStyle("label")
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight,
                wordWrap = false,
            };
            _gsButton = new GUIStyle("button")
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            _gsSelectedButton = new GUIStyle("box")
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
            };
            _gsToggle = new GUIStyle("toggle")
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
            };
            _gsTextField = new GUIStyle("textField")
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            _gsTextArea = new GUIStyle("textArea")
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
            };
            _gsTile = new GUIStyle("button")
            {
                normal = {
                    background = CreateColorTexture(new Color(0, 0, 0, 0.5f))
                },
                hover = {
                    background = CreateColorTexture(new Color(0.75f, 0.75f, 0.75f, 0.5f))
                },
                active = {
                    background = CreateColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.5f))
                }
            };
            _gsTileLabel = new GUIStyle("button")
            {
                fontSize = 12,
                alignment = TextAnchor.LowerCenter,
                wordWrap = true,
                normal = {
                    background = CreateColorTexture(new Color(0, 0, 0, 0.5f))
                },
            };
            _gsTagLabel = new GUIStyle("box")
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                normal = {
                    background = CreateColorTexture(new Color(0, 0, 0, 0))
                },
            };
            _gsTagBackground = new GUIStyle("box")
            {
                normal = {
                    background = CreateColorTexture(Color.white)
                },
            };
            _gsMask = new GUIStyle("box")
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = {
                    background = CreateColorTexture(new Color(0, 0, 0, 0.5f))
                }
            };
            _gsBox = new GUIStyle("box")
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
        }

        public static Vector2 defaultPadding = new Vector2(10, 10);
        public static float defaultMargin = 5;
        public static Texture2D texDummy = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        public static Texture2D texWhite = CreateColorTexture(Color.white);

        public static IGUIOption option = new GUIOptionBase();

        public enum LayoutDirection
        {
            Vertical,
            Horizontal,
            Free,
        }

        public GUIView()
        {
            Init(Rect.zero);
        }

        public GUIView(float x, float y, float width, float height)
        {
            Init(new Rect(x, y, width, height));
        }

        public GUIView(Rect viewRect)
        {
            Init(viewRect);
        }

        public void Init(float x, float y, float width, float height)
        {
            Init(new Rect(x, y, width, height));
        }

        public void Init(Rect viewRect)
        {
            this._viewRect = viewRect;
            ResetLayout();
        }

        public void ResetLayout()
        {
            this.layoutDirection = LayoutDirection.Vertical;
            this.currentPos = Vector2.zero;
            this.layoutMaxPos = Vector2.zero;

            //MTEUtils.LogDebug("ResetLayout frame={0} _fieldCacheIndex={1} _transformCacheIndex={2}",
            //    Time.frameCount, _fieldCacheIndex, _transformCacheIndex);

            this._fieldCacheIndex = 0;
            this._intFieldCacheIndex = 0;
            this._transformCacheIndex = 0;
            this._colorFieldCacheIndex = 0;

            EndEnabled();
        }

        public void BeginLayout(LayoutDirection direction)
        {
            this.layoutDirection = direction;
        }

        public void BeginHorizontal()
        {
            BeginLayout(LayoutDirection.Horizontal);
        }

        public void EndLayout()
        {
            this.currentPos.x = 0;
            this.currentPos.y = this.layoutMaxPos.y;
            this.layoutDirection = LayoutDirection.Vertical;
        }

        private void UpdateScrollViewContentRect(Rect newContentRect)
        {
            if (newContentRect.width < 0f) newContentRect.width = viewRect.width - 20;
            if (newContentRect.height < 0f) newContentRect.height = scrollViewContentRect.height;
            if (newContentRect.height < scrollViewRect.height) newContentRect.height = scrollViewRect.height;
            scrollViewContentRect = newContentRect;
        }

        public void BeginScrollView(
            float width,
            float height,
            Rect contentRect,
            GUIStyle horizontalScrollbar,
            GUIStyle verticalScrollbar)
        {
            var savedPadding = padding;
            padding = Vector2.zero;
            scrollViewRect = GetDrawRect(width, height);
            padding = savedPadding;

            UpdateScrollViewContentRect(contentRect);

            scrollPosition = GUI.BeginScrollView(
                scrollViewRect,
                scrollPosition,
                scrollViewContentRect,
                horizontalScrollbar,
                verticalScrollbar);

            this.isScrollViewEnabled = true;
            this.currentPos = Vector2.zero;
        }

        public readonly static Rect AutoScrollViewRect = new Rect(0, 0, -1, -1);

        public void BeginScrollView()
        {
            BeginScrollView(-1, -1, AutoScrollViewRect, false, true);;
        }

        public void BeginScrollView(
            float width,
            float height,
            Rect contentRect,
            bool alwaysShowHorizontal,
            bool alwaysShowVertical)
        {
            var savedPadding = padding;
            padding = Vector2.zero;
            scrollViewRect = GetDrawRect(width, height);
            padding = savedPadding;

            UpdateScrollViewContentRect(contentRect);

            scrollPosition = GUI.BeginScrollView(
                scrollViewRect,
                scrollPosition,
                scrollViewContentRect,
                alwaysShowHorizontal,
                alwaysShowVertical);

            this.isScrollViewEnabled = true;
            this.currentPos = Vector2.zero;
            this.layoutMaxPos = Vector2.zero;
        }

        public void EndScrollView()
        {
            scrollViewContentRect.height = currentPos.y + 20;

            GUI.EndScrollView();
            this.isScrollViewEnabled = false;

            currentPos = scrollViewRect.position;
            NextElement(scrollViewRect);

            this.scrollViewRect = Rect.zero;
        }

        public void NextElement(Rect drawRect)
        {
            if (this.layoutDirection == LayoutDirection.Vertical)
            {
                this.currentPos.x = 0;
                this.currentPos.y += drawRect.height + margin;
                this.layoutMaxPos.y = Math.Max(this.layoutMaxPos.y, this.currentPos.y);
            }
            if (this.layoutDirection == LayoutDirection.Horizontal)
            {
                this.currentPos.x += drawRect.width + margin;
                this.layoutMaxPos.x = Math.Max(this.layoutMaxPos.x, this.currentPos.x);
                this.layoutMaxPos.y = Math.Max(this.layoutMaxPos.y, this.currentPos.y + drawRect.height + margin);
            }
        }

        /// <summary>
        /// スクロールビューの表示範囲から外れているか。
        /// 件数の多いリストで画面外の要素の描画を省くために使う
        /// (位置送りは呼び出し側で NextElement を呼んで必ず行うこと。
        /// 送らないと以降の行位置とスクロール範囲がずれる)。
        /// スクロールビュー外は判定材料 (scrollViewRect) が無いため常に false を返す
        /// </summary>
        public bool IsOutOfScrollView(Rect drawRect)
        {
            if (!isScrollViewEnabled)
            {
                return false;
            }

            return drawRect.position.y + drawRect.height < scrollPosition.y ||
                drawRect.position.y > scrollPosition.y + scrollViewRect.height;
        }

        public void BeginColor(Color color)
        {
            if (color != defaultColor)
            {
                GUI.color = color;
            }
        }

        public void EndColor()
        {
            if (GUI.color != defaultColor)
            {
                GUI.color = defaultColor;
            }
        }

        public void SetEnabled(bool enabled)
        {
            this.guiEnabled = enabled;
            EndEnabled();
        }

        public void BeginEnabled(bool enabled)
        {
            if (enabled) return;

            if (enabled != guiEnabled)
            {
                GUI.enabled = enabled;
            }
        }

        public void EndEnabled()
        {
            if (GUI.enabled != guiEnabled)
            {
                GUI.enabled = guiEnabled;
            }
        }

        public Rect GetDrawRect(float x, float y, float width, float height)
        {
            x += this.viewRect.x + padding.x;
            y += this.viewRect.y + padding.y;
            if (width < 0) width = this.viewRect.width - currentPos.x - this.padding.x * 2;
            if (height < 0) height = this.viewRect.height - currentPos.y - this.padding.y * 2;
            return new Rect(x, y, width, height);
        }

        public Rect GetDrawRect(float width, float height)
        {
            return GetDrawRect(this.currentPos.x, this.currentPos.y, width, height);
        }

        public void DrawEmpty(float width, float height)
        {
            var drawRect = GetDrawRect(width, height);
            NextElement(drawRect);
        }

        public bool DrawTextureButton(
            Texture2D texture,
            float width,
            float height,
            float offsetSize = 0f,
            bool enabled = true,
            GUIStyle style = null)
        {
            var drawRect = GetDrawRect(width, height);
            BeginEnabled(enabled);
            bool result = GUI.Button(drawRect, "", style ?? gsButton);
            DrawTileThumb(texture, offsetSize * 0.5f, offsetSize * 0.5f, drawRect.width - offsetSize, drawRect.height - offsetSize);
            EndEnabled();
            NextElement(drawRect);
            return result;
        }

        public bool DrawButton(
            string text,
            float width,
            float height,
            bool enabled = true,
            Color? color = null,
            GUIStyle style = null)
        {
            var drawRect = GetDrawRect(width, height);
            if (IsOutOfScrollView(drawRect))
            {
                this.NextElement(drawRect);
                return false;
            }

            BeginEnabled(enabled);
            if (color != null) BeginColor(color.Value);
            var result = GUI.Button(drawRect, text, style ?? gsButton);
            this.NextElement(drawRect);
            if (color != null) EndColor();
            EndEnabled();
            return result;
        }

        public bool DrawRepeatButton(string text, float width, float height)
        {
            var drawRect = GetDrawRect(width, height);
            var isPressed = GUI.RepeatButton(drawRect, text, gsButton);
            this.NextElement(drawRect);

            bool result = false;
            if (isPressed)
            {
                var frameNo = Time.frameCount;
                var currentTime = Time.realtimeSinceStartup;
                var info = repeatButtonInfo;

                if (info.lastPressFrame < frameNo - 1)
                {
                    info.startTime = currentTime;
                    info.lastInvokeTime = currentTime;
                    result = true;
                }

                info.lastPressFrame = frameNo;

                if (currentTime > info.startTime + option.keyRepeatTimeFirst &&
                    currentTime > info.lastInvokeTime + option.keyRepeatTime)
                {
                    //MTEUtils.LogDebug("DrawRepeatButton: repeat frame={0} lastInvokeTime={1}",
                    //    frameNo, info.lastInvokeTime);
                    info.lastInvokeTime = currentTime;
                    result = true;
                }
            }

            return result;
        }
        
        public class DragInfo
        {
            public bool isDragging;
            public Vector3 lastMousePos;
            public Vector2 startPos;
            public Vector2 pos;
        }

        public void DrawDraggableButton(
            string text,
            float width,
            float height,
            DragInfo info,
            Vector2 pos,
            Action<Vector2> onStart,
            Action<Vector2> onDragging)
        {
            var drawRect = GetDrawRect(width, height);

            InvokeActionOnDragStart(drawRect, info, pos, onStart);
            InvokeActionOnDragging(info, onDragging);

            GUI.Button(drawRect, text, gsButton);
            NextElement(drawRect);
        }

        public void InvokeActionOnDragging(
            float width,
            float height,
            DragInfo info,
            Vector2 pos,
            Action<Vector2> onStart,
            Action<Vector2> onDragging)
        {
            var drawRect = GetDrawRect(width, height);
            InvokeActionOnDragStart(drawRect, info, pos, onStart);
            InvokeActionOnDragging(info, onDragging);    
        }

        public void InvokeActionOnDragStart(
            Rect drawRect,
            DragInfo info,
            Vector2 pos,
            Action<Vector2> onStart)
        {
            if (Event.current.type == EventType.MouseDown &&
                drawRect.Contains(Event.current.mousePosition) && 
                Event.current.button == 0)
            {
                info.isDragging = true;
                info.lastMousePos = MTEUtils.mousePosition;
                info.startPos = pos;
                info.pos = pos;
                onStart?.Invoke(info.pos);
            }
        }

        public void InvokeActionOnDragging(
            DragInfo info,
            Action<Vector2> onDragging)
        {
            if (info.isDragging && !Input.GetMouseButton(0))
            {
                info.isDragging = false;
            }

            if (info.isDragging)
            {
                var mousePos = MTEUtils.mousePosition;
                var diff = mousePos - info.lastMousePos;
                diff.y = -diff.y;
                if (diff.sqrMagnitude > 0)
                {
                    info.pos += new Vector2(diff.x, diff.y);
                    onDragging?.Invoke(info.pos);
                    info.lastMousePos = mousePos;
                }
            }
        }

        public bool DrawToggle(
            string label,
            bool value,
            float width,
            float height,
            bool enabled,
            Action<bool> onChanged)
        {
            var drawRect = GetDrawRect(width, height);
            BeginEnabled(enabled);
            BeginColor(value ? option.accentColor : Color.white);
            bool newValue = GUI.Toggle(drawRect, value, label, gsToggle);
            EndColor();
            EndEnabled();
            this.NextElement(drawRect);

            if (newValue != value)
            {
                onChanged(newValue);
                return true;
            }
            return false;
        }

        public bool DrawToggle(string label, bool value, float width, float height, Action<bool> onChanged)
        {
            return DrawToggle(label, value, width, height, true, onChanged);
        }

        public bool DrawToggle(bool value, float width, float height, Action<bool> onChanged)
        {
            return DrawToggle(null, value, width, height, true, onChanged);
        }

        /// <summary>アイコン表示のトグルボタン。ON のときアクセント色でティントする</summary>
        public bool DrawToggle(
            Texture2D icon,
            bool value,
            float width,
            float height,
            Action<bool> onChanged,
            float offsetSize = 0f)
        {
            var drawRect = GetDrawRect(width, height);
            BeginColor(value ? option.accentColor : Color.white);
            bool newValue = GUI.Toggle(drawRect, value, "", gsButton);
            DrawTileThumb(icon, offsetSize * 0.5f, offsetSize * 0.5f,
                drawRect.width - offsetSize, drawRect.height - offsetSize);
            EndColor();
            NextElement(drawRect);

            if (newValue != value)
            {
                onChanged(newValue);
                return true;
            }
            return false;
        }

        public void DrawLabel(
            string text,
            float width,
            float height,
            Color? textColor = null,
            GUIStyle style = null,
            Action onClickAction = null)
        {
            var drawRect = GetDrawRect(width, height);
            if (IsOutOfScrollView(drawRect))
            {
                this.NextElement(drawRect);
                return;
            }

            if (textColor != null) BeginColor(textColor.Value);
            GUI.Label(drawRect, text, style ?? gsLabel);
            if (textColor != null) EndColor();
            this.NextElement(drawRect);

            if (onClickAction != null
                && drawRect.Contains(Event.current.mousePosition)
                && Event.current.type == EventType.MouseDown
                && Event.current.button == 0)
            {
                onClickAction();
            }
        }

        /// <summary>
        /// 左右ドラッグで数値を増減できるラベル。1pxあたり sensitivity、Shift押下中は0.1倍。
        /// ドラッグ中はウィンドウごと動いてしまわないようイベントを消費する
        /// </summary>
        public void DrawDragLabel(
            string text,
            float width,
            float height,
            float sensitivity,
            Action<float> onDelta,
            GUIStyle style = null)
        {
            var drawRect = GetDrawRect(width, height);
            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            GUI.Label(drawRect, text, style ?? gsLabel);
            this.NextElement(drawRect);

            var e = Event.current;
            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && drawRect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        var scale = e.shift ? 0.1f : 1f;
                        if (e.delta.x != 0f)
                        {
                            onDelta(e.delta.x * sensitivity * scale);
                        }
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }

        public bool DrawTextField(
            string label,
            float labelWidth,
            string text,
            float width,
            float height,
            Action<string> onChanged = null,
            bool hasNewLine = false)
        {
            if (!string.IsNullOrEmpty(label))
            {
                if (labelWidth <= 0f)
                {
                    labelWidth = this.labelWidth;
                }

                var labelRect = GetDrawRect(labelWidth, height);
                GUI.Label(labelRect, label, gsLabel);
                currentPos.x += labelWidth + margin;
                width -= labelWidth + margin;
            }

            if (onChanged == null) GUI.enabled = false;

            var drawRect = GetDrawRect(width, height);
            var newText = text;
            if (hasNewLine)
            {
                newText = GUI.TextArea(drawRect, text, gsTextArea);
            }
            else
            {
                newText = GUI.TextField(drawRect, text, gsTextField);
            }
            this.NextElement(drawRect);

            if (onChanged == null) GUI.enabled = guiEnabled;

            var updated = false;
            if (newText != text)
            {
                onChanged(newText);
                updated = true;
            }

            return updated;
        }

        public void DrawTextField(
            string text,
            float width,
            float height,
            Action<string> onChanged = null)
        {
            DrawTextField(null, 0f, text, width, height, onChanged);
        }

        public struct TextFieldOption
        {
            public string label;
            public float labelWidth;
            public float width;
            public string value;
            public Action<string> onChanged;
            public int maxLines;
            public bool disabled;
            public bool hiddenButton;
        }

        public bool DrawTextField(TextFieldOption option)
        {
            var height = option.maxLines > 1 ? 20 * option.maxLines : 20;
            var subViewRect = GetDrawRect(option.width > 0 ? option.width : -1, height);
            var updated = false;

            BeginSubView(subViewRect, LayoutDirection.Horizontal);
            {
                subView.BeginEnabled(!option.disabled);

                if (!string.IsNullOrEmpty(option.label))
                {
                    subView.DrawLabel(option.label, option.labelWidth, 20);
                }

                var buttonWidth = option.hiddenButton ? 0 : 20 * 2;
                var fieldWidth = subViewRect.width - subView.currentPos.x - buttonWidth;

                updated = subView.DrawTextField(
                    "",
                    0f,
                    option.value,
                    fieldWidth,
                    height,
                    option.onChanged,
                    option.maxLines > 1);

                if (!option.hiddenButton)
                {
                    if (subView.DrawButton("C", 20, 20))
                    {
                        GUIUtility.systemCopyBuffer = option.value;
                    }

                    if (subView.DrawButton("P", 20, 20))
                    {
                        option.onChanged(GUIUtility.systemCopyBuffer);
                        updated = true;
                    }
                }

                subView.EndEnabled();
            }
            EndSubView();

            return updated;
        }

        public GUIView subView;

        public GUIView BeginSubView(Rect subViewRect, LayoutDirection direction)
        {
            if (subView == null)
            {
                subView = new GUIView();
            }

            subView.parent = this;
            subView.Init(subViewRect);
            subView.margin = 0;
            subView.padding = Vector2.zero;
            subView.BeginLayout(direction);

            return subView;
        }

        public void EndSubView()
        {
            subView.EndLayout();
            NextElement(subView._viewRect);
        }

        public struct FloatFieldOption
        {
            public string label;
            public float labelWidth;
            public FloatFieldType fieldType;
            public float value;
            public float minValue;
            public float maxValue;
            public float width;
            public float height;
            public FloatFieldCache fieldCache;
            public Action<float> onChanged;
            public Action onReset;
        }

        public bool DrawFloatField(FloatFieldOption option)
        {
            var fieldCache = option.fieldCache;
            if (fieldCache == null)
            {
                fieldCache = GetFieldCache(option.label, option.fieldType);
                fieldCache.UpdateValue(option.value);
            }

            var updated = false;

            Action<string> onChanged = null;
            if (option.onChanged != null)
            {
                onChanged = newText =>
                {
                    fieldCache.text = newText;

                    float newValue;
                    if (float.TryParse(newText, out newValue))
                    {
                        if (option.minValue != 0f || option.maxValue != 0f)
                        {
                            newValue = Mathf.Clamp(newValue, option.minValue, option.maxValue);
                        }
                        fieldCache.UpdateValue(newValue, false);
                        option.onChanged(newValue);
                        updated = true;
                    }
                };
            }

            if (option.onReset != null)
            {
                var subViewRect = GetDrawRect(option.width, option.height);

                BeginSubView(subViewRect, LayoutDirection.Horizontal);
                {
                    var fieldWidth = subViewRect.width - 20;

                    subView.DrawTextField(
                        option.label,
                        option.labelWidth,
                        fieldCache.text,
                        fieldWidth,
                        option.height,
                        onChanged);

                    if (subView.DrawButton("R", 20, 20))
                    {
                        option.onReset();
                        updated = true;
                    }
                }
                EndSubView();
            }
            else
            {
                DrawTextField(
                    option.label,
                    option.labelWidth,
                    fieldCache.text,
                    option.width,
                    option.height,
                    onChanged);
            }

            return updated;
        }

        public struct Vector3RowOption
        {
            public string label;
            public float labelWidth;
            /// <summary>ラベルのスタイル。null なら既定</summary>
            public GUIStyle labelStyle;
            /// <summary>行の高さ。0 なら 20</summary>
            public float height;
            /// <summary>ドラッグラベルの 1px あたりの増減量</summary>
            public float dragSensitivity;
            public Vector3 value;
            public Action<Vector3> onChanged;
            /// <summary>変更後の値と変更軸の index を受け取る。onChanged とどちらかを設定する</summary>
            public Action<Vector3, int> onChangedAxis;
            /// <summary>null ならリセットボタンを出さない</summary>
            public Action onReset;
            /// <summary>連動トグルのアイコン。null ならテキスト表示にフォールバック</summary>
            public Texture2D linkIcon;
            public bool linked;
            /// <summary>null なら連動トグルを出さない</summary>
            public Action<bool> onLinkChanged;
        }

        private static readonly string[] Vector3AxisNames = { "X", "Y", "Z" };
        public static readonly float Vector3DragLabelWidth = 14f;
        public static readonly float Vector3ResetButtonWidth = 20f;
        // 連動トグルのアイコン余白 (ツールバーのアイコンボタンと同じ見た目の大きさに揃える)
        public static readonly float Vector3LinkIconOffset = 4f;
        public static readonly float Vector3FieldMinWidth = 40f;

        /// <summary>
        /// ラベル + XYZ (ドラッグラベル + 数値入力) + リセットボタンの 1 行。
        /// 数値入力の幅は行の残り幅を 3 軸で等分して算出し、ウィンドウサイズに追従する
        /// </summary>
        public void DrawVector3Row(Vector3RowOption option)
        {
            var height = option.height > 0f ? option.height : 20f;
            var value = option.value;
            var hasReset = option.onReset != null;
            var hasLink = option.onLinkChanged != null;

            // ラベル・ドラッグラベル・リセット以外の残り幅を 3 軸で分け合う。
            // viewRect はスクロールビュー中もコンテンツ幅を返すため分岐不要
            // (GetDrawRect の auto-width と同じ式)
            var available = viewRect.width - padding.x * 2;
            // margin は NextElement が要素ごとに加算するため、要素数ぶん引く
            // (ラベル 1 + ドラッグラベル 3 + 数値入力 3 + リセット 1)
            available -= option.labelWidth + margin
                + (Vector3DragLabelWidth + margin) * 3
                + margin * 3;
            if (hasReset)
            {
                available -= Vector3ResetButtonWidth + margin;
            }
            if (hasLink)
            {
                available -= Vector3ResetButtonWidth + margin;
            }
            var fieldWidth = Mathf.Max(available / 3f, Vector3FieldMinWidth);

            BeginHorizontal();
            {
                DrawLabel(option.label, option.labelWidth, height, style: option.labelStyle);

                for (var i = 0; i < 3; i++)
                {
                    var index = i;

                    DrawDragLabel(Vector3AxisNames[index], Vector3DragLabelWidth, height,
                        option.dragSensitivity,
                        delta =>
                        {
                            value[index] += delta;
                            NotifyChanged(option, value, index);
                        });

                    // ドラッグで変わった値を表示へ反映するため、キャッシュを自前で更新する
                    var fieldCache = GetFieldCache(option.label + index, FloatFieldType.F3);
                    fieldCache.UpdateValue(value[index]);

                    DrawFloatField(new FloatFieldOption
                    {
                        value = value[index],
                        width = fieldWidth,
                        height = height,
                        fieldCache = fieldCache,
                        onChanged = newValue =>
                        {
                            value[index] = newValue;
                            NotifyChanged(option, value, index);
                        },
                    });
                }

                if (hasReset &&
                    DrawButton("R", Vector3ResetButtonWidth, height))
                {
                    option.onReset();
                }

                if (hasLink)
                {
                    if (option.linkIcon != null)
                    {
                        DrawToggle(option.linkIcon, option.linked,
                            Vector3ResetButtonWidth, height, option.onLinkChanged,
                            Vector3LinkIconOffset);
                    }
                    else
                    {
                        // アイコンが読み込めない環境向けのテキストフォールバック
                        DrawToggle("連", option.linked,
                            Vector3ResetButtonWidth, height, option.onLinkChanged);
                    }
                }
            }
            EndLayout();
        }

        private static void NotifyChanged(Vector3RowOption option, Vector3 value, int index)
        {
            if (option.onChangedAxis != null)
            {
                option.onChangedAxis(value, index);
            }
            else if (option.onChanged != null)
            {
                option.onChanged(value);
            }
        }

        public struct IntFieldOption
        {
            public string label;
            public float labelWidth;
            public int value;
            public int minValue;
            public int maxValue;
            public float width;
            public float height;
            public IntFieldCache fieldCache;
            public Action<int> onChanged;
            public Action onReset;
        }

        public bool DrawIntField(IntFieldOption option)
        {
            var fieldCache = option.fieldCache;
            if (fieldCache == null)
            {
                fieldCache = GetIntFieldCache(option.label);
                fieldCache.UpdateValue(option.value);
            }

            var updated = false;

            Action<string> onChanged = null;
            if (option.onChanged != null)
            {
                onChanged = newText =>
                {
                    fieldCache.text = newText;

                    int newValue;
                    if (int.TryParse(newText, out newValue))
                    {
                        if (option.minValue != 0 || option.maxValue != 0)
                        {
                            newValue = Mathf.Clamp(newValue, option.minValue, option.maxValue);
                        }
                        fieldCache.UpdateValue(newValue, false);
                        option.onChanged(newValue);
                        updated = true;
                    }
                };
            }

            if (option.onReset != null)
            {
                var subViewRect = GetDrawRect(option.width, option.height);

                BeginSubView(subViewRect, LayoutDirection.Horizontal);
                {
                    var fieldWidth = subViewRect.width - 20;

                    subView.DrawTextField(
                        option.label,
                        option.labelWidth,
                        fieldCache.text,
                        fieldWidth,
                        option.height,
                        onChanged);

                    if (subView.DrawButton("R", 20, 20))
                    {
                        option.onReset();
                        updated = true;
                    }
                }
                EndSubView();
            }
            else
            {
                DrawTextField(
                    option.label,
                    option.labelWidth,
                    fieldCache.text,
                    option.width,
                    option.height,
                    onChanged);
            }

            return updated;
        }

        public Color DrawColorFieldCache(
            string label,
            ColorFieldCache fieldCache,
            float width,
            float height)
        {
            DrawTextField(label, 0f, fieldCache.text, width, height, newText =>
            {
                fieldCache.text = newText;

                Color newColor;
                if (ColorUtility.TryParseHtmlString(newText, out newColor))
                {
                    fieldCache.UpdateColor(newColor, false);
                }
            });

            return fieldCache.color;
        }

        private float DrawSlider(
            string label,
            float value,
            float min,
            float max,
            float width,
            float height)
        {
            if (label != null)
            {
                var labelRect = GetDrawRect(labelWidth, height);
                GUI.Label(labelRect, label, gsLabel);
                currentPos.x += labelWidth + margin;
                width -= labelWidth + margin;
            }

            var drawRect = GetDrawRect(width, height);
            value = GUI.HorizontalSlider(drawRect, value, min, max);
            this.NextElement(drawRect);

            return value;
        }

        private float DrawSlider(
            float value,
            float min,
            float max,
            float width,
            float height)
        {
            return DrawSlider(null, value, min, max, width, height);
        }

        public void DrawBox(float width, float height)
        {
            var drawRect = GetDrawRect(width, height);
            GUI.Box(drawRect, GUIContent.none, gsBox);
            //NextElement(drawRect);
        }

        public static Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            // GUIStyle は UnityEngine.Object ではないため参照とみなされず、シーン遷移時の
            // Resources.UnloadUnusedAssets() で破棄されてしまう。DontSave で保護する
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        public void DrawTexture(
            Texture2D texture,
            float width,
            float height,
            Color color,
            EventType eventType,
            Action<Vector2> onClickAction)
        {
            var drawRect = GetDrawRect(width, height);
            BeginColor(color);
            GUI.DrawTexture(drawRect, texture);
            EndColor();
            NextElement(drawRect);

            if (onClickAction != null
                && drawRect.Contains(Event.current.mousePosition)
                && Event.current.type == eventType
                && Event.current.button == 0)
            {
                Vector2 pos = Event.current.mousePosition - new Vector2(drawRect.x, drawRect.y);
                onClickAction(pos);
            }
        }

        public void DrawTexture(Texture2D texture, float width, float height, Color color)
        {
            DrawTexture(texture, width, height, color, EventType.MouseDown, null);
        }

        public void DrawTexture(Texture2D texture, float width, float height)
        {
            DrawTexture(texture, width, height, Color.white, EventType.MouseDown, null);
        }

        public void DrawTexture(Texture2D texture)
        {
            DrawTexture(texture, texture.width, texture.height);
        }

        public void DrawTexture(Texture2D texture, Color color)
        {
            DrawTexture(texture, texture.width, texture.height, color);
        }

        public void DrawHorizontalLine(Color color)
        {
            DrawTexture(texWhite, -1, 1, color);
        }

        public void DrawHorizontalLine()
        {
            DrawHorizontalLine(Color.gray);
        }

        public void DrawRect(
            float width,
            float height,
            Color color,
            float borderSize)
        {
            var drawRect = GetDrawRect(width, height);
            DrawRectInternal(drawRect, color, borderSize);
            NextElement(drawRect);
        }

        public void DrawRectInternal(
            Rect drawRect,
            Color color,
            float borderSize)
        {
            BeginColor(color);

            // 上
            GUI.DrawTexture(new Rect(drawRect.x, drawRect.y, drawRect.width, borderSize), texWhite);
            // 下
            GUI.DrawTexture(new Rect(drawRect.x, drawRect.y + drawRect.height - borderSize, drawRect.width, borderSize), texWhite);
            // 左
            GUI.DrawTexture(new Rect(drawRect.x, drawRect.y, borderSize, drawRect.height), texWhite);
            // 右
            GUI.DrawTexture(new Rect(drawRect.x + drawRect.width - borderSize, drawRect.y, borderSize, drawRect.height), texWhite);

            EndColor();
        }

        public void InvokeActionOnEvent(
            float width,
            float height,
            EventType eventType,
            Action<Vector2> onClickAction)
        {
            var drawRect = GetDrawRect(width, height);

            if (onClickAction != null
                && drawRect.Contains(Event.current.mousePosition)
                && Event.current.type == eventType
                && Event.current.button == 0)
            {
                Vector2 pos = Event.current.mousePosition - new Vector2(drawRect.x, drawRect.y);
                onClickAction(pos);
            }
        }

        public void InvokeActionOnMouse(
            float width,
            float height,
            Action<Vector2> onAction)
        {
            var drawRect = GetDrawRect(width, height);

            if (onAction != null
                && drawRect.Contains(Event.current.mousePosition)
                && Event.current.button == 0)
            {
                Vector2 pos = Event.current.mousePosition - new Vector2(drawRect.x, drawRect.y);
                onAction(pos);
            }
        }

        public bool IsMouseOverRect(float width, float height)
        {
            var drawRect = GetDrawRect(width, height);
            return drawRect.Contains(Event.current.mousePosition);
        }

        /// <summary>
        /// ビュー領域内で右クリックされたらイベントを消費して true を返す。
        /// スクロールビュー内は座標系がずれるため、BeginScrollView の外から呼ぶこと
        /// </summary>
        public bool ConsumeRightClickInView()
        {
            var e = Event.current;
            if (!guiEnabled || e.type != EventType.MouseDown || e.button != 1 || !_viewRect.Contains(e.mousePosition))
            {
                return false;
            }

            e.Use();
            return true;
        }

        public void SetFocusComboBox(GUIComboBoxBase comboBox)
        {
            focusedComboBox = comboBox;
        }

        public void CancelFocusComboBox()
        {
            focusedComboBox = null;
        }

        public int DrawListView<T>(
            List<T> items,
            Func<T, int, string> getName,
            Func<T, int, bool> getEnabled,
            float width,
            float height,
            int currentIndex,
            float buttonHeight,
            GUIStyle style = null)
        {
            int selectedIndex = -1;
            var contentHeight = (buttonHeight + margin) * items.Count;
            var contentRect = GetDrawRect(0, 0, width, height);
            contentRect.width -= 20; // スクロールバーの幅分狭める
            contentRect.height = contentHeight;
            BeginScrollView(
                width,
                height,
                contentRect,
                false,
                false);

            var buttonWidth = contentRect.width;

            BeginLayout(LayoutDirection.Vertical);

            for (int i = 0; i < items.Count; i++)
            {
                var color = i == currentIndex ? option.accentColor : Color.white;
                var name = getName(items[i], i);
                var enabled = getEnabled != null ? getEnabled(items[i], i) : true;
                if (DrawButton(name, buttonWidth, buttonHeight, enabled, color, style))
                {
                    selectedIndex = i;
                    break;
                }
            }

            EndLayout();

            EndScrollView();
            return selectedIndex;
        }

        public void DrawContentListView<T>(
            List<T> items,
            Action<GUIView, T, int> drawContent,
            float width,
            float height,
            float itemHeight)
        {
            var contentHeight = (itemHeight + margin) * items.Count + 20;
            var contentRect = GetDrawRect(0, 0, width, height);
            contentRect.width -= 20; // スクロールバーの幅分狭める
            contentRect.height = contentHeight;
            BeginScrollView(
                width,
                height,
                contentRect,
                false,
                true);

            var itemWidth = contentRect.width;

            BeginLayout(LayoutDirection.Vertical);

            var itemRect = new Rect(0, 0, itemWidth, itemHeight);
            BeginSubView(itemRect, LayoutDirection.Vertical);
            subView.scrollViewRect = scrollViewRect;
            subView.scrollPosition = scrollPosition;

            for (int i = 0; i < items.Count; i++)
            {
                var drawRect = GetDrawRect(itemWidth, itemHeight);
                subView.Init(drawRect);

                var item = items[i];
                drawContent(subView, item, i);

                NextElement(drawRect);
            }

            EndSubView();
            EndLayout();
            EndScrollView();
        }

        public void DrawTileThumb(
            Texture2D thumb,
            float x,
            float y,
            float width,
            float height)
        {
            var drawRect = GetDrawRect(currentPos.x + x, currentPos.y + y, width, height);
            DrawTileThumb(thumb, drawRect);
        }

        public void DrawTileThumb(
            Texture2D thumb,
            Rect drawRect)
        {
            if (thumb == null)
            {
                return;
            }

            float aspect = (float)thumb.width / thumb.height;

            float thmbWidth = drawRect.width;
            float thmbHeight = thmbWidth / aspect;

            if (thmbHeight > drawRect.height) {
                thmbHeight = drawRect.height;
                thmbWidth = thmbHeight * aspect;
            }

            float thumbX = drawRect.x + (drawRect.width - thmbWidth) / 2;
            float thumbY = drawRect.y + (drawRect.height - thmbHeight) / 2;

            var imageRect = new Rect(thumbX, thumbY, thmbWidth, thmbHeight);

            if (!GUI.enabled) BeginColor(new Color(1f, 1f, 1f, 0.5f));
            GUI.DrawTexture(imageRect, thumb);
            if (!GUI.enabled) EndColor();
        }

        private static GUIContent _tempContent = null;

        public static Vector2 CalcSize(GUIStyle style, string text)
        {
            if (_tempContent == null)
            {
                _tempContent = new GUIContent();
            }

            _tempContent.text = text;
            return style.CalcSize(_tempContent);
        }

        public static float CalcWidth(GUIStyle style, string text)
        {
            if (_tempContent == null)
            {
                _tempContent = new GUIContent();
            }

            _tempContent.text = text;
            return style.CalcSize(_tempContent).x;
        }

        public static float CalcHeight(GUIStyle style, string text, float width)
        {
            if (_tempContent == null)
            {
                _tempContent = new GUIContent();
            }

            _tempContent.text = text;
            return style.CalcHeight(_tempContent, width);
        }

        private static Dictionary<string, Vector2> _tagSizeCache = new Dictionary<string, Vector2>();

        private static Vector2 CalcTagSize(string text)
        {
            Vector2 size;
            if (!_tagSizeCache.TryGetValue(text, out size))
            {
                size = CalcSize(gsTagLabel, text);
                _tagSizeCache[text] = size;
            }
            return size;
        }

        public bool DrawTile(
            ITileViewContent content,
            float width,
            float height,
            Action<ITileViewContent> onMouseOver,
            Action<ITileViewContent> onDeleted)
        {
            var drawRect = GetDrawRect(width, height);

            if (IsOutOfScrollView(drawRect))
            {
                NextElement(drawRect);
                return false;
            }

            var deleteButtonRect = new Rect(drawRect.x, drawRect.y, 20, 20);
            var favoriteButtonRect = new Rect(drawRect.x, drawRect.y, 20, 20);

            if (onDeleted != null && content.canDelete)
            {
                favoriteButtonRect.x += 20;
            }

            if (onDeleted != null && content.canDelete &&
                deleteButtonRect.Contains(Event.current.mousePosition))
            {
                BeginEnabled(false);
            }

            if (content.canFavorite &&
                favoriteButtonRect.Contains(Event.current.mousePosition))
            {
                BeginEnabled(false);
            }

            bool isClicked = GUI.Button(drawRect, "", gsTile);

            EndEnabled();

            DrawTileThumb(content.thum, 0, 0, drawRect.width, drawRect.height - 20);

            if (!string.IsNullOrEmpty(content.name))
            {
                if (content.nameHeight < 0f)
                {
                    content.nameHeight = CalcHeight(gsTileLabel, content.name, drawRect.width);
                }
                var labelRect = new Rect(drawRect.x, drawRect.y + drawRect.height - content.nameHeight, drawRect.width, content.nameHeight);
                GUI.Label(labelRect, content.name, gsTileLabel);
            }

            if (!string.IsNullOrEmpty(content.tag))
            {
                var tagSize = CalcTagSize(content.tag);
                var tagRect = new Rect(drawRect.x + drawRect.width - tagSize.x, drawRect.y, tagSize.x, tagSize.y);

                var savedColor = GUI.color;
                GUI.color = content.tagColor;
                GUI.Box(tagRect, "", gsTagBackground);
                GUI.color = savedColor;

                GUI.Label(tagRect, content.tag, gsTagLabel);
            }

            if (content.isSelected)
            {
                DrawRectInternal(drawRect, option.accentColor, 2);
            }

            bool isMouseOver = drawRect.Contains(Event.current.mousePosition);

            if (onMouseOver != null && isMouseOver)
            {
                onMouseOver.Invoke(content);
            }

            if (onDeleted != null && content.canDelete)
            {
                if (GUI.Button(deleteButtonRect, "x", gsButton))
                {
                    onDeleted.Invoke(content);
                }
            }

            if (content.canFavorite)
            {
                var favoriteTexture = content.isFavorite ? option.favoriteOnIcon : option.favoriteOffIcon;
                if (isMouseOver || content.isFavorite)
                {
                    if (GUI.Button(favoriteButtonRect, "", gsButton))
                    {
                        content.isFavorite = !content.isFavorite;
                    }

                    DrawTileThumb(favoriteTexture, favoriteButtonRect);
                }
            }

            NextElement(drawRect);
            return isClicked;
        }

        public bool DrawTileDir(
            ITileViewContent content,
            float width,
            float height,
            Action<ITileViewContent> onMouseOver,
            Action<ITileViewContent> onDeleted)
        {
            var drawRect = GetDrawRect(width, height);

            if (IsOutOfScrollView(drawRect))
            {
                NextElement(drawRect);
                return false;
            }

            var deleteButtonRect = new Rect(drawRect.x, drawRect.y, 20, 20);
            var favoriteButtonRect = new Rect(drawRect.x, drawRect.y, 20, 20);

            if (onDeleted != null && content.canDelete)
            {
                favoriteButtonRect.x += 20;
            }

            if (onDeleted != null && content.canDelete &&
                deleteButtonRect.Contains(Event.current.mousePosition))
            {
                BeginEnabled(false);
            }

            if (content.canFavorite &&
                favoriteButtonRect.Contains(Event.current.mousePosition))
            {
                BeginEnabled(false);
            }

            bool isClicked = GUI.Button(drawRect, "", gsTile);

            EndEnabled();

            var thumbWidth = drawRect.width / 2;
            var thumbHeight = (drawRect.height - 20) / 2;

            var children = content.children;
            for (int i = 0; i < children.Count; i++)
            {
                if (i >= 4)
                {
                    break;
                }

                var child = children[i];
                DrawTileThumb(
                    child.thum,
                    (i % 2) * thumbWidth,
                    (i / 2) * thumbHeight,
                    thumbWidth,
                    thumbHeight);
            }

            if (!string.IsNullOrEmpty(content.name))
            {
                float labelHeight = gsTileLabel.CalcHeight(new GUIContent(content.name), drawRect.width);
                var labelRect = new Rect(drawRect.x, drawRect.y + drawRect.height - labelHeight, drawRect.width, labelHeight);
                GUI.Label(labelRect, content.name, gsTileLabel);
            }

            if (!string.IsNullOrEmpty(content.tag))
            {
                var tagSize = gsTagLabel.CalcSize(new GUIContent(content.tag));
                var tagRect = new Rect(drawRect.x + drawRect.width - tagSize.x, drawRect.y, tagSize.x, tagSize.y);

                var savedColor = GUI.color;
                GUI.color = content.tagColor;
                GUI.Box(tagRect, "", gsTagBackground);
                GUI.color = savedColor;

                GUI.Label(tagRect, content.tag, gsTagLabel);
            }

            bool isMouseOver = drawRect.Contains(Event.current.mousePosition);

            if (onMouseOver != null && isMouseOver)
            {
                onMouseOver.Invoke(content);
            }

            if (onDeleted != null && content.canDelete)
            {
                if (GUI.Button(deleteButtonRect, "x", gsButton))
                {
                    onDeleted.Invoke(content);
                }
            }

            if (content.canFavorite)
            {
                var favoriteTexture = content.isFavorite ? option.favoriteOnIcon : option.favoriteOffIcon;
                if (isMouseOver || content.isFavorite)
                {
                    if (GUI.Button(favoriteButtonRect, "", gsButton))
                    {
                        content.isFavorite = !content.isFavorite;
                    }

                    DrawTileThumb(favoriteTexture, favoriteButtonRect);
                }
            }

            NextElement(drawRect);
            return isClicked;
        }

        public void DrawTileViewContent(
            ITileViewContent content,
            float tileWidth,
            float tileHeight,
            Action<ITileViewContent> onSelected,
            Action<ITileViewContent> onMouseOver,
            Action<ITileViewContent> onDeleted)
        {
            if (currentPos.x + tileWidth > viewRect.width)
            {
                EndLayout();
                BeginLayout(LayoutDirection.Horizontal);
            }

            if (content.isDir)
            {
                if (DrawTileDir(content, tileWidth, tileHeight, onMouseOver, onDeleted))
                {
                    onSelected(content);
                }
            }
            else
            {
                if (DrawTile(content, tileWidth, tileHeight, onMouseOver, onDeleted))
                {
                    onSelected(content);
                }
            }
        }

        public void DrawTileView(
            ITileViewContent content,
            float width,
            float height,
            float tileWidth,
            float tileHeight,
            Action<ITileViewContent> onSelected,
            Action<ITileViewContent> onMouseOver = null,
            Action<ITileViewContent> onDeleted = null)
        {
            BeginScrollView(
                width,
                height,
                AutoScrollViewRect,
                false,
                true);

            BeginLayout(LayoutDirection.Horizontal);

            foreach (var child in content.children)
            {
                DrawTileViewContent(child, tileWidth, tileHeight, onSelected, onMouseOver, onDeleted);
            }

            EndLayout();
            EndScrollView();
        }

        public bool DrawFloatSelect(
            string label,
            float step1,
            float step2,
            Action onReset,
            float value,
            Action<float> onChanged,
            Action<float> onDiffChanged)
        {
            return DrawValueSelect(label, FloatFieldType.Float, step1, step2, onReset, value, onChanged, onDiffChanged);
        }

        public bool DrawIntSelect(
            string label,
            int step1,
            int step2,
            Action onReset,
            int value,
            Action<int> onChanged,
            Action<int> onDiffChanged)
        {
            return DrawValueSelect(
                label,
                FloatFieldType.Int,
                step1,
                step2,
                onReset,
                value,
                v => onChanged((int)v), 
                v => onDiffChanged((int)v)
            );
        }

        public bool DrawValueSelect(
            string label,
            FloatFieldType fieldType,
            float step1,
            float step2,
            Action onReset,
            float value,
            Action<float> onChanged,
            Action<float> onDiffChanged)
        {
            var fieldCache = GetFieldCache(label, fieldType);
            fieldCache.UpdateValue(value);

            var newValue = value;
            var diffValue = 0f;
            var updated = false;

            var subViewRect = GetDrawRect(220, 20);
            var subView = new GUIView(subViewRect)
            {
                parent = this,
                margin = 0,
                padding = Vector2.zero
            };

            subView.BeginLayout(LayoutDirection.Horizontal);
            {
                if (!string.IsNullOrEmpty(label))
                {
                    subView.DrawLabel(label, 50, 20);
                }

                if (step2 != 0f && subView.DrawRepeatButton("<<", 25, 20))
                {
                    diffValue = -step2;
                }
                if (subView.DrawRepeatButton("<", 20, 20))
                {
                    diffValue = -step1;
                }

                subView.DrawFloatField(new FloatFieldOption
                {
                    value = value,
                    width = 50,
                    height = 20,
                    fieldCache = fieldCache,
                    onChanged = x => newValue = x,
                });

                if (subView.DrawRepeatButton(">", 20, 20))
                {
                    diffValue = step1;
                }
                if (step2 != 0f && subView.DrawRepeatButton(">>", 25, 20))
                {
                    diffValue = step2;
                }

                subView.AddSpace(5);

                if (onReset != null && subView.DrawButton("R", 20, 20))
                {
                    onReset();
                    updated = true;
                }
            }
            subView.EndLayout();

            NextElement(subViewRect);

            if (!float.IsNaN(newValue) && newValue != value)
            {
                onChanged(newValue);
                updated = true;
            }
            if (diffValue != 0f)
            {
                onDiffChanged(diffValue);
                updated = true;
            }

            return updated;
        }

        public struct SliderOption
        {
            public string label;
            public float labelWidth;
            public float width;
            public FloatFieldType fieldType;
            public float min;
            public float max;
            public float step;
            public float defaultValue;
            public float value;
            public bool hiddenResetButton;
            public Action<float> onChanged;
        }

        public bool DrawSliderValue(SliderOption option)
        {
            var fieldCache = GetFieldCache(option.label, option.fieldType);
            fieldCache.UpdateValue(option.value);

            var newValue = option.value;
            var updated = false;
            var width = option.width == 0f ? 250f : option.width;

            var subViewRect = GetDrawRect(width, 20f);
            width = subViewRect.width;
            
            BeginSubView(subViewRect, LayoutDirection.Horizontal);
            {
                var sliderWidth = width - 80f;

                if (option.hiddenResetButton)
                {
                    sliderWidth += 25f;
                }

                var label = fieldCache.label;
                if (!string.IsNullOrEmpty(label))
                {
                    subView.DrawLabel(label, option.labelWidth, 20);
                    sliderWidth -= option.labelWidth;
                }

                subView.DrawFloatField(new FloatFieldOption
                {
                    value = option.value,
                    minValue = option.min,
                    maxValue = option.max,
                    width = 50,
                    height = 20,
                    fieldCache = fieldCache,
                    onChanged = x => newValue = x,
                });

                if (option.step > 0f)
                {
                    if (subView.DrawRepeatButton("<", 20, 20))
                    {
                        newValue -= option.step;
                    }
                    if (subView.DrawRepeatButton(">", 20, 20))
                    {
                        newValue += option.step;
                    }
                    sliderWidth -= 40;
                }

                subView.AddSpace(5);

                newValue = subView.DrawSlider(newValue, option.min, option.max, sliderWidth, 20);

                if (!option.hiddenResetButton)
                {
                    subView.AddSpace(5);

                    if (subView.DrawButton("R", 20, 20))
                    {
                        newValue = option.defaultValue;
                    }
                }
            }
            EndSubView();

            if (!float.IsNaN(newValue) && newValue != option.value)
            {
                option.onChanged(newValue);
                updated = true;
            }

            return updated;
        }

        /// <summary>
        /// 色設定を一行で描画する。
        /// 「編集」ボタンで ColorPickerWindow を開き、そちらで詳細な編集を行う
        /// </summary>
        public bool DrawColor(
            ColorFieldCache fieldCache,
            Color color,
            Color resetColor,
            Action<Color> onColorChanged)
        {
            fieldCache.UpdateColor(color, true);
            fieldCache.UpdateDefaultColor(resetColor);

            var label = fieldCache.label;
            var picker = ColorPickerWindow.instance;
            var isEditing = picker.IsEditing(label);

            BeginLayout(LayoutDirection.Horizontal);
            {
                if (label != null)
                {
                    DrawLabel(label, 90, 20);
                }

                DrawTexture(texWhite, 20, 20, color);

                DrawColorFieldCache(null, fieldCache, 100, 20);

                if (DrawButton("R", 20, 20))
                {
                    fieldCache.ResetColor();
                }

                // 編集ウィンドウをボタンに被らない位置へ出すため、描画前に矩形を控えておく
                var buttonRect = GetDrawRect(45, 20);

                if (DrawButton("編集", 45, 20, true, isEditing ? option.accentColor : (Color?)null))
                {
                    if (isEditing)
                    {
                        picker.Close();
                    }
                    else
                    {
                        var screenPos = GUIUtility.GUIToScreenPoint(buttonRect.position);
                        var anchorRect = new Rect(screenPos.x, screenPos.y, buttonRect.width, buttonRect.height);

                        picker.Open(
                            label,
                            fieldCache.color,
                            resetColor,
                            fieldCache.hasAlpha,
                            onColorChanged,
                            anchorRect);
                    }
                }
            }
            EndLayout();

            var updated = false;
            if (fieldCache.color != color)
            {
                onColorChanged(fieldCache.color);
                updated = true;
            }

            // 編集ウィンドウへ最新の状態を渡す。渡されなくなったら向こう側で自動的に閉じる
            picker.Sync(label, fieldCache.color, resetColor, fieldCache.hasAlpha, onColorChanged);

            return updated;
        }

        /// <summary>
        /// カーブ設定を一行で描画する。
        /// 「編集」ボタンで CurveEditorWindow を開き、そちらでキー操作による編集を行う
        /// </summary>
        public void DrawCurve(
            string label,
            CurveData curve,
            Color curveColor,
            Action onChanged)
        {
            var editor = CurveEditorWindow.instance;
            var isEditing = editor.IsEditing(label);

            BeginLayout(LayoutDirection.Horizontal);
            {
                if (label != null)
                {
                    DrawLabel(label, 90, 20);
                }

                DrawTexture(curve.GetPreviewTexture(64, 20), 64, 20);

                if (DrawButton("R", 20, 20))
                {
                    curve.CopyFrom(CurveData.Linear());
                    onChanged?.Invoke();
                }

                // 編集ウィンドウをボタンに被らない位置へ出すため、描画前に矩形を控えておく
                var buttonRect = GetDrawRect(45, 20);

                if (DrawButton("編集", 45, 20, true, isEditing ? option.accentColor : (Color?)null))
                {
                    if (isEditing)
                    {
                        editor.Close();
                    }
                    else
                    {
                        var screenPos = GUIUtility.GUIToScreenPoint(buttonRect.position);
                        var anchorRect = new Rect(screenPos.x, screenPos.y, buttonRect.width, buttonRect.height);

                        editor.Open(label, curve, curveColor, onChanged, anchorRect);
                    }
                }
            }
            EndLayout();

            // 編集ウィンドウへ最新の状態を渡す。渡されなくなったら向こう側で自動的に閉じる
            editor.Sync(label, curve, curveColor, onChanged);
        }

        public T DrawTabs<T>(
            T currentTab,
            float width,
            float height,
            float tabMargin = 0f)
        {
            var tabTypes = Enum.GetValues(typeof(T));

            var maxWidth = viewRect.width - currentPos.x - padding.x;
            var subViewWidth = Mathf.Min((width + tabMargin) * tabTypes.Length, maxWidth);
            var rows = Mathf.CeilToInt((width + tabMargin) * tabTypes.Length / maxWidth);
            var subViewHeight = height * rows;
            var subViewRect = GetDrawRect(subViewWidth, subViewHeight);

            BeginSubView(subViewRect, LayoutDirection.Horizontal);
            {
                subView.margin = tabMargin;
                foreach (T tabType in tabTypes)
                {
                    if (subView.currentPos.x + width > subView.viewRect.width)
                    {
                        subView.EndLayout();
                        subView.BeginLayout(LayoutDirection.Horizontal);
                    }

                    var color = currentTab.Equals(tabType) ? option.accentColor : Color.white;
                    if (subView.DrawButton(tabType.ToString(), width, height, true, color))
                    {
                        currentTab = tabType;
                    }
                }
            }
            EndSubView();

            AddSpace(5);

            return currentTab;
        }

        public void AddSpace(float width, float height)
        {
            var drawRect = GetDrawRect(width, height);
            NextElement(drawRect);
        }

        public void AddSpace(float size)
        {
            AddSpace(size, size);
        }

        public FloatFieldCache GetFieldCache(
            string label,
            FloatFieldType fieldType = FloatFieldType.Float)
        {
            if (parent != null)
            {
                return parent.GetFieldCache(label, fieldType);
            }

            FloatFieldCache fieldCache;
            if (_fieldCacheIndex >= _fieldCaches.Count)
            {
                fieldCache = new FloatFieldCache();
                _fieldCaches.Add(fieldCache);
                MTEUtils.LogDebug("Add FieldCache: " + label);
            }

            fieldCache = _fieldCaches[_fieldCacheIndex++];
            fieldCache.label = label;
            fieldCache.fieldType = fieldType;
            return fieldCache;
        }

        public FloatFieldCache GetFieldCache(string label, float value)
        {
            var fieldCache = GetFieldCache(label);
            fieldCache.UpdateValue(value);
            return fieldCache;
        }

        public IntFieldCache GetIntFieldCache(string label)
        {
            if (parent != null)
            {
                return parent.GetIntFieldCache(label);
            }

            IntFieldCache fieldCache;
            if (_intFieldCacheIndex >= _intFieldCaches.Count)
            {
                fieldCache = new IntFieldCache();
                _intFieldCaches.Add(fieldCache);
                MTEUtils.LogDebug("Add IntFieldCache: " + label);
            }

            fieldCache = _intFieldCaches[_intFieldCacheIndex++];
            fieldCache.label = label;
            return fieldCache;
        }

        public FloatFieldCache[] GetFieldCaches(string[] label)
        {
            var fieldCaches = new FloatFieldCache[label.Length];
            for (var i = 0; i < label.Length; i++)
            {
                fieldCaches[i] = GetFieldCache(label[i]);
            }
            return fieldCaches;
        }

        public TransformCache GetTransformCache(Transform transform = null)
        {
            if (parent != null)
            {
                return parent.GetTransformCache(transform);
            }

            if (_transformCacheIndex < _transformCaches.Count)
            {
                var cache = _transformCaches[_transformCacheIndex++];
                cache.Update(transform);
                return cache;
            }

            {
                var cache = new TransformCache();
                cache.Update(transform);
                _transformCaches.Add(cache);
                _transformCacheIndex++;
                MTEUtils.LogDebug("Add TransformCache: " + transform);
                return cache;
            }
        }

        public ColorFieldCache GetColorFieldCache(
            string label,
            bool hasAlpha)
        {
            if (parent != null)
            {
                return parent.GetColorFieldCache(label, hasAlpha);
            }

            ColorFieldCache fieldCache;
            if (_colorFieldCacheIndex >= _colorFieldCaches.Count)
            {
                fieldCache = new ColorFieldCache();
                _colorFieldCaches.Add(fieldCache);
                MTEUtils.LogDebug("Add ColorFieldCache: " + label);
            }

            fieldCache = _colorFieldCaches[_colorFieldCacheIndex++];
            fieldCache.label = label;
            fieldCache.hasAlpha = hasAlpha;
            return fieldCache;
        }
    }
}