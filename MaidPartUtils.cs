using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    public static class MaidPartUtils
    {
        public static readonly List<MaidPartType> allMaidPartTypes
            = MTEUtils.GetEnumValues<MaidPartType>().ToList();

        private static List<MaidPartType> _equippableMaidPartTypes = null;
        public static List<MaidPartType> equippableMaidPartTypes
        {
            get
            {
                if (_equippableMaidPartTypes == null)
                {
                    _equippableMaidPartTypes = allMaidPartTypes.Where(IsEquippableType).ToList();
                }

                return _equippableMaidPartTypes;
            }
        }

        private static readonly Dictionary<MaidPartType, MPN> _toMpnMap =
                Enum.GetValues(typeof(MaidPartType)).Cast<MaidPartType>().ToDictionary(
                    type => type,
                    type => MPNUtils.GetMPN(type.ToString()));

        public static MPN ToMPN(this MaidPartType type)
        {
            if (type == MaidPartType.null_mpn)
            {
                return MPN.null_mpn;
            }

            return _toMpnMap.GetOrDefault(type);
        }

        private static readonly Dictionary<string, MaidPartType> _maidPartTypeMap =
                Enum.GetValues(typeof(MaidPartType)).Cast<MaidPartType>().ToDictionary(
                    type => type.ToString(),
                    type => type,
                    StringComparer.OrdinalIgnoreCase);


        public static MaidPartType ToMaidPartType(this string name)
        {
            return _maidPartTypeMap.GetOrDefault(name);
        }

        private static readonly Dictionary<MaidPartType, string> _maidPartNameMap =
                Enum.GetValues(typeof(MaidPartType)).Cast<MaidPartType>().ToDictionary(
                    type => type,
                    type => type.ToString());

        public static string GetMaidPartName(MaidPartType type)
        {
            return _maidPartNameMap.GetOrDefault(type);
        }

        public readonly static Dictionary<MaidPartType, string> maidPartJpNameMap = new Dictionary<MaidPartType, string>
        {
            // 顔
            { MaidPartType.head, "顔" },
            { MaidPartType.facegloss, "頬" },
            { MaidPartType.folder_mayu, "眉" },
            { MaidPartType.folder_matsuge_up, "上まつ毛" },
            { MaidPartType.folder_matsuge_low, "下まつ毛" },
            { MaidPartType.folder_futae, "二重" },
            { MaidPartType.folder_eye, "目" },
            { MaidPartType.eye_hi, "目ハイライト" },
            { MaidPartType.folder_eyewhite, "白目" },
            { MaidPartType.nose, "鼻" },
            { MaidPartType.hokuro, "ほくろ" },
            { MaidPartType.lip, "唇" },
            { MaidPartType.accha, "歯" },

            // 髪
            { MaidPartType.hairf, "前髪" },
            { MaidPartType.hairr, "後髪" },
            { MaidPartType.hairs, "横髪" },
            { MaidPartType.hairt, "エクステ髪" },
            { MaidPartType.hairaho, "アホ毛" },

            // 体
            { MaidPartType.folder_skin, "肌" },
            { MaidPartType.chikubi, "乳首" },
            { MaidPartType.acctatoo, "タトゥー" },
            { MaidPartType.accnail, "ネイル" },
            { MaidPartType.folder_underhair, "アンダーヘア" },
            { MaidPartType.body, "ボディ" },

            // 服装
            { MaidPartType.acchat, "帽子" },
            { MaidPartType.headset, "ヘッドドレス" },
            { MaidPartType.wear, "トップス" },
            { MaidPartType.skirt, "ボトムス" },
            { MaidPartType.onepiece, "ワンピース" },
            { MaidPartType.mizugi, "水着" },
            { MaidPartType.bra, "ブラジャー" },
            { MaidPartType.panz, "パンツ" },
            { MaidPartType.stkg, "靴下" },
            { MaidPartType.shoes, "靴" },

            // アクセ
            { MaidPartType.acckami, "前髪アクセ" },
            { MaidPartType.megane, "メガネ" },
            { MaidPartType.acchead, "アイマスク" },
            { MaidPartType.acchana, "鼻アクセ" },
            { MaidPartType.accmimi, "耳アクセ" },
            { MaidPartType.glove, "手袋" },
            { MaidPartType.acckubi, "ネックレス" },
            { MaidPartType.acckubiwa, "チョーカー" },
            { MaidPartType.acckamisub, "リボン" },
            { MaidPartType.accnip, "乳首アクセ" },
            { MaidPartType.accude, "腕" },
            { MaidPartType.accheso, "へそアクセ" },
            { MaidPartType.accashi, "足首" },
            { MaidPartType.accsenaka, "背中" },
            { MaidPartType.accshippo, "しっぽ" },
            { MaidPartType.accvag, "前穴" },
            { MaidPartType.accxxx, "前穴" },
            { MaidPartType.accanl, "後穴" },

            // セット
            { MaidPartType.set_body, "体型" },
            { MaidPartType.set_maidwear, "メイド服" },
            { MaidPartType.set_mywear, "私服" },
            { MaidPartType.set_underwear, "下着" },
        };

        public static string GetMaidPartJpName(MaidPartType type)
        {
            var name = maidPartJpNameMap.GetOrDefault(type);
            return string.IsNullOrEmpty(name) ? type.ToString() : name;
        }

        public enum MaidPartCategory
        {
            None,
            Face,
            Hair,
            Body,
            Wear,
            Accessory,
            Set,
        }

        private readonly static Dictionary<MaidPartType, MaidPartCategory> _maidPartCategoryMap = new Dictionary<MaidPartType, MaidPartCategory>
        {
            // 顔
            { MaidPartType.head, MaidPartCategory.Face },
            { MaidPartType.facegloss, MaidPartCategory.Face },
            { MaidPartType.folder_mayu, MaidPartCategory.Face },
            { MaidPartType.folder_matsuge_up, MaidPartCategory.Face },
            { MaidPartType.folder_matsuge_low, MaidPartCategory.Face },
            { MaidPartType.folder_futae, MaidPartCategory.Face },
            { MaidPartType.folder_eye, MaidPartCategory.Face },
            { MaidPartType.eye_hi, MaidPartCategory.Face },
            { MaidPartType.folder_eyewhite, MaidPartCategory.Face },
            { MaidPartType.nose, MaidPartCategory.Face },
            { MaidPartType.hokuro, MaidPartCategory.Face },
            { MaidPartType.lip, MaidPartCategory.Face },
            { MaidPartType.accha, MaidPartCategory.Face },

            // 髪
            { MaidPartType.hairf, MaidPartCategory.Hair },
            { MaidPartType.hairr, MaidPartCategory.Hair },
            { MaidPartType.hairs, MaidPartCategory.Hair },
            { MaidPartType.hairt, MaidPartCategory.Hair },
            { MaidPartType.hairaho, MaidPartCategory.Hair },

            // 体
            { MaidPartType.folder_skin, MaidPartCategory.Body },
            { MaidPartType.chikubi, MaidPartCategory.Body },
            { MaidPartType.acctatoo, MaidPartCategory.Body },
            { MaidPartType.folder_underhair, MaidPartCategory.Body },
            { MaidPartType.body, MaidPartCategory.Body },
            { MaidPartType.accnail, MaidPartCategory.Body },

            // 衣装
            { MaidPartType.acchat, MaidPartCategory.Wear },
            { MaidPartType.headset, MaidPartCategory.Wear },
            { MaidPartType.wear, MaidPartCategory.Wear },
            { MaidPartType.skirt, MaidPartCategory.Wear },
            { MaidPartType.onepiece, MaidPartCategory.Wear },
            { MaidPartType.mizugi, MaidPartCategory.Wear },
            { MaidPartType.bra, MaidPartCategory.Wear },
            { MaidPartType.panz, MaidPartCategory.Wear },
            { MaidPartType.stkg, MaidPartCategory.Wear },
            { MaidPartType.shoes, MaidPartCategory.Wear },

            // アクセ
            { MaidPartType.acckami, MaidPartCategory.Accessory },
            { MaidPartType.megane, MaidPartCategory.Accessory },
            { MaidPartType.acchead, MaidPartCategory.Accessory },
            { MaidPartType.acchana, MaidPartCategory.Accessory },
            { MaidPartType.accmimi, MaidPartCategory.Accessory },
            { MaidPartType.glove, MaidPartCategory.Accessory },
            { MaidPartType.acckubi, MaidPartCategory.Accessory },
            { MaidPartType.acckubiwa, MaidPartCategory.Accessory },
            { MaidPartType.acckamisub, MaidPartCategory.Accessory },
            { MaidPartType.accnip, MaidPartCategory.Accessory },
            { MaidPartType.accude, MaidPartCategory.Accessory },
            { MaidPartType.accheso, MaidPartCategory.Accessory },
            { MaidPartType.accashi, MaidPartCategory.Accessory },
            { MaidPartType.accsenaka, MaidPartCategory.Accessory },
            { MaidPartType.accshippo, MaidPartCategory.Accessory },
            { MaidPartType.accvag, MaidPartCategory.Accessory },
            { MaidPartType.accxxx, MaidPartCategory.Accessory },
            { MaidPartType.accanl, MaidPartCategory.Accessory },

            // セット
            { MaidPartType.set_body, MaidPartCategory.Set },
            { MaidPartType.set_maidwear, MaidPartCategory.Set },
            { MaidPartType.set_mywear, MaidPartCategory.Set },
            { MaidPartType.set_underwear, MaidPartCategory.Set },
        };

        public static MaidPartCategory GetMaidPartCategory(MaidPartType type)
        {
            return _maidPartCategoryMap.GetOrDefault(type);
        }

        private readonly static Dictionary<MaidPartCategory, Color> _maidPartCategoryColorMap = new Dictionary<MaidPartCategory, Color>
        {
            { MaidPartCategory.None, new Color(0.4f, 0.4f, 0.4f, 1f) },
            { MaidPartCategory.Face, new Color(0.75f, 0.3f, 0.3f, 1f) },
            { MaidPartCategory.Hair, new Color(0.6f, 0.4f, 0.2f, 1f) },
            { MaidPartCategory.Body, new Color(0.5f, 0.3f, 0.5f, 1f) },
            { MaidPartCategory.Wear, new Color(0.2f, 0.4f, 0.7f, 1f) },
            { MaidPartCategory.Accessory, new Color(0.35f, 0.65f, 0.35f, 1f) },
            { MaidPartCategory.Set, new Color(0.8f, 0.5f, 0.2f, 1f) },
        };

        public static Color GetMaidPartColor(MaidPartType type, float alpha)
        {
            var category = GetMaidPartCategory(type);
            var color = _maidPartCategoryColorMap.GetOrDefault(category);
            color.a = alpha;
            return color;
        }

        public static IEnumerable<MaidPartType> GetAllMaidPartType()
        {
            return maidPartJpNameMap.Keys;
        }

        /// <summary>
        /// 編集可能なMPNかどうかを取得
        /// （セットも含まれる）
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsEditableType(MaidPartType type)
        {
            return _maidPartCategoryMap.ContainsKey(type);
        }

        /// <summary>
        /// 装備可能なMPNかどうかを取得
        /// （セットは除外される）
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsEquippableType(MaidPartType type)
        {
            var category = GetMaidPartCategory(type);
            return category != MaidPartCategory.None && category != MaidPartCategory.Set;
        }

        private readonly static HashSet<MaidPartType> _folderTypeSet = new HashSet<MaidPartType>
        {
            MaidPartType.folder_futae,
            MaidPartType.folder_matsuge_low,
            MaidPartType.folder_matsuge_up,
            MaidPartType.folder_eye,
            MaidPartType.folder_mayu,
            MaidPartType.folder_skin,
            MaidPartType.folder_underhair,
            MaidPartType.chikubi,
        };

        public static bool IsFolderType(MaidPartType mpn)
        {
            return _folderTypeSet.Contains(mpn);
        }
    }
}