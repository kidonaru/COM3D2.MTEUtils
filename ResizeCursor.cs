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
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAwElEQVR42mNgGAWjYBSMglEwCkgDOlRSQzLw1NbWviojI3OHkEIZGZnbILUgPdSw2FJNTe1Ac3Pzm+/fv/9XVla+y8DAYI4Pg9SA1IL0gPSCzCDLZikpqa25ubnPnj9//h8GJk+e/Co5OfkePgxSA1MP0gsyA2QWyQ6QlJTEcMDEiRNfJSUl3ceHQWrQHQAyi6IoaGlpeYsUBab4MCwKQHooigIcifA2EYnwDjUTITlZTHe0tBoFo2AUjIJRQAoAAPjZmcY9h+8YAAAAAElFTkSuQmCC",
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAu0lEQVR42mNgGAWjYBRQACQlJbdKSUltHSj7LXNzc5+BMIhNd9vV1NQOPH/+/D8Ig9j0tt+zubn5zX8oALFBYnSzXVtb++r3799h9v8HsUFi9LJfR0ZG5raysvLdyZMnv5o4ceIrEFtGRuYOSI6e0WCenJx8Lykp6T4DA4PpQOSCUQeMOmDUAQPmAFBBdAe9IJKVlb3NwMCgOyKKYnBl1NLS8hbmABCbrpXRYKiOB75BMhiaZKNgFAwPAADhXZnJ8KKp2AAAAABJRU5ErkJggg==",
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAABFklEQVR42u2VsU7DMBRF7+wNhErsxVk6VV0TS1F+IE5IPgTxK0x0YI6yZclGqvxBWWFwU7GDMhR2o4eoxILYHCH5/MA971p+D/B4PJ6/uQBQAVjMJVClafoax/FBSrlljN0AWLsUWFC4tdaO42ibprF5nu+llI8Alk4MaHIKP9G27QfnfOOsAqqdJiemabJlWRoA5y6fYV0Uxdj3/THLsq/nUEqNAAJnBpzzZyHELYBLpdQ8Ej/wEr9KcM7vhBBPs0horffDMBy11gcAK5cSZ1VVGfqiRF3XljF27Sw9CIL7ruveT4vKGGPDMHxwlb+UUu6odpqcwokoil6+D5kzVlQ7TU7hSZK8ASjmPOFXrte1x+P5n3wCW12enkkd3QoAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAABF0lEQVR42u2VPU6FQBSFTz2NxdM4DAlMoY3FK5mEsAECJOTtwNrCvWihPQUltIawAgsrY4I+tDeQ+NOPueaxBO8rnG8DZ+be3O8ADofD8TccA9gAWHGGroUQl2EYdsaYMUmSdwAFR/BpEAQPeZ6/1HVtx3G0RBRFb2wT8Dzvpm3bb7tjGAartb7jHP9hWZbDPM+/D6iqygI45wpXcRxvKThN07Hv+88sy0YAZ6zhxphXAEdKqSvf9x/3Es65c/5wKeWTUup6Xz9fF0Wx7brug+6dfexkOJIMMU2TLcvyGcAB28JJr4vhiKZpvqSUt2zFQm5fDEeSoTvXWt8DOOF4wIaKhdxOehVCXHBJZmG1azXWanU4HP+HH5Ktno/EVBRCAAAAAElFTkSuQmCC",
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
