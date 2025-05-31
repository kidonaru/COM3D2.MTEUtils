using System.Collections.Generic;
using System.Text;

namespace COM3D2.MotionTimelineEditor
{
    // 自然順序での比較
    public class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int len1 = x.Length;
            int len2 = y.Length;
            int marker1 = 0;
            int marker2 = 0;

            while (marker1 < len1 && marker2 < len2)
            {
                char ch1 = x[marker1];
                char ch2 = y[marker2];

                var space1 = new StringBuilder();
                var space2 = new StringBuilder();

                while (marker1 < len1 && char.IsDigit(x[marker1]))
                    space1.Append(x[marker1++]);
                while (marker2 < len2 && char.IsDigit(y[marker2]))
                    space2.Append(y[marker2++]);

                if (space1.Length > 0 && space2.Length > 0)
                {
                    int num1, num2;
                    if (int.TryParse(space1.ToString(), out num1) && 
                        int.TryParse(space2.ToString(), out num2))
                    {
                        int result = num1.CompareTo(num2);
                        if (result != 0) return result;
                    }
                }

                if (ch1 != ch2) return ch1.CompareTo(ch2);
                marker1++;
                marker2++;
            }

            return len1 - len2;
        }
    }
}