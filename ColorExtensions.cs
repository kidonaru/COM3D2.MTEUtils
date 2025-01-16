using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    public static partial class ColorExtensions
    {
        public static Vector3 ToVector3(this Color color)
        {
            return new Vector3(color.r, color.g, color.b);
        }

        public static Color ToColor(this Vector3 vector)
        {
            return new Color(vector.x, vector.y, vector.z);
        }

        public static Vector4 ToHSVA(this Color color)
        {
            Vector4 hsv = Vector4.zero;
            Color.RGBToHSV(color, out hsv.x, out hsv.y, out hsv.z);
            hsv.w = color.a;
            return hsv;
        }

        public static Color FromHSVA(this Vector4 hsv)
        {
            var color = Color.HSVToRGB(hsv.x, hsv.y, hsv.z);
            color.a = hsv.w;
            return color;
        }

        public static int IntR(this Color color)
        {
            return Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
        }

        public static int IntG(this Color color)
        {
            return Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
        }

        public static int IntB(this Color color)
        {
            return Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
        }

        public static int IntA(this Color color)
        {
            return Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
        }

        public static string ToHexRGB(this Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        public static string ToHexRGBA(this Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(color);
        }
    }
}