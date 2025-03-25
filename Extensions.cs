using System.Reflection;
using System.Collections.Generic;
using RootMotion.FinalIK;
using UnityEngine;
using System.Linq;
using System;
using System.IO;

namespace COM3D2.MotionTimelineEditor
{
    public static partial class Extensions
    {
        public static void ResizeTexture(
            this Texture2D sourceTexture,
            int targetWidth,
            int targetHeight)
        {
            float sourceAspect = (float)sourceTexture.width / sourceTexture.height;
            float targetAspect = (float)targetWidth / targetHeight;

            int width, height;
            if (sourceAspect > targetAspect)
            {
                width = targetWidth;
                height = (int)(width / sourceAspect);
            }
            else
            {
                height = targetHeight;
                width = (int)(height * sourceAspect);
            }
            TextureScale.Bilinear(sourceTexture, width, height);
        }

        public static Texture2D ResizeAndCropTexture(
            this Texture2D sourceTexture,
            int targetWidth,
            int targetHeight)
        {
            float sourceAspect = (float)sourceTexture.width / sourceTexture.height;
            float targetAspect = (float)targetWidth / targetHeight;

            int width, height;
            if (sourceAspect > targetAspect)
            {
                height = targetHeight;
                width = (int)(sourceTexture.width * ((float)targetHeight / sourceTexture.height));
            }
            else
            {
                width = targetWidth;
                height = (int)(sourceTexture.height * ((float)targetWidth / sourceTexture.width));
            }
            TextureScale.Bilinear(sourceTexture, width, height);

            var pixels = new Color[targetWidth * targetHeight];

            int x = (width - targetWidth) / 2;
            int y = (height - targetHeight) / 2;

            for (int i = 0; i < targetHeight; i++)
            {
                for (int j = 0; j < targetWidth; j++)
                {
                    pixels[i * targetWidth + j] = sourceTexture.GetPixel(x + j, y + i);
                }
            }

            var resultTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.ARGB32, false);
            resultTexture.SetPixels(pixels);
            resultTexture.Apply();

            return resultTexture;
        }

        private static FieldInfo fieldPathDic = null;

        public static Dictionary<string, CacheBoneDataArray.BoneData> GetPathDic(
            this CacheBoneDataArray cacheBoneDataArray)
        {
            if (fieldPathDic == null)
            {
                fieldPathDic = typeof(CacheBoneDataArray).GetField("path_dic_", BindingFlags.Instance | BindingFlags.NonPublic);
                MTEUtils.AssertNull(fieldPathDic != null, "fieldPathDic is null");
            }
            return (Dictionary<string, CacheBoneDataArray.BoneData>) fieldPathDic.GetValue(cacheBoneDataArray);
        }

        public static CacheBoneDataArray.BoneData GetBoneData(
            this CacheBoneDataArray cacheBoneDataArray,
            string path)
        {
            var pathDic = cacheBoneDataArray.GetPathDic();
            CacheBoneDataArray.BoneData boneData;
            if (pathDic.TryGetValue(path, out boneData))
            {
                return boneData;
            }
            return null;
        }

        private static FieldInfo fieldIkFabrik = null;

        public static FABRIK GetIkFabrik(this LimbControl limbControl)
        {
            if (limbControl == null)
            {
                return null;
            }
            if (fieldIkFabrik == null)
            {
                fieldIkFabrik = typeof(LimbControl).GetField("ik_fabrik_", BindingFlags.NonPublic | BindingFlags.Instance);
                MTEUtils.AssertNull(fieldIkFabrik != null, "fieldIkFabrik is null");
            }
            return (FABRIK) fieldIkFabrik.GetValue(limbControl);
        }

        private static FieldInfo fieldJointDragPoint = null;

        public static IKDragPoint GetJointDragPoint(
            this LimbControl limbControl)
        {
            if (fieldJointDragPoint == null)
            {
                fieldJointDragPoint = typeof(LimbControl).GetField("joint_drag_point_", BindingFlags.NonPublic | BindingFlags.Instance);
                MTEUtils.AssertNull(fieldJointDragPoint != null, "fieldJointDragPoint is null");
            }
            return (IKDragPoint) fieldJointDragPoint.GetValue(limbControl);
        }

