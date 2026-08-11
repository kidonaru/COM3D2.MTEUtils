namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// 画面サイズ変化に追従して配置をスケールできるウィンドウ。
    /// IGUIWindow.OnScreenSizeChanged (引数なし・クランプ用途) とは別に、
    /// config に保存済みの配置と基準画面サイズから現在の画面サイズ向けの
    /// 配置を再計算する窓だけが実装する。
    /// config は更新しない (保存はユーザー操作の確定時のみ)。保存値からの
    /// 再計算にすることで、最小サイズクランプ後も元の比率へ正確に戻れる
    /// </summary>
    public interface IScreenScalableWindow
    {
        /// <summary>
        /// 画面サイズ変化時の配置再計算。連続リサイズ中は毎フレーム settled=false で呼ばれ、
        /// サイズが安定した最初のフレームに settled=true で呼ばれる。
        /// RT 作り直し等の重い後処理は settled=true のときだけ行うこと
        /// </summary>
        void OnScreenSizeScaled(bool settled);
    }
}
