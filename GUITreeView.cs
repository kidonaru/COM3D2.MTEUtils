using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// 展開/折りたたみ + 検索 + 行仮想化を備えた汎用ツリービュー。
    /// ノード型 T のたどり方と行の見た目をデリゲートで受け取るため、
    /// GameObject 階層でもボーン階層でも同じ部品で描ける。
    ///
    /// 行は GUILayout ではなく固定高で手動配置する。
    /// キー操作でのスクロール量を行番号から正確に計算できるようにするためで、
    /// 併せて表示範囲外の行を描画から省ける。
    ///
    /// ツリーの実体も選択状態も保持しない (利用側のものを参照するだけ)。
    /// 保持するのは展開状態・検索語・組み立て済みの行リストだけ
    /// </summary>
    public class GUITreeView<T> where T : class
    {
        // ---- 木のたどり方 (利用側が必ず設定する) ----

        /// <summary>ノードの一意な ID。展開状態の記録とスクロール予約の突き合わせに使う</summary>
        public Func<T, int> getId;
        /// <summary>検索フィルタに使う名前</summary>
        public Func<T, string> getName;
        /// <summary>ノードがまだ生きているか。false なら自身も子も行に出さない</summary>
        public Func<T, bool> isAlive;
        public Func<T, int> getChildCount;
        public Func<T, int, T> getChild;

        // ---- 行の見た目と操作 (利用側が必ず設定する) ----

        public Func<T, string> getLabel;
        public Func<T, Color> getLabelColor;
        /// <summary>矢印キー操作の起点を求めるための選択判定</summary>
        public Func<T, bool> isSelected;
        public Action<T> onSelected;

        // ---- 寸法 ----

        public float rowHeight = 20f;
        public float indentWidth = 14f;
        public float toggleWidth = 20f;
        public float scrollBarWidth = 16f;

        /// <summary>表示中の 1 行。矢印キーでの移動もこの並びをたどる</summary>
        private struct Row
        {
            public T node;
            public int depth;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly HashSet<int> _expanded = new HashSet<int>();
        private IList<T> _roots = null;
        // 行の組み直しが必要か。ルート・展開状態・検索語の変化で立てる
        private bool _rowsDirty = true;
        private string _searchText = "";
        // 選択行を画面内へ送るスクロール量。次の描画で反映する
        private int _scrollToRow = -1;
        // 表示したいノードの ID。行位置は行構築後でないと確定しないため予約だけしておく
        private int _pendingRevealId = 0;
        private bool _hasPendingReveal = false;

        /// <summary>検索語。変えると次の描画で行を組み直す</summary>
        public string searchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value ?? "";
                    _rowsDirty = true;
                }
            }
        }

        /// <summary>
        /// ルート一覧を差し替える。
        /// 同じリストを利用側が中身だけ入れ替えている場合は参照が変わらないため、
        /// そのときは別途 SetDirty() を呼ぶこと
        /// </summary>
        public void SetRoots(IList<T> roots)
        {
            if (!ReferenceEquals(_roots, roots))
            {
                _roots = roots;
                _rowsDirty = true;
            }
        }

        public void SetDirty()
        {
            _rowsDirty = true;
        }

        /// <summary>
        /// 展開状態・行・スクロール予約を捨てる (シーン切替時など)。
        /// ルート参照は保持したままにする。ここで捨てると SetRoots の呼び直しを
        /// 忘れたときに「何も表示されない」という分かりにくい形で症状が出るため
        /// </summary>
        public void Clear()
        {
            _rows.Clear();
            _expanded.Clear();
            _scrollToRow = -1;
            _hasPendingReveal = false;
            _rowsDirty = true;
        }

        /// <summary>指定 ID を展開する。祖先をまとめて開くときに使う</summary>
        public void Expand(int id)
        {
            if (_expanded.Add(id))
            {
                _rowsDirty = true;
            }
        }

        /// <summary>指定 ID の行を画面内へ送るよう予約する。行に出ていなければ何も起きない</summary>
        public void Reveal(int id)
        {
            _pendingRevealId = id;
            _hasPendingReveal = true;
        }

        /// <summary>
        /// 表示予約を取り消す。選択が外れたときに呼ぶと、直前に予約した行へ
        /// 意図せずスクロールしてしまうのを防げる
        /// </summary>
        public void CancelReveal()
        {
            _hasPendingReveal = false;
        }

        /// <summary>
        /// 行が古ければ組み直す。行番号に依存する処理の前に呼ぶ。
        /// Draw / HandleKeyboard は内部で呼ぶため、利用側から呼ぶ必要は通常ない
        /// </summary>
        public void EnsureRows()
        {
            ValidateDelegates();

            if (_rowsDirty)
            {
                BuildRows();
            }
            ResolvePendingReveal();
        }

        private bool _validated = false;

        /// <summary>
        /// 必須デリゲートの設定漏れを検査する。
        /// 未設定のまま描くと内部の奥で NullReferenceException になり原因が分かりにくいため、
        /// 何が足りないかを名指しで知らせる。
        /// 一度通ればもう変わらないので、成功したときだけ以降の検査を省く
        /// (失敗時に省いてしまうと、例外を握り潰す呼び出し元では
        /// 2 フレーム目以降が無名の NullReferenceException に戻ってしまう)
        /// </summary>
        private void ValidateDelegates()
        {
            if (_validated)
            {
                return;
            }

            var missing = new List<string>();
            if (getId == null) missing.Add("getId");
            if (getName == null) missing.Add("getName");
            if (isAlive == null) missing.Add("isAlive");
            if (getChildCount == null) missing.Add("getChildCount");
            if (getChild == null) missing.Add("getChild");
            if (getLabel == null) missing.Add("getLabel");
            if (getLabelColor == null) missing.Add("getLabelColor");
            if (isSelected == null) missing.Add("isSelected");
            if (onSelected == null) missing.Add("onSelected");

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "GUITreeView のデリゲートが設定されていません: " + string.Join(", ", missing.ToArray()));
            }

            _validated = true;
        }

        /// <summary>展開状態と検索条件から、実際に表示する行を組み立てる</summary>
        private void BuildRows()
        {
            _rowsDirty = false;
            _rows.Clear();

            if (_roots == null)
            {
                return;
            }

            var searching = !string.IsNullOrEmpty(_searchText);
            for (var i = 0; i < _roots.Count; i++)
            {
                AddRows(_roots[i], 0, searching);
            }
        }

        private void AddRows(T node, int depth, bool searching)
        {
            if (node == null || !isAlive(node))
            {
                return;
            }

            // 検索中は一致するものだけフラット表示
            var matched = !searching ||
                getName(node).IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
            if (matched)
            {
                _rows.Add(new Row { node = node, depth = searching ? 0 : depth });
            }

            if (searching || _expanded.Contains(getId(node)))
            {
                var childCount = getChildCount(node);
                for (var i = 0; i < childCount; i++)
                {
                    AddRows(getChild(node, i), depth + 1, searching);
                }
            }
        }

        private void ResolvePendingReveal()
        {
            if (!_hasPendingReveal)
            {
                return;
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                if (getId(_rows[i].node) == _pendingRevealId)
                {
                    _scrollToRow = i;
                    break;
                }
            }
            _hasPendingReveal = false;
        }

        /// <summary>
        /// listRect の領域にツリーを描く。
        /// 行位置とスクロール量をずらさないよう padding なしで描くため、
        /// 呼び出し前後で view.padding は保存・復元する
        /// </summary>
        public void Draw(GUIView view, Rect listRect)
        {
            if (listRect.height <= 0f)
            {
                return;
            }

            EnsureRows();
            ApplyScrollToRow(view, listRect.height);

            var savedPadding = view.padding;
            view.padding = Vector2.zero;

            var contentWidth = Mathf.Max(listRect.width - scrollBarWidth, 0f);
            var contentHeight = _rows.Count * rowHeight;
            // 内容矩形は毎フレーム行数から与える。EndScrollView が最後に描いた行の位置で
            // 高さを書き戻すが、次フレームのここで上書きされるためスクロール範囲は保たれる。
            // 縦バーは他ウィンドウと揃えて常時表示にする (幅は contentWidth で常に確保済み)
            view.BeginScrollView(
                listRect.width, listRect.height,
                new Rect(0f, 0f, contentWidth, contentHeight), false, true);
            {
                // 表示範囲に入っている行だけ描く。
                // 行内のボタン操作で行が組み直されて縮む場合があるため、毎回件数を見る
                var firstRow = Mathf.Max((int)(view.scrollPosition.y / rowHeight), 0);
                var lastRow = Mathf.Min(
                    (int)((view.scrollPosition.y + listRect.height) / rowHeight) + 1, _rows.Count - 1);
                for (var i = firstRow; i <= lastRow && i < _rows.Count; i++)
                {
                    DrawRow(view, _rows[i], i, contentWidth);
                }
            }
            view.EndScrollView();

            view.padding = savedPadding;
        }

        /// <summary>index 行目を描く。行の位置は行番号から直接決めるため currentPos を毎回置き直す</summary>
        private void DrawRow(GUIView view, Row row, int index, float contentWidth)
        {
            var node = row.node;
            // 行は組み直しまでキャッシュされるため、破棄済みが残りうる。
            // 放っておくと空白行が残り続けるので、見つけた時点で組み直しを予約する
            // (反復中なのでその場では組み直さない)
            if (!isAlive(node))
            {
                _rowsDirty = true;
                return;
            }

            view.currentPos = new Vector2(row.depth * indentWidth, index * rowHeight);
            view.BeginHorizontal();
            {
                if (getChildCount(node) > 0)
                {
                    var id = getId(node);
                    var isExpanded = _expanded.Contains(id);
                    if (view.DrawButton(isExpanded ? "-" : "+", toggleWidth, rowHeight))
                    {
                        ToggleExpanded(id);
                    }
                }
                else
                {
                    // 子がなくてもラベルの開始位置は揃える
                    view.DrawEmpty(toggleWidth, rowHeight);
                }

                var labelWidth = contentWidth - view.currentPos.x;
                if (view.DrawButton(
                    getLabel(node), labelWidth, rowHeight, true,
                    getLabelColor(node), GUIView.gsLabel))
                {
                    onSelected(node);
                }
            }
            view.EndLayout();
        }

        /// <summary>
        /// 展開状態を切り替える。行の描画ループから呼ばれるため、ここでは行を組み直さない
        /// (組み直すと反復中のリストが縮んで添字が範囲外になる)。次フレームの BuildRows で反映される
        /// </summary>
        private void ToggleExpanded(int id)
        {
            if (!_expanded.Remove(id))
            {
                _expanded.Add(id);
            }
            _rowsDirty = true;
        }

        /// <summary>予約された行がスクロール範囲外なら、見える位置まで送る</summary>
        private void ApplyScrollToRow(GUIView view, float viewHeight)
        {
            if (_scrollToRow < 0)
            {
                return;
            }

            var top = _scrollToRow * rowHeight;
            var bottom = top + rowHeight;

            var scrollPosition = view.scrollPosition;
            if (scrollPosition.y > top)
            {
                scrollPosition.y = top;
            }
            else if (scrollPosition.y + viewHeight < bottom)
            {
                scrollPosition.y = bottom - viewHeight;
            }
            view.scrollPosition = scrollPosition;

            _scrollToRow = -1;
        }

        // ---- キーボード操作 ----

        /// <summary>
        /// 矢印キーで選択行を移動する (← 折りたたみ/親へ、→ 展開/子へ)。
        /// 使いたいウィンドウだけが描画前に呼ぶ。
        /// どこかの入力欄が編集中ならキャレット移動を優先して何もしない。
        /// 自窓の検索欄だけでなく他窓の数値入力も対象にする必要があるため、
        /// コントロール名ではなく「キーボードフォーカスを持つコントロールの有無」で判定する
        /// (GUIView の入力欄はコントロール名を設定しないため名前では判別できない)
        /// </summary>
        public void HandleKeyboard()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0)
            {
                return;
            }

            EnsureRows();

            switch (e.keyCode)
            {
                case KeyCode.UpArrow:
                    MoveSelection(-1);
                    break;
                case KeyCode.DownArrow:
                    MoveSelection(1);
                    break;
                case KeyCode.RightArrow:
                    ExpandOrSelectChild();
                    break;
                case KeyCode.LeftArrow:
                    CollapseOrSelectParent();
                    break;
                default:
                    return;
            }

            e.Use();
        }

        /// <summary>現在の選択が表示行の何番目か。選択なし・非表示なら -1</summary>
        private int GetSelectedRowIndex()
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                // 破棄済みノードを先に弾く。表示範囲外の行は生存確認を通っておらず、
                // Unity の GameObject/Component は破棄済みでも null と等値になるため、
                // 「未選択」を破棄済み行と取り違えて選択の起点がずれるのを防ぐ
                if (isAlive(_rows[i].node) && isSelected(_rows[i].node))
                {
                    return i;
                }
            }
            return -1;
        }

        private void MoveSelection(int delta)
        {
            if (_rows.Count == 0)
            {
                return;
            }

            var index = GetSelectedRowIndex();
            // 未選択・折りたたまれて見えない場合は端から始める
            var next = index < 0
                ? (delta > 0 ? 0 : _rows.Count - 1)
                : Mathf.Clamp(index + delta, 0, _rows.Count - 1);

            SelectRow(next);
        }

        /// <summary>→: 折りたたまれていれば展開し、展開済みなら最初の子へ移る</summary>
        private void ExpandOrSelectChild()
        {
            var index = GetSelectedRowIndex();
            if (index < 0)
            {
                MoveSelection(1);
                return;
            }

            var node = _rows[index].node;
            if (!isAlive(node) || getChildCount(node) == 0)
            {
                return;
            }

            if (_expanded.Add(getId(node)))
            {
                // 展開結果は次フレームの BuildRows で反映する (ToggleExpanded と同じ理由)
                _rowsDirty = true;
                return;
            }

            // 展開済みなら直後の行が最初の子になる
            if (index + 1 < _rows.Count)
            {
                SelectRow(index + 1);
            }
        }

        /// <summary>←: 展開済みなら折りたたみ、そうでなければ親へ移る</summary>
        private void CollapseOrSelectParent()
        {
            var index = GetSelectedRowIndex();
            if (index < 0)
            {
                return;
            }

            var node = _rows[index].node;
            if (!isAlive(node))
            {
                return;
            }

            if (_expanded.Remove(getId(node)))
            {
                _rowsDirty = true;
                return;
            }

            // 親は「自分より浅い深さで直前に現れる行」。
            // 親を型で辿らずに済むので、ノード型に親参照が無くても動く
            var depth = _rows[index].depth;
            for (var i = index - 1; i >= 0; i--)
            {
                if (_rows[i].depth < depth)
                {
                    SelectRow(i);
                    return;
                }
            }
        }

        private void SelectRow(int index)
        {
            onSelected(_rows[index].node);
            _scrollToRow = index;
        }
    }
}