        private static FieldInfo fieldTipDragPoint = null;

        public static IKDragPoint GetTipDragPoint(
            this LimbControl limbControl)
        {
            if (fieldTipDragPoint == null)
            {
                fieldTipDragPoint = typeof(LimbControl).GetField("tip_drag_point_", BindingFlags.NonPublic | BindingFlags.Instance);
                MTEUtils.AssertNull(fieldTipDragPoint != null, "fieldTipDragPoint is null");
            }
            return (IKDragPoint) fieldTipDragPoint.GetValue(limbControl);
        }

        private static FieldInfo fieldBackupLocalPos = null;

        public static Vector3 GetBackupLocalPos(
            this IKDragPoint ikDragPoint)
        {
            if (fieldBackupLocalPos == null)
            {
                fieldBackupLocalPos = typeof(IKDragPoint).GetField("backup_local_pos_", BindingFlags.NonPublic | BindingFlags.Instance);
                MTEUtils.AssertNull(fieldBackupLocalPos != null, "fieldBackupLocalPos is null");
            }
            return (Vector3) fieldBackupLocalPos.GetValue(ikDragPoint);
        }

        public static void PositonCorrection(this IKDragPoint ikDragPoint)
        {
            if (ikDragPoint.PositonCorrectionEnabled)
            {
                ikDragPoint.target_ik_point_trans.localPosition = ikDragPoint.GetBackupLocalPos();
            }
        }

        private static FieldInfo fieldEyeEulerAngle = null;

        public static Vector3 GetEyeEulerAngle(this TBody body)
        {
            if (fieldEyeEulerAngle == null)
            {
                fieldEyeEulerAngle = typeof(TBody).GetField("EyeEulerAngle", BindingFlags.NonPublic | BindingFlags.Instance);
                MTEUtils.AssertNull(fieldEyeEulerAngle != null, "fieldEyeEulerAngle is null");
            }
            return (Vector3) fieldEyeEulerAngle.GetValue(body);
        }

        public static void SetEyeEulerAngle(this TBody body, Vector3 eulerAngle)
        {
            if (fieldEyeEulerAngle == null)
            {
                fieldEyeEulerAngle = typeof(TBody).GetField("EyeEulerAngle", BindingFlags.NonPublic | BindingFlags.Instance);
                MTEUtils.AssertNull(fieldEyeEulerAngle != null, "fieldEyeEulerAngle is null");
            }
            fieldEyeEulerAngle.SetValue(body, eulerAngle);
        }

        public static T GetCustomAttribute<T>(
            this System.Type type)
            where T : System.Attribute
        {
            var attributes = type.GetCustomAttributes(typeof(T), false);
            if (attributes.Length > 0)
            {
                return (T) attributes[0];
            }
            return default(T);
        }

        private static FieldInfo _objectTargetListField = null;

