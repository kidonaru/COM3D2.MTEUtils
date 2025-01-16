using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    public static class TextureLoader
    {
        private static byte[] m_texTempFile;

        public static void ClearCache()
        {
            m_texTempFile = null;
        }

        public static TextureResource LoadTextureFile(string f_strFileName)
        {
            try
            {
                using (var aFileBase = GameUty.FileOpen(f_strFileName, GameUty.FileSystem))
                {
                    if (!aFileBase.IsValid())
                    {
                        MTEUtils.LogError("LoadTexture テクスチャコンテナが読めません。 :" + f_strFileName);
                        return null;
                    }

                    return LoadTextureFile(aFileBase);
                }
            }
            catch (Exception ex)
            {
                MTEUtils.LogException(ex);
                return null;
            }
        }

        public static TextureResource LoadTextureFile(AFileBase file)
        {
            if (file == null || !file.IsValid())
            {
                return null;
            }

            if (m_texTempFile == null)
            {
                m_texTempFile = new byte[Math.Max(500000, file.GetSize())];
            }
            else if (m_texTempFile.Length < file.GetSize())
            {
                m_texTempFile = new byte[file.GetSize()];
            }

            file.Read(ref m_texTempFile, file.GetSize());
            BinaryReader binaryReader = new BinaryReader(new MemoryStream(m_texTempFile), Encoding.UTF8);
            string text = binaryReader.ReadString();
            if (text != "CM3D2_TEX")
            {
                MTEUtils.LogError("ヘッダーファイルが不正です。" + text);
                binaryReader.Close();
                binaryReader = null;
                return null;
            }

            int num = binaryReader.ReadInt32();
            binaryReader.ReadString();

            int width = 0;
            int height = 0;
            TextureFormat textureFormat = TextureFormat.ARGB32;
            Rect[] array = null;
            if (1010 <= num)
            {
                if (1011 <= num)
                {
                    int num2 = binaryReader.ReadInt32();
                    if (0 < num2)
                    {
                        array = new Rect[num2];
                        for (int i = 0; i < num2; i++)
                        {
                            float x = binaryReader.ReadSingle();
                            float y = binaryReader.ReadSingle();
                            float width2 = binaryReader.ReadSingle();
                            float height2 = binaryReader.ReadSingle();
                            ref Rect reference = ref array[i];
                            reference = new Rect(x, y, width2, height2);
                        }
                    }
                }

                width = binaryReader.ReadInt32();
                height = binaryReader.ReadInt32();
                textureFormat = (TextureFormat)binaryReader.ReadInt32();
            }

            int num3 = binaryReader.ReadInt32();
            byte[] array2 = new byte[num3];
            binaryReader.Read(array2, 0, num3);

            if (num == 1000)
            {
                width = (array2[16] << 24) | (array2[17] << 16) | (array2[18] << 8) | array2[19];
                height = (array2[20] << 24) | (array2[21] << 16) | (array2[22] << 8) | array2[23];
            }

            binaryReader.Close();
            binaryReader = null;
            return new TextureResource(width, height, textureFormat, array, array2);
        }
    }
}