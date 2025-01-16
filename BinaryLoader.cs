using System;

namespace COM3D2.MotionTimelineEditor
{
    public static class BinaryLoader
    {
        private static byte[] _fileBuffer;

        public static void ClearCache()
        {
            _fileBuffer = null;
        }

        public static byte[] ReadAFileBase(string filename)
        {
            return ReadAFileBase(filename, ref _fileBuffer);
        }

        public static byte[] ReadAFileBase(string filename, ref byte[] fileBuffer)
        {
            try
            {
                byte[] result;
                using (AFileBase afileBase = GameUty.FileOpen(filename, null))
                {
                    if (afileBase == null || !afileBase.IsValid() || afileBase.GetSize() == 0)
                    {
                        MTEUtils.LogWarning("AFileBase '" + filename + "' not found");
                        result = null;
                    }
                    else
                    {
                        if (fileBuffer == null)
                        {
                            fileBuffer = new byte[Math.Max(500000L, afileBase.GetSize())];
                        }
                        else if ((long)fileBuffer.Length < afileBase.GetSize())
                        {
                            fileBuffer = new byte[afileBase.GetSize()];
                        }
                        afileBase.Read(ref fileBuffer, afileBase.GetSize());
                        result = fileBuffer;
                    }
                }
                return result;
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                MTEUtils.LogError("Could not read file '" + filename + "'");
                return null;
            }
        }
    }
}