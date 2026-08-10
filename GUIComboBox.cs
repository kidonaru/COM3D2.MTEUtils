namespace COM3D2.MotionTimelineEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public abstract class GUIComboBoxBase
    {
        public string defaultName;
        public Texture2D defaultTexture;
        public int currentIndex = 0;
        public Vector2 buttonPos;
        public float labelWidth = 100;
        public Vector2 buttonSize = new Vector2(110, 20);
        public Vector2 contentSize = new Vector2(110, 300);
        public bool showArrow = true;

        public abstract int prevIndex { get; }
        public abstract int nextIndex { get; }

        public abstract void DrawButton(string label, GUIView view);

        public void DrawButton(GUIView view)
        {
            DrawButton("", view);
        }

        public abstract void DrawTextureButton(GUIView view);

        /// <summary>ポップアップウィンドウのサイズ。項目数が少なければ高さを詰める</summary>
        public abstract Vector2 GetPopupSize();

        /// <summary>ポップアップの中身を描く。選択が確定したら true を返す</summary>
        public abstract bool DrawPopupContent(GUIView view);
    }

    public class GUIComboBox<T> : GUIComboBoxBase
    {
        public List<T> items = new List<T>();
        public Func<T, int, string> getName;
        public Func<T, int, Texture2D> getTexture;
        public Func<T, int, bool> getEnabled;
        public Action<T, int> onSelected;

        public override int prevIndex
        {
            get
            {
                var prevIndex = currentIndex - 1;
                if (prevIndex < 0)
                {
                    prevIndex = items.Count - 1;
                }
                return prevIndex;
            }
        }

        public override int nextIndex
        {
            get
            {
                var nextIndex = currentIndex + 1;
                if (nextIndex >= items.Count)
                {
                    nextIndex = 0;
                }
                return nextIndex;
            }
        }

        public T currentItem
        {
            get
            {
                if (currentIndex >= 0 && currentIndex < items.Count)
                {
                    return items[currentIndex];
                }
                return default(T);
            }
            set
            {
                currentIndex = items.IndexOf(value);
            }
        }

        private GUIView _buttonSubView = new GUIView(Rect.zero)
        {
            margin = 0,
            padding = Vector2.zero,
        };

        public override void DrawButton(string label, GUIView view)
        {
            var name = this.defaultName;
            if (name == null)
            {
                if (currentIndex >= 0 && currentIndex < this.items.Count)
                {
                    name = this.getName(this.items[currentIndex], currentIndex);
                }
            }

            var subViewWidth = buttonSize.x;
            if (!string.IsNullOrEmpty(label))
            {
                subViewWidth += labelWidth;
            }
            if (showArrow)
            {
                subViewWidth += 40;
            }

            var subViewRect = view.GetDrawRect(subViewWidth, buttonSize.y);
            _buttonSubView.parent = view;
            _buttonSubView.Init(subViewRect);

            _buttonSubView.BeginHorizontal();
            {
                if (!string.IsNullOrEmpty(label))
                {
                    _buttonSubView.DrawLabel(label, labelWidth, buttonSize.y);
                }

                if (showArrow)
                {
                    // 候補が空だと prevIndex / nextIndex が範囲外になるため押せなくする
                    if (_buttonSubView.DrawButton("<", 20, 20, items.Count > 0))
                    {
                        this.currentIndex = this.prevIndex;
                        if (this.onSelected != null)
                        {
                            this.onSelected(this.items[this.currentIndex], this.currentIndex);
                        }
                    }
                }

                var buttonDrawRect = _buttonSubView.GetDrawRect(buttonSize.x, buttonSize.y);
                buttonPos = buttonDrawRect.position;

                // 入れ子のスクロールビュー内でもポップアップがボタン直下に出るよう、
                // 祖先まで含めたスクロール量で絶対座標に直す
                buttonPos += view.scrollOffset;

                if (_buttonSubView.DrawButton(name, buttonSize.x, buttonSize.y))
                {
                    view.SetFocusComboBox(this);
                }

                if (showArrow)
                {
                    if (_buttonSubView.DrawButton(">", 20, 20, items.Count > 0))
                    {
                        this.currentIndex = this.nextIndex;
                        if (this.onSelected != null)
                        {
                            this.onSelected(this.items[this.currentIndex], this.currentIndex);
                        }
                    }
                }
            }
            _buttonSubView.EndLayout();

            view.NextElement(subViewRect);
        }

        public override void DrawTextureButton(GUIView view)
        {
            var texture = this.defaultTexture;
            if (texture == null)
            {
                if (currentIndex >= 0 && currentIndex < this.items.Count)
                {
                    texture = this.getTexture(this.items[currentIndex], currentIndex);
                }
            }

            var subViewWidth = buttonSize.x;
            if (showArrow)
            {
                subViewWidth += 40;
            }

            var subViewRect = view.GetDrawRect(subViewWidth, buttonSize.y);
            _buttonSubView.parent = view;
            _buttonSubView.Init(subViewRect);

            _buttonSubView.BeginHorizontal();
            {
                if (showArrow)
                {
                    // 候補が空だと prevIndex / nextIndex が範囲外になるため押せなくする
                    if (_buttonSubView.DrawButton("<", 20, 20, items.Count > 0))
                    {
                        this.currentIndex = this.prevIndex;
                        if (this.onSelected != null)
                        {
                            this.onSelected(this.items[this.currentIndex], this.currentIndex);
                        }
                    }
                }

                var buttonDrawRect = _buttonSubView.GetDrawRect(buttonSize.x, buttonSize.y);
                buttonPos = buttonDrawRect.position;

                // 入れ子のスクロールビュー内でもポップアップがボタン直下に出るよう、
                // 祖先まで含めたスクロール量で絶対座標に直す
                buttonPos += view.scrollOffset;

                if (_buttonSubView.DrawTextureButton(texture, buttonSize.x, buttonSize.y))
                {
                    view.SetFocusComboBox(this);
                }

                if (showArrow)
                {
                    if (_buttonSubView.DrawButton(">", 20, 20, items.Count > 0))
                    {
                        this.currentIndex = this.nextIndex;
                        if (this.onSelected != null)
                        {
                            this.onSelected(this.items[this.currentIndex], this.currentIndex);
                        }
                    }
                }
            }
            _buttonSubView.EndLayout();

            view.NextElement(subViewRect);
        }

        public override Vector2 GetPopupSize()
        {
            var width = this.contentSize.x + 20; // スクロールバー分広げる
            var height = Mathf.Min(
                this.contentSize.y,
                this.items.Count * this.buttonSize.y);
            // 空リストでも枠が潰れないよう 1 行分は確保する
            height = Mathf.Max(height, this.buttonSize.y);
            return new Vector2(width, height);
        }

        public override bool DrawPopupContent(GUIView view)
        {
            var selectedIndex = view.DrawListView(
                this.items,
                this.getName,
                this.getEnabled,
                view.viewRect.width,
                view.viewRect.height,
                this.currentIndex,
                this.buttonSize.y);

            if (selectedIndex >= 0 && selectedIndex < this.items.Count)
            {
                this.currentIndex = selectedIndex;
                if (this.onSelected != null)
                {
                    this.onSelected(this.items[this.currentIndex], this.currentIndex);
                }
                return true;
            }
            return false;
        }
    }
}