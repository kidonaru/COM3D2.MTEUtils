using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{

    public class AnimationLayerInfo
    {
        public object original = null;

        public int layer = 0;

        private string _anmName = "";
        public string anmName
        {
            get => _anmName;
            set
            {
                if (_anmName != value)
                {
                    _anmName = value;
                    anmTag = value.ToLower();
                }
            }
        }

        public string anmTag { get; private set; }

        public float startTime = 0f;
        public float weight = 1f;
        public float speed = 1f;
        public bool loop = true;
        public bool overrideTime = false;

        public AnimationState state = null;

        public AnimationLayerInfo(int layer)
        {
            this.layer = layer;
        }

        public void Reset()
        {
            anmName = "";
            startTime = 0f;
            weight = 1f;
            speed = 1f;
            loop = true;
            overrideTime = false;
            state = null;
        }
    }
}