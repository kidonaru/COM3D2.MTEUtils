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
        public bool useLocalSpace = true;
        /// <summary>表示倍率。配置モデル用に小さくする場合などに使う</summary>
        public float sizeScale = 1f;
        /// <summary>ドラッグで target を書き換えた直後に呼ばれる</summary>
        public Action onTransformChanged;

        public bool isDragging { get; private set; }

        private const float HitThreshold = 8f;
        private const float GizmoScreenScale = 0.15f; // カメラ距離に対するギズモサイズ比

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
        /// カメラ距離に比例したギズモの世界サイズ。見かけの大きさを一定に保つ。
        /// ギズモ以外のカメラ距離比例な描画 (ライトアイコン等) からも使う
        /// </summary>
        public static float CalcGizmoSize(Camera camera, Vector3 position)
        {
            return Vector3.Distance(camera.transform.position, position) * GizmoScreenScale;
        }

        private float GizmoSize(Camera camera, Vector3 position)
        {
            return CalcGizmoSize(camera, position) * sizeScale;
        }

        /// <summary>軸方向 (Local/Global 設定に従う)</summary>
        private Vector3 AxisDirection(int axis)
        {
            if (useLocalSpace)
            {
                switch (axis)
                {
                    case 0: return target.right;
                    case 1: return target.up;
                    default: return target.forward;
                }
            }

            switch (axis)
            {
                case 0: return Vector3.right;
                case 1: return Vector3.up;
                default: return Vector3.forward;
            }
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

            switch (tool)
            {
                case GizmoTool.Move:
                case GizmoTool.Scale:
                {
                    var isScale = tool == GizmoTool.Scale;
                    for (var axis = 0; axis < 3; axis++)
                    {
                        DrawAxisLine(origin, AxisDirection(axis), size, AxisColor(axis), isScale);
                    }
                    // 2 軸を同時に動かす面ハンドル。移動は四角、拡縮は三角で GizmoRender と揃える
                    for (var axis = 0; axis < 3; axis++)
                    {
                        DrawPlaneHandle(origin, axis, size * PlaneSizeRatio, PlaneColor(axis), isScale);
                    }
                    break;
                }
                case GizmoTool.Rotate:
                    for (var axis = 0; axis < 3; axis++)
                    {
                        DrawCircle(camera, origin, AxisDirection(axis), size, AxisColor(axis));
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

        /// <summary>面ハンドルを張る 2 軸。法線の軸以外の 2 本を使う</summary>
        private void PlaneAxes(int normalAxis, out Vector3 u, out Vector3 v)
        {
            u = AxisDirection((normalAxis + 1) % 3);
            v = AxisDirection((normalAxis + 2) % 3);
        }

        /// <summary>
        /// 2 軸を同時に動かす面ハンドル。半透明で塗ったうえで輪郭を描く
        /// (GizmoRender の DrawQuad / DrawTri と同じ見た目)
        /// </summary>
        private void DrawPlaneHandle(Vector3 origin, int normalAxis, float size, Color color, bool triangle)
        {
            Vector3 u, v;
            PlaneAxes(normalAxis, out u, out v);

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

            GL.Begin(GL.LINES);
            GL.Color(color);
            for (var i = 0; i < CircleSegments; i++)
            {
                GL.Vertex(ArcPoint(center, basis1, basis2, ArcAngle(i)));
                GL.Vertex(ArcPoint(center, basis1, basis2, ArcAngle(i + 1)));
            }
            GL.End();
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
            var bestAxis = -1;
            var bestDistance = HitThreshold;

            for (var axis = 0; axis < 3; axis++)
            {
                float distance;
                if (tool == GizmoTool.Rotate)
                {
                    distance = DistanceToCircle(camera, rtPoint, origin, AxisDirection(axis), size);
                }
                else
                {
                    bool v0, v1;
                    var a = ToRtPoint(camera, origin, out v0);
                    var b = ToRtPoint(camera, origin + AxisDirection(axis) * size, out v1);
                    distance = (v0 && v1) ? DistanceToSegment(rtPoint, a, b) : float.MaxValue;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestAxis = axis;
                }
            }

            // 軸を掴めなかったときだけ面ハンドルを見る。
            // 面の 2 辺は軸線と重なっているため、軸を優先しないと辺が掴めなくなる
            var bestPlane = -1;
            if (bestAxis < 0 && tool != GizmoTool.Rotate)
            {
                for (var axis = 0; axis < 3; axis++)
                {
                    if (IsInsidePlaneHandle(camera, rtPoint, origin, axis, size * PlaneSizeRatio,
                        tool == GizmoTool.Scale))
                    {
                        bestPlane = axis;
                        break;
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
            // 面ドラッグ (bestAxis < 0) は軸方向を使う経路を通らないため zero でよい
            _dragAxisDir = bestAxis >= 0 ? AxisDirection(bestAxis) : Vector3.zero;
            _dragStartPosition = target.position;
            _dragStartRotation = target.rotation;
            _dragStartScale = target.localScale;

            if (bestPlane >= 0)
            {
                // 視線と面がほぼ平行だと交点が取れない。基準点が定まらないまま
                // ドラッグを始めると前回の残留値を基準にして対象が飛ぶため、掴まない
                if (!PlanePointAt(camera, rtPoint, bestPlane, out _dragStartPlanePoint))
                {
                    EndDrag();
                    return false;
                }
            }
            else
            {
                _dragStartParam = tool == GizmoTool.Rotate
                    ? RotationAngleAt(camera, rtPoint)
                    : AxisParamAt(camera, rtPoint);
            }
            return true;
        }

        /// <summary>rtPoint が面ハンドルの内側か。画面へ投影した多角形で判定する</summary>
        private bool IsInsidePlaneHandle(
            Camera camera, Vector2 rtPoint, Vector3 origin, int normalAxis, float size, bool triangle)
        {
            Vector3 u, v;
            PlaneAxes(normalAxis, out u, out v);

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

        /// <summary>マウスレイと操作面の交点。面と平行で交わらなければ false</summary>
        private bool PlanePointAt(Camera camera, Vector2 rtPoint, int normalAxis, out Vector3 point)
        {
            var plane = new Plane(AxisDirection(normalAxis), _dragStartPosition);
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

            var best = float.MaxValue;
            for (var i = 0; i < CircleSegments; i++)
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

        /// <summary>回転面上でのマウス位置の角度 (度)</summary>
        private float RotationAngleAt(Camera camera, Vector2 rtPoint)
        {
            var axis = _dragAxisDir;
            var plane = new Plane(axis, _dragStartPosition);
            var ray = camera.ScreenPointToRay(new Vector3(rtPoint.x, rtPoint.y, 0f));

            float enter;
            if (!plane.Raycast(ray, out enter))
            {
                return 0f;
            }

            var onPlane = ray.GetPoint(enter) - _dragStartPosition;
            Vector3 basis1, basis2;
            CalcCircleBasis(axis, out basis1, out basis2);
            return Mathf.Atan2(Vector3.Dot(onPlane, basis2), Vector3.Dot(onPlane, basis1)) * Mathf.Rad2Deg;
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
                    var angle = RotationAngleAt(_dragCamera, rtPoint) - _dragStartParam;
                    target.rotation =
                        Quaternion.AngleAxis(angle, _dragAxisDir) * _dragStartRotation;
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
            if (!PlanePointAt(_dragCamera, rtPoint, _dragPlane, out point))
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
        }
    }
}
