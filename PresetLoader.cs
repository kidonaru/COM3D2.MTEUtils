using System;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    public class PresetData
    {
        public CharacterMgr.Preset preset;
        public byte[] textureBytes;
        public long lastWriteAt;
    }

    public static class PresetLoader
    {
        private static byte[] m_tempBuffer;

        public static void ClearCache()
        {
            m_tempBuffer = null;
        }

        public static PresetData Load(string f_strFileName)
        {
            FileStream fileStream = new FileStream(f_strFileName, FileMode.Open);
            if (fileStream == null)
            {
                return null;
            }

            var lastWriteAt = File.GetLastWriteTime(f_strFileName).Ticks;

            byte[] buffer = m_tempBuffer;
            if (buffer == null || buffer.Length < fileStream.Length)
            {
                buffer = new byte[fileStream.Length];
                m_tempBuffer = buffer;
            }

            fileStream.Read(buffer, 0, (int)fileStream.Length);
            fileStream.Close();
            fileStream.Dispose();
            fileStream = null;

            BinaryReader binaryReader = new BinaryReader(new MemoryStream(buffer));
            PresetData result = Load(binaryReader, Path.GetFileName(f_strFileName), lastWriteAt);
            binaryReader.Close();
            binaryReader = null;
            return result;
        }

        private static PresetData Load(BinaryReader brRead, string f_strFileName, long lastWriteAt)
        {
            var preset = new CharacterMgr.Preset();
            preset.strFileName = f_strFileName;
            string text = brRead.ReadString();
            NDebug.Assert(text == "CM3D2_PRESET", "プリセットファイルのヘッダーが不正です。");
            int num = (preset.nVersion = brRead.ReadInt32());
            if (preset.nVersion >= 30000 || (1560 <= preset.nVersion && preset.nVersion < 20000))
            {
                Debug.LogWarning("プリセットファイル[" + f_strFileName + "]のバージョンは" + preset.nVersion + "のためカスタムオーダーメイド3D2では使用できません。読み込みを中止しました。");
                return null;
            }

            int ePreType = brRead.ReadInt32();
            preset.ePreType = (CharacterMgr.PresetType)ePreType;
            int num2 = brRead.ReadInt32();
            byte[] textureBytes = null;
            if (num2 != 0)
            {
                textureBytes = brRead.ReadBytes(num2);
            }

            preset.texThum = null;
            preset.listMprop = Maid.DeserializePropPre(brRead);
            if (num >= 2)
            {
                preset.aryPartsColor = Maid.DeserializeMultiColorPre(brRead);
            }

            if (num >= 200)
            {
                Maid.DeserializeBodyPre(brRead);
            }

            return new PresetData
            {
                preset = preset,
                textureBytes = textureBytes,
                lastWriteAt = lastWriteAt,
            };
        }
    }
}
