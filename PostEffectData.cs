using UnityEngine;

namespace COM3D2.MotionTimelineEditor.PostEffects
{
    /// <summary>
    /// プラグイン間で受け渡すポストエフェクトの値。
    /// PostEffects.Plugin と SceneEditor が同じソースを各々コンパイルするため
    /// CLR 上は別型になり、境界では ReflectionFieldCopier で同名フィールドを写す。
    /// **フィールド名は PostEffects.Plugin 側の実体型と 1:1 で一致させること**
    /// (名前が食い違うと、その値だけ黙って既定値のまま素通りする)
    /// </summary>
    public class ParaffinData
    {
        public bool enabled = false;
        public Color color1 = new Color(0.68f, 0.34f, 0f, 1f);
        public Color color2 = new Color(0.68f, 0.34f, 0f, 0f);

        public Vector2 centerPosition = new Vector2(0.5f, 1.0f);
        public float radiusFar = 1f;
        public float radiusNear = 0f;
        public Vector2 radiusScale = new Vector2(1f, 1f);
        // 0=マスクなし / 1=キャラ除外 / 2=キャラのみ
        public int maskMode = 0;

        public float useNormal = 0f;
        public float useAdd = 1f;
        public float useMultiply = 0f;
        public float useOverlay = 0f;
        public float useSubstruct = 0f;
    }

    public class DistanceFogData
    {
        public bool enabled = false;
        public Color color1 = new Color(1f, 1f, 1f, 1f);
        public Color color2 = new Color(1f, 1f, 1f, 0f);

        public float fogStart = 0f;
        public float fogEnd = 40f;
        public float fogExp = 1f;

        public float useNormal = 1f;
        public float useAdd = 0f;
        public float useMultiply = 0f;
        public float useOverlay = 0f;
        public float useSubstruct = 0f;
    }

    public class RimlightData
    {
        public bool enabled = false;
        public Color color1 = new Color(0.77f, 0.70f, 1f, 1f);
        public Color color2 = new Color(0.77f, 0.70f, 1f, 0f);

        public Vector3 rotation = new Vector3(10f, -40f, 0f);
        public float lightArea = 1f;
        public float fadeRange = 0.2f;
        public float fadeExp = 1f;

        public float useNormal = 0f;
        public float useAdd = 0.8f;
        public float useMultiply = 0f;
        public float useOverlay = 0f;
        public float useSubstruct = 0f;

        public bool isWorldSpace = false;
        // 頭部 (顔・髪・頭アクセ) にリムライトを乗せない
        public bool excludeFace = true;
        // excludeFace 時でも髪 (髪・帽子・髪アクセ) には適用する
        public bool applyHair = false;
        // 0=マスクなし / 1=キャラ除外 / 2=キャラのみ
        public int maskMode = 2;
    }

    public class GTToneMapData
    {
        public bool enabled = false;
        public float maxBrightness = 1f;
        public float contrast = 1f;
        public float linearStart = 0.22f;
        public float linearLength = 0.4f;
        public float blackTightness = 1.33f;
        public float blackOffset = 0f;
    }

    /// <summary>
    /// 被写界深度。タイムラインが駆動する項目だけを持ち、
    /// DX11 ボケ等の実体側だけの項目は持たない (触らない = PostEffects 側 UI の管轄)
    /// </summary>
    public class DepthOfFieldData
    {
        public bool enabled = false;
        public float focalLength = 10f;
        public float focalSize = 0.05f;
        public float aperture = 11.5f;
        public float maxBlurSize = 2f;
        // メイドの頭にフォーカスを追従させる
        public bool maidFocus = false;
        // 準備完了メイド一覧の中のインデックス
        public int maidIndex = 0;
    }

