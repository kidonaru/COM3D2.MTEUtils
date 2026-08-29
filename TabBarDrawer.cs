using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// タブ列の描画とクリック通知。ホストが push したタブ状態 (titles/activeIndex) だけで
    /// 描けるよう TabGroup へ依存しない。内部窓 (EditorSubWindow) と
    /// 外部窓 (DockableWindowBase) が共有する
    /// </summary>
    public static class TabBarDrawer
    {
        public static readonly int TAB_WIDTH = 90;
        public static readonly int TAB_HEIGHT = 20;
        public static readonly int TAB_MARGIN = 2;
        /// <summary>アクティブタブのアクセント色。連結表示 (CONNECT_ACCENT_COLOR) と揃える</summary>
        public static readonly Color ACCENT_COLOR = Color.cyan;

        private static GUIStyle _tabLabelStyle;

        /// <summary>タブ名用の中央寄せスタイル。GUIStyle は OnGUI 中でしか作れないため遅延生成する</summary>
        private static GUIStyle tabLabelStyle
        {
            get
            {
                if (_tabLabelStyle == null)
                {
                    _tabLabelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        // タブ名が長くても 2 行に折り返さず 1 行で表示する
                        wordWrap = false,
                        clipping = TextClipping.Clip,
                    };
                }
                return _tabLabelStyle;
            }
        }

        /// <summary>
        /// タブ列を描く。x/y は呼び出し元 GUI.Window のローカル座標、
        /// availableWidth はタブ列に使ってよい幅 (右側のボタン領域を除いた値)。
        /// 全タブが下限幅で収まらない場合は両端に &lt; &gt; ボタンを出し、
        /// scrollOffset (呼び出し元ウィンドウが保持) の位置から見える枚数だけ描く。
        /// タブ押下は onTabMouseDown(グループ全体でのタブindex, ウィンドウローカル押下位置) へ
        /// 通知してイベントを消費する
        /// (アクティブ化とつまみドラッグ候補の処理は呼び出し側の責務)
        /// </summary>
        public static void Draw(
            string[] titles, int activeIndex,
            float x, float y, float availableWidth,
            ref int scrollOffset,
            Action<int, Vector2> onTabMouseDown)
        {
            if (titles == null || titles.Length == 0)
            {
                return;
            }

            var count = titles.Length;
            var layout = TabBarLayout.Calc(count, availableWidth, scrollOffset, activeIndex);
            if (layout.visibleCount <= 0)
            {
                return;
            }
            // クランプ・アクティブ追従の結果を呼び出し元の保持値へ書き戻す
            scrollOffset = layout.firstVisible;

            var e = Event.current;

            if (layout.scrollable)
            {
                var maxOffset = count - layout.visibleCount;
                if (DrawScrollButton(x, y, "<", layout.firstVisible > 0))
                {
                    scrollOffset = layout.firstVisible - 1;
                }
                if (DrawScrollButton(
                        x + availableWidth - TabBarLayout.SCROLL_BUTTON_WIDTH, y, ">",
                        layout.firstVisible < maxOffset))
                {
                    scrollOffset = layout.firstVisible + 1;
                }
            }

            var tabX = x + layout.tabsOriginX;
            var last = layout.firstVisible + layout.visibleCount - 1;
            for (var i = layout.firstVisible; i <= last; i++)
            {
                var tabRect = new Rect(tabX, y, layout.tabWidth, TAB_HEIGHT);
                var isActive = i == activeIndex;

                if (e.type == EventType.MouseDown && e.button == 0 && tabRect.Contains(e.mousePosition))
                {
                    if (onTabMouseDown != null)
                    {
                        onTabMouseDown(i, e.mousePosition);
                    }
                    // タブ押下でウィンドウ全体のドラッグが始まらないよう消費する
                    e.Use();
                }

                var oldColor = GUI.color;
                if (isActive)
                {
                    // アクティブ: 明るい背景 + 白文字 + 下端にアクセントライン
                    GUI.color = new Color(1f, 1f, 1f, 0.15f);
                    GUI.DrawTexture(tabRect, Texture2D.whiteTexture);
                    GUI.color = ACCENT_COLOR;
                    GUI.DrawTexture(
                        new Rect(tabRect.x, tabRect.yMax - 2, tabRect.width, 2),
                        Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                else
                {
                    // 非アクティブ: 暗い背景。ホバー中は中間の明るさにする。
                    // 文字色はアクティブと同じ白にして、区別は背景の明暗だけで付ける
                    var hovered = tabRect.Contains(e.mousePosition);
                    GUI.color = new Color(0f, 0f, 0f, hovered ? 0.15f : 0.4f);
                    GUI.DrawTexture(tabRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                GUI.Label(tabRect, GetTruncatedTitle(titles[i], layout.tabWidth), tabLabelStyle);
                GUI.color = oldColor;

                tabX += layout.tabWidth + TAB_MARGIN;
            }
        }

        /// <summary>
        /// スクロールボタン 1 つ。ボタン背景は非アクティブタブと同じ暗色矩形を自前で敷く
        /// (既定のボタン背景だと高さ・見た目がタブから浮くため)
        /// </summary>
        private static bool DrawScrollButton(float x, float y, string label, bool enabled)
        {
            var rect = new Rect(x, y, TabBarLayout.SCROLL_BUTTON_WIDTH, TAB_HEIGHT);
            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.4f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            // 端に達している側は押せないことが分かるよう淡くする
            GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            GUI.enabled = enabled;
            var pressed = GUI.Button(rect, label, tabLabelStyle);
            GUI.enabled = true;
            GUI.color = oldColor;
            return pressed;
        }

        private struct TruncatedTitleEntry
        {
            public int tabWidth;
            public string result;
        }

        /// <summary>
        /// 省略結果のキャッシュ。描画毎の文字列生成を避ける。
        /// リサイズ中に幅ごとのエントリが無制限に増えないよう、タイトルごとに直近 1 幅分のみ保持する
        /// </summary>
        private static readonly Dictionary<string, TruncatedTitleEntry> _truncatedTitleCache =
            new Dictionary<string, TruncatedTitleEntry>();

        /// <summary>タブ幅に収まらないタイトルを末尾 "…" 付きで省略する</summary>
        private static string GetTruncatedTitle(string title, float tabWidth)
        {
            if (string.IsNullOrEmpty(title))
            {
                return title;
            }

            var width = (int) tabWidth;
            TruncatedTitleEntry entry;
            if (_truncatedTitleCache.TryGetValue(title, out entry) && entry.tabWidth == width)
            {
                return entry.result;
            }

            var result = title;
            if (GUIView.CalcWidth(tabLabelStyle, title) > tabWidth)
            {
                // 1 文字も収まらない極端な狭さでは "…" 単体にフォールバックする
                result = "…";
                for (var length = title.Length - 1; length > 0; length--)
                {
                    var candidate = title.Substring(0, length) + "…";
                    if (GUIView.CalcWidth(tabLabelStyle, candidate) <= tabWidth)
                    {
                        result = candidate;
                        break;
                    }
                }
            }

            _truncatedTitleCache[title] = new TruncatedTitleEntry
            {
                tabWidth = width,
                result = result,
            };
            return result;
        }
    }
}
