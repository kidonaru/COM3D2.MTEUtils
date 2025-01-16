using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    public static class MPNUtils
    {
        private static readonly Dictionary<string, MPN> _mpnTypeMap =
                Enum.GetValues(typeof(MPN)).Cast<MPN>().ToDictionary(mpn => mpn.ToString().ToLower(), mpn => mpn);

        public static MPN GetMPN(string mpnName)
        {
            return _mpnTypeMap.GetOrDefault(mpnName.ToLower());
        }

        private readonly static Dictionary<MPN, string> _mpnNameMap = new Dictionary<MPN, string>
        {
            // 顔
            { MPN.head, "顔" },
            { MPN.folder_mayu, "眉" },
            { MPN.folder_eye, "目" },
            { MPN.folder_eyewhite, "白目" },
            { MPN.eye_hi, "目ハイライト" },
            { MPN.hokuro, "ほくろ" },
            { MPN.lip, "唇" },
            { MPN.accha, "歯" },
            { MPN.nose, "鼻" },
            { MPN.folder_matsuge_up, "上まつげ" },
            { MPN.folder_matsuge_low, "下まつげ" },
            { MPN.folder_futae, "二重まぶた" },

            // 髪
            { MPN.hairf, "前髪" },
            { MPN.hairr, "後髪" },
            { MPN.hairs, "横髪" },
            { MPN.hairt, "エクステ髪" },
            { MPN.hairaho, "アホ毛" },

            // 体
            { MPN.folder_skin, "肌" },
            { MPN.chikubi, "乳首" },
            { MPN.chikubicolor, "乳首色" },
            { MPN.acctatoo, "タトゥー" },
            { MPN.folder_underhair, "アンダーヘア" },
            { MPN.body, "ボディ" },
            { MPN.accnail, "ネイル" },

            // 衣装
            { MPN.acchat, "帽子" },
            { MPN.headset, "ヘッドドレス" },
            { MPN.wear, "トップス" },
            { MPN.skirt, "ボトムス" },
            { MPN.onepiece, "ワンピース" },
            { MPN.mizugi, "水着" },
            { MPN.bra, "ブラジャー" },
            { MPN.panz, "パンツ" },
            { MPN.stkg, "靴下" },
            { MPN.shoes, "靴" },

            // アクセ
            { MPN.acckami, "前髪アクセ" },
            { MPN.megane, "メガネ" },
            { MPN.acchead, "アイマスク" },
            { MPN.acchana, "鼻アクセ" },
            { MPN.accmimi, "耳アクセ" },
            { MPN.glove, "手袋" },
            { MPN.acckubi, "ネックレス" },
            { MPN.acckubiwa, "チョーカー" },
            { MPN.acckamisub, "リボン" },
            { MPN.accnip, "乳首アクセ" },
            { MPN.accude, "腕" },
            { MPN.accheso, "へそアクセ" },
            { MPN.accashi, "足首" },
            { MPN.accsenaka, "背中" },
            { MPN.accshippo, "しっぽ" },
            { MPN.accvag, "前穴" },
            { MPN.accxxx, "前穴" },
            { MPN.accanl, "後穴" },

            // セット
            { MPN.set_maidwear, "メイド服セット" },
            { MPN.set_mywear, "私服セット" },
            { MPN.set_underwear, "下着セット" },
            { MPN.set_body, "身体セット" },
        };

        public static string GetMPNName(MPN mpn)
        {
            var name = _mpnNameMap.GetOrDefault(mpn);
            return string.IsNullOrEmpty(name) ? mpn.ToString() : name;
        }

        public enum MPNCategory
        {
            None,
            Face,
            Hair,
            Body,
            Wear,
            Accessory,
            Set,
        }

        private readonly static Dictionary<MPN, MPNCategory> _mpnCategoryMap = new Dictionary<MPN, MPNCategory>
        {
            // 顔
            { MPN.head, MPNCategory.Face },
            { MPN.folder_mayu, MPNCategory.Face },
            { MPN.folder_eye, MPNCategory.Face },
            { MPN.folder_eyewhite, MPNCategory.Face },
            { MPN.eye_hi, MPNCategory.Face },
            { MPN.hokuro, MPNCategory.Face },
            { MPN.lip, MPNCategory.Face },
            { MPN.accha, MPNCategory.Face },
            { MPN.nose, MPNCategory.Face },
            { MPN.folder_matsuge_up, MPNCategory.Face },
            { MPN.folder_matsuge_low, MPNCategory.Face },
            { MPN.folder_futae, MPNCategory.Face },
            
            // 髪
            { MPN.hairf, MPNCategory.Hair },
            { MPN.hairr, MPNCategory.Hair },
            { MPN.hairs, MPNCategory.Hair },
            { MPN.hairt, MPNCategory.Hair },
            { MPN.hairaho, MPNCategory.Hair },

            // 体
            { MPN.folder_skin, MPNCategory.Body },
            { MPN.chikubi, MPNCategory.Body },
            { MPN.chikubicolor, MPNCategory.Body },
            { MPN.acctatoo, MPNCategory.Body },
            { MPN.folder_underhair, MPNCategory.Body },
            { MPN.body, MPNCategory.Body },
            { MPN.accnail, MPNCategory.Body },

            // 衣装
            { MPN.acchat, MPNCategory.Wear },
            { MPN.headset, MPNCategory.Wear },
            { MPN.wear, MPNCategory.Wear },
            { MPN.skirt, MPNCategory.Wear },
            { MPN.onepiece, MPNCategory.Wear },
            { MPN.mizugi, MPNCategory.Wear },
            { MPN.bra, MPNCategory.Wear },
            { MPN.panz, MPNCategory.Wear },
            { MPN.stkg, MPNCategory.Wear },
            { MPN.shoes, MPNCategory.Wear },

            // アクセ
            { MPN.acckami, MPNCategory.Accessory },
            { MPN.megane, MPNCategory.Accessory },
            { MPN.acchead, MPNCategory.Accessory },
            { MPN.acchana, MPNCategory.Accessory },
            { MPN.accmimi, MPNCategory.Accessory },
            { MPN.glove, MPNCategory.Accessory },
            { MPN.acckubi, MPNCategory.Accessory },
            { MPN.acckubiwa, MPNCategory.Accessory },
            { MPN.acckamisub, MPNCategory.Accessory },
            { MPN.accnip, MPNCategory.Accessory },
            { MPN.accude, MPNCategory.Accessory },
            { MPN.accheso, MPNCategory.Accessory },
            { MPN.accashi, MPNCategory.Accessory },
            { MPN.accsenaka, MPNCategory.Accessory },
            { MPN.accshippo, MPNCategory.Accessory },
            { MPN.accvag, MPNCategory.Accessory },
            { MPN.accxxx, MPNCategory.Accessory },
            { MPN.accanl, MPNCategory.Accessory },

            // セット
            { MPN.set_maidwear, MPNCategory.Set },
            { MPN.set_mywear, MPNCategory.Set },
            { MPN.set_underwear, MPNCategory.Set },
            { MPN.set_body, MPNCategory.Set },
        };

        public static MPNCategory GetMPNCategory(MPN mpn)
        {
            return _mpnCategoryMap.GetOrDefault(mpn);
        }

        private readonly static Dictionary<MPNCategory, Color> _mpnCategoryColorMap = new Dictionary<MPNCategory, Color>
        {
            { MPNCategory.None, new Color(0.4f, 0.4f, 0.4f, 1f) },
            { MPNCategory.Face, new Color(0.75f, 0.3f, 0.3f, 1f) },
            { MPNCategory.Hair, new Color(0.6f, 0.4f, 0.2f, 1f) },
            { MPNCategory.Body, new Color(0.5f, 0.3f, 0.5f, 1f) },
            { MPNCategory.Wear, new Color(0.2f, 0.4f, 0.7f, 1f) },
            { MPNCategory.Accessory, new Color(0.35f, 0.65f, 0.35f, 1f) },
            { MPNCategory.Set, new Color(0.8f, 0.5f, 0.2f, 1f) },
        };

        public static Color GetMPNTagColor(MPN mpn)
        {
            var category = GetMPNCategory(mpn);
            return _mpnCategoryColorMap.GetOrDefault(category);
        }

        public static IEnumerable<MPN> GetAllMPN()
        {
            return _mpnNameMap.Keys;
        }

        /// <summary>
        /// 編集可能なMPNかどうかを取得
        /// （セットも含まれる）
        /// </summary>
        /// <param name="mpn"></param>
        /// <returns></returns>
        public static bool IsEditableMPN(MPN mpn)
        {
            return _mpnCategoryMap.ContainsKey(mpn);
        }

        /// <summary>
        /// 装備可能なMPNかどうかを取得
        /// （セットは除外される）
        /// </summary>
        /// <param name="mpn"></param>
        /// <returns></returns>
        public static bool IsEquippableMPN(MPN mpn)
        {
            var category = GetMPNCategory(mpn);
            return category != MPNCategory.None && category != MPNCategory.Set;
        }

        private readonly static HashSet<MPN> _folderMPNSet = new HashSet<MPN>
        {
            MPN.folder_futae,
            MPN.folder_matsuge_low,
            MPN.folder_matsuge_up,
            MPN.folder_eye,
            MPN.folder_mayu,
            MPN.folder_skin,
            MPN.folder_underhair,
            MPN.chikubi,
        };

        public static bool IsFolderMPN(MPN mpn)
        {
            return _folderMPNSet.Contains(mpn);
        }
    }
}