using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// 画像選択用の共通ウィンドウ。
    /// TextureFileCache.DrawPathField の「選択」ボタンから開かれ、
    /// 指定フォルダ配下の png をサムネイル付きの一覧から選ばせる
    /// </summary>
    public class TexturePickerWindow : IGUIWindow
    {
        /// <summary>他ウィンドウと衝突する場合はプラグイン側で差し替える</summary>
        public static int windowId = 896433;

        public static readonly int WINDOW_WIDTH = 340;
        public static readonly int WINDOW_HEIGHT = 400;
        public static readonly int HEADER_HEIGHT = 20;
        public static readonly string WINDOW_NAME = "画像選択";

        private static readonly int THUMB_SIZE = 40;
        private static readonly int THUMB_MARGIN = 5;
        private static readonly int ROW_HEIGHT = 44;
        private static readonly int ANCHOR_MARGIN = 2;
        private static readonly int SCROLLBAR_WIDTH = 20;

        private static TexturePickerWindow _instance = null;
        public static TexturePickerWindow instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TexturePickerWindow();
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

        private bool _initializedGUI = false;

        private GUIView _rootView = new GUIView();
        private GUIView _headerView = new GUIView();
        private GUIView _contentView = new GUIView();

        // 編集対象。DrawPathField から毎フレーム同期される。
        // ラベルはエフェクト間で重複しうるため、識別は呼び出し元インスタンスで行う
        private object _target = null;
        private string _targetLabel = null;
        private string _currentPath = null;
        private string _searchDir = null;
        private string _baseDir = null;
        private Action<string> _onSelected = null;
        private int _syncedFrame = -1;
        private int _openedFrame = -1;

        // 呼び出し元ボタンの矩形（GUI 座標系のスクリーン位置）
        private Rect _anchorRect = Rect.zero;

        /// <summary>一覧の 1 件分。テクスチャはウィンドウを閉じるまで保持する</summary>
        private class ImageEntry
        {
            public string relativePath;
            public string displayName;
            public Texture2D thumbnail;
        }

        private readonly List<ImageEntry> _entries = new List<ImageEntry>();
        private string _scanError = null;

        private TexturePickerWindow()
        {
            _windowRect = new Rect(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT);
        }

        /// <summary>
        /// 指定の呼び出し元の画像を選択中かどうか
        /// </summary>
        public bool IsEditing(object target)
        {
            return isShowWnd && _target != null && ReferenceEquals(_target, target);
        }

        /// <param name="target">呼び出し元インスタンス。選択対象の識別に使う</param>
        /// <param name="searchDir">走査対象のフォルダ（絶対パス）</param>
        /// <param name="baseDir">選択結果を相対パスで返すときの基準フォルダ（絶対パス）</param>
        /// <param name="anchorRect">呼び出し元ボタンの矩形（GUI 座標系のスクリーン位置）</param>
        public void Open(
            object target,
            string label,
            string currentPath,
            string searchDir,
            string baseDir,
            Action<string> onSelected,
            Rect anchorRect)
        {
            _target = target;
            _targetLabel = label;
            _currentPath = currentPath;
            _searchDir = searchDir;
            _baseDir = baseDir;
            _onSelected = onSelected;
            _syncedFrame = Time.frameCount;
            _openedFrame = Time.frameCount;

            _contentView.scrollPosition = Vector2.zero;
            Rescan();

            isShowWnd = true;
            _anchorRect = anchorRect;
            ApplyAnchorPosition();
        }

        /// <summary>
        /// 編集対象の最新状態を反映する。
        /// 呼び出し元の描画が止まった場合はウィンドウを閉じる判定にも使う
        /// </summary>
        public void Sync(object target, string currentPath, Action<string> onSelected)
        {
            if (!IsEditing(target))
            {
                return;
            }

            _currentPath = currentPath;
            _onSelected = onSelected;
            _syncedFrame = Time.frameCount;
        }

        /// <summary>
        /// 対象フォルダ配下の png を baseDir からの相対パスで列挙する。
        /// 「選択」ウィンドウと呼び出し元の前後送りで同じ並び順を使うためのもの
        /// </summary>
        public static List<string> ListImageFiles(string searchDir, string baseDir)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(searchDir) || !Directory.Exists(searchDir))
            {
                return result;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(searchDir, "*.png", SearchOption.AllDirectories);
            }
            catch (Exception e)
            {
                // OnGUI の途中で投げるとそのフレームの描画ごと落ちるため、ここで握って空一覧にする
                MTEUtils.LogException(e);
                return result;
            }

            Array.Sort(files, new NaturalStringComparer());

            foreach (var file in files)
            {
                result.Add(MakeRelativePath(baseDir, file));
            }
            return result;
        }

        /// <summary>
        /// 対象フォルダ配下の png を再走査してサムネイルを読み直す
        /// </summary>
        private void Rescan()
        {
            ClearEntries();
            _scanError = null;

            if (string.IsNullOrEmpty(_searchDir) || !Directory.Exists(_searchDir))
            {
                _scanError = "フォルダが見つかりません: " + _searchDir;
                return;
            }

            foreach (var relativePath in ListImageFiles(_searchDir, _baseDir))
            {
                var fullPath = Path.Combine(_baseDir, relativePath);
                var thumbnail = LoadThumbnail(fullPath);
                if (thumbnail == null)
                {
                    continue;
                }

                _entries.Add(new ImageEntry
                {
                    // 呼び出し元が同じ基準フォルダで解決するため、そのまま渡せる形に揃える
                    relativePath = relativePath,
                    displayName = MakeRelativePath(_searchDir, fullPath),
                    thumbnail = thumbnail,
                });
            }

            if (_entries.Count == 0)
            {
                _scanError = "png が見つかりません: " + _searchDir;
            }
        }

        /// <summary>
        /// baseDir 配下のパスを相対パスへ直す。配下でなければそのまま返す
        /// </summary>
        public static string MakeRelativePath(string baseDir, string fullPath)
        {
            var prefix = baseDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(prefix.Length);
            }
            return fullPath;
        }

        private static Texture2D LoadThumbnail(string path)
        {
            try
            {
                // Texture2D の生成前に読み込む。逆順だとファイル I/O の例外で生成済みテクスチャが漏れる
                var data = File.ReadAllBytes(path);

                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(data))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                // シーン遷移時の Resources.UnloadUnusedAssets() で破棄されないように保護する
                texture.hideFlags = HideFlags.HideAndDontSave;
                return texture;
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                return null;
            }
        }

        private void ClearEntries()
        {
            foreach (var entry in _entries)
            {
                if (entry.thumbnail != null)
                {
                    UnityEngine.Object.Destroy(entry.thumbnail);
                }
            }
            _entries.Clear();
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
            if (y + WINDOW_HEIGHT > Screen.height)
            {
                y = _anchorRect.y - WINDOW_HEIGHT - ANCHOR_MARGIN;
            }

            _windowRect.x = Mathf.Max(x, 0);
            _windowRect.y = Mathf.Max(y, 0);
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
            _target = null;
            _targetLabel = null;
            _onSelected = null;
            ClearEntries();
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
            _rootView.Init(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT);
            _headerView.Init(0, 0, WINDOW_WIDTH, HEADER_HEIGHT);
            _contentView.Init(0, HEADER_HEIGHT, WINDOW_WIDTH, WINDOW_HEIGHT - HEADER_HEIGHT);

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
            view.BeginHorizontal();
            {
                view.currentPos.x = WINDOW_WIDTH - 70;

                if (view.DrawButton("更新", 45, 20))
                {
                    Rescan();
                }

                if (view.DrawButton("x", 20, 20))
                {
                    Close();
                }
            }
            view.EndLayout();
        }

        private void DrawContent()
        {
            var view = _contentView;
            view.ResetLayout();

            view.BeginScrollView();
            {
                var rowWidth = WINDOW_WIDTH - (int)view.padding.x * 2 - SCROLLBAR_WIDTH;

                DrawRow(view, rowWidth, null, "（なし）", null);

                foreach (var entry in _entries)
                {
                    DrawRow(view, rowWidth, entry.thumbnail, entry.displayName, entry.relativePath);
                }

                if (_scanError != null)
                {
                    view.DrawLabel(_scanError, rowWidth, 20, Color.gray);
                }
            }
            view.EndScrollView();
        }

        /// <summary>
        /// サムネイル＋パスのボタンを 1 行描画する。selectPath が null の行は選択解除用
        /// </summary>
        private void DrawRow(GUIView view, float rowWidth, Texture2D thumbnail, string label, string selectPath)
        {
            // 呼び出し元は絶対パスで持っていることもあるため、一覧側と同じ相対パスに揃えて比べる
            var currentPath = MakeRelativePath(_baseDir, _currentPath ?? "");
            var isCurrent = string.Equals(currentPath, selectPath ?? "", StringComparison.OrdinalIgnoreCase);

            view.BeginHorizontal();
            {
                DrawThumbnail(view, thumbnail);

                if (view.DrawButton(label, rowWidth - THUMB_SIZE - THUMB_MARGIN, ROW_HEIGHT, true, isCurrent ? GUIView.option.accentColor : (Color?)null))
                {
                    _currentPath = selectPath;
                    _onSelected?.Invoke(selectPath ?? "");
                    Close();
                }
            }
            view.EndLayout();
        }

        /// <summary>
        /// 縦横比を保ったままサムネイル枠に収めて描画する。
        /// LUT のような極端な横長画像でも潰れずに中身が判る
        /// </summary>
        private void DrawThumbnail(GUIView view, Texture2D thumbnail)
        {
            var slotRect = view.GetDrawRect(THUMB_SIZE, ROW_HEIGHT);

            if (thumbnail != null)
            {
                var scale = Mathf.Min(THUMB_SIZE / (float)thumbnail.width, THUMB_SIZE / (float)thumbnail.height);
                var width = thumbnail.width * scale;
                var height = thumbnail.height * scale;
                GUI.DrawTexture(
                    new Rect(
                        slotRect.x + (slotRect.width - width) * 0.5f,
                        slotRect.y + (slotRect.height - height) * 0.5f,
                        width,
                        height),
                    thumbnail);
            }

            view.NextElement(slotRect);
        }
    }
}
