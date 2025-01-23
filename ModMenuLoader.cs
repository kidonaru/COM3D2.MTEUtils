using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace COM3D2.MotionTimelineEditor
{
    public class MenuInfo
    {
        public static int CacheVersion = 2;

        public string fileName;
        public string path;
        public int rid;
        public string name;
        public string setumei;
        public MaidPartType maidPartType = MaidPartType.null_mpn;
        public string iconName;
        public byte[] iconData = new byte[0];
        public float priority;
        public string modelFileName;
        public MaidPartType colorSetMaidPartType = MaidPartType.null_mpn;
        public string colorSetMenuName;
        public string variationBaseFileName;
        public string modBaseFileName;
        public long lastWriteAt;
        public bool isHidden;
        public bool isOfficial;
        public bool isMan;

        private MPN _mpn = MPN.null_mpn;
        public MPN mpn
        {
            get
            {
                if (_mpn == MPN.null_mpn)
                {
                    _mpn = maidPartType.ToMPN();
                }
                return _mpn;
            }
        }

        private MPN _colorSetMPN = MPN.null_mpn;
        public MPN colorSetMPN
        {
            get
            {
                if (_colorSetMPN == MPN.null_mpn)
                {
                    _colorSetMPN = colorSetMaidPartType.ToMPN();
                }
                return _colorSetMPN;
            }
        }

        public static MenuInfo Deserialize(BinaryReader binaryReader)
        {
            return new MenuInfo
            {
                fileName = binaryReader.ReadNullableString(),
                path = binaryReader.ReadNullableString(),
                rid = binaryReader.ReadInt32(),
                name = binaryReader.ReadNullableString(),
                setumei = binaryReader.ReadNullableString(),
                maidPartType = (MaidPartType)binaryReader.ReadInt32(),
                iconName = binaryReader.ReadNullableString(),
                iconData = binaryReader.ReadBytes(binaryReader.ReadInt32()),
                priority = binaryReader.ReadSingle(),
                modelFileName = binaryReader.ReadNullableString(),
                colorSetMaidPartType = (MaidPartType)binaryReader.ReadInt32(),
                colorSetMenuName = binaryReader.ReadNullableString(),
                variationBaseFileName = binaryReader.ReadNullableString(),
                modBaseFileName = binaryReader.ReadNullableString(),
                lastWriteAt = binaryReader.ReadInt64(),
                isHidden = binaryReader.ReadBoolean(),
                isOfficial = binaryReader.ReadBoolean(),
                isMan = binaryReader.ReadBoolean(),
            };
        }

        public void Serialize(BinaryWriter binaryWriter)
        {
            binaryWriter.WriteNullableString(fileName);
            binaryWriter.WriteNullableString(path);
            binaryWriter.Write(rid);
            binaryWriter.WriteNullableString(name);
            binaryWriter.WriteNullableString(setumei);
            binaryWriter.Write((int)maidPartType);
            binaryWriter.WriteNullableString(iconName);
            binaryWriter.Write(iconData.Length);
            binaryWriter.Write(iconData);
            binaryWriter.Write(priority);
            binaryWriter.WriteNullableString(modelFileName);
            binaryWriter.Write((int)colorSetMaidPartType);
            binaryWriter.WriteNullableString(colorSetMenuName);
            binaryWriter.WriteNullableString(variationBaseFileName);
            binaryWriter.WriteNullableString(modBaseFileName);
            binaryWriter.Write(lastWriteAt);
            binaryWriter.Write(isHidden);
            binaryWriter.Write(isOfficial);
            binaryWriter.Write(isMan);
        }

        public void Dump()
        {
            MTEUtils.Log("fileName: {0}", fileName);
            MTEUtils.Log("path: {0}", path);
            MTEUtils.Log("rid: {0}", rid);
            MTEUtils.Log("name: {0}", name);
            MTEUtils.Log("setumei: {0}", setumei);
            MTEUtils.Log("mpn: {0}", maidPartType);
            MTEUtils.Log("iconName: {0}", iconName);
            MTEUtils.Log("iconData: {0}", iconData.Length);
            MTEUtils.Log("priority: {0}", priority);
            MTEUtils.Log("modelFileName: {0}", modelFileName);
            MTEUtils.Log("colorSetMPN: {0}", colorSetMaidPartType);
            MTEUtils.Log("colorSetMenuName: {0}", colorSetMenuName);
            MTEUtils.Log("variationBaseFileName: {0}", variationBaseFileName);
            MTEUtils.Log("modBaseFileName: {0}", modBaseFileName);
            MTEUtils.Log("lastWriteAt: {0}", lastWriteAt);
            MTEUtils.Log("isHidden: {0}", isHidden);
            MTEUtils.Log("isOfficial: {0}", isOfficial);
            MTEUtils.Log("isMan: {0}", isMan);
        }
    }

    public static class ModMenuLoader
    {
        private static Dictionary<string, MenuInfo> menuCache = new Dictionary<string, MenuInfo>(32);
        private static byte[] _fileBuffer;

        public static void ClearCache()
        {
            menuCache.Clear();
            _fileBuffer = null;
        }

        public static MenuInfo Load(string menuFileName)
        {
            MenuInfo menu;
            if (menuCache.TryGetValue(menuFileName, out menu))
            {
                return menu;
            }

            menu = LoadDirect(menuFileName, string.Empty, false, ref _fileBuffer);
            menuCache[menuFileName] = menu;

            return menu;
        }

        private static readonly Regex _variationRegex = new Regex("_z\\d{1,4}", RegexOptions.Compiled);

        public static MenuInfo LoadDirect(string menuFileName, string menuFilePath = "", bool isOfficial = false)
        {
            return LoadDirect(menuFileName, menuFilePath, isOfficial, ref _fileBuffer);
        }

        public static MenuInfo LoadDirect(
            string menuFileName,
            string menuFilePath,
            bool isOfficial,
            ref byte[] fileBuffer)
        {
            if (menuFileName.IndexOf("mod_") == 0)
            {
                return LoadModDirect(menuFileName, menuFilePath, ref fileBuffer);
            }

            try
            {
                byte[] buffer = BinaryLoader.ReadAFileBase(menuFileName, ref fileBuffer);
                if (buffer == null)
                {
                    return null;
                }

                var menu = new MenuInfo
                {
                    fileName = menuFileName.ToLower(),
                    rid = menuFileName.GetHashCode(),
                    isOfficial = isOfficial,
                };

                using (var reader = new BinaryReader(new MemoryStream(buffer), Encoding.UTF8))
                {
                    if (!(reader.ReadString() == "CM3D2_MENU"))
                    {
                        return null;
                    }
                    reader.ReadInt32();
                    menu.path = reader.ReadString();
                    menu.name = reader.ReadString();
                    reader.ReadString();
                    reader.ReadString();
                    reader.ReadInt32();

                    for (;;)
                    {
                        byte b = reader.ReadByte();
                        string text = string.Empty;
                        if (b == 0)
                        {
                            break;
                        }
                        for (int i = 0; i < (int)b; i++)
                        {
                            text = text + "\"" + reader.ReadString() + "\"";
                        }
                        if (!string.IsNullOrEmpty(text))
                        {
                            string stringCom = UTY.GetStringCom(text);
                            string[] stringList = UTY.GetStringList(text);
                            if (stringCom == "end")
                            {
                                break;
                            }

                            if (stringCom == "name")
                            {
                                if (stringList.Length > 1 && !string.IsNullOrEmpty(stringList[1]))
                                {
                                    menu.name = stringList[1];
                                }
                            }
                            else if (stringCom == "setumei")
                            {
                                if (stringList.Length > 1 && !string.IsNullOrEmpty(stringList[1]))
                                {
                                    menu.setumei = stringList[1];
                                }
                            }
                            else if (stringCom == "category")
                            {
                                if (stringList.Length > 1 && !string.IsNullOrEmpty(stringList[1]))
                                {
                                    menu.maidPartType = MaidPartUtils.GetMaidPartType(stringList[1]);
                                }
                            }
                            else if (stringCom == "icons" || stringCom == "icon")
                            {
                                if (stringList.Length > 1 && !string.IsNullOrEmpty(stringList[1]))
                                {
                                    menu.iconName = stringList[1];
                                }
                            }
                            else if (stringCom == "priority")
                            {
                                if (stringList.Length > 1 && !string.IsNullOrEmpty(stringList[1]))
                                {
                                    menu.priority = float.Parse(stringList[1]);
                                }
                            }
                            else if (stringCom == "additem")
                            {
                                if (stringList.Length > 1 && !string.IsNullOrEmpty(stringList[1]))
                                {
                                    menu.modelFileName = stringList[1];
                                }
                            }
                            else if (stringCom == "color_set")
                            {
                                if (stringList.Length > 2 && !string.IsNullOrEmpty(stringList[1]))
                                {
                                    menu.colorSetMaidPartType = MaidPartUtils.GetMaidPartType(stringList[1]);
                                    menu.colorSetMenuName = stringList[2].ToLower();
                                }
                            }
                            else if (stringCom == "メニューフォルダ")
                            {
                                if (stringList.Length > 1 && stringList[1].ToLower() == "man")
                                {
                                    menu.isMan = true;
                                }
                            }
                        }
                    }
                }

                FixMenu(menu, menuFileName);

                return menu;
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                return null;
            }
        }

        public static MenuInfo LoadModDirect(
            string menuFileName,
            string menuFilePath,
            ref byte[] fileBuffer)
        {
            try
            {
                var menu = new MenuInfo
                {
                    fileName = menuFileName.ToLower(),
                    rid = menuFileName.GetHashCode(),
                };

                menu.priority = 1000f;

                using (FileStream fileStream = new FileStream(menuFilePath, FileMode.Open))
                {
                    if (fileStream == null)
                    {
                        return null;
                    }

                    if (fileStream.Length > fileBuffer.Length)
                    {
                        fileBuffer = new byte[fileStream.Length];
                    }

                    fileStream.Read(fileBuffer, 0, (int)fileStream.Length);
                }

                if (fileBuffer == null)
                {
                    return null;
                }
                using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(fileBuffer), Encoding.UTF8))
                {
                    if (binaryReader.ReadString() != "CM3D2_MOD")
                    {
                        return null;
                    }
                    binaryReader.ReadInt32();
                    menu.iconName = binaryReader.ReadString();
                    menu.modBaseFileName = binaryReader.ReadString();
                    menu.name = binaryReader.ReadString();
                    menu.maidPartType = binaryReader.ReadMPN();
                    menu.setumei = binaryReader.ReadString();
                    menu.colorSetMaidPartType = binaryReader.ReadMPN();
                    if (menu.colorSetMaidPartType != MaidPartType.null_mpn)
                    {
                        menu.colorSetMenuName = binaryReader.ReadString();
                    }

                    binaryReader.ReadString();

                    int num = binaryReader.ReadInt32();
                    for (int j = 0; j < num; j++)
                    {
                        var key = binaryReader.ReadString();
                        var count = binaryReader.ReadInt32();
                        var data = binaryReader.ReadBytes(count);
                        if (string.Compare(key, menu.iconName, true) == 0)
                        {
                            menu.iconData = data;
                        }
                    }
                }

                FixMenu(menu, menuFileName);

                return menu;
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                return null;
            }
        }

        private static void FixMenu(MenuInfo menu, string menuFileName)
        {
            // メニュー名がない場合はファイル名を使用
            if (string.IsNullOrEmpty(menu.name))
            {
                menu.name = Path.GetFileNameWithoutExtension(menuFileName);
            }

            // パス修正
            menu.path = FixMenuPath(menu.path, menuFileName);

            // 説明の改行
            if (!string.IsNullOrEmpty(menu.setumei))
            {
                menu.setumei = menu.setumei.Replace("《改行》", "\n");
            }

            // バリデーション元メニュー名検索
            if (_variationRegex.IsMatch(menu.fileName))
            {
                var baseFileName = _variationRegex.Replace(menu.fileName, "");
                baseFileName = baseFileName.Replace("_i.", "_i_.");
                menu.variationBaseFileName = baseFileName;
            }

            // 非表示メニュー
            if (!MaidPartUtils.IsEditableType(menu.maidPartType) ||
                !string.IsNullOrEmpty(menu.variationBaseFileName) ||
                menu.fileName.Contains("_zurashi") ||
                menu.fileName.Contains("_mekure") ||
                menu.fileName.Contains("_porori") ||
                menu.fileName.Contains("_back"))
            {
                menu.isHidden = true;
            }
        }

        private static string FixMenuPath(string menuPath, string menuFileName)
        {
            try
            {
                if (string.IsNullOrEmpty(menuPath))
                {
                    menuPath = string.Empty;
                }
                else
                {
                    menuPath = menuPath.Replace("assets/", "")
                            .Replace("menu/", "")
                            .Replace("parts/", "")
                            .Replace("dress/", "");
                    menuPath = menuPath.Replace('/', Path.DirectorySeparatorChar);
                    menuPath = Path.Combine(Path.GetDirectoryName(menuPath), menuFileName);
                }
            }
            catch (Exception)
            {
                menuPath = string.Empty;
            }

            if (!menuPath.Contains(Path.DirectorySeparatorChar))
            {
                menuPath = Path.Combine("menu", menuFileName);
            }

            return menuPath;
        }
    }
}