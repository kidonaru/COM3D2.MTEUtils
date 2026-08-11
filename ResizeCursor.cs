using System;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// リサイズカーソルを出したいウィンドウ。
    /// カーソルはアプリ全体で 1 つしかないため、各ウィンドウが直接 ResizeCursor.Set を
    /// 呼ぶと毎フレーム奪い合ってちらつく。WindowManager がこの要求を集約して適用する
    /// </summary>
    public interface IResizeCursorProvider
    {
        bool isResizing { get; }
        ResizeCursor.Kind desiredCursorKind { get; }
    }

    /// <summary>
    /// リサイズ方向を示すマウスカーソル。
    /// OS 標準のリサイズカーソルは Win32 経由でしか取れず、
    /// ゲーム本体が使う CursorMode.ForceSoftware の描画と二重に出る恐れがあるため、
    /// 自前の画像を Cursor.SetCursor で差し替える。
    ///
    /// 画像は assets/cursors/resize-arrow.svg を generate.js で 32x32 の PNG へ
    /// ラスタライズしたもの。形を変えるときは SVG を直して再生成し、下の base64 を貼り替えること
    /// </summary>
    public static class ResizeCursor
    {
        public enum Kind
        {
            None,
            // 左右 (←→)
            Horizontal,
            // 上下 (↑↓)
            Vertical,
            // 左上⇔右下 (↖↘)
            DiagonalDown,
            // 右上⇔左下 (↗↙)
            DiagonalUp,
        }

        // 32x32 PNG (base64)。添字は Kind と対応させること。
        // sharp が吐く PNG は Texture2D.LoadImage が読めないため、generate.js 側で
        // Node 標準の zlib を使って組み立て直している (詳細は generate.js のコメント)
        private static readonly string[] PNG_BASE64 =
        {
            null,
            "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",
        };

        private static readonly Texture2D[] _textures = new Texture2D[PNG_BASE64.Length];

        private static Kind _currentKind = Kind.None;

        public static void Set(Kind kind)
        {
            if (_currentKind == kind)
            {
                return;
            }

            var texture = kind != Kind.None ? GetTexture(kind) : null;
            _currentKind = texture != null ? kind : Kind.None;

            // null を渡すと既定のカーソルへ戻る
            var hotspot = texture != null
                ? new Vector2(texture.width * 0.5f, texture.height * 0.5f)
                : Vector2.zero;
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
        }

        private static Texture2D GetTexture(Kind kind)
        {
            var index = (int)kind;
            if (_textures[index] == null)
            {
                _textures[index] = CreateTexture(PNG_BASE64[index]);
            }
            return _textures[index];
        }

        private static Texture2D CreateTexture(string base64)
        {
            // カーソルにはミップマップを持たせられない。
            // 幅・高さと形式は LoadImage が PNG に合わせて作り直す
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                if (texture.LoadImage(Convert.FromBase64String(base64)))
                {
                    return texture;
                }
                MTEUtils.LogError("リサイズカーソルの画像を読み込めませんでした");
            }
            catch (Exception e)
            {
                // 読み込めなくてもリサイズ操作自体は動くので、カーソルだけ既定のままにする
                MTEUtils.LogError("リサイズカーソルの画像の展開に失敗しました");
                MTEUtils.LogException(e);
            }

            UnityEngine.Object.Destroy(texture);
            return null;
        }
    }
}
