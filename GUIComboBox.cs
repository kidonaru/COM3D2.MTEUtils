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

        /// <summary>前後送り矢印 1 個分のサイズ。矢印込みの幅を外側でレイアウトする際にも参照する</summary>
        public const float ARROW_SIZE = 20f;

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

        /// <summary>
        /// 描画したビューの値変更フック。ポップアップ側で選択が確定したとき (別ビュー) にも
        /// 同じフックを通せるよう、DrawButton のたびに控える。
        /// 最後に描いたビューのフックが残るため、1 つのインスタンスを
        /// フック設定の違う複数箇所から描かないこと
        /// </summary>
        private Action _onBeforeSelected;

        private void InvokeSelected()
        {
            if (this.onSelected == null)
            {
                return;
            }
            _onBeforeSelected?.Invoke();
            this.onSelected(this.items[this.currentIndex], this.currentIndex);
        }

        public override void DrawButton(string label, GUIView view)
        {
            _onBeforeSelected = view.onBeforeValueChanged;

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
                subViewWidth += ARROW_SIZE * 2;
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
                    if (_buttonSubView.DrawButton("<", ARROW_SIZE, ARROW_SIZE, items.Count > 0))
                    {
                        this.currentIndex = this.prevIndex;
                        InvokeSelected();
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
                    if (_buttonSubView.DrawButton(">", ARROW_SIZE, ARROW_SIZE, items.Count > 0))
                    {
                        this.currentIndex = this.nextIndex;
                        InvokeSelected();
                    }
                }
            }
            _buttonSubView.EndLayout();

            view.NextElement(subViewRect);
        }

        public override void DrawTextureButton(GUIView view)
        {
            _onBeforeSelected = view.onBeforeValueChanged;

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
                subViewWidth += ARROW_SIZE * 2;
            }

            var subViewRect = view.GetDrawRect(subViewWidth, buttonSize.y);
            _buttonSubView.parent = view;
            _buttonSubView.Init(subViewRect);

            _buttonSubView.BeginHorizontal();
            {
                if (showArrow)
                {
                    // 候補が空だと prevIndex / nextIndex が範囲外になるため押せなくする
                    if (_buttonSubView.DrawButton("<", ARROW_SIZE, ARROW_SIZE, items.Count > 0))
                    {
                        this.currentIndex = this.prevIndex;
                        InvokeSelected();
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
                    if (_buttonSubView.DrawButton(">", ARROW_SIZE, ARROW_SIZE, items.Count > 0))
                    {
                        this.currentIndex = this.nextIndex;
                        InvokeSelected();
                    }
                }
            }
            _buttonSubView.EndLayout();

            view.NextElement(subViewRect);
        }

        /// <summary>ポップアップの行数。派生で先頭行などを足す場合に上書きする</summary>
        protected virtual int popupRowCount => items.Count;

        /// <summary>
        /// ポップアップのサイズ。行高・枠幅はメニューバーと共通 (GUIView.POPUP_*)。
        /// 収まらないときは項目側を狭めてスクロールバーを出すため、幅は contentSize.x のまま
        /// </summary>
        public override Vector2 GetPopupSize()
        {
            var height = Mathf.Min(contentSize.y, GUIView.GetPopupHeight(popupRowCount));
            return new Vector2(contentSize.x, height);
        }

        public override bool DrawPopupContent(GUIView view)
        {
            var selectedIndex = -1;

            var itemWidth = view.BeginPopupList(items.Count);
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var isCurrent = i == currentIndex;
                    var color = isCurrent ? GUIView.option.accentColor : Color.white;
                    var enabled = getEnabled == null || getEnabled(item, i);

                    if (view.DrawPopupRow(getName(item, i), isCurrent, itemWidth, enabled, color))
                    {
                        selectedIndex = i;
                    }
                }
            }
            view.EndPopupList();

            if (selectedIndex >= 0)
            {
                this.currentIndex = selectedIndex;
                InvokeSelected();
                return true;
            }
            return false;
        }
    }
}