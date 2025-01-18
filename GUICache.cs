namespace COM3D2.MotionTimelineEditor
{
    using UnityEngine;

    public enum FloatFieldType
    {
        Float = 0,
        Int,
    }

    public class FloatFieldCache
    {
        public string label = null;
        public string text = "";

        private FloatFieldType _fieldType = FloatFieldType.Float;
        public FloatFieldType fieldType
        {
            get
            {
                return _fieldType;
            }
            set
            {
                if (value == _fieldType)
                {
                    return;
                }

                _fieldType = value;
                _value = float.NaN;
                text = "";
            }
        }

        public string format => fieldType == FloatFieldType.Int ? "F0" : "F2";

        private float _value = float.NaN;
        public float value => _value;

        public void UpdateValue(float value, bool updateText)
        {
            if (value == _value)
            {
                return;
            }

            _value = value;

            if (updateText)
            {
                text = float.IsNaN(_value) ? "" : value.ToString(format);
            }
        }

        public void UpdateValue(float value)
        {
            UpdateValue(value, true);
        }

        public FloatFieldCache()
        {
        }
    }

    public class IntFieldCache
    {
        public string label = null;
        public string text = "";

        private int _value = 0;
        public int value => _value;

        public void UpdateValue(int value, bool updateText)
        {
            if (value == _value)
            {
                return;
            }

            _value = value;

            if (updateText)
            {
                text = value.ToString();
            }
        }

        public void UpdateValue(int value)
        {
            UpdateValue(value, true);
        }

        public IntFieldCache()
        {
        }
    }

    public class ColorFieldCache
    {
        public string label = null;
        public string text = "";
        public bool hasAlpha = false;

        private Color _color = Color.white;
        public Color color => _color;

        private Vector4 _hsv = Vector4.zero;
        public Vector4 hsv => _hsv;

        private Color _defaultColor = Color.white;
        public Color defaultColor => _defaultColor;

        private Vector4 _defaultHSV = Color.white.ToHSVA();
        public Vector4 defaultHSV => _defaultHSV;

        public void UpdateColor(Color color, bool updateText)
        {
            if (color == _color && text.Length > 0)
            {
                return;
            }

            _color = color;
            _hsv = color.ToHSVA();

            if (updateText)
            {
                UpdateText();
            }
        }

        public void UpdateHSV(Vector4 hsv, bool updateText)
        {
            if (hsv == this._hsv && text.Length > 0)
            {
                return;
            }

            _hsv = hsv;
            _color = hsv.FromHSVA();

            if (updateText)
            {
                UpdateText();
            }
        }

        public void UpdateDefaultColor(Color defaultColor)
        {
            if (defaultColor == _defaultColor)
            {
                return;
            }

            _defaultColor = defaultColor;
            _defaultHSV = defaultColor.ToHSVA();
        }

        public void ResetColor()
        {
            if (_color == _defaultColor)
            {
                return;
            }

            _color = _defaultColor;
            _hsv = _defaultHSV;
            UpdateText();
        }

        public void UpdateText()
        {
            if (hasAlpha)
            {
                text = _color.ToHexRGBA();
            }
            else
            {
                text = _color.ToHexRGB();
            }
        }

        public ColorFieldCache()
        {
        }

        public ColorFieldCache(string label, bool hasAlpha)
        {
            this.label = label;
            this.hasAlpha = hasAlpha;
        }
    }
}