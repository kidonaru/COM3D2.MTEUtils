namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// タブグループへ加入しうるウィンドウ。
    /// 非アクティブタブとして畳まれている間は描画されないため、
    /// 従属するポップアップ (コンボボックス等) が畳み状態を検知するために使う。
    /// タブ機能を持たない窓は実装しなくてよい (常に表示中とみなされる)
    /// </summary>
    public interface ITabVisibleWindow
    {
        /// <summary>非アクティブタブとして畳まれていないか</summary>
        bool isTabVisible { get; }
    }
}