        public static List<PhotoTransTargetObject> GetTargetList(this ObjectManagerWindow self)
        {
            if (_objectTargetListField == null)
            {
                _objectTargetListField = typeof(ObjectManagerWindow).GetField("target_list_",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return (List<PhotoTransTargetObject>) _objectTargetListField.GetValue(self);
        }

        private static FieldInfo _lightTargetListField = null;

        public static List<PhotoTransTargetObject> GetTargetList(this LightWindow self)
        {
            if (_lightTargetListField == null)
            {
                _lightTargetListField = typeof(LightWindow).GetField("targetList",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return (List<PhotoTransTargetObject>) _lightTargetListField.GetValue(self);
        }

        private static FieldInfo _valueOpenField = null;

        public static void SetValueOpenOnly(this FingerBlend.BaseFinger self, float value)
        {
            if (_valueOpenField == null)
            {
                _valueOpenField = typeof(FingerBlend.BaseFinger).GetField("value_open_",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            _valueOpenField.SetValue(self, value);
        }

        private static FieldInfo _valueFistField = null;

        public static void SetValueFistOnly(this FingerBlend.BaseFinger self, float value)
        {
            if (_valueFistField == null)
            {
                _valueFistField = typeof(FingerBlend.BaseFinger).GetField("value_fist_",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            _valueFistField.SetValue(self, value);
        }

        private static FieldInfo _blendEnabledField = null;

        public static void SetEnabledOnly(this FingerBlend.BaseFinger self, bool value)
        {
            if (_blendEnabledField == null)
            {
                _blendEnabledField = typeof(FingerBlend.BaseFinger).GetField("enabled_",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            _blendEnabledField.SetValue(self, value);
        }

        public static bool IsLock(this FingerBlend.BaseFinger baseFinger, int index)
        {
            var armFinger = baseFinger as FingerBlend.ArmFinger;
            if (armFinger != null)
            {
                switch (index)
                {
                    case 0:
                        return armFinger.lock_enabled0;
                    case 1:
                        return armFinger.lock_enabled1;
                    case 2:
                        return armFinger.lock_enabled2;
                    case 3:
                        return armFinger.lock_enabled3;
                    case 4:
                        return armFinger.lock_enabled4;
                }
            }

            var legFinger = baseFinger as FingerBlend.LegFinger;
            if (legFinger != null)
            {
                switch (index)
                {
                    case 0:
                        return legFinger.lock_enabled0;
                    case 1:
                        return legFinger.lock_enabled1;
                    case 2:
                        return legFinger.lock_enabled2;
                }
            }

            return false;
        }

        public static void LockAllItems(this FingerBlend.BaseFinger baseFinger, bool isLock)
        {
            var armFinger = baseFinger as FingerBlend.ArmFinger;
            if (armFinger != null)
            {
                armFinger.lock_enabled0 = isLock;
                armFinger.lock_enabled1 = isLock;
                armFinger.lock_enabled2 = isLock;
                armFinger.lock_enabled3 = isLock;
                armFinger.lock_enabled4 = isLock;
            }

            var legFinger = baseFinger as FingerBlend.LegFinger;
            if (legFinger != null)
            {
                legFinger.lock_enabled0 = isLock;
                legFinger.lock_enabled1 = isLock;
                legFinger.lock_enabled2 = isLock;
            }
        }

        public static void LockReverse(this FingerBlend.BaseFinger baseFinger)
        {
            var armFinger = baseFinger as FingerBlend.ArmFinger;
            if (armFinger != null)
            {
                armFinger.lock_enabled0 = !armFinger.lock_enabled0;
                armFinger.lock_enabled1 = !armFinger.lock_enabled1;
                armFinger.lock_enabled2 = !armFinger.lock_enabled2;
                armFinger.lock_enabled3 = !armFinger.lock_enabled3;
                armFinger.lock_enabled4 = !armFinger.lock_enabled4;
            }

            var legFinger = baseFinger as FingerBlend.LegFinger;
            if (legFinger != null)
            {
                legFinger.lock_enabled0 = !legFinger.lock_enabled0;
                legFinger.lock_enabled1 = !legFinger.lock_enabled1;
                legFinger.lock_enabled2 = !legFinger.lock_enabled2;
            }
        }

        public static void CopyFrom(
            this FingerBlend.BaseFinger baseFinger,
            FingerBlend.BaseFinger source)
        {
            baseFinger.enabled = source.enabled;

            var armFinger = baseFinger as FingerBlend.ArmFinger;
            var sourceArmFinger = source as FingerBlend.ArmFinger;
            if (armFinger != null && sourceArmFinger != null)
            {
                armFinger.lock_enabled0 = sourceArmFinger.lock_enabled0;
                armFinger.lock_enabled1 = sourceArmFinger.lock_enabled1;
                armFinger.lock_enabled2 = sourceArmFinger.lock_enabled2;
                armFinger.lock_enabled3 = sourceArmFinger.lock_enabled3;
                armFinger.lock_enabled4 = sourceArmFinger.lock_enabled4;

                armFinger.lock_value0 = sourceArmFinger.lock_value0;
                armFinger.lock_value1 = sourceArmFinger.lock_value1;
                armFinger.lock_value2 = sourceArmFinger.lock_value2;
                armFinger.lock_value3 = sourceArmFinger.lock_value3;
                armFinger.lock_value4 = sourceArmFinger.lock_value4;
            }

            var legFinger = baseFinger as FingerBlend.LegFinger;
            var sourceLegFinger = source as FingerBlend.LegFinger;
            if (legFinger != null && sourceLegFinger != null)
            {
                legFinger.lock_enabled0 = sourceLegFinger.lock_enabled0;
                legFinger.lock_enabled1 = sourceLegFinger.lock_enabled1;
                legFinger.lock_enabled2 = sourceLegFinger.lock_enabled2;

                legFinger.lock_value0 = sourceLegFinger.lock_value0;
                legFinger.lock_value1 = sourceLegFinger.lock_value1;
                legFinger.lock_value2 = sourceLegFinger.lock_value2;
            }

            baseFinger.value_open = source.value_open;
            baseFinger.value_fist = source.value_fist;
        }

        private static FieldInfo _m_bonesField = null;

        public static Transform[] GetBones(this TMorph morph)
        {
            if (_m_bonesField == null)
            {
                _m_bonesField = typeof(TMorph).GetField("m_bones",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return (Transform[]) _m_bonesField.GetValue(morph);
        }

        public static Transform GetBone(this TMorph morph, int index)
        {
            var bones = morph.GetBones();
            return bones.GetOrDefault(index);
        }

        private static FieldInfo _m_bwsField = null;

        public static BoneWeight[] GetBoneWeights(this TMorph morph)
        {
            if (_m_bwsField == null)
            {
                _m_bwsField = typeof(TMorph).GetField("m_bws",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return (BoneWeight[]) _m_bwsField.GetValue(morph);
        }

        public static BoneWeight GetBoneWeight(this TMorph morph, int index)
        {
            var bws = morph.GetBoneWeights();
            return bws.GetOrDefault(index, new BoneWeight());
        }

        public static ColorPaletteManager.ColorData.CommonItem GetItem(
            this ref ColorPaletteManager.ColorData self,
            ColorPaletteManager.Category category)
        {
            if (category == ColorPaletteManager.Category.Main)
            {
                return self.main;
            }
            else if (category == ColorPaletteManager.Category.Shadow)
            {
                return self.shadow;
            }
            else
            {
                return self.outline;
            }
        }

        public static void SetItem(
            this ref ColorPaletteManager.ColorData self,
            ColorPaletteManager.Category category,
            ColorPaletteManager.ColorData.CommonItem item)
        {
            if (category == ColorPaletteManager.Category.Main)
            {
                self.main = item;
            }
            else if (category == ColorPaletteManager.Category.Shadow)
            {
                self.shadow = item;
            }
            else
            {
                self.outline = item;
            }
        }

        public static List<string> GetTags(this TMorph morph)
        {
            if (morph != null)
            {
                var tags = new List<string>(morph.hash.Count);
                foreach (string tag in morph.hash.Keys)
                {
                    tags.Add(tag);
                }
                return tags;
            }

            return new List<string>();
        }

        public static string GetFullPath(this Transform transform, Transform root)
        {
            if (transform == null || root == null)
            {
                return "";
            }

            var parent = transform.parent;
            if (parent == null || parent == root)
            {
                return transform.name;
            }

            return parent.GetFullPath(root) + "/" + transform.name;
        }

        public static Vector3 ToVector3(this float[] values)
        {
            if (values.Length != 3)
            {
                MTEUtils.LogError("ToVector3: 不正なfloat配列です length={0}", values.Length);
                return Vector3.zero;
            }
            return new Vector3(values[0], values[1], values[2]);
        }

        public static Vector4 ToVector4(this float[] values)
        {
            if (values.Length != 4)
            {
                MTEUtils.LogError("ToVector4: 不正なfloat配列です length={0}", values.Length);
                return Vector4.zero;
            }
            return new Vector4(values[0], values[1], values[2], values[3]);
        }

        public static Quaternion ToQuaternion(this float[] values)
        {
            if (values.Length != 4)
            {
                MTEUtils.LogError("ToQuaternion: 不正なfloat配列です length={0}", values.Length);
                return Quaternion.identity;
            }
            return new Quaternion(values[0], values[1], values[2], values[3]);
        }

        public static Color ToColor(this float[] values)
        {
            if (values.Length == 4)
            {
                return new Color(values[0], values[1], values[2], values[3]);
            }
            if (values.Length == 3)
            {
                return new Color(values[0], values[1], values[2]);
            }

            MTEUtils.LogError("ToColor: 不正なfloat配列です length={0}", values.Length);
            return Color.white;
        }

        private static readonly Dictionary<string, MaidParts.PARTS_COLOR> _partsColorIdMap =
                Enum.GetValues(typeof(MaidParts.PARTS_COLOR)).Cast<MaidParts.PARTS_COLOR>().ToDictionary(
                    type => type.ToString(),
                    type => type,
                    StringComparer.OrdinalIgnoreCase);

        public static MaidParts.PARTS_COLOR ToPartsColorId(this string name)
        {
            return _partsColorIdMap.GetOrDefault(name, MaidParts.PARTS_COLOR.NONE);
        }

        public static void RemoveAllButFirst<T>(this List<T> list)
        {
            if (list == null)
            {
                MTEUtils.LogError("RemoveAllButFirst: list is null");
                return;
            }

            if (list.Count <= 1)
            {
                return;
            }

            T first = list[0];
            list.Clear();
            list.Add(first);
        }

        public static float GetRotationZ(this Camera camera)
        {
            return camera.transform.eulerAngles.z;
        }

        public static void SetRotationZ(this Camera camera, float z)
        {
            Vector3 eulerAngles = camera.transform.eulerAngles;
            eulerAngles.z = z;
            camera.transform.eulerAngles = eulerAngles;
        }

        public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key)
            where TValue : new()
        {
            TValue value;
            if (!self.TryGetValue(key, out value))
            {
                value = new TValue();
                self[key] = value;
            }

            return value;
        }

        public static TValue GetOrCreate<TKey, TValue>(
            this Dictionary<TKey, TValue> self,
            TKey key,
            Func<TValue> create)
        {
            TValue value;
            if (!self.TryGetValue(key, out value))
            {
                value = create();
                self[key] = value;
            }

            return value;
        }

        public static TValue GetOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> self,
            TKey key)
        {
            if (key == null)
            {
                return default(TValue);
            }

            TValue value;
            if (!self.TryGetValue(key, out value))
            {
                return default(TValue);
            }

            return value;
        }

        public static TValue GetOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> self,
            TKey key,
            TValue defaultValue)
        {
            if (key == null)
            {
                return defaultValue;
            }

            TValue value;
            if (!self.TryGetValue(key, out value))
            {
                return defaultValue;
            }

            return value;
        }

        public static T GetOrDefault<T>(this List<T> list, int index, T defaultValue)
        {
            if (index >= 0 && index < list.Count)
            {
                return list[index];
            }
            return defaultValue;
        }

        public static T GetOrDefault<T>(this List<T> list, int index)
            where T : class
        {
            if (index >= 0 && index < list.Count)
            {
                return list[index];
            }
            return null;
        }

        public static T GetOrDefault<T>(this T[] array, int index)
            where T : class
        {
            if (index >= 0 && index < array.Length)
            {
                return array[index];
            }
            return null;
        }

        public static T GetOrDefault<T>(this T[] array, int index, T defaultValue)
        {
            if (index >= 0 && index < array.Length)
            {
                return array[index];
            }
            return defaultValue;
        }

        private static FieldInfo _mVelocityField = null;

        public static void SetVelocity(this UltimateOrbitCamera self, Vector3 velocity)
        {
            if (_mVelocityField == null)
            {
                _mVelocityField = typeof(UltimateOrbitCamera).GetField("mVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            _mVelocityField.SetValue(self, velocity);
        }

        private static FieldInfo _cameraMoveField = null;

        public static void SetCameraMove(this UltimateOrbitCamera self, bool cameraMove)
        {
            if (_cameraMoveField == null)
            {
                _cameraMoveField = typeof(UltimateOrbitCamera).GetField("cameraMove", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            _cameraMoveField.SetValue(self, cameraMove);
        }

        public static void MoveTarget(this UltimateOrbitCamera camera, Vector3 newTargetWorldPos)
        {
            var cameraTransform = camera.transform;
            var target = camera.target;

            var currentCameraPos = cameraTransform.position;
            var currentCameraRot = cameraTransform.rotation;
            var cameraForward = cameraTransform.forward;

            var distanceToNewTarget = Vector3.Dot(newTargetWorldPos - currentCameraPos, cameraForward);
            var adjustedTargetPos = currentCameraPos + cameraForward * distanceToNewTarget;

            target.position = adjustedTargetPos;
            camera.SetVelocity(adjustedTargetPos);

            float newDistance = distanceToNewTarget;
            camera.SetDistance(newDistance);

            cameraTransform.position = currentCameraPos;
            cameraTransform.rotation = currentCameraRot;

            camera.SetCameraMove(false);
        }

        public static AnimationState GetAnimationState(this Maid maid)
        {
            var animation = maid.GetAnimation();
            if (animation == null)
            {
                return null;
            }

            var anmName = maid.body0.LastAnimeFN;
            return animation[anmName.ToLower()];
        }

        private static FieldInfo m_AnimCacheField = null;
        public static Dictionary<string, byte> GetAnimCache(this TBody body)
        {
            if (m_AnimCacheField == null)
            {
                m_AnimCacheField = typeof(TBody).GetField("m_AnimCache", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return (Dictionary<string, byte>) m_AnimCacheField.GetValue(body);
        }

        private static FieldInfo anistField = null;
        public static AnimationState GetAnist(this TBody body)
        {
            if (anistField == null)
            {
                anistField = typeof(TBody).GetField("anist", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return (AnimationState) anistField.GetValue(body);
        }

        public static void SetAnist(this TBody body, AnimationState anist)
        {
            if (anistField == null)
            {
                anistField = typeof(TBody).GetField("anist", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            anistField.SetValue(body, anist);
        }

        public static AnimationState CrossFadeLayer(
            this TBody body,
            string filename,
            AFileSystemBase fileSystem,
            int layer,
            bool additive = false,
            bool loop = false,
            bool boAddQue = false,
            float fade = 0.5f,
            float weight = 1f)
        {
            if (body.m_Bones == null)
            {
                NDebug.Assert("まだ読み込まれる前のBodyにモーションを指定しようとしました。" + body.gameObject.name, false);
                return null;
            }

            if (string.IsNullOrEmpty(filename))
            {
                return null;
            }

            var animeTag = filename.ToLower();
            var prevAnimeFn = body.LastAnimeFN;
            var animationState = body.LoadAnime(animeTag, fileSystem, filename, additive, loop);
            if (animationState == null)
            {
                return null;
            }

            animationState.layer = layer;
            if (layer != 0)
            {
                body.LastAnimeFN = prevAnimeFn;
            }

            var animation = body.m_Animation;
            if (boAddQue)
            {
                if (weight != 1f)
                {
                    animation.PlayQueued(animeTag, QueueMode.CompleteOthers);
                    animation[animeTag].weight = weight;
                }
                else
                {
                    animation.CrossFadeQueued(animeTag, fade, QueueMode.CompleteOthers);
                }
            }
            else
            {
                if (animationState.layer == 0)
                {
                    body.SetAnist(animationState);
                }
                if (weight != 1f)
                {
                    animation.Play(animeTag);
                    animation[animeTag].weight = weight;
                }
                else
                {
                    animation.CrossFade(animeTag, fade);
                }
            }

            return animationState;
        }

        public static AnimationState CrossFadeLayer(
            this TBody body,
            string tag,
            byte[] byte_data,
            int layer,
            bool additive = false,
            bool loop = false,
            bool boAddQue = false,
            float fade = 0.5f,
            float weight = 1f)
        {
            if (body.m_Bones == null)
            {
                NDebug.Assert("まだ読み込まれる前のBodyにモーションを指定しようとしました。" + body.gameObject.name, false);
            }

            var prevAnimeFn = body.LastAnimeFN;
            var animationState = body.LoadAnime(tag, byte_data, additive, loop);

            animationState.layer = layer;
            if (layer != 0)
            {
                body.LastAnimeFN = prevAnimeFn;
            }

            var animation = body.m_Animation;
            if (boAddQue)
            {
                if (weight != 1f)
                {
                    animation.PlayQueued(tag, QueueMode.CompleteOthers);
                    animation[tag].weight = weight;
                }
                else
                {
                    animation.CrossFadeQueued(tag, fade, QueueMode.CompleteOthers);
                }
            }
            else
            {
                if (animationState.layer == 0)
                {
                    body.SetAnist(animationState);
                }
                if (weight != 1f)
                {
                    animation.Play(tag);
                    animation[tag].weight = weight;
                }
                else
                {
                    animation.CrossFade(tag, fade);
                }
            }

            return animationState;
        }

        public static AnimationState CrossFadeLayerByFullPath(
            this TBody body,
            string fullPath,
            int layer,
            bool additive = false,
            bool loop = false,
            bool boAddQue = false,
            float fade = 0.5f,
            float weight = 1f)
        {
            byte[] anmData = new byte[0];
            try
            {
                using (FileStream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                {
                    anmData = new byte[fileStream.Length];
                    fileStream.Read(anmData, 0, anmData.Length);
                }
            }
            catch
            {
                MTEUtils.LogError("アニメーションファイルの読み込みに失敗しました。" + fullPath);
            }

            AnimationState state = null;

            if (anmData.Length > 0)
            {
                var anmTag = Path.GetFileName(fullPath).ToLower();
                state = body.CrossFadeLayer(anmTag, anmData, layer, additive, loop, boAddQue, fade, weight);
                body.maid.SetAutoTwistAll(true);
            }

            return state;
        }

        public static void StopAndDestroyAnimeLayer(this TBody body, int layerno)
        {
            if (body.m_Bones == null)
            {
                Debug.LogError("未だキャラがロードさていません。" + body.gameObject.name);
            }
            if (layerno < 2)
            {
                Debug.LogError("モーションレイヤーの停止は2以上を指定して下さい。");
            }

            var animation = body.m_Animation;
            var statesToStop = new List<AnimationState>();

            // foreachを使って対象のアニメーション状態を収集
            foreach (AnimationState state in animation)
            {
                if (state.layer == layerno && animation.IsPlaying(state.name))
                {
                    statesToStop.Add(state);
                }
            }

            var animCache = body.GetAnimCache();

            // 収集したアニメーション状態を停止し、クリップを破棄
            foreach (AnimationState state in statesToStop)
            {
                animation.Stop(state.name);
                AnimationClip clip = animation.GetClip(state.name);
                int num = state.name.IndexOf(" - Queued Clone");
                if (num <= 0)
                {
                    animation.RemoveClip(state.name);
                }
                UnityEngine.Object.Destroy(clip);

                if (animCache.ContainsKey(state.name))
                {
                    animCache.Remove(state.name);
                }
            }
        }

        public static void SeekTime(this Animation animation, AnimationState state, float time)
        {
            if (animation == null || state == null)
            {
                return;
            }

            bool isPlaying = state.enabled;
            state.time = time;
            state.enabled = true;
            animation.Sample();
            state.enabled = isPlaying;
        }

        public static float GetPlayingTime(this AnimationState state)
        {
            if (state == null || state.length <= 0f)
            {
                return 0f;
            }

            float value = state.time;
            if (state.length < state.time)
            {
                if (state.wrapMode == WrapMode.ClampForever)
                {
                    value = state.length;
                }
                else
                {
                    value = state.time - state.length * (float)((int)(state.time / state.length));
                }
            }
            return value;
        }

        public static void StopAndDestroy(this TBody body, string fileName)
        {
            var animeTag = fileName.ToLower();
            var animation = body.m_Animation;
            var animationState = animation[animeTag];
            if (animationState == null)
            {
                return;
            }

            animation.Stop(animeTag);

            AnimationClip clip = animation.GetClip(animationState.name);
            int num = animationState.name.IndexOf(" - Queued Clone");
            if (num <= 0)
            {
                animation.RemoveClip(animationState.name);
            }
            UnityEngine.Object.Destroy(clip);
        }
    }
}