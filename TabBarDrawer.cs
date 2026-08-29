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

        /// <summary>
        /// タブ切替メニューを描く GUI.Window の ID。
        /// 開いているメニューは常に 1 つ (別のタブバーを右クリックすると前のは閉じる) なので
        /// ゲスト側へコピーされた TabBarDrawer と共有しても衝突しない。
        /// ComboBoxPopupWindow と同じく MTEUtils 共有のポップアップ用 ID 帯から採る
        /// </summary>
        public static readonly int MENU_WINDOW_ID = 8903375;

        // ---- 右クリックメニュー状態 (同時に開くのは 1 窓だけなので static で持つ) ----
        private static int _menuWindowId = -1;
        /// <summary>メニューの左上位置 (ホストウィンドウのローカル座標)。ホストのドラッグに追従させるため相対で持つ</summary>
        private static Vector2 _menuAnchor;
        /// <summary>今フレームの描画で使うメニュー矩形 (スクリーンGUI座標)</summary>
        private static Rect _menuScreenRect;
        /// <summary>メニューを開いたフレーム。開いた直後の押下で即閉じないためのガード</summary>
        private static int _menuOpenedFrame = -1;
        // GUI.Window のコールバックは引数を取れないため、描画に要る情報を毎フレーム控える
        private static string[] _menuTitles;
        private static int _menuActiveIndex = -1;
        private static Action<int> _menuOnTabSelected;
        private const float MENU_ITEM_HEIGHT = 22f;
        private const float MENU_WIDTH = 140f;

        /// <summary>
        /// この window のタブ切替メニューを閉じる。
        /// 描画が止まる経路 (ウィンドウを閉じる / グループ離脱) では
        /// 呼び出し元から明示的に閉じて状態を残さない
        /// </summary>
        public static void CloseContextMenu(int windowId)
        {
            if (_menuWindowId == windowId)
            {
                CloseContextMenu();
            }
        }

        private static void CloseContextMenu()
        {
            _menuWindowId = -1;
            _menuTitles = null;
            _menuOnTabSelected = null;
        }

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
            int windowId, string[] titles, int activeIndex,
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

            // タブバー領域 (ボタンを含む availableWidth 全域) の右クリックでメニューを開閉する
            var barRect = new Rect(x, y, availableWidth, TAB_HEIGHT);
            if (e.type == EventType.MouseDown && e.button == 1 && barRect.Contains(e.mousePosition))
            {
                if (_menuWindowId == windowId)
                {
                    CloseContextMenu();
                }
                else
                {
                    _menuWindowId = windowId;
                    // 位置はホストウィンドウのローカル座標で覚え、
                    // 描画時にホスト矩形を足してスクリーン座標へ直す (ホストの移動に追従させるため)
                    _menuAnchor = new Vector2(e.mousePosition.x, y + TAB_HEIGHT);
                    _menuOpenedFrame = Time.frameCount;
                }
                e.Use();
            }

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
        /// 右クリックで開いたタブ一覧メニューを、ホストとは別の GUI.Window として描く。
        /// ホストウィンドウの中に描くと矩形でクリップされてタブが多いときに見切れるため、
        /// ドロップダウン (ComboBoxPopupWindow) と同じく独立ウィンドウにしている。
        /// **ホストの GUI.Window の外** (OnGUI 直下) から毎フレーム呼ぶこと。
        /// hostRect はホストウィンドウのスクリーン矩形 (メニュー位置の追従に使う)
        /// </summary>
        public static void DrawContextMenuWindow(
            int windowId, Rect hostRect, string[] titles, int activeIndex, Action<int> onTabSelected)
        {
            if (_menuWindowId != windowId)
            {
                return;
            }
            if (titles == null || titles.Length == 0)
            {
                // グループ離脱で描くものが無くなった。開きっぱなしにしない
                CloseContextMenu();
                return;
            }

            // メニューの外を押したら閉じる。IMGUI のイベントは下の GUI.Window に
            // 消費されうるため、コンボのポップアップと同じく Input を直接見る。
            // Layout パスに絞ってフレームあたり 1 回だけ判定する。
            // 右ボタンも見るのは、別プラグインのタブバーを右クリックして
            // あちらのメニューが開いたときにこちらを閉じるため (メニュー窓 ID は共有)
            if (Event.current.type == EventType.Layout &&
                Time.frameCount != _menuOpenedFrame &&
                (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) &&
                !_menuScreenRect.Contains(MTEUtils.rawGuiPosition))
            {
                CloseContextMenu();
                return;
            }

            _menuTitles = titles;
            _menuActiveIndex = activeIndex;
            _menuOnTabSelected = onTabSelected;
            _menuScreenRect = CalcMenuScreenRect(hostRect, titles.Length);

            GUI.Window(MENU_WINDOW_ID, _menuScreenRect, DrawContextMenuContents, "", GUIView.gsWin);
            // 他のウィンドウに隠されないよう最前面へ
            GUI.BringWindowToFront(MENU_WINDOW_ID);
        }

        /// <summary>メニュー矩形をホスト相対から求め、画面内へ収める</summary>
        private static Rect CalcMenuScreenRect(Rect hostRect, int count)
        {
            var height = count * MENU_ITEM_HEIGHT;
            var x = hostRect.x + _menuAnchor.x;
            var y = hostRect.y + _menuAnchor.y;
            // 下へ収まらなければアンカーの上へ反転し、それでも溢れるなら画面内へクランプする
            if (y + height > Screen.height)
            {
                y = hostRect.y + _menuAnchor.y - TAB_HEIGHT - height;
            }
            y = Mathf.Clamp(y, 0, Mathf.Max(0, Screen.height - height));
            x = Mathf.Clamp(x, 0, Mathf.Max(0, Screen.width - MENU_WIDTH));
            return new Rect(x, y, MENU_WIDTH, height);
        }

        private static void DrawContextMenuContents(int id)
        {
            var titles = _menuTitles;
            if (titles == null)
            {
                return;
            }

            var oldColor = GUI.color;
            // 背景 (下のウィンドウが透けて見えないよう不透明寄りにする)
            var bgRect = new Rect(0, 0, _menuScreenRect.width, _menuScreenRect.height);
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            for (var i = 0; i < titles.Length; i++)
            {
                var itemRect = new Rect(0, i * MENU_ITEM_HEIGHT, MENU_WIDTH, MENU_ITEM_HEIGHT);
                var isActive = i == _menuActiveIndex;
                if (isActive)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.15f);
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = oldColor;
                }
                var label = GetTruncatedTitle(titles[i], MENU_WIDTH, _truncatedMenuTitleCache);
                if (GUI.Button(itemRect, label, tabLabelStyle))
                {
                    var onTabSelected = _menuOnTabSelected;
                    CloseContextMenu();
                    if (!isActive && onTabSelected != null)
                    {
                        onTabSelected(i);
                    }
                }
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

        /// <summary>
        /// 右クリックメニュー用の省略結果キャッシュ。タブ列とは幅が違うため辞書を分ける。
        /// 共有すると同じタイトルを 2 つの幅で毎フレーム上書きし合ってキャッシュが機能しない
        /// </summary>
        private static readonly Dictionary<string, TruncatedTitleEntry> _truncatedMenuTitleCache =
            new Dictionary<string, TruncatedTitleEntry>();

        /// <summary>タブ幅に収まらないタイトルを末尾 "…" 付きで省略する</summary>
        private static string GetTruncatedTitle(
            string title, float tabWidth,
            Dictionary<string, TruncatedTitleEntry> cache = null)
        {
            cache = cache ?? _truncatedTitleCache;
            if (string.IsNullOrEmpty(title))
            {
                return title;
            }

            var width = (int) tabWidth;
            TruncatedTitleEntry entry;
            if (cache.TryGetValue(title, out entry) && entry.tabWidth == width)
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

            cache[title] = new TruncatedTitleEntry
            {
                tabWidth = width,
                result = result,
            };
            return result;
        }
    }
}