    /// <summary>
    /// ブルーム (ゲーム内蔵 Bloom)。
    /// 実体の enum 3 種はゲーム側アセンブリの型なのでここでは持てず、int で受け渡す
    /// (値の対応は TimelineBridge が変換する)。
    /// キャラ/背景の分離設定も実体ではネストクラスだが、ネストのままだと
    /// ReflectionFieldCopier が別アセンブリの同名クラスを型不一致で捨てるため
    /// separation 接頭辞を付けて平置きにしてある
    /// </summary>
    public class BloomData
    {
        public bool enabled = false;
        // ゲーム標準のブルーム (CameraMain が毎フレーム有効化する) を強制無効化する
        public bool gameEffectDisabled = false;
        // Bloom.HDRBloomMode: 0=Auto, 1=On, 2=Off
        public int hdr = 0;
        // Bloom.BloomScreenBlendMode: 0=Screen, 1=Add
        public int screenBlendMode = 0;
        public bool highQuality = true;
        public float intensity = 2.1375f;
        public float threshold = 0.7f;
        public Color thresholdColor = Color.white;
        public int blurIterations = 3;
        public float blurSpread = 3.48f;

        // キャラと背景のブルーム分離
        public bool separationEnabled = false;
        public bool separationCharactersEnabled = true;
        public bool separationBackgroundEnabled = true;
        public float separationCharacterIntensity = 2.1375f;
        public float separationCharacterThreshold = 0.7f;
        public float separationCharacterRadius = 3.48f;

        // レンズフレア
        // Bloom.LensFlareStyle: 0=Ghosting, 1=Anamorphic, 2=Combined
        public int lensFlareMode = 1;
        public float lensFlareIntensity = 0f;
        public float lensFlareSaturation = 0.75f;
        public float lensFlareThreshold = 0.3f;
        public float flareRotation = 0f;
        public float hollyStretchWidth = 2.5f;
        public int hollywoodFlareBlurIterations = 2;
        public Color flareColorA = new Color(0.4f, 0.4f, 0.8f, 0.75f);
        public Color flareColorB = new Color(0.4f, 0.8f, 0.8f, 0.75f);
        public Color flareColorC = new Color(0.8f, 0.4f, 0.8f, 0.75f);
        public Color flareColorD = new Color(0.8f, 0.4f, 0f, 0.75f);
    }

    /// <summary>ポストエフェクト DTO のキーフレーム間補間。enabled 等の非連続値は start 側を採る</summary>
    public static class PostEffectDataLerp
    {
        public static ParaffinData Lerp(ParaffinData a, ParaffinData b, float t)
        {
            return new ParaffinData
            {
                enabled = a.enabled,
                color1 = Color.Lerp(a.color1, b.color1, t),
                color2 = Color.Lerp(a.color2, b.color2, t),
                centerPosition = Vector2.Lerp(a.centerPosition, b.centerPosition, t),
                radiusFar = Mathf.Lerp(a.radiusFar, b.radiusFar, t),
                radiusNear = Mathf.Lerp(a.radiusNear, b.radiusNear, t),
                radiusScale = Vector2.Lerp(a.radiusScale, b.radiusScale, t),
                maskMode = a.maskMode,
                useNormal = Mathf.Lerp(a.useNormal, b.useNormal, t),
                useAdd = Mathf.Lerp(a.useAdd, b.useAdd, t),
                useMultiply = Mathf.Lerp(a.useMultiply, b.useMultiply, t),
                useOverlay = Mathf.Lerp(a.useOverlay, b.useOverlay, t),
                useSubstruct = Mathf.Lerp(a.useSubstruct, b.useSubstruct, t),
            };
        }

