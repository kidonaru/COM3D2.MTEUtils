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
    /// 画像は assets/cursors/resize-arrow.svg を generate.js で 64x64 の PNG へ
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

        // 64x64 PNG (base64)。添字は Kind と対応させること。
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
