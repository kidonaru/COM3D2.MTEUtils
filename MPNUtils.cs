using System;
using System.Collections.Generic;
using System.Linq;

namespace COM3D2.MotionTimelineEditor
{
    public static class MPNUtils
    {
        private static readonly Dictionary<string, MPN> _mpnTypeMap =
                Enum.GetValues(typeof(MPN)).Cast<MPN>().ToDictionary(
                    mpn => mpn.ToString(),
                    mpn => mpn,
                    StringComparer.OrdinalIgnoreCase);

        public static MPN GetMPN(string mpnName)
        {
            return _mpnTypeMap.GetOrDefault(mpnName);
        }
    }
}