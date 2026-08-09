using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// XML シリアライズ可能な AnimationCurve のデータ表現。
    /// CurveEditorWindow / GUIView.DrawCurve の編集対象として使う
    /// </summary>
    public class CurveKeyData
    {
        [XmlAttribute]
        public float time;
        [XmlAttribute]
        public float value;
        [XmlAttribute]
        public float inTangent;
        [XmlAttribute]
        public float outTangent;

        public CurveKeyData()
        {
        }

        public CurveKeyData(Keyframe key)
        {
            time = key.time;
            value = key.value;
            inTangent = key.inTangent;
            outTangent = key.outTangent;
        }

        public Keyframe ToKeyframe()
        {
            return new Keyframe(time, value, inTangent, outTangent);
        }
    }

    public class CurveData
    {
        [XmlElement("key")]
        public List<CurveKeyData> keys = new List<CurveKeyData>();

        /// <summary>編集のたびに加算される。適用側・プレビューの更新判定に使う</summary>
        [XmlIgnore]
        public int version = 0;

        [XmlIgnore]
        private Texture2D _previewTexture = null;
        [XmlIgnore]
        private int _previewVersion = -1;

        public static CurveData Linear()
        {
            return new CurveData
            {
                keys = new List<CurveKeyData>
                {
                    new CurveKeyData { time = 0f, value = 0f, inTangent = 1f, outTangent = 1f },
                    new CurveKeyData { time = 1f, value = 1f, inTangent = 1f, outTangent = 1f },
                },
            };
        }

        public AnimationCurve ToAnimationCurve()
        {
            // 空データ（旧 config の読み込み等）は直線として扱う
            if (keys == null || keys.Count < 2)
            {
                return Linear().ToAnimationCurve();
            }

            var frames = new Keyframe[keys.Count];
            for (var i = 0; i < keys.Count; i++)
            {
                frames[i] = keys[i].ToKeyframe();
            }
            return new AnimationCurve(frames);
        }

        public void FromAnimationCurve(AnimationCurve curve)
        {
            keys.Clear();
            foreach (var key in curve.keys)
            {
                keys.Add(new CurveKeyData(key));
            }
            version++;
        }

        public void CopyFrom(CurveData source)
        {
            keys.Clear();
            foreach (var key in source.keys)
            {
                keys.Add(new CurveKeyData { time = key.time, value = key.value, inTangent = key.inTangent, outTangent = key.outTangent });
            }
            version++;
        }

        /// <summary>
        /// 一行表示用のプレビューテクスチャ。version が変わるまでキャッシュされる
        /// </summary>
        public Texture2D GetPreviewTexture(int width, int height)
        {
            if (_previewTexture != null && _previewVersion == version)
            {
                return _previewTexture;
            }

            if (_previewTexture == null)
            {
                _previewTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                // シーン遷移時の Resources.UnloadUnusedAssets() で破棄されないように保護する
                _previewTexture.hideFlags = HideFlags.HideAndDontSave;
                _previewTexture.wrapMode = TextureWrapMode.Clamp;
            }
            _previewVersion = version;

            CurveTextureUtils.RenderCurve(_previewTexture, ToAnimationCurve(), Color.white, drawGrid: false);
            return _previewTexture;
        }
    }

    public static class CurveTextureUtils
    {
        private static readonly Color BackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color GridColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        /// <summary>
        /// キーのドラッグ中は毎フレーム呼ばれるため、SetPixel ではなくバッファへ書いて
        /// SetPixels32 で一括転送する。SetPixels32 は配列長の完全一致を要求するので
        /// プロット用・プレビュー用のサイズごとにバッファを保持する
        /// </summary>
        private static readonly Dictionary<int, Color32[]> _pixelBuffers = new Dictionary<int, Color32[]>();

        private static Color32[] GetPixelBuffer(int pixelCount)
        {
            Color32[] buffer;
            if (!_pixelBuffers.TryGetValue(pixelCount, out buffer))
            {
                buffer = new Color32[pixelCount];
                _pixelBuffers[pixelCount] = buffer;
            }
            return buffer;
        }

        /// <summary>
        /// カーブをテクスチャへ描画する。値域は 0〜1 を表示範囲とし、範囲外はクランプ表示
        /// </summary>
        public static void RenderCurve(Texture2D texture, AnimationCurve curve, Color curveColor, bool drawGrid)
        {
            var width = texture.width;
            var height = texture.height;
            var pixelCount = width * height;

            var pixels = GetPixelBuffer(pixelCount);

            var background = (Color32)BackgroundColor;
            for (var i = 0; i < pixelCount; i++)
            {
                pixels[i] = background;
            }

            if (drawGrid)
            {
                var grid = (Color32)GridColor;
                // 0.25 刻みのグリッド線
                for (var i = 1; i < 4; i++)
                {
                    var gx = Mathf.Clamp(Mathf.RoundToInt(width * i / 4f), 0, width - 1);
                    var gy = Mathf.Clamp(Mathf.RoundToInt(height * i / 4f), 0, height - 1);
                    for (var y = 0; y < height; y++)
                    {
                        pixels[y * width + gx] = grid;
                    }
                    var rowOffset = gy * width;
                    for (var x = 0; x < width; x++)
                    {
                        pixels[rowOffset + x] = grid;
                    }
                }
            }

            var line = (Color32)curveColor;
            // 隣接カラム間を縦に埋めて線が途切れないようにする
            var prevY = -1;
            for (var x = 0; x < width; x++)
            {
                var t = x / (float)(width - 1);
                var value = Mathf.Clamp01(curve.Evaluate(t));
                var y = Mathf.Clamp(Mathf.RoundToInt(value * (height - 1)), 0, height - 1);

                var minY = prevY < 0 ? y : Mathf.Min(prevY, y);
                var maxY = prevY < 0 ? y : Mathf.Max(prevY, y);
                for (var py = minY; py <= maxY; py++)
                {
                    pixels[py * width + x] = line;
                }
                prevY = y;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }
    }
}
