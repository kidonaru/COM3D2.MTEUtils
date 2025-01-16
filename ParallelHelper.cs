using System;
using System.Collections.Generic;
using System.Threading;

namespace COM3D2.MotionTimelineEditor
{
    public static class ParallelHelper
    {
        public static void ForEach<T>(List<T> items, System.Action<T> body)
        {
            int processorCount = System.Environment.ProcessorCount;
            var resetEvent = new ManualResetEvent(false);
            int remaining = processorCount;
            int itemCount = items.Count;

            for (int i = 0; i < processorCount; i++)
            {
                int index = i;
                ThreadPool.QueueUserWorkItem(state =>
                {
                    for (int j = index; j < itemCount; j += processorCount)
                    {
                        try
                        {
                            body(items[j]);
                        }
                        catch (Exception e)
                        {
                            MTEUtils.LogException(e);
                        }
                    }

                    if (Interlocked.Decrement(ref remaining) == 0)
                    {
                        resetEvent.Set();
                    }
                });
            }

            resetEvent.WaitOne();
        }
    }
}