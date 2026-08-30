using System;
using System.Collections.Generic;
using System.Reflection;
using PEData = COM3D2.MotionTimelineEditor.PostEffects;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// PostEffects.Plugin の TimelineBridge へのリフレクションブリッジ。
    ///
    /// UnityInjector はファイル名順にプラグインをロードするため
    /// SceneEditor が PostEffects より先に来る。コンパイル時参照にすると
    /// ロード時の型解決に失敗し、Mono はその束縛失敗をプロセス寿命の間
    /// キャッシュするので後から復帰できない。そのため参照は持たず、
    /// ホスト型が見つかるまで毎回探し直す (ModelProviderClient と同じ作法)。
    ///
    /// 値は MTEUtils の共有 DTO で受け渡し、ホスト側 DTO との相互変換は
    /// ReflectionFieldCopier が担う
    /// </summary>
    public static class PostEffectsClient
    {
        private const string HostAssemblyName = "COM3D25.PostEffects.Plugin";
        private const string HostTypeName = "COM3D25.PostEffects.Plugin.TimelineBridge";

        private static bool _initialized;

        private static Func<int> _getMaxParaffinCount;
        private static Func<int> _getMaxDistanceFogCount;
        private static Func<int> _getMaxRimlightCount;

        private static Func<int> _getParaffinCount;
        private static Action<int> _setParaffinCount;
        private static Func<bool> _getParaffinEnabled;
        private static Action<bool> _setParaffinEnabled;
        private static Func<int, object> _getParaffinData;
        private static MethodInfo _applyParaffin;

        private static Func<int> _getDistanceFogCount;
        private static Action<int> _setDistanceFogCount;
        private static Func<bool> _getDistanceFogEnabled;
        private static Action<bool> _setDistanceFogEnabled;
        private static Func<int, object> _getDistanceFogData;
        private static MethodInfo _applyDistanceFog;

        private static Func<int> _getRimlightCount;
        private static Action<int> _setRimlightCount;
        private static Func<bool> _getRimlightEnabled;
        private static Action<bool> _setRimlightEnabled;
        private static Func<int, object> _getRimlightData;
        private static MethodInfo _applyRimlight;

        private static Func<object> _getGTToneMap;
        private static MethodInfo _applyGTToneMap;
        private static Func<object> _getDepthOfField;
        private static MethodInfo _applyDepthOfField;

        // Apply 系へ渡すホスト側 DTO のインスタンスを使い回す (毎フレーム生成しない)
        private static object _paraffinArg;
        private static object _distanceFogArg;
        private static object _rimlightArg;
        private static object _gtToneMapArg;
        private static object _depthOfFieldArg;

        // MethodInfo.Invoke 用の引数配列も使い回す
        private static readonly object[] _args1 = new object[1];
        private static readonly object[] _args2 = new object[2];

        // 毎フレーム走るパスなので、例外はメンバごとに初回だけログして以後は黙らせる。
        // 単一フラグで全メンバを止めると、別のメンバの恒常的な失敗を見逃す
        private static readonly HashSet<string> _errorLoggedMembers = new HashSet<string>();

        /// <summary>PostEffects.Plugin と接続できているか。未ロードの間は false を返し続ける</summary>
        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _getParaffinCount != null;
            }
        }

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // ロード順によってはホストのアセンブリが未登場のことがあるため、
            // 型が見つかるまでは _initialized を立てずに再試行を続ける
            var type = FindHostType();
            if (type == null)
            {
                return;
            }

            // ここから先はホストの型は見つかっている。シグネチャ不一致は
            // バージョン差による恒久的な問題なので、この場合のみ無効へ確定する
            _initialized = true;

            try
            {
                _getMaxParaffinCount = CreateFuncInt(type, "GetMaxParaffinCount");
                _getMaxDistanceFogCount = CreateFuncInt(type, "GetMaxDistanceFogCount");
                _getMaxRimlightCount = CreateFuncInt(type, "GetMaxRimlightCount");

                _getParaffinCount = CreateFuncInt(type, "GetParaffinCount");
                _setParaffinCount = CreateActionInt(type, "SetParaffinCount");
                _getParaffinEnabled = CreateFuncBool(type, "GetParaffinEnabled");
                _setParaffinEnabled = CreateActionBool(type, "SetParaffinEnabled");
                _getParaffinData = CreateFuncIntObject(type, "GetParaffinData");
                _applyParaffin = type.GetMethod("ApplyParaffin", BindingFlags.Public | BindingFlags.Static);

                _getDistanceFogCount = CreateFuncInt(type, "GetDistanceFogCount");
                _setDistanceFogCount = CreateActionInt(type, "SetDistanceFogCount");
                _getDistanceFogEnabled = CreateFuncBool(type, "GetDistanceFogEnabled");
                _setDistanceFogEnabled = CreateActionBool(type, "SetDistanceFogEnabled");
                _getDistanceFogData = CreateFuncIntObject(type, "GetDistanceFogData");
                _applyDistanceFog = type.GetMethod("ApplyDistanceFog", BindingFlags.Public | BindingFlags.Static);

                _getRimlightCount = CreateFuncInt(type, "GetRimlightCount");
                _setRimlightCount = CreateActionInt(type, "SetRimlightCount");
                _getRimlightEnabled = CreateFuncBool(type, "GetRimlightEnabled");
                _setRimlightEnabled = CreateActionBool(type, "SetRimlightEnabled");
                _getRimlightData = CreateFuncIntObject(type, "GetRimlightData");
                _applyRimlight = type.GetMethod("ApplyRimlight", BindingFlags.Public | BindingFlags.Static);

                _getGTToneMap = CreateFuncObject(type, "GetGTToneMap");
                _applyGTToneMap = type.GetMethod("ApplyGTToneMap", BindingFlags.Public | BindingFlags.Static);
                _getDepthOfField = CreateFuncObject(type, "GetDepthOfField");
                _applyDepthOfField = type.GetMethod("ApplyDepthOfField", BindingFlags.Public | BindingFlags.Static);

                if (_getMaxParaffinCount == null || _getMaxDistanceFogCount == null ||
                    _getMaxRimlightCount == null ||
                    _getParaffinCount == null || _setParaffinCount == null ||
                    _getParaffinEnabled == null || _setParaffinEnabled == null ||
                    _getParaffinData == null || _applyParaffin == null ||
                    _getDistanceFogCount == null || _setDistanceFogCount == null ||
                    _getDistanceFogEnabled == null || _setDistanceFogEnabled == null ||
                    _getDistanceFogData == null || _applyDistanceFog == null ||
                    _getRimlightCount == null || _setRimlightCount == null ||
                    _getRimlightEnabled == null || _setRimlightEnabled == null ||
                    _getRimlightData == null || _applyRimlight == null ||
                    _getGTToneMap == null || _applyGTToneMap == null ||
                    _getDepthOfField == null || _applyDepthOfField == null)
                {
                    MTEUtils.LogWarning(
                        "PostEffectsClient: TimelineBridge にシグネチャの一致するメソッドが見つかりませんでした");
                    Disable();
                    return;
                }

                // Apply 系へ渡すホスト側 DTO を 1 個ずつ確保して使い回す
                _paraffinArg = Activator.CreateInstance(_applyParaffin.GetParameters()[1].ParameterType);
                _distanceFogArg = Activator.CreateInstance(_applyDistanceFog.GetParameters()[1].ParameterType);
                _rimlightArg = Activator.CreateInstance(_applyRimlight.GetParameters()[1].ParameterType);
                _gtToneMapArg = Activator.CreateInstance(_applyGTToneMap.GetParameters()[0].ParameterType);
                _depthOfFieldArg = Activator.CreateInstance(_applyDepthOfField.GetParameters()[0].ParameterType);

                WarnUnmappedFields("パラフィン", typeof(PEData.ParaffinData), _paraffinArg);
                WarnUnmappedFields("距離フォグ", typeof(PEData.DistanceFogData), _distanceFogArg);
                WarnUnmappedFields("リムライト", typeof(PEData.RimlightData), _rimlightArg);
                WarnUnmappedFields("GTトーンマップ", typeof(PEData.GTToneMapData), _gtToneMapArg);
                WarnUnmappedFields("被写界深度", typeof(PEData.DepthOfFieldData), _depthOfFieldArg);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning(
                    "PostEffectsClient: TimelineBridge との接続に失敗しました: " + e.Message);
                Disable();
            }
        }

        /// <summary>
        /// 共有 DTO とホスト側 DTO でフィールド名がずれていないか接続時に 1 回だけ検査する。
        /// ずれた値は例外を出さず既定値のまま素通りするため、ログでしか気付けない
        /// </summary>
        private static void WarnUnmappedFields(string label, Type shared, object hostArg)
        {
            var unmapped = ReflectionFieldCopier.FindUnmappedFields(shared, hostArg.GetType());
            if (unmapped.Count == 0)
            {
                return;
            }
            MTEUtils.LogWarning(
                "PostEffectsClient: {0} の共有 DTO に、PostEffects.Plugin 側と対応しないフィールドがあります " +
                "(この値は反映されません): {1}",
                label, string.Join(", ", unmapped.ToArray()));
        }

        private static void Disable()
        {
            // isAvailable の判定に使う 1 個を落とせば全体が無効になる
            _getParaffinCount = null;
        }

        private static void LogHostError(string member, Exception e)
        {
            if (!_errorLoggedMembers.Add(member))
            {
                return;
            }
            MTEUtils.LogWarning(
                "PostEffectsClient: {0} の呼び出しで例外が発生しました (以後この警告は出しません): {1}",
                member, e.Message);
        }

        /// <summary>
        /// PostEffects.Plugin 内の TimelineBridge を解決する。
        /// ロードが自分より後の場合は Type.GetType が null を返すため、
        /// AppDomain の読み込み済みアセンブリからも探す
        /// </summary>
        private static Type FindHostType()
        {
            var type = Type.GetType(HostTypeName + ", " + HostAssemblyName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != HostAssemblyName)
                {
                    continue;
                }
                type = assembly.GetType(HostTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static Func<int> CreateFuncInt(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            return method == null
                ? null
                : (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), method);
        }

        private static Func<bool> CreateFuncBool(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            return method == null
                ? null
                : (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), method);
        }

        private static Action<int> CreateActionInt(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            return method == null
                ? null
                : (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), method);
        }

        private static Action<bool> CreateActionBool(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            return method == null
                ? null
                : (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), method);
        }

        // 戻り値は参照型なので object へのデリゲート束縛 (共変) が使える
        private static Func<object> CreateFuncObject(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            return method == null
                ? null
                : (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), method);
        }

        private static Func<int, object> CreateFuncIntObject(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            return method == null
                ? null
                : (Func<int, object>)Delegate.CreateDelegate(typeof(Func<int, object>), method);
        }

        public static int maxParaffinCount
        {
            get
            {
                if (!isAvailable)
                {
                    return 0;
                }
                try { return _getMaxParaffinCount(); }
                catch (Exception e) { LogHostError("GetMaxParaffinCount", e); return 0; }
            }
        }

        public static int maxDistanceFogCount
        {
            get
            {
                if (!isAvailable)
                {
                    return 0;
                }
                try { return _getMaxDistanceFogCount(); }
                catch (Exception e) { LogHostError("GetMaxDistanceFogCount", e); return 0; }
            }
        }

        public static int maxRimlightCount
        {
            get
            {
                if (!isAvailable)
                {
                    return 0;
                }
                try { return _getMaxRimlightCount(); }
                catch (Exception e) { LogHostError("GetMaxRimlightCount", e); return 0; }
            }
        }

        public static int paraffinCount
        {
            get
            {
                if (!isAvailable)
                {
                    return 0;
                }
                try { return _getParaffinCount(); }
                catch (Exception e) { LogHostError("GetParaffinCount", e); return 0; }
            }
            set
            {
                if (!isAvailable)
                {
                    return;
                }
                try { _setParaffinCount(value); }
                catch (Exception e) { LogHostError("SetParaffinCount", e); }
            }
        }

        public static int distanceFogCount
        {
            get
            {
                if (!isAvailable)
                {
                    return 0;
                }
                try { return _getDistanceFogCount(); }
                catch (Exception e) { LogHostError("GetDistanceFogCount", e); return 0; }
            }
            set
            {
                if (!isAvailable)
                {
                    return;
                }
                try { _setDistanceFogCount(value); }
                catch (Exception e) { LogHostError("SetDistanceFogCount", e); }
            }
        }

        public static int rimlightCount
        {
            get
            {
                if (!isAvailable)
                {
                    return 0;
                }
                try { return _getRimlightCount(); }
                catch (Exception e) { LogHostError("GetRimlightCount", e); return 0; }
            }
            set
            {
                if (!isAvailable)
                {
                    return;
                }
                try { _setRimlightCount(value); }
                catch (Exception e) { LogHostError("SetRimlightCount", e); }
            }
        }

        public static bool paraffinEnabled
        {
            get
            {
                if (!isAvailable)
                {
                    return false;
                }
                try { return _getParaffinEnabled(); }
                catch (Exception e) { LogHostError("GetParaffinEnabled", e); return false; }
            }
            set
            {
                if (!isAvailable)
                {
                    return;
                }
                try { _setParaffinEnabled(value); }
                catch (Exception e) { LogHostError("SetParaffinEnabled", e); }
            }
        }

        public static bool distanceFogEnabled
        {
            get
            {
                if (!isAvailable)
                {
                    return false;
                }
                try { return _getDistanceFogEnabled(); }
                catch (Exception e) { LogHostError("GetDistanceFogEnabled", e); return false; }
            }
            set
            {
                if (!isAvailable)
                {
                    return;
                }
                try { _setDistanceFogEnabled(value); }
                catch (Exception e) { LogHostError("SetDistanceFogEnabled", e); }
            }
        }

        public static bool rimlightEnabled
        {
            get
            {
                if (!isAvailable)
                {
                    return false;
                }
                try { return _getRimlightEnabled(); }
                catch (Exception e) { LogHostError("GetRimlightEnabled", e); return false; }
            }
            set
            {
                if (!isAvailable)
                {
                    return;
                }
                try { _setRimlightEnabled(value); }
                catch (Exception e) { LogHostError("SetRimlightEnabled", e); }
            }
        }

        public static PEData.ParaffinData GetParaffinData(int index)
        {
            var dto = new PEData.ParaffinData();
            if (!isAvailable)
            {
                return dto;
            }
            try
            {
                ReflectionFieldCopier.Copy(_getParaffinData(index), dto);
            }
            catch (Exception e)
            {
                LogHostError("GetParaffinData", e);
            }
            return dto;
        }

        public static void ApplyParaffin(int index, PEData.ParaffinData data)
        {
            if (!isAvailable)
            {
                return;
            }
            try
            {
                ReflectionFieldCopier.Copy(data, _paraffinArg);
                _args2[0] = index;
                _args2[1] = _paraffinArg;
                _applyParaffin.Invoke(null, _args2);
            }
            catch (Exception e)
            {
                LogHostError("ApplyParaffin", e);
            }
        }

        public static PEData.DistanceFogData GetDistanceFogData(int index)
        {
            var dto = new PEData.DistanceFogData();
            if (!isAvailable)
            {
                return dto;
            }
            try
            {
                ReflectionFieldCopier.Copy(_getDistanceFogData(index), dto);
            }
            catch (Exception e)
            {
                LogHostError("GetDistanceFogData", e);
            }
            return dto;
        }

        public static void ApplyDistanceFog(int index, PEData.DistanceFogData data)
        {
            if (!isAvailable)
            {
                return;
            }
            try
            {
                ReflectionFieldCopier.Copy(data, _distanceFogArg);
                _args2[0] = index;
                _args2[1] = _distanceFogArg;
                _applyDistanceFog.Invoke(null, _args2);
            }
            catch (Exception e)
            {
                LogHostError("ApplyDistanceFog", e);
            }
        }

        public static PEData.RimlightData GetRimlightData(int index)
        {
            var dto = new PEData.RimlightData();
            if (!isAvailable)
            {
                return dto;
            }
            try
            {
                ReflectionFieldCopier.Copy(_getRimlightData(index), dto);
            }
            catch (Exception e)
            {
                LogHostError("GetRimlightData", e);
            }
            return dto;
        }

        public static void ApplyRimlight(int index, PEData.RimlightData data)
        {
            if (!isAvailable)
            {
                return;
            }
            try
            {
                ReflectionFieldCopier.Copy(data, _rimlightArg);
                _args2[0] = index;
                _args2[1] = _rimlightArg;
                _applyRimlight.Invoke(null, _args2);
            }
            catch (Exception e)
            {
                LogHostError("ApplyRimlight", e);
            }
        }

        public static PEData.GTToneMapData GetGTToneMap()
        {
            var dto = new PEData.GTToneMapData();
            if (!isAvailable)
            {
                return dto;
            }
            try
            {
                ReflectionFieldCopier.Copy(_getGTToneMap(), dto);
            }
            catch (Exception e)
            {
                LogHostError("GetGTToneMap", e);
            }
            return dto;
        }

        public static void ApplyGTToneMap(PEData.GTToneMapData data)
        {
            if (!isAvailable)
            {
                return;
            }
            try
            {
                ReflectionFieldCopier.Copy(data, _gtToneMapArg);
                _args1[0] = _gtToneMapArg;
                _applyGTToneMap.Invoke(null, _args1);
            }
            catch (Exception e)
            {
                LogHostError("ApplyGTToneMap", e);
            }
        }

        public static PEData.DepthOfFieldData GetDepthOfField()
        {
            var dto = new PEData.DepthOfFieldData();
            if (!isAvailable)
            {
                return dto;
            }
            try
            {
                ReflectionFieldCopier.Copy(_getDepthOfField(), dto);
            }
            catch (Exception e)
            {
                LogHostError("GetDepthOfField", e);
            }
            return dto;
        }

        public static void ApplyDepthOfField(PEData.DepthOfFieldData data)
        {
            if (!isAvailable)
            {
                return;
            }
            try
            {
                ReflectionFieldCopier.Copy(data, _depthOfFieldArg);
                _args1[0] = _depthOfFieldArg;
                _applyDepthOfField.Invoke(null, _args1);
            }
            catch (Exception e)
            {
                LogHostError("ApplyDepthOfField", e);
            }
        }
    }
}
