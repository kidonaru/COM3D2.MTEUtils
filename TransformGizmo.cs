using System;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>ギズモの操作種別</summary>
    public enum GizmoTool
    {
        /// <summary>ギズモ非表示</summary>
        None,
        Move,
        Rotate,
        Scale,
    }

    /// <summary>ギズモを表示する対象</summary>
    public enum GizmoTargetType
    {
        All,
        Selected,
    }

    /// <summary>ギズモの軸の座標系</summary>
    public enum GizmoSpace
    {
        /// <summary>対象の回転に沿った軸</summary>
        Local,
        /// <summary>ワールド軸</summary>
        Global,
        /// <summary>ギズモを描いている (掴んだ) カメラの右・上・前</summary>
        Camera,
    }

    /// <summary>ギズモの軸 3 本の組。軸番号 0 / 1 / 2 = 右 / 上 / 前</summary>
    public struct GizmoBasis
    {
        public Vector3 right;
        public Vector3 up;
        public Vector3 forward;

        public static readonly GizmoBasis world =
            new GizmoBasis(Vector3.right, Vector3.up, Vector3.forward);

        public GizmoBasis(Vector3 right, Vector3 up, Vector3 forward)
        {
            this.right = right;
            this.up = up;
            this.forward = forward;
        }

        public static GizmoBasis FromTransform(Transform t)
        {
            return new GizmoBasis(t.right, t.up, t.forward);
        }

        public Vector3 this[int axis]
        {
            get
            {
                switch (axis)
                {
                    case 0: return right;
                    case 1: return up;
                    default: return forward;
                }
            }
        }
    }

    /// <summary>
    /// カメラ非依存の Transform 操作ギズモ。
    /// 任意カメラの OnPostRender から Draw し、そのカメラの RT ピクセル座標で
    /// TryBeginDrag / UpdateDrag を呼ぶ。ドラッグ解決は開始時のカメラ基準で行うため、
    /// SceneView で掴んだドラッグが他ビューの座標で解釈されることはない。
    /// SceneEditor の GizmoRenderer から抽出した実装 (数式は同一)
    /// </summary>
    public class TransformGizmo
    {
        public Transform target;
        public GizmoTool tool = GizmoTool.Move;
        public GizmoSpace space = GizmoSpace.Local;

        /// <summary>
        /// 旧 API との互換用の bool 表現 (true = Local)。ModItemExplorer など 2 値で扱う利用側向け。
        /// Camera は Local ではないので false を返す。false の代入では Camera を保つ
        /// (bool しか知らない側は Global と Camera を区別できず、同期のたびに Camera が消えるため)
        /// </summary>
        public bool useLocalSpace
        {
            get { return space == GizmoSpace.Local; }
            set
            {
                if (value == useLocalSpace)
                {
                    return;
                }
                space = value ? GizmoSpace.Local : GizmoSpace.Global;
            }
        }
        /// <summary>表示倍率。配置モデル用に小さくする場合などに使う</summary>
        public float sizeScale = 1f;
        /// <summary>ドラッグで target を書き換えた直後に呼ばれる</summary>
        public Action onTransformChanged;

        public bool isDragging { get; private set; }

        private const float HitThreshold = 8f;
        /// <summary>
        /// 回転リングの掴み判定幅。リングは線 1 本で軸ハンドルより細く、
        /// ボーン用の小さいギズモでは 8px だと狙いづらいため広めに取る
        /// </summary>
        private const float RotateHitThreshold = 16f;
        private const float GizmoScreenScale = 0.15f; // 基準画角でのカメラ距離に対するギズモサイズ比

        // 見た目はゲーム本体の GizmoRender に合わせている。
        // 半円は円周 100 分割、矢じりの円錐は 30 分割、比率は軸長に対する値
        private const int CircleSegments = 100;
        private const int ConeSegments = 30;
        /// <summary>矢じり・立方体の半径。GizmoRender の DrawAxis の fct と同じ</summary>
        private const float TipRadiusRatio = 0.04f;
        /// <summary>矢じりの根本位置。GizmoRender の DrawAxis の fct2 と同じ</summary>
        private const float TipBaseRatio = 0.9f;
        /// <summary>外積が潰れたと判定する閾値 (軸が基準ベクトルと平行なとき)</summary>
        public const float DegenerateEpsilon = 0.001f;
        // レイと軸がほぼ平行と判定する閾値
        private const float ParallelEpsilon = 0.0001f;
        /// <summary>
        /// 回転面が視線とほぼ平行と判定する閾値 (面法線と視線方向の内積)。
        /// これを下回る面は円が線に潰れて角度が安定せず、わずかなマウス移動で
        /// 対象が飛ぶため掴ませない
        /// </summary>
        private const float RotationPlaneMinDot = 0.1f;

        // GizmoRender と同じ純色。選択中の軸は同じく半透明の黄で塗る
        private static readonly Color[] AxisColors =
        {
            new Color(1f, 0f, 0f, 1f), // X
            new Color(0f, 1f, 0f, 1f), // Y
            new Color(0f, 0f, 1f, 1f), // Z
        };
        private static readonly Color SelectedAxisColor = new Color(1f, 1f, 0f, 0.5f);
        /// <summary>面ハンドルの選択色。GizmoRender の colorWhite と同じ</summary>
        private static readonly Color SelectedPlaneColor = new Color(1f, 1f, 1f, 0.3f);
        /// <summary>面ハンドルの塗り。GizmoRender の DrawQuad / DrawTri と同じ</summary>
        private const float PlaneFillAlpha = 0.3f;
        /// <summary>面ハンドルの一辺。GizmoRender と同じく軸長の 0.3 倍</summary>
        private const float PlaneSizeRatio = 0.3f;
        /// <summary>
        /// カメラ座標系の回転で視線まわりを回す外周リングの半径 (軸長に対する比)。
        /// 右・上の線 (長さ = 軸長) の端と離して掴み分けられるよう外側に置く
        /// </summary>
        private const float ViewRingRadiusRatio = 1.2f;
        /// <summary>外周リングは全周を描くので半周の倍の分割</summary>
        private const int FullCircleSegments = CircleSegments * 2;
        /// <summary>外周リングの色。軸の色と区別するため白にする</summary>
        private static readonly Color ViewRingColor = new Color(1f, 1f, 1f, 0.8f);

        // GL 描画用マテリアルは全インスタンス共有
        private static Material _lineMaterial;
        private static bool _materialFailed;

        // 毎フレームの GC を避けるため使い回す
        private readonly Vector3[] _cubeCorners = new Vector3[4];

        // ドラッグ状態 (GizmoRenderer から移植)
        private Camera _dragCamera;
        private int _dragAxis = -1;      // 軸ドラッグ中の軸。面ドラッグ中は -1
        private int _dragPlane = -1;     // 面ドラッグ中の法線の軸。軸ドラッグ中は -1
        private Vector3 _dragStartPosition;
        private Quaternion _dragStartRotation;
        private Vector3 _dragStartScale;
        private float _dragStartParam;   // 軸上パラメータ or 回転角
        private Vector3 _dragStartPlanePoint;  // 面ドラッグ開始時の面上の交点
        // ドラッグ開始時の軸方向。Local モードでは軸が target の回転に追従するため、
        // 現在値を使うと回転ドラッグで軸自体が動いてフィードバックし対象が暴れる
        private Vector3 _dragAxisDir;
        // 面ドラッグの面の法線。軸と同じく開始時に固定する
        // (カメラ座標系ではドラッグ中にカメラが動くと面が変わってしまうため)
        private Vector3 _dragPlaneNormal;
        // 画面上の移動量で回しているか (カメラ座標系の右・上リング)
        private bool _dragByScreen;
        private Vector2 _dragStartRtPoint;  // 画面ドラッグ開始時の RT 座標
        private float _dragRadiusPixels;    // 画面ドラッグのリング半径 (px)
        // ドラッグ開始時の軸 3 本。カメラ座標系の描画に使う
        private GizmoBasis _dragBasis;

        /// <summary>GL 用マテリアルを遅延生成する。シェーダ不在なら false</summary>
        public static bool EnsureMaterial()
        {
            if (_lineMaterial != null) return true;
            if (_materialFailed) return false;

            // GL 描画用の頂点カラーシェーダ。ゲームのビルドに含まれない可能性があるため
            // 取得できなければギズモ描画だけ諦める
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                _materialFailed = true;
                MTEUtils.LogError("ギズモ描画用シェーダ (Hidden/Internal-Colored) が見つかりません。ギズモは表示されません");
                return false;
            }

            _lineMaterial = new Material(shader);
            _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            // ギズモは常に手前に見せたいので深度テストを無効化する
            _lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            return true;
        }

        /// <summary>
        /// ギズモの大きさを合わせる基準の画角。この画角では補正前 (距離 × GizmoScreenScale) と一致する
        /// </summary>
        private const float ReferenceFov = 45f;

        /// <summary>
        /// 画面上の見かけの大きさを一定に保つギズモの世界サイズ。
        /// 距離だけに比例させると、望遠 (fov 小) で画面いっぱいに広がり、広角で小さくなりすぎるため、
        /// 対象位置での画面の高さで正規化する。
        /// ギズモ以外のカメラ距離比例な描画 (ライトアイコン等) からも使う
        /// </summary>
        public static float CalcGizmoSize(Camera camera, Vector3 position)
        {
            // オルソでは距離が見かけの大きさに影響しないので、画角の代わりに表示範囲を使う
            var halfHeight = camera.orthographic
                ? camera.orthographicSize
                : Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                    * Vector3.Distance(camera.transform.position, position);
            return CalcGizmoSizeFromHalfHeight(halfHeight);
        }

        /// <summary>
        /// 対象位置での「画面半分の高さ」からギズモの世界サイズを求める。
        /// Camera を必要としないので単体テストから検証できる
        /// </summary>
        public static float CalcGizmoSizeFromHalfHeight(float halfHeight)
        {
            return halfHeight * GizmoScreenScale / Mathf.Tan(ReferenceFov * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>カメラ座標系で視線方向になる軸の番号 (前)</summary>
        public const int ViewAxis = 2;

        /// <summary>画面ドラッグ角の換算で、画面上の半径がこれより小さいときの下限 (px)。0 除算を避ける</summary>
        private const float MinScreenRadiusPixels = 1f;

        /// <summary>
        /// ツールに対して実際に使う座標系。拡縮は localScale をローカル軸で伸ばすだけなので、
        /// カメラ軸で描くと見た目の軸と伸びる方向が食い違う。カメラ指定でもローカルで動かす
        /// </summary>
        public static GizmoSpace ResolveSpace(GizmoSpace space, GizmoTool tool)
        {
            if (space == GizmoSpace.Camera && tool == GizmoTool.Scale)
            {
                return GizmoSpace.Local;
            }
            return space;
        }

        /// <summary>座標系に応じた軸 3 本。Global はワールド軸で、local / camera は使わない</summary>
        public static GizmoBasis SelectBasis(GizmoSpace space, GizmoBasis local, GizmoBasis camera)
        {
            switch (space)
            {
                case GizmoSpace.Local: return local;
                case GizmoSpace.Camera: return camera;
                default: return GizmoBasis.world;
            }
        }

        /// <summary>
        /// 軸ハンドル (移動・拡縮の矢印) を出すか。カメラ座標系の前軸は視線と重なって
        /// 画面上で点に潰れ、掴むと奥行きが大きく飛ぶため出さない
        /// </summary>
        public static bool IsAxisHandleEnabled(GizmoSpace space, int axis)
        {
            return space != GizmoSpace.Camera || axis != ViewAxis;
        }

        /// <summary>
        /// 面ハンドルを出すか。カメラ座標系では画面に平行な面 (法線が前軸) 以外は
        /// 視線を含んで線に潰れ、中心付近のクリックを奪うため出さない
        /// </summary>
        public static bool IsPlaneHandleEnabled(GizmoSpace space, int normalAxis)
        {
            return space != GizmoSpace.Camera || normalAxis == ViewAxis;
        }

        /// <summary>回転ツールで、画面に正対する外周リングとして描く軸か (カメラ座標系の前軸)</summary>
        public static bool IsViewRing(GizmoSpace space, int axis)
        {
            return space == GizmoSpace.Camera && axis == ViewAxis;
        }

        /// <summary>
        /// 回転ツールで、面との交点ではなく画面上の移動量で回す軸か (カメラ座標系の右・上)。
        /// このリングは視線を含む面にあり、画面上で線に潰れて角度が取れない
        /// </summary>
        public static bool IsScreenDragRing(GizmoSpace space, int axis)
        {
            return space == GizmoSpace.Camera && axis != ViewAxis;
        }

        /// <summary>
        /// カメラ座標系の右・上軸まわりのリングを画面ドラッグで回す角度 (度)。
        /// トラックボールと同じく、リングの手前側がマウスに付いて動く向きに回し、半径ぶん動かすと 1 rad。
        /// 回転の微分は「軸 × 点」で、手前側の点はカメラの後ろ向きにあるため、
        /// 右軸の正回転は手前側を画面の上へ、上軸の正回転は画面の左へ動かす
        /// </summary>
        /// <param name="axis">0 = 右、1 = 上</param>
        /// <param name="delta">ドラッグ開始からの RT ピクセル移動量 (左下原点)</param>
        /// <param name="radiusPixels">リング半径の画面上の長さ</param>
        public static float CalcScreenDragAngle(int axis, Vector2 delta, float radiusPixels)
        {
            var radius = Mathf.Max(radiusPixels, MinScreenRadiusPixels);
            var along = axis == 0 ? delta.y : -delta.x;
            return along / radius * Mathf.Rad2Deg;
        }

        private float GizmoSize(Camera camera, Vector3 position)
        {
            return CalcGizmoSize(camera, position) * sizeScale;
        }

        /// <summary>ツールを考慮した実際の座標系 (拡縮のカメラはローカル)</summary>
        private GizmoSpace effectiveSpace
        {
            get { return ResolveSpace(space, tool); }
        }

        /// <summary>
        /// 今の座標系で使う軸 3 本。Transform のプロパティ取得を軸ごとに繰り返さないよう、
        /// 描画・判定の入口で 1 回だけ求めて使い回す。カメラ座標系では渡されたカメラの軸
        /// </summary>
        private GizmoBasis CurrentAxes(Camera camera)
        {
            // SelectBasis と同じ選び方。両方の基底を渡すと使わない側の Transform まで毎回読むため分岐する
            switch (effectiveSpace)
            {
                case GizmoSpace.Local: return GizmoBasis.FromTransform(target);
                case GizmoSpace.Camera: return GizmoBasis.FromTransform(camera.transform);
                default: return GizmoBasis.world;
            }
        }

        /// <summary>
        /// 描画に使う軸 3 本。カメラ座標系で掴んでいる間は、操作と同じく開始時の軸で描く
        /// (ドラッグ中にカメラが動いたとき、描いた矢印と実際に動く向きが食い違わないように)
        /// </summary>
        private GizmoBasis DrawAxes(Camera camera)
        {
            if (isDragging && camera == _dragCamera && effectiveSpace == GizmoSpace.Camera)
            {
                return _dragBasis;
            }
            return CurrentAxes(camera);
        }

        /// <summary>指定カメラの OnPostRender から呼ぶ</summary>
        public void Draw(Camera camera)
        {
            if (target == null || tool == GizmoTool.None || !EnsureMaterial())
            {
                return;
            }

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;

            var origin = target.position;
            var size = GizmoSize(camera, origin);
            var drawSpace = effectiveSpace;
            var axes = DrawAxes(camera);

            switch (tool)
            {
                case GizmoTool.Move:
                case GizmoTool.Scale:
                {
                    var isScale = tool == GizmoTool.Scale;
                    for (var axis = 0; axis < 3; axis++)
                    {
                        if (IsAxisHandleEnabled(drawSpace, axis))
                        {
                            DrawAxisLine(origin, axes[axis], size, AxisColor(axis), isScale);
                        }
                    }
                    // 2 軸を同時に動かす面ハンドル。移動は四角、拡縮は三角で GizmoRender と揃える
                    for (var axis = 0; axis < 3; axis++)
                    {
                        if (IsPlaneHandleEnabled(drawSpace, axis))
                        {
                            DrawPlaneHandle(origin, axes, axis, size * PlaneSizeRatio, PlaneColor(axis), isScale);
                        }
                    }
                    break;
                }
                case GizmoTool.Rotate:
                    for (var axis = 0; axis < 3; axis++)
                    {
                        if (IsViewRing(drawSpace, axis))
                        {
                            DrawFullCircle(origin, axes[axis], size * ViewRingRadiusRatio, ViewRingDrawColor(axis));
                        }
                        else
                        {
                            // カメラ座標系の右・上リングは視線を含む面にあり、画面上では線として描かれる
                            DrawCircle(camera, origin, axes[axis], size, AxisColor(axis));
                        }
                    }
                    break;
            }

            GL.PopMatrix();
        }

        /// <summary>ドラッグ中の軸だけハイライトする</summary>
        private Color AxisColor(int axis)
        {
            return isDragging && _dragAxis == axis ? SelectedAxisColor : AxisColors[axis];
        }

        /// <summary>ドラッグ中の面だけハイライトする</summary>
        private Color PlaneColor(int normalAxis)
        {
            return isDragging && _dragPlane == normalAxis ? SelectedPlaneColor : AxisColors[normalAxis];
        }

        /// <summary>外周リングの色。掴んでいる間は軸と同じ選択色にする</summary>
        private Color ViewRingDrawColor(int axis)
        {
            return isDragging && _dragAxis == axis ? SelectedAxisColor : ViewRingColor;
        }

        /// <summary>面ハンドルを張る 2 軸。法線の軸以外の 2 本を使う</summary>
        private static void PlaneAxes(GizmoBasis axes, int normalAxis, out Vector3 u, out Vector3 v)
        {
            u = axes[(normalAxis + 1) % 3];
            v = axes[(normalAxis + 2) % 3];
        }

        /// <summary>
        /// 2 軸を同時に動かす面ハンドル。半透明で塗ったうえで輪郭を描く
        /// (GizmoRender の DrawQuad / DrawTri と同じ見た目)
        /// </summary>
        private void DrawPlaneHandle(
            Vector3 origin, GizmoBasis axes, int normalAxis, float size, Color color, bool triangle)
        {
            Vector3 u, v;
            PlaneAxes(axes, normalAxis, out u, out v);

            var a = origin;
            var b = origin + u * size;
            var c = origin + (u + v) * size;
            var d = origin + v * size;

            var fill = color;
            fill.a = PlaneFillAlpha;

            if (triangle)
            {
                GL.Begin(GL.TRIANGLES);
                GL.Color(fill);
                GL.Vertex(a); GL.Vertex(b); GL.Vertex(d);
                GL.End();

                GL.Begin(GL.LINES);
                GL.Color(color);
                GL.Vertex(a); GL.Vertex(b);
                GL.Vertex(b); GL.Vertex(d);
                GL.Vertex(d); GL.Vertex(a);
                GL.End();
                return;
            }

            GL.Begin(GL.QUADS);
            GL.Color(fill);
            GL.Vertex(a); GL.Vertex(b); GL.Vertex(c); GL.Vertex(d);
            GL.End();

            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex(a); GL.Vertex(b);
            GL.Vertex(b); GL.Vertex(c);
            GL.Vertex(c); GL.Vertex(d);
            GL.Vertex(d); GL.Vertex(a);
            GL.End();
        }

        /// <summary>軸線と先端の立体。移動は円錐の矢じり、拡縮は立方体で見分ける</summary>
        private void DrawAxisLine(Vector3 origin, Vector3 dir, float length, Color color, bool boxTip)
        {
            var tip = origin + dir * length;

            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex(origin);
            GL.Vertex(tip);
            GL.End();

            var radius = length * TipRadiusRatio;
            var tipBase = origin + dir * (length * TipBaseRatio);

            if (boxTip)
            {
                DrawCubeTip(tipBase, dir, radius, color);
            }
            else
            {
                DrawConeTip(tip, tipBase, dir, radius, color);
            }
        }

        /// <summary>矢じりの円錐。底面の縁と頂点を結ぶ三角形で張る</summary>
        private static void DrawConeTip(Vector3 tip, Vector3 baseCenter, Vector3 dir, float radius, Color color)
        {
            Vector3 basis1, basis2;
            CalcCircleBasis(dir, out basis1, out basis2);

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            for (var i = 0; i < ConeSegments; i++)
            {
                var a0 = i * Mathf.PI * 2f / ConeSegments;
                var a1 = (i + 1) * Mathf.PI * 2f / ConeSegments;
                GL.Vertex(baseCenter + (basis1 * Mathf.Cos(a0) + basis2 * Mathf.Sin(a0)) * radius);
                GL.Vertex(baseCenter + (basis1 * Mathf.Cos(a1) + basis2 * Mathf.Sin(a1)) * radius);
                GL.Vertex(tip);
            }
            GL.End();
        }

        /// <summary>拡縮の先端に置く立方体。軸方向を法線とする 2 面と、両者をつなぐ 4 本の柱で表す</summary>
        private void DrawCubeTip(Vector3 center, Vector3 dir, float radius, Color color)
        {
            Vector3 basis1, basis2;
            CalcCircleBasis(dir, out basis1, out basis2);

            var u = basis1 * radius;
            var v = basis2 * radius;
            var w = dir * radius;

            // 面の 4 隅を反時計回りに並べ、隣り合う隅を結んで辺を張る
            _cubeCorners[0] = center + u + v;
            _cubeCorners[1] = center - u + v;
            _cubeCorners[2] = center - u - v;
            _cubeCorners[3] = center + u - v;

            GL.Begin(GL.LINES);
            GL.Color(color);
            for (var i = 0; i < 4; i++)
            {
                var a = _cubeCorners[i];
                var b = _cubeCorners[(i + 1) % 4];

                GL.Vertex(a - w); GL.Vertex(b - w);  // 奥の面
                GL.Vertex(a + w); GL.Vertex(b + w);  // 手前の面
                GL.Vertex(a - w); GL.Vertex(a + w);  // 2 面をつなぐ柱
            }
            GL.End();
        }

        /// <summary>
        /// 回転リング。GizmoRender と同じく、カメラを向いている側の半周だけ描く
        /// (裏側まで描くと重なって軸が読み取りにくくなるため)
        /// </summary>
        private void DrawCircle(Camera camera, Vector3 center, Vector3 axis, float radius, Color color)
        {
            Vector3 basis1, basis2;
            CalcVisibleArcBasis(camera, center, axis, radius, out basis1, out basis2);
            DrawArc(center, basis1, basis2, CircleSegments, color);
        }

        /// <summary>basis1 / basis2 が張る円を segments 分割ぶん線で描く (半周か全周かは分割数で決まる)</summary>
        private static void DrawArc(Vector3 center, Vector3 basis1, Vector3 basis2, int segments, Color color)
        {
            GL.Begin(GL.LINES);
            GL.Color(color);
            for (var i = 0; i < segments; i++)
            {
                GL.Vertex(ArcPoint(center, basis1, basis2, ArcAngle(i)));
                GL.Vertex(ArcPoint(center, basis1, basis2, ArcAngle(i + 1)));
            }
            GL.End();
        }

        /// <summary>画面に正対する外周リング。手前・奥の区別が無いので全周を描く</summary>
        private static void DrawFullCircle(Vector3 center, Vector3 axis, float radius, Color color)
        {
            Vector3 basis1, basis2;
            CalcCircleBasis(axis, out basis1, out basis2);
            DrawArc(center, basis1 * radius, basis2 * radius, FullCircleSegments, color);
        }

        /// <summary>
        /// 手前側の半周を張る基底 (長さは radius 込み)。
        /// 軸に垂直かつカメラ方向に依存した基底を取ると、角度 0〜π がそのまま手前側になる。
        /// 描画とヒット判定で同じ弧を使うため共有する
        /// </summary>
        private static void CalcVisibleArcBasis(
            Camera camera, Vector3 center, Vector3 axis, float radius,
            out Vector3 basis1, out Vector3 basis2)
        {
            var toCamera = camera.transform.position - center;

            basis1 = Vector3.Cross(axis, toCamera);
            if (basis1.sqrMagnitude < DegenerateEpsilon)
            {
                CalcCircleBasis(axis, out basis1, out _);
            }
            basis1 = basis1.normalized * radius;
            basis2 = Vector3.Cross(basis1, axis).normalized * radius;
        }

        /// <summary>半周を CircleSegments 等分したときの index 番目の角度 (rad)</summary>
        private static float ArcAngle(int index)
        {
            return index * Mathf.PI / CircleSegments;
        }

        private static Vector3 ArcPoint(Vector3 center, Vector3 basis1, Vector3 basis2, float angle)
        {
            return center + basis1 * Mathf.Cos(angle) + basis2 * Mathf.Sin(angle);
        }

        /// <summary>回転面の直交基底。軸が真上を向いていると外積が潰れるため別の軸で取り直す</summary>
        public static void CalcCircleBasis(Vector3 axis, out Vector3 basis1, out Vector3 basis2)
        {
            basis1 = Vector3.Cross(axis, Vector3.up);
            if (basis1.sqrMagnitude < DegenerateEpsilon)
            {
                basis1 = Vector3.Cross(axis, Vector3.right);
            }
            basis1.Normalize();
            basis2 = Vector3.Cross(axis, basis1).normalized;
        }

        // ---- ヒット判定・ドラッグ ----

        /// <summary>ワールド座標を RT ピクセル座標 (左下原点) へ。カメラ背後は無効値</summary>
        private static Vector2 ToRtPoint(Camera camera, Vector3 worldPos, out bool valid)
        {
            var sp = camera.WorldToScreenPoint(worldPos);
            valid = sp.z > 0f;
            return new Vector2(sp.x, sp.y);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>rtPoint がいずれかのギズモ要素上ならドラッグを開始して true</summary>
        public bool TryBeginDrag(Camera camera, Vector2 rtPoint)
        {
            if (camera == null || target == null || tool == GizmoTool.None)
            {
                return false;
            }

            var origin = target.position;
            var size = GizmoSize(camera, origin);
            var dragSpace = effectiveSpace;
            var axes = CurrentAxes(camera);

            // 面ハンドルを先に見る。面の 2 辺は軸線と重なっているため、
            // 軸を優先すると四角形の内側でも軸を掴んでしまう
            var bestPlane = -1;
            if (tool != GizmoTool.Rotate)
            {
                for (var axis = 0; axis < 3; axis++)
                {
                    if (!IsPlaneHandleEnabled(dragSpace, axis))
                    {
                        continue;
                    }
                    if (IsInsidePlaneHandle(camera, rtPoint, origin, axes, axis, size * PlaneSizeRatio,
                        tool == GizmoTool.Scale))
                    {
                        bestPlane = axis;
                        break;
                    }
                }
            }

            // 面ハンドルを掴めなかったときだけ軸を見る
            var bestAxis = -1;
            if (bestPlane < 0)
            {
                var bestDistance = tool == GizmoTool.Rotate ? RotateHitThreshold : HitThreshold;
                for (var axis = 0; axis < 3; axis++)
                {
                    float distance;
                    var axisDir = axes[axis];
                    if (tool == GizmoTool.Rotate)
                    {
                        distance = DistanceToRotateHandle(camera, rtPoint, dragSpace, axis, origin, axisDir, size);
                    }
                    else
                    {
                        if (!IsAxisHandleEnabled(dragSpace, axis))
                        {
                            continue;
                        }
                        bool v0, v1;
                        var a = ToRtPoint(camera, origin, out v0);
                        var b = ToRtPoint(camera, origin + axisDir * size, out v1);
                        distance = (v0 && v1) ? DistanceToSegment(rtPoint, a, b) : float.MaxValue;
                    }

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestAxis = axis;
                    }
                }
            }

            if (bestAxis < 0 && bestPlane < 0)
            {
                return false;
            }

            isDragging = true;
            // ドラッグ解決は掴んだカメラ基準で行う。別ビューの座標で解釈されないよう保持する
            _dragCamera = camera;
            _dragAxis = bestAxis;
            _dragPlane = bestPlane;
            // 軸・面の法線は開始時に固定する。Local では回転ドラッグで軸自体が動き、
            // Camera ではドラッグ中のカメラ移動で軸が変わるため、現在値を使うと対象が暴れる
            _dragAxisDir = bestAxis >= 0 ? axes[bestAxis] : Vector3.zero;
            _dragPlaneNormal = bestPlane >= 0 ? axes[bestPlane] : Vector3.zero;
            _dragBasis = axes;
            _dragByScreen = tool == GizmoTool.Rotate && bestAxis >= 0 && IsScreenDragRing(dragSpace, bestAxis);
            _dragStartPosition = target.position;
            _dragStartRotation = target.rotation;
            _dragStartScale = target.localScale;

            if (bestPlane >= 0)
            {
                // 視線と面がほぼ平行だと交点が取れない。基準点が定まらないまま
                // ドラッグを始めると前回の残留値を基準にして対象が飛ぶため、掴まない
                if (!PlanePointAt(camera, rtPoint, out _dragStartPlanePoint))
                {
                    EndDrag();
                    return false;
                }
            }
            else if (_dragByScreen)
            {
                _dragStartRtPoint = rtPoint;
                _dragRadiusPixels = ScreenLength(camera, origin, size);
                // 原点がカメラの背後などで半径が取れないと、わずかな移動で大きく回ってしまう
                if (_dragRadiusPixels <= 0f)
                {
                    EndDrag();
                    return false;
                }
            }
            else if (tool == GizmoTool.Rotate)
            {
                // 開始角が取れないまま掴むと、最初の更新で角度差が丸ごとズレて
                // 対象が飛ぶ。面ドラッグと同じく掴まないことで防ぐ
                if (!TryRotationAngleAt(camera, rtPoint, out _dragStartParam))
                {
                    EndDrag();
                    return false;
                }
            }
            else
            {
                _dragStartParam = AxisParamAt(camera, rtPoint);
            }
            return true;
        }

        /// <summary>
        /// 回転ハンドル (リング) までの画面距離。掴めないリングは float.MaxValue
        /// </summary>
        private float DistanceToRotateHandle(
            Camera camera, Vector2 rtPoint, GizmoSpace dragSpace, int axis, Vector3 origin, Vector3 axisDir, float size)
        {
            if (IsViewRing(dragSpace, axis))
            {
                return DistanceToFullCircle(camera, rtPoint, origin, axisDir, size * ViewRingRadiusRatio);
            }
            if (IsScreenDragRing(dragSpace, axis))
            {
                // 視線を含む面のリングは線に潰れて描かれる。面の角度は取れないが
                // 画面上の移動量で回すので、描かれた線そのものを掴ませる
                return DistanceToCircle(camera, rtPoint, origin, axisDir, size);
            }
            // 視線と平行に近い回転面は角度が安定しないので候補から外す。
            // ここで弾いておけば手前に見えている別の軸を掴める
            if (!IsRotationPlaneStable(RayDirection(camera, rtPoint), axisDir))
            {
                return float.MaxValue;
            }
            return DistanceToCircle(camera, rtPoint, origin, axisDir, size);
        }

        /// <summary>rtPoint が面ハンドルの内側か。画面へ投影した多角形で判定する</summary>
        private bool IsInsidePlaneHandle(
            Camera camera, Vector2 rtPoint, Vector3 origin, GizmoBasis axes, int normalAxis, float size,
            bool triangle)
        {
            Vector3 u, v;
            PlaneAxes(axes, normalAxis, out u, out v);

            bool va, vb, vc, vd;
            var a = ToRtPoint(camera, origin, out va);
            var b = ToRtPoint(camera, origin + u * size, out vb);
            var c = ToRtPoint(camera, origin + (u + v) * size, out vc);
            var d = ToRtPoint(camera, origin + v * size, out vd);

            if (!va || !vb || !vd)
            {
                return false;
            }

            if (triangle)
            {
                return IsInsideTriangle(rtPoint, a, b, d);
            }

            return vc && (IsInsideTriangle(rtPoint, a, b, c) || IsInsideTriangle(rtPoint, a, c, d));
        }

        /// <summary>三角形の内外判定。3 辺すべてで外積の符号が揃えば内側</summary>
        private static bool IsInsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Cross2(p - a, b - a);
            var d2 = Cross2(p - b, c - b);
            var d3 = Cross2(p - c, a - c);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Cross2(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        /// <summary>マウスレイと操作面 (開始時に固定した法線) の交点。面と平行で交わらなければ false</summary>
        private bool PlanePointAt(Camera camera, Vector2 rtPoint, out Vector3 point)
        {
            var plane = new Plane(_dragPlaneNormal, _dragStartPosition);
            var ray = camera.ScreenPointToRay(new Vector3(rtPoint.x, rtPoint.y, 0f));

            float enter;
            if (!plane.Raycast(ray, out enter))
            {
                point = _dragStartPlanePoint;
                return false;
            }

            point = ray.GetPoint(enter);
            return true;
        }

        /// <summary>
        /// 見えている半周までの画面距離。描画と同じ弧で判定しないと、
        /// 描かれていない裏側の半周まで掴めてしまう
        /// </summary>
        private float DistanceToCircle(
            Camera camera, Vector2 rtPoint, Vector3 center, Vector3 axis, float radius)
        {
            Vector3 basis1, basis2;
            CalcVisibleArcBasis(camera, center, axis, radius, out basis1, out basis2);
            return DistanceToArc(camera, rtPoint, center, basis1, basis2, CircleSegments);
        }

        /// <summary>DrawArc と同じ弧までの画面距離。カメラの背後にかかる線分は見ない</summary>
        private static float DistanceToArc(
            Camera camera, Vector2 rtPoint, Vector3 center, Vector3 basis1, Vector3 basis2, int segments)
        {
            var best = float.MaxValue;
            for (var i = 0; i < segments; i++)
            {
                bool v0, v1;
                var p0 = ToRtPoint(camera, ArcPoint(center, basis1, basis2, ArcAngle(i)), out v0);
                var p1 = ToRtPoint(camera, ArcPoint(center, basis1, basis2, ArcAngle(i + 1)), out v1);
                if (v0 && v1)
                {
                    best = Mathf.Min(best, DistanceToSegment(rtPoint, p0, p1));
                }
            }
            return best;
        }

        /// <summary>外周リング (全周) までの画面距離。描画と同じ全周で判定する</summary>
        private float DistanceToFullCircle(
            Camera camera, Vector2 rtPoint, Vector3 center, Vector3 axis, float radius)
        {
            Vector3 basis1, basis2;
            CalcCircleBasis(axis, out basis1, out basis2);
            return DistanceToArc(camera, rtPoint, center, basis1 * radius, basis2 * radius, FullCircleSegments);
        }

        /// <summary>
        /// 対象位置での長さ length の画面上のピクセル長。画面に平行な方向 (カメラの上) で測る。
        /// カメラの背後なら 0 (CalcScreenDragAngle 側で下限に丸める)
        /// </summary>
        private static float ScreenLength(Camera camera, Vector3 origin, float length)
        {
            bool v0, v1;
            var a = ToRtPoint(camera, origin, out v0);
            var b = ToRtPoint(camera, origin + camera.transform.up * length, out v1);
            return v0 && v1 ? Vector2.Distance(a, b) : 0f;
        }

        /// <summary>マウスレイとドラッグ軸の最近接パラメータ (軸方向の距離 m)</summary>
        private float AxisParamAt(Camera camera, Vector2 rtPoint)
        {
            var ray = camera.ScreenPointToRay(new Vector3(rtPoint.x, rtPoint.y, 0f));
            var axisDir = _dragAxisDir;

            // 2 直線の最近接点: 軸上のパラメータ t を解く。
            // 標準形は w = 軸原点 - レイ原点。符号を逆にするとドラッグ方向が反転するので注意
            var w = _dragStartPosition - ray.origin;
            var b = Vector3.Dot(axisDir, ray.direction);
            var d = Vector3.Dot(axisDir, w);
            var e = Vector3.Dot(ray.direction, w);
            var denom = 1f - b * b;
            if (Mathf.Abs(denom) < ParallelEpsilon)
            {
                return 0f; // 軸とレイがほぼ平行
            }
            return (b * e - d) / denom;
        }

        /// <summary>RT ピクセル座標を通すマウスレイの方向</summary>
        private static Vector3 RayDirection(Camera camera, Vector2 rtPoint)
        {
            return camera.ScreenPointToRay(new Vector3(rtPoint.x, rtPoint.y, 0f)).direction;
        }

        /// <summary>
        /// 回転面がレイに対して十分傾いていて、角度を安定して取れるか。
        /// 当たり判定と角度計算で基準がずれると「掴めたのに角度が取れない」が起きるため、
        /// どちらも同じマウスレイの方向で判定する
        /// </summary>
        private static bool IsRotationPlaneStable(Vector3 rayDirection, Vector3 axis)
        {
            return Mathf.Abs(Vector3.Dot(rayDirection, axis)) >= RotationPlaneMinDot;
        }

        /// <summary>
        /// 回転面上でのマウス位置の角度 (度)。レイが面と交わらない
        /// (視線とほぼ平行 / 面の裏側を指している) 場合は false を返す。
        /// 角度 0 で代用すると開始角との差が丸ごと回転量になり対象が飛ぶ
        /// </summary>
        private bool TryRotationAngleAt(Camera camera, Vector2 rtPoint, out float angle)
        {
            angle = 0f;

            var axis = _dragAxisDir;
            var ray = camera.ScreenPointToRay(new Vector3(rtPoint.x, rtPoint.y, 0f));
            if (!IsRotationPlaneStable(ray.direction, axis))
            {
                return false;
            }

            var plane = new Plane(axis, _dragStartPosition);
            float enter;
            if (!plane.Raycast(ray, out enter))
            {
                return false;
            }

            var onPlane = ray.GetPoint(enter) - _dragStartPosition;
            Vector3 basis1, basis2;
            CalcCircleBasis(axis, out basis1, out basis2);
            angle = Mathf.Atan2(Vector3.Dot(onPlane, basis2), Vector3.Dot(onPlane, basis1)) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>ドラッグを進める。座標は TryBeginDrag と同じビューの RT ピクセル座標</summary>
        public void UpdateDrag(Vector2 rtPoint)
        {
            if (!isDragging || target == null || _dragCamera == null)
            {
                EndDrag();
                return;
            }

            if (_dragPlane >= 0)
            {
                UpdatePlaneDrag(rtPoint);
                return;
            }

            switch (tool)
            {
                case GizmoTool.Move:
                {
                    var t = AxisParamAt(_dragCamera, rtPoint) - _dragStartParam;
                    target.position = _dragStartPosition + _dragAxisDir * t;
                    break;
                }
                case GizmoTool.Rotate:
                {
                    if (_dragByScreen)
                    {
                        var angle = CalcScreenDragAngle(_dragAxis, rtPoint - _dragStartRtPoint, _dragRadiusPixels);
                        target.rotation = Quaternion.AngleAxis(angle, _dragAxisDir) * _dragStartRotation;
                        break;
                    }
                    float current;
                    // 面から外れたフレームは角度が取れない。据え置いて次のフレームを待つ
                    if (!TryRotationAngleAt(_dragCamera, rtPoint, out current))
                    {
                        return;
                    }
                    target.rotation =
                        Quaternion.AngleAxis(current - _dragStartParam, _dragAxisDir) * _dragStartRotation;
                    break;
                }
                case GizmoTool.Scale:
                {
                    var size = GizmoSize(_dragCamera, _dragStartPosition);
                    var t = AxisParamAt(_dragCamera, rtPoint) - _dragStartParam;
                    var factor = Mathf.Max(1f + t / size, 0.01f);
                    var scale = _dragStartScale;
                    scale[_dragAxis] *= factor;
                    target.localScale = scale;
                    break;
                }
            }

            NotifyTransformChanged();
        }

        /// <summary>
        /// 面ハンドルのドラッグ。移動は面上の変位をそのまま足し、
        /// 拡縮は原点からの距離比を面を張る 2 軸へ掛ける
        /// </summary>
        private void UpdatePlaneDrag(Vector2 rtPoint)
        {
            Vector3 point;
            if (!PlanePointAt(_dragCamera, rtPoint, out point))
            {
                return;
            }

            if (tool == GizmoTool.Move)
            {
                target.position = _dragStartPosition + (point - _dragStartPlanePoint);
                NotifyTransformChanged();
                return;
            }

            var startVector = _dragStartPlanePoint - _dragStartPosition;
            var startLength = startVector.magnitude;
            if (startLength < ParallelEpsilon)
            {
                // 原点を掴んだ場合は基準が取れないので拡縮しない
                return;
            }

            var factor = Mathf.Max((point - _dragStartPosition).magnitude / startLength, 0.01f);
            var scale = _dragStartScale;
            scale[(_dragPlane + 1) % 3] *= factor;
            scale[(_dragPlane + 2) % 3] *= factor;
            target.localScale = scale;
            NotifyTransformChanged();
        }

        private void NotifyTransformChanged()
        {
            if (onTransformChanged != null)
            {
                onTransformChanged();
            }
        }

        public void EndDrag()
        {
            isDragging = false;
            _dragCamera = null;
            _dragAxis = -1;
            _dragPlane = -1;
            _dragByScreen = false;
        }
    }
}
