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

        private List<FloatFieldCache> _fieldCaches = new List<FloatFieldCache>();
        private int _fieldCacheIndex = 0;

        private List<IntFieldCache> _intFieldCaches = new List<IntFieldCache>();
        private int _intFieldCacheIndex = 0;

        private List<TransformCache> _transformCaches = new List<TransformCache>();
        private int _transformCacheIndex = 0;

        private static GUIStyle _gsWin = null;
        public static GUIStyle gsWin
        {
            get
            {
                if (_gsWin == null)
                {
                    _gsWin = new GUIStyle("box")
                    {
                        fontSize = 12,
                        alignment = TextAnchor.UpperLeft,
                    };

                    var windowHoverColor = option.windowHoverColor;
                    var hoverTex = GUIView.CreateColorTexture(windowHoverColor);

                    _gsWin.onHover.background = hoverTex;
                    _gsWin.hover.background = hoverTex;
                    _gsWin.onFocused.background = hoverTex;
                    _gsWin.focused.background = hoverTex;

                    _gsWin.onHover.textColor = Color.white;
                    _gsWin.hover.textColor = Color.white;
                    _gsWin.onFocused.textColor = Color.white;
                    _gsWin.focused.textColor = Color.white;
                }
                return _gsWin;
            }
        }
        public static GUIStyle gsLabel = new GUIStyle("label")
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
        };
        public static GUIStyle gsLabelRight = new GUIStyle("label")
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleRight,
            wordWrap = false,
        };
        public static GUIStyle gsButton = new GUIStyle("button")
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter
        };
        public static GUIStyle gsSelectedButton = new GUIStyle("box")
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
        };
        public static GUIStyle gsToggle = new GUIStyle("toggle")
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
        };
        public static GUIStyle gsTextField = new GUIStyle("textField")
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        public static GUIStyle gsTextArea = new GUIStyle("textArea")
        {
            fontSize = 12,
            alignment = TextAnchor.UpperLeft,
        };
        public static GUIStyle gsTile = new GUIStyle("button")
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
        public static GUIStyle gsTileLabel = new GUIStyle("button")
        {
            fontSize = 12,
            alignment = TextAnchor.LowerCenter,
            wordWrap = true,
            normal = {
                background = CreateColorTexture(new Color(0, 0, 0, 0.5f))
            },
        };
        public static GUIStyle gsTagLabel = new GUIStyle("box")
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            normal = {
                background = CreateColorTexture(new Color(0, 0, 0, 0))
            },
        };
        public static GUIStyle gsTagBackground = new GUIStyle("box")
        {
            normal = {
                background = CreateColorTexture(Color.white)
            },
        };
        public static GUIStyle gsMask = new GUIStyle("box")
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            normal = {
                background = CreateColorTexture(new Color(0, 0, 0, 0.5f))
            }
        };
        public static GUIStyle gsBox = new GUIStyle("box")
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter
        };

        public static Vector2 defaultPadding = new Vector2(10, 10);
        public static float defaultMargin = 5;
        public static Texture2D texDummy = new Texture2D(1, 1);
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
            BeginEnabled(enabled);
            if (color != null) BeginColor(color.Value);
            var drawRect = GetDrawRect(width, height);
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
        
        public class DraggableInfo
        {
            public bool isDragging;
            public Vector3 lastMousePosition;
            public Vector2 value;
        }

        public void DrawDraggableButton(
            string text,
            float width,
            float height,
            DraggableInfo info,
            Vector2 value,
            Action<Vector2, Vector2> onAction)
        {
            var drawRect = GetDrawRect(width, height);

            InvokeActionOnDragging(drawRect, info, value, onAction);

            GUI.Button(drawRect, text, gsButton);
            NextElement(drawRect);
        }

        public void InvokeActionOnDragging(
            float width,
            float height,
            DraggableInfo info,
            Vector2 value,
            Action<Vector2, Vector2> onAction)
        {
            var drawRect = GetDrawRect(width, height);
            InvokeActionOnDragging(drawRect, info, value, onAction);    
        }

        public void InvokeActionOnDragging(
            Rect drawRect,
            DraggableInfo info,
            Vector2 value,
            Action<Vector2, Vector2> onAction)
        {
            if (Event.current.type == EventType.MouseDown &&
                drawRect.Contains(Event.current.mousePosition) && 
                Event.current.button == 0)
            {
                info.isDragging = true;
                info.lastMousePosition = Input.mousePosition;
                info.value = value;
            }

            if (info.isDragging && !Input.GetMouseButton(0))
            {
                info.isDragging = false;
            }

            if (info.isDragging)
            {
                var diff = Input.mousePosition - info.lastMousePosition;
                diff.y = -diff.y;
                if (diff.sqrMagnitude > 0)
                {
                    info.value += new Vector2(diff.x, diff.y);
                    onAction?.Invoke(diff, info.value);
                    info.lastMousePosition = Input.mousePosition;
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
            BeginColor(value ? Color.green : Color.white);
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

        public void DrawLabel(
            string text,
            float width,
            float height,
            Color? textColor = null,
            GUIStyle style = null,
            Action onClickAction = null)
        {
            var drawRect = GetDrawRect(width, height);
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

        public bool DrawTextField(
            string label,
            float labelWidth,
            string text,
            float width,
            float height,
            Action<string> onChanged,
            bool hasNewLine)
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

        public bool DrawTextField(
            string label,
            float labelWidth,
            string text,
            float width,
            float height,
            Action<string> onChanged)
        {
            return DrawTextField(label, labelWidth, text, width, height, onChanged, false);
        }

        public void DrawTextField(
            string text,
            float width,
            float height,
            Action<string> onChanged)
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
            public bool hiddenButton;
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

        public bool DrawTextField(TextFieldOption option)
        {
            var height = option.maxLines > 1 ? 20 * option.maxLines : 20;
            var subViewRect = GetDrawRect(option.width > 0 ? option.width : -1, height);
            var updated = false;

            BeginSubView(subViewRect, LayoutDirection.Horizontal);
            {
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
                    }
                }
            }
            EndSubView();

            return updated;
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

        public void SetFocusComboBox(GUIComboBoxBase comboBox)
        {
            focusedComboBox = comboBox;
        }

        public bool IsComboBoxFocused()
        {
            return focusedComboBox != null;
        }

        public void CancelFocusComboBox()
        {
            focusedComboBox = null;
        }

        private GUIView _comboBoxSubView = null;

        public void DrawComboBox()
        {
            SetEnabled(true);

            if (focusedComboBox != null)
            {
                if (_comboBoxSubView == null)
                {
                    _comboBoxSubView = new GUIView()
                    {
                        padding = Vector2.zero,
                        margin = 0,
                    };
                }

                _comboBoxSubView.parent = this;
                _comboBoxSubView.Init(_viewRect);

                focusedComboBox.DrawContent(_comboBoxSubView);
            }
        }

        public int DrawListView<T>(
            List<T> items,
            Func<T, int, string> getName,
            Func<T, int, bool> getEnabled,
            float width,
            float height,
            int currentIndex,
            float buttonHeight)
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
                var color = i == currentIndex ? Color.green : Color.white;
                var name = getName(items[i], i);
                var enabled = getEnabled != null ? getEnabled(items[i], i) : true;
                if (DrawButton(name, buttonWidth, buttonHeight, enabled, color))
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

        private static Vector2 CalcSize(GUIStyle style, string text)
        {
            if (_tempContent == null)
            {
                _tempContent = new GUIContent();
            }

            _tempContent.text = text;
            return style.CalcSize(_tempContent);
        }

        private static float CalcHeight(GUIStyle style, string text, float width)
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

            if (drawRect.position.y + drawRect.height < scrollPosition.y ||
                drawRect.position.y > scrollPosition.y + scrollViewRect.height)
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
                DrawRectInternal(drawRect, Color.green, 2);
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

            if (drawRect.position.y + drawRect.height < scrollPosition.y ||
                drawRect.position.y > scrollPosition.y + scrollViewRect.height)
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

                subView.AddSpace(5);

                if (subView.DrawButton("R", 20, 20))
                {
                    newValue = option.defaultValue;
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

        public bool DrawColor(
            ColorFieldCache fieldCache,
            Color color,
            Color resetColor,
            Action<Color> onColorChanged)
        {
            fieldCache.UpdateColor(color, true);
            fieldCache.UpdateDefaultColor(resetColor);

            if (option.useHSVColor)
            {
                var newHSV = fieldCache.hsv;
                var defaultHSV = fieldCache.defaultHSV;

                DrawSliderValue(new SliderOption
                {
                    label = "H",
                    labelWidth = 30,
                    min = 0f,
                    max = 1f,
                    step = 0.01f,
                    defaultValue = defaultHSV.x,
                    value = newHSV.x,
                    onChanged = x => newHSV.x = x,
                });

                DrawSliderValue(new SliderOption
                {
                    label = "S",
                    labelWidth = 30,
                    min = 0f,
                    max = 1f,
                    step = 0.01f,
                    defaultValue = defaultHSV.y,
                    value = newHSV.y,
                    onChanged = y => newHSV.y = y,
                });

                DrawSliderValue(new SliderOption
                {
                    label = "V",
                    labelWidth = 30,
                    min = 0f,
                    max = 1f,
                    step = 0.01f,
                    defaultValue = defaultHSV.z,
                    value = newHSV.z,
                    onChanged = z => newHSV.z = z,
                });

                if (fieldCache.hasAlpha)
                {
                    DrawSliderValue(new SliderOption
                    {
                        label = "A",
                        labelWidth = 30,
                        min = 0f,
                        max = 1f,
                        step = 0.01f,
                        defaultValue = defaultHSV.w,
                        value = newHSV.w,
                        onChanged = z => newHSV.w = z,
                    });
                }

                fieldCache.UpdateHSV(newHSV, true);
            }
            else
            {
                var newColor = color;

                DrawSliderValue(new SliderOption
                {
                    label = "R",
                    labelWidth = 30,
                    min = 0f,
                    max = 1f,
                    step = 0.01f,
                    defaultValue = resetColor.r,
                    value = color.r,
                    onChanged = x => newColor.r = x,
                });

                DrawSliderValue(new SliderOption
                {
                    label = "G",
                    labelWidth = 30,
                    min = 0f,
                    max = 1f,
                    step = 0.01f,
                    defaultValue = resetColor.g,
                    value = color.g,
                    onChanged = y => newColor.g = y,
                });

                DrawSliderValue(new SliderOption
                {
                    label = "B",
                    labelWidth = 30,
                    min = 0f,
                    max = 1f,
                    step = 0.01f,
                    defaultValue = resetColor.b,
                    value = color.b,
                    onChanged = z => newColor.b = z,
                });

                if (fieldCache.hasAlpha)
                {
                    DrawSliderValue(new SliderOption
                    {
                        label = "A",
                        labelWidth = 30,
                        min = 0f,
                        max = 1f,
                        step = 0.01f,
                        defaultValue = resetColor.a,
                        value = color.a,
                        onChanged = z => newColor.a = z,
                    });
                }

                fieldCache.UpdateColor(newColor, true);
            }

            BeginLayout(LayoutDirection.Horizontal);
            {
                if (fieldCache.label != null)
                {
                    DrawLabel(fieldCache.label, 50, 20);
                }

                DrawTexture(texWhite, 20, 20, color);

                DrawColorFieldCache(null, fieldCache, 120, 20);

                if (DrawButton("R", 20, 20))
                {
                    fieldCache.ResetColor();
                }

                if (DrawTextureButton(option.changeIcon, 20, 20, 0))
                {
                    option.useHSVColor = !option.useHSVColor;
                }
            }
            EndLayout();

            var updated = false;
            if (fieldCache.color != color)
            {
                onColorChanged(fieldCache.color);
                updated = true;
            }

            return updated;
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

                    var color = currentTab.Equals(tabType) ? Color.green : Color.white;
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
            FloatFieldType fieldType)
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
            }

            fieldCache = _fieldCaches[_fieldCacheIndex++];
            fieldCache.label = label;
            fieldCache.fieldType = fieldType;
            return fieldCache;
        }

        public FloatFieldCache GetFieldCache(string label)
        {
            return GetFieldCache(label, FloatFieldType.Float);
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
                return cache;
            }
        }
    }
}