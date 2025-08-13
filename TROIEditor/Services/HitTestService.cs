using System.Windows;
using System.Windows.Media;
using TROIEditor.Models;
using TROIEditor.Models.Roi;

namespace TROIEditor.Services;

/// <summary>
/// 命中测试服务
/// </summary>
public static class HitTestService
{
    /// <summary>
    /// 对 ROI 集合进行命中测试
    /// </summary>
    /// <param name="rois">ROI 集合</param>
    /// <param name="point">测试点（图像坐标）</param>
    /// <param name="pixelTolerance">像素容差</param>
    /// <param name="zoomLevel">当前缩放级别</param>
    /// <returns>命中的 ROI 和命中结果</returns>
    public static (RoiBase? roi, RoiHitTestResult result) HitTest(
        IEnumerable<RoiBase> rois, 
        Point point, 
        double pixelTolerance, 
        double zoomLevel)
    {
        // 将像素容差转换为图像坐标容差
        var imageTolerance = pixelTolerance / zoomLevel;

        // 从后向前遍历（后绘制的在前面）
        var roiList = rois.ToList();
        for (int i = roiList.Count - 1; i >= 0; i--)
        {
            var roi = roiList[i];
            
            // 先检查控制点命中（优先级更高）
            if (roi.IsSelected)
            {
                var controlPointResult = HitTestControlPoints(roi, point, imageTolerance);
                if (controlPointResult.HitType != Models.RoiHitType.None)
                {
                    return (roi, controlPointResult);
                }
            }
            
            // 再检查ROI主体命中
            var result = roi.HitTest(point, imageTolerance);
            if (result.HitType != Models.RoiHitType.None)
            {
                return (roi, result);
            }
        }

        return (null, new RoiHitTestResult(Models.RoiHitType.None, -1, point));
    }

    /// <summary>
    /// 控制点命中测试 - 新增方法
    /// </summary>
    /// <param name="roi">ROI对象</param>
    /// <param name="point">测试点</param>
    /// <param name="tolerance">容差</param>
    /// <returns>命中结果</returns>
    public static RoiHitTestResult HitTestControlPoints(RoiBase roi, Point point, double tolerance)
    {
        var controlPoints = GetControlPoints(roi);
        
        for (int i = 0; i < controlPoints.Count; i++)
        {
            var distance = (point - controlPoints[i]).Length;
            if (distance <= tolerance)
            {
                // 根据 ROI 类型和控制点索引确定命中类型
                var hitType = GetControlPointHitType(roi, i);
                return new RoiHitTestResult(hitType, i, point);
            }
        }
        
        return new RoiHitTestResult(Models.RoiHitType.None, -1, point);
    }

    /// <summary>
    /// 获取控制点的命中类型 - 新增方法
    /// </summary>
    /// <param name="roi">ROI对象</param>
    /// <param name="controlPointIndex">控制点索引</param>
    /// <returns>命中类型</returns>
    private static Models.RoiHitType GetControlPointHitType(RoiBase roi, int controlPointIndex)
    {
        switch (roi)
        {
            case RectRoi _:
                // 矩形：0-7为缩放控制点，8为旋转控制点
                return controlPointIndex == 8 ? Models.RoiHitType.RotateHandle : Models.RoiHitType.ScaleHandle;
            case EllipseRoi _ when roi is not CircleRoi:
                // 椭圆：0-3为轴向缩放控制点，4为旋转控制点
                return controlPointIndex == 4 ? Models.RoiHitType.RotateHandle : Models.RoiHitType.ScaleHandle;
            case CircleRoi _:
                // 圆形：所有控制点都是缩放控制点
                return Models.RoiHitType.ScaleHandle;
            case PolygonRoi _:
                // 多边形：所有控制点都是顶点
                return Models.RoiHitType.Vertex;
            case SectorRoi _:
                // 扇形：所有控制点都是顶点
                return Models.RoiHitType.Vertex;
            default:
                return Models.RoiHitType.ScaleHandle;
        }
    }

