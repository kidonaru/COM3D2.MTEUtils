using System;
using System.Collections.Generic;
using System.Reflection;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// 構造が同じだが CLR 上は別型になるクラス間で、同名 public フィールドを写す。
    /// プラグインごとに同じソースをコンパイルする DTO の受け渡しに使う。
    /// 型ペアごとにフィールド対応をキャッシュするため、毎フレーム呼んでも
    /// リフレクション探索は初回だけで済む
    /// </summary>
    public static class ReflectionFieldCopier
    {
        private struct TypePair : IEquatable<TypePair>
        {
            public readonly Type src;
            public readonly Type dst;

            public TypePair(Type src, Type dst)
            {
                this.src = src;
                this.dst = dst;
            }

            public bool Equals(TypePair other)
            {
                return src == other.src && dst == other.dst;
            }

            public override bool Equals(object obj)
            {
                return obj is TypePair && Equals((TypePair)obj);
            }

            public override int GetHashCode()
            {
                return src.GetHashCode() ^ (dst.GetHashCode() << 1);
            }
        }

        private class FieldPair
        {
            public FieldInfo src;
            public FieldInfo dst;
        }

        private static readonly Dictionary<TypePair, List<FieldPair>> _cache
            = new Dictionary<TypePair, List<FieldPair>>();

        /// <summary>
        /// src の public インスタンスフィールドを、dst の同名・代入可能なフィールドへ写す。
        /// 対応の無いフィールドは黙って無視する (dst 側は既定値のまま残る)
        /// </summary>
        public static void Copy(object src, object dst)
        {
            if (src == null || dst == null)
            {
                return;
            }

            foreach (var pair in GetFieldPairs(src.GetType(), dst.GetType()))
            {
                pair.dst.SetValue(dst, pair.src.GetValue(src));
            }
        }

        /// <summary>
        /// src にあって dst に写せなかった public インスタンスフィールド名を返す。
        /// 名前がずれた値は例外を出さず既定値のまま素通りするため、
        /// 接続時の自己診断でこれを出して気付けるようにする
        /// </summary>
        public static List<string> FindUnmappedFields(Type srcType, Type dstType)
        {
            var mapped = new List<string>();
            foreach (var pair in GetFieldPairs(srcType, dstType))
            {
                mapped.Add(pair.src.Name);
            }

            var unmapped = new List<string>();
            foreach (var field in srcType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!mapped.Contains(field.Name))
                {
                    unmapped.Add(field.Name);
                }
            }
            return unmapped;
        }

        private static List<FieldPair> GetFieldPairs(Type srcType, Type dstType)
        {
            var key = new TypePair(srcType, dstType);
            List<FieldPair> pairs;
            if (_cache.TryGetValue(key, out pairs))
            {
                return pairs;
            }

            pairs = new List<FieldPair>();
            var flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var srcField in srcType.GetFields(flags))
            {
                var dstField = dstType.GetField(srcField.Name, flags);
                if (dstField == null)
                {
                    continue;
                }
                // 別アセンブリの同名 struct (Color / Vector3 等) は同一型に解決されるが、
                // 型が食い違う場合は写すと例外になるため弾く
                if (!dstField.FieldType.IsAssignableFrom(srcField.FieldType))
                {
                    continue;
                }
                pairs.Add(new FieldPair { src = srcField, dst = dstField });
            }

            _cache[key] = pairs;
            return pairs;
        }
    }
}