        public static DistanceFogData Lerp(DistanceFogData a, DistanceFogData b, float t)
        {
            return new DistanceFogData
            {
                enabled = a.enabled,
                color1 = Color.Lerp(a.color1, b.color1, t),
                color2 = Color.Lerp(a.color2, b.color2, t),
                fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t),
                fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t),
                fogExp = Mathf.Lerp(a.fogExp, b.fogExp, t),
                useNormal = Mathf.Lerp(a.useNormal, b.useNormal, t),
                useAdd = Mathf.Lerp(a.useAdd, b.useAdd, t),
                useMultiply = Mathf.Lerp(a.useMultiply, b.useMultiply, t),
                useOverlay = Mathf.Lerp(a.useOverlay, b.useOverlay, t),
                useSubstruct = Mathf.Lerp(a.useSubstruct, b.useSubstruct, t),
            };
        }

        public static RimlightData Lerp(RimlightData a, RimlightData b, float t)
        {
            return new RimlightData
            {
                enabled = a.enabled,
                color1 = Color.Lerp(a.color1, b.color1, t),
                color2 = Color.Lerp(a.color2, b.color2, t),
                rotation = Vector3.Lerp(a.rotation, b.rotation, t),
                lightArea = Mathf.Lerp(a.lightArea, b.lightArea, t),
                fadeRange = Mathf.Lerp(a.fadeRange, b.fadeRange, t),
                fadeExp = Mathf.Lerp(a.fadeExp, b.fadeExp, t),
                useNormal = Mathf.Lerp(a.useNormal, b.useNormal, t),
                useAdd = Mathf.Lerp(a.useAdd, b.useAdd, t),
                useMultiply = Mathf.Lerp(a.useMultiply, b.useMultiply, t),
                useOverlay = Mathf.Lerp(a.useOverlay, b.useOverlay, t),
                useSubstruct = Mathf.Lerp(a.useSubstruct, b.useSubstruct, t),
                isWorldSpace = a.isWorldSpace,
                excludeFace = a.excludeFace,
                applyHair = a.applyHair,
                maskMode = a.maskMode,
            };
        }

        public static BloomData Lerp(BloomData a, BloomData b, float t)
        {
            return new BloomData
            {
                enabled = a.enabled,
                gameEffectDisabled = a.gameEffectDisabled,
                hdr = a.hdr,
                screenBlendMode = a.screenBlendMode,
                highQuality = a.highQuality,
                intensity = Mathf.Lerp(a.intensity, b.intensity, t),
                threshold = Mathf.Lerp(a.threshold, b.threshold, t),
                thresholdColor = Color.Lerp(a.thresholdColor, b.thresholdColor, t),
                // 反復回数は途中の非整数値に意味が無いため補間しない
                blurIterations = a.blurIterations,
                blurSpread = Mathf.Lerp(a.blurSpread, b.blurSpread, t),

                separationEnabled = a.separationEnabled,
                separationCharactersEnabled = a.separationCharactersEnabled,
                separationBackgroundEnabled = a.separationBackgroundEnabled,
                separationCharacterIntensity = Mathf.Lerp(
                    a.separationCharacterIntensity, b.separationCharacterIntensity, t),
                separationCharacterThreshold = Mathf.Lerp(
                    a.separationCharacterThreshold, b.separationCharacterThreshold, t),
                separationCharacterRadius = Mathf.Lerp(
                    a.separationCharacterRadius, b.separationCharacterRadius, t),

                lensFlareMode = a.lensFlareMode,
                lensFlareIntensity = Mathf.Lerp(a.lensFlareIntensity, b.lensFlareIntensity, t),
                lensFlareSaturation = Mathf.Lerp(a.lensFlareSaturation, b.lensFlareSaturation, t),
                lensFlareThreshold = Mathf.Lerp(a.lensFlareThreshold, b.lensFlareThreshold, t),
                flareRotation = Mathf.Lerp(a.flareRotation, b.flareRotation, t),
                hollyStretchWidth = Mathf.Lerp(a.hollyStretchWidth, b.hollyStretchWidth, t),
                hollywoodFlareBlurIterations = a.hollywoodFlareBlurIterations,
                flareColorA = Color.Lerp(a.flareColorA, b.flareColorA, t),
                flareColorB = Color.Lerp(a.flareColorB, b.flareColorB, t),
                flareColorC = Color.Lerp(a.flareColorC, b.flareColorC, t),
                flareColorD = Color.Lerp(a.flareColorD, b.flareColorD, t),
            };
        }
    }
}