    /// <summary>
    /// 获取指定区域内的所有 ROI
    /// </summary>
    /// <param name="rois">ROI 集合</param>
    /// <param name="selectionRect">选择矩形（图像坐标）</param>
    /// <param name="intersectionMode">相交模式（true=相交即选中，false=完全包含才选中）</param>
    /// <returns>选中的 ROI 列表</returns>
    public static List<RoiBase> GetRoisInRect(
        IEnumerable<RoiBase> rois, 
        Rect selectionRect, 
        bool intersectionMode = true)
    {
        var result = new List<RoiBase>();
        var selectionGeometry = new RectangleGeometry(selectionRect);

        foreach (var roi in rois)
        {
            var roiGeometry = roi.GetGeometry();
            if (roi.Transform != null && !roi.Transform.Value.IsIdentity)
            {
                roiGeometry = roiGeometry.Clone();
                roiGeometry.Transform = roi.Transform;
            }

            if (intersectionMode)
            {
                // 相交模式：只要有重叠就选中
                if (GeometryBooleanService.DoGeometriesIntersect(selectionGeometry, roiGeometry))
                {
                    result.Add(roi);
                }
            }
            else
            {
                // 包含模式：ROI 必须完全在选择区域内
                var roiBounds = roiGeometry.Bounds;
                if (selectionRect.Contains(roiBounds))
                {
                    result.Add(roi);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 获取 ROI 的控制点（用于 Adorner）
    /// </summary>
    /// <param name="roi">ROI</param>
    /// <returns>控制点集合</returns>
    public static List<Point> GetControlPoints(RoiBase roi)
    {
        var points = new List<Point>();

        switch (roi)
        {
            case RectRoi rect:
                points.AddRange(GetRectangleControlPoints(rect));
                break;
            case EllipseRoi ellipse when ellipse is not CircleRoi:
                points.AddRange(GetEllipseControlPoints(ellipse));
                break;
            case CircleRoi circle:
                points.AddRange(GetCircleControlPoints(circle));
                break;
            case PolygonRoi polygon:
                points.AddRange(polygon.Points);
                break;
            case SectorRoi sector:  // 修改：将ArcRoi改为SectorRoi
                points.AddRange(GetSectorControlPoints(sector));
                break;
        }

        return points;
    }

    /// <summary>
    /// 获取矩形控制点（8个角点和边中点 + 旋转控制点）
    /// </summary>
    private static List<Point> GetRectangleControlPoints(RectRoi rect)
    {
        var points = new List<Point>();
        var halfWidth = rect.Width / 2;
        var halfHeight = rect.Height / 2;

        // 8个控制点（角点和边中点）
        var localPoints = new[]
        {
            new Point(-halfWidth, -halfHeight), // 左上角
            new Point(0, -halfHeight),          // 上边中点
            new Point(halfWidth, -halfHeight),  // 右上角
            new Point(halfWidth, 0),            // 右边中点
            new Point(halfWidth, halfHeight),   // 右下角
            new Point(0, halfHeight),           // 下边中点
            new Point(-halfWidth, halfHeight),  // 左下角
            new Point(-halfWidth, 0),           // 左边中点
        };

        // 变换到世界坐标
        var matrix = new Matrix();
        matrix.Rotate(rect.Angle);
        matrix.Translate(rect.Center.X, rect.Center.Y);

        foreach (var localPoint in localPoints)
        {
            points.Add(matrix.Transform(localPoint));
        }

        // 旋转控制点（上方中点外侧）
        var rotatePoint = new Point(0, -halfHeight - 20);
        points.Add(matrix.Transform(rotatePoint));

        return points;
    }

    /// <summary>
    /// 获取椭圆控制点
    /// </summary>
    private static List<Point> GetEllipseControlPoints(EllipseRoi ellipse)
    {
        var points = new List<Point>();

        // 4个轴向控制点（右、上、左、下）
        var localPoints = new[]
        {
            new Point(ellipse.RadiusX, 0),   // 右边控制点 (X轴正方向)
            new Point(0, -ellipse.RadiusY),  // 上边控制点 (Y轴负方向)
            new Point(-ellipse.RadiusX, 0),  // 左边控制点 (X轴负方向)
            new Point(0, ellipse.RadiusY),   // 下边控制点 (Y轴正方向)
        };

        // 变换到世界坐标
        var matrix = new Matrix();
        matrix.Rotate(ellipse.Angle);
        matrix.Translate(ellipse.Center.X, ellipse.Center.Y);

        foreach (var localPoint in localPoints)
        {
            points.Add(matrix.Transform(localPoint));
        }

        // 旋转控制点
        var rotatePoint = new Point(0, -ellipse.RadiusY - 20);
        points.Add(matrix.Transform(rotatePoint));

        return points;
    }

    /// <summary>
    /// 获取圆控制点
    /// </summary>
    private static List<Point> GetCircleControlPoints(CircleRoi circle)
    {
        var points = new List<Point>();

        // 4个主要控制点 + 4个辅助控制点
        var angles = new[] { 0, 45, 90, 135, 180, 225, 270, 315 };
        
        foreach (var angle in angles)
        {
            var radian = angle * Math.PI / 180;
            var point = new Point(
                circle.Center.X + circle.Radius * Math.Cos(radian),
                circle.Center.Y + circle.Radius * Math.Sin(radian)
            );
            points.Add(point);
        }

        return points;
    }

    /// <summary>
    /// 获取扇形控制点 - 修改：将圆弧改为扇形
    /// </summary>
    private static List<Point> GetSectorControlPoints(SectorRoi sector)
    {
        var points = new List<Point>
        {
            sector.Center,      // 中心点
            sector.GetStartPoint(), // 起始点
            sector.GetEndPoint()    // 结束点
        };

        return points;
    }

    /// <summary>
    /// 检查点是否在控制点附近
    /// </summary>
    /// <param name="point">测试点</param>
    /// <param name="controlPoint">控制点</param>
    /// <param name="tolerance">容差</param>
    /// <returns>是否命中</returns>
    public static bool IsPointNearControlPoint(Point point, Point controlPoint, double tolerance)
    {
        var distance = (point - controlPoint).Length;
        return distance <= tolerance;
    }

    /// <summary>
    /// 获取最近的控制点索引
    /// </summary>
    /// <param name="point">测试点</param>
    /// <param name="controlPoints">控制点列表</param>
    /// <param name="tolerance">容差</param>
    /// <returns>控制点索引，-1表示没有命中</returns>
    public static int GetNearestControlPointIndex(Point point, List<Point> controlPoints, double tolerance)
    {
        int nearestIndex = -1;
        double nearestDistance = double.MaxValue;

        for (int i = 0; i < controlPoints.Count; i++)
        {
            var distance = (point - controlPoints[i]).Length;
            if (distance <= tolerance && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    /// <summary>
    /// 更新 ROI 控制点位置 - 新增方法，处理各种类型 ROI 的控制点更新
    /// </summary>
    /// <param name="roi">要更新的 ROI</param>
    /// <param name="controlPointIndex">控制点索引</param>
    /// <param name="newPosition">新位置</param>
    public static void UpdateRoiControlPoint(RoiBase roi, int controlPointIndex, Point newPosition)
    {
        switch (roi)
        {
            case RectRoi rect:
                rect.UpdateByControlPoint(controlPointIndex, newPosition);
                break;
            case EllipseRoi ellipse when ellipse is not CircleRoi:
                ellipse.UpdateByControlPoint(controlPointIndex, newPosition);
                break;
            case CircleRoi circle:
                circle.UpdateByControlPoint(controlPointIndex, newPosition);
                break;
            case PolygonRoi polygon:
                polygon.UpdateByControlPoint(controlPointIndex, newPosition);
                break;
            case SectorRoi sector:
                sector.UpdateByControlPoint(controlPointIndex, newPosition);
                break;
        }
    }
}