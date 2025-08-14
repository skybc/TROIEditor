using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TROIEditor.ViewModels;

namespace TROIEditor.Controls;

/// <summary>
/// ROI 编辑控件 - 增强版本，支持实时绘制、批量选择、画布平移等交互功能
/// </summary>
public partial class RoiEditorControl : UserControl
{
    #region 依赖属性

    /// <summary>
    /// ViewModel 依赖属性
    /// </summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(RoiEditorViewModel), typeof(RoiEditorControl),
            new PropertyMetadata(null, OnViewModelChanged));

    /// <summary>
    /// ViewModel
    /// </summary>
    public RoiEditorViewModel ViewModel
    {
        get => (RoiEditorViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RoiEditorControl control)
        {
            control.grid.DataContext = e.NewValue;
            control.RoiContextMenu.DataContext = e.NewValue;
            // 取消旧的事件订阅
            if (e.OldValue is RoiEditorViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= control.OnViewModelPropertyChanged;
                oldViewModel.SelectedRois.CollectionChanged -= control.SelectedRois_CollectionChanged;
            }

            // 订阅新的事件
            if (e.NewValue is RoiEditorViewModel newViewModel)
            {
                newViewModel.PropertyChanged -= control.OnViewModelPropertyChanged;
                newViewModel.PropertyChanged += control.OnViewModelPropertyChanged;
                newViewModel.SelectedRois.CollectionChanged -= control.SelectedRois_CollectionChanged;
                newViewModel.SelectedRois.CollectionChanged += control.SelectedRois_CollectionChanged;
                control.InvalidateVisual();
            }
        }
    }



    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    public RoiEditorControl()
    {
        InitializeComponent();

        // 默认创建 ViewModel
        if (ViewModel == null)
        {
            ViewModel = new RoiEditorViewModel();
        }

        // 确保控件可以获得焦点以接收键盘事件
        Focusable = true;
        FocusVisualStyle = null;
    }

    /// <summary>
    /// 处理 ViewModel 属性变更事件 - 增强版本，支持更多属性触发重绘
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 当关键属性变更时触发重绘
        switch (e.PropertyName)
        {
            // 图像相关属性
            case nameof(RoiEditorViewModel.ImageSource):
            case nameof(RoiEditorViewModel.ImageSize):
            case nameof(RoiEditorViewModel.Zoom):
            case nameof(RoiEditorViewModel.Pan):

            // ROI 相关属性
            case nameof(RoiEditorViewModel.Rois):
            case nameof(RoiEditorViewModel.SelectedRois):

            // 绘制状态属性
            case nameof(RoiEditorViewModel.IsDrawing):
            case nameof(RoiEditorViewModel.CreatingRoi):
            case nameof(RoiEditorViewModel.IsRealTimeDrawing):

            // 交互状态属性
            case nameof(RoiEditorViewModel.IsBatchSelecting):
            case nameof(RoiEditorViewModel.BatchSelectRect):
            case nameof(RoiEditorViewModel.IsPolygonDrawing):
            case nameof(RoiEditorViewModel.PolygonPoints):
            case nameof(RoiEditorViewModel.IsSectorDrawing):  // 修改：将IsArcDrawing改为IsSectorDrawing
            case nameof(RoiEditorViewModel.IsDraggingRoi):
            case nameof(RoiEditorViewModel.DraggingRoi): // 修复：添加拖动ROI状态监听
            case nameof(RoiEditorViewModel.SectorDrawingStep): // 修复：添加扇形绘制步骤状态监听
            case nameof(RoiEditorViewModel.SectorFixedRadius): // 修复：添加扇形固定半径状态监听

            // 控制点编辑状态属性 - 新增
            case nameof(RoiEditorViewModel.EditingRoi):
            case nameof(RoiEditorViewModel.EditingControlPointIndex):
                InvalidateVisual();
                break;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SelectedRois_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        this.InvalidateVisual();
        ViewModel.UnionCommand.NotifyCanExecuteChanged();
        ViewModel.IntersectCommand.NotifyCanExecuteChanged();
        ViewModel.SubtractCommand.NotifyCanExecuteChanged();
        ViewModel.DeleteRoiCommand.NotifyCanExecuteChanged();
        

    }

    /// <summary>
    /// 鼠标按下事件处理 - 保持现有接口，ViewModel 处理具体逻辑
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        // 获得焦点以接收键盘事件
        Focus();

        var position = e.GetPosition(this);
        // 获取相对于画布的鼠标位置
        if (e.ChangedButton == MouseButton.Right && ViewModel.IsDrawing == false)
        {
            // 判断是否处于绘制多边形，扇形，如果不是，则切换到选择模式
            if (!ViewModel.TryToSelect())
            {
                return;
            }

            if (this.grid.ContextMenu != null)
            {
                var imagePoint = Services.TransformService.ScreenToImage(position, ViewModel.Zoom, ViewModel.Pan);
                // 更新 ViewModel 的右键菜单状态
                ViewModel.UpdateContextMenuState(imagePoint); 
                this.grid.ContextMenu.IsOpen = true;
                e.Handled = true;
                return;
            }
        }
        ViewModel?.HandleMouseDown(position, e.ChangedButton);
    }

    /// <summary>
    /// 鼠标移动事件处理 - 保持现有接口，ViewModel 处理具体逻辑
    /// </summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this);
        ViewModel?.HandleMouseMove(position,e);
    }

    /// <summary>
    /// 鼠标抬起事件处理 - 保持现有接口，ViewModel 处理具体逻辑
    /// </summary>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        var position = e.GetPosition(this);
        ViewModel?.HandleMouseUp(position, e.ChangedButton);
    }

    /// <summary>
    /// 双击事件处理 - 保持现有功能
    /// </summary>
    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        var position = e.GetPosition(this);
        ViewModel?.HandleMouseDoubleClick(position);
    }

    /// <summary>
    /// 鼠标滚轮事件处理（缩放）- 保持现有功能
    /// </summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (ViewModel != null)
        {
            var position = e.GetPosition(this);
            var zoomFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;

            // 如果按住 Ctrl 键，进行更精细的缩放
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                zoomFactor = e.Delta > 0 ? 1.05 : 1.0 / 1.05;
            }

            // 以鼠标位置为中心缩放
            var (newZoom, newPan) = Services.TransformService.CalculateCenterZoom(
                position, zoomFactor, ViewModel.Zoom, ViewModel.Pan);

            ViewModel.Zoom = Math.Max(ViewModel.MinZoom, Math.Min(ViewModel.MaxZoom, newZoom));
            ViewModel.Pan = newPan;
        }

        e.Handled = true;
    }

    /// <summary>
    /// 键盘按下事件处理 - 保持现有功能
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        ViewModel?.HandleKeyDown(e.Key);
        e.Handled = true;
    }

    /// <summary>
    /// 渲染控件内容 - 增强版本，支持批量选择框、实时绘制等新功能的渲染
    /// </summary>
    /// <remarks>
    /// 此方法负责渲染整个 ROI 编辑器的视觉内容，包括：
    /// 1. 背景图像的显示和变换
    /// 2. 所有已完成 ROI 的绘制
    /// 3. 正在创建中的 ROI 的实时预览
    /// 4. 选中 ROI 的控制点显示
    /// 5. 批量选择框的显示
    /// 6. 多边形绘制过程中的实时预览
    /// 7. 圆弧绘制过程中的实时预览
    /// 
    /// 渲染流程：
    /// 1. 先调用基类的 OnRender 方法，确保基本的 UI 元素正常渲染
    /// 2. 绘制灰色背景作为画布
    /// 3. 应用坐标变换矩阵（缩放和平移）
    /// 4. 在变换坐标系下绘制图像和所有内容
    /// 5. 绘制实时交互元素（批量选择框等）
    /// 6. 恢复坐标系统
    /// 
    /// 坐标系统说明：
    /// - 图像坐标系：以图像左上角为原点，像素为单位
    /// - 屏幕坐标系：以控件左上角为原点，设备无关像素为单位  
    /// - 变换矩阵：将图像坐标转换为屏幕坐标（包含缩放和平移）
    /// 
    /// 注意事项：
    /// - 必须确保 PushTransform 和 Pop 成对使用
    /// - 图像尺寸优先从 ImageSource 获取，保证准确性
    /// - 实时绘制的 ROI 使用不同的样式以区分状态
    /// - 批量选择框在屏幕坐标系中绘制，不受变换影响
    /// - 控制点的大小需要根据缩放级别调整
    /// </remarks>
    /// <param name="drawingContext">绘图上下文</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        // 1. 先调用基类方法，确保基础 UI 正常渲染
        base.OnRender(drawingContext);

        // 2. 如果没有 ViewModel，直接返回
        if (ViewModel == null) return;

        // 3. 绘制控件背景（浅灰色画布背景）
        drawingContext.DrawRectangle(Brushes.LightGray, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // 4. 获取图像信息和尺寸
        var imageSource = ViewModel.ImageSource;
        Size imageSize;

        // 如果有图像源，从图像源获取真实尺寸；否则使用 ViewModel 中的尺寸
        if (imageSource != null)
        {
            imageSize = new Size(imageSource.Width, imageSource.Height);

            // 同步更新 ViewModel 的图像尺寸（确保数据一致性）
            if (ViewModel.ImageSize != imageSize)
            {
                ViewModel.ImageSize = imageSize;
            }
        }
        else
        {
            imageSize = ViewModel.ImageSize;
        }

        // 5. 创建并应用坐标变换矩阵（从图像坐标系到屏幕坐标系）
        Matrix? transform = null;
        if (imageSize.Width > 0 && imageSize.Height > 0)
        {
            transform = Services.TransformService.CreateImageToScreenMatrix(ViewModel.Zoom, ViewModel.Pan);
            drawingContext.PushTransform(new MatrixTransform(transform.Value));
        }

        try
        {
            // 6. 在变换坐标系中绘制图像（如果存在）
            if (imageSource != null && imageSize.Width > 0 && imageSize.Height > 0)
            {
                // 定义图像在图像坐标系中的矩形（从原点开始）
                var imageRect = new Rect(0, 0, imageSize.Width, imageSize.Height);
                drawingContext.DrawImage(imageSource, imageRect);
            }

            // 7. 绘制所有已完成的 ROI
            foreach (var roi in ViewModel.Rois)
            {
                DrawRoi(drawingContext, roi, false);
            }

            // 8. 绘制正在创建中的 ROI（实时绘制）
            if ((ViewModel.IsRealTimeDrawing || ViewModel.IsPolygonDrawing || ViewModel.IsSectorDrawing) && ViewModel.CreatingRoi != null)
            {
                DrawRoi(drawingContext, ViewModel.CreatingRoi, true);
            }

            // 9. 绘制多边形绘制过程中的临时顶点和连线
            if (ViewModel.IsPolygonDrawing && ViewModel.PolygonPoints.Count > 0)
            {
                DrawPolygonDrawingHelper(drawingContext);
            }

            // 10. 绘制扇形绘制过程中的辅助线 - 修改：将圆弧改为扇形
            if (ViewModel.IsSectorDrawing && ViewModel.CreatingRoi is Models.Roi.SectorRoi sector)
            {
                DrawSectorDrawingHelper(drawingContext, sector);
            }
        }
        finally
        {
            // 11. 恢复坐标系统（如果应用了变换）
            if (transform.HasValue)
            {
                drawingContext.Pop();
            }
        }

        // 12. 绘制批量选择框（在屏幕坐标系中，不受图像变换影响）
        if (ViewModel.IsBatchSelecting && !ViewModel.BatchSelectRect.IsEmpty)
        {
            DrawBatchSelectionRect(drawingContext);
        }
    }

    /// <summary>
    /// 绘制单个 ROI - 增强版本，支持更多状态和样式
    /// </summary>
    /// <param name="drawingContext">绘图上下文</param>
    /// <param name="roi">要绘制的 ROI</param>
    /// <param name="isCreating">是否为正在创建的 ROI</param>
    private void DrawRoi(DrawingContext drawingContext, Models.Roi.RoiBase roi, bool isCreating)
    {
        var geometry = roi.GetGeometry();

        if (geometry == null || geometry.IsEmpty()) return;

        // 应用 ROI 的变换
        if (roi.Transform != null && !roi.Transform.Value.IsIdentity)
        {
            geometry = geometry.Clone();
            var transformGroup = new TransformGroup();
            if (geometry.Transform != null)
            {
                transformGroup.Children.Add(geometry.Transform);
            }

            transformGroup.Children.Add(roi.Transform);
            geometry.Transform = transformGroup;
        }

        // 选择绘制样式
        Brush fillBrush;
        Pen strokePen;

        if (isCreating)
        {
            // 正在创建的 ROI 样式：实时绘制时的样式
            if (ViewModel!.IsRealTimeDrawing)
            {
                fillBrush = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)); // 半透明绿色
                strokePen = new Pen(Brushes.LimeGreen, 2.0 / ViewModel.Zoom);
                strokePen.DashStyle = DashStyles.Dash;
            }
            else if (ViewModel.IsPolygonDrawing)
            {
                fillBrush = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)); // 半透明橙色
                strokePen = new Pen(Brushes.Orange, 1.5 / ViewModel.Zoom);
                strokePen.DashStyle = DashStyles.DashDot;
            }
            else if (ViewModel.IsSectorDrawing)
            {
                fillBrush = new SolidColorBrush(Color.FromArgb(30, 138, 43, 226)); // 半透明紫色
                strokePen = new Pen(Brushes.BlueViolet, 1.5 / ViewModel.Zoom);
                strokePen.DashStyle = DashStyles.DashDotDot;
            }
            else
            {
                fillBrush = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0)); // 默认绿色
                strokePen = new Pen(Brushes.Green, 2.0 / ViewModel.Zoom);
                strokePen.DashStyle = DashStyles.Dash;
            }
        }
        else if (roi.IsSelected)
        {
            // 选中的 ROI 样式
            fillBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 0)); // 半透明黄色
            strokePen = new Pen(Brushes.Orange, 2.0 / ViewModel!.Zoom);
        }
        else if (ViewModel!.IsDraggingRoi && ViewModel.DraggingRoi == roi)
        {
            // 正在拖动的 ROI 样式
            fillBrush = new SolidColorBrush(Color.FromArgb(60, 0, 191, 255)); // 半透明天蓝色
            strokePen = new Pen(Brushes.DeepSkyBlue, 2.5 / ViewModel.Zoom);
        }
        else
        {
            // 普通 ROI 样式
            fillBrush = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0)); // 半透明红色
            strokePen = new Pen(Brushes.Red, 1.0 / ViewModel.Zoom);
        }

        // 绘制 ROI
        drawingContext.DrawGeometry(fillBrush, strokePen, geometry);

        // 如果选中且不是正在创建，绘制控制点
        if (roi.IsSelected && !isCreating)
        {
            DrawControlPoints(drawingContext, roi);
        }
    }

    /// <summary>
    /// 绘制控制点 - 增强版本，区分不同类型的控制点
    /// </summary>
    /// <param name="drawingContext">绘图上下文</param>
    /// <param name="roi">ROI 对象</param>
    private void DrawControlPoints(DrawingContext drawingContext, Models.Roi.RoiBase roi)
    {
        var controlPoints = Services.HitTestService.GetControlPoints(roi);
        var handleSize = 4.0 / ViewModel!.Zoom;

        for (int i = 0; i < controlPoints.Count; i++)
        {
            var point = controlPoints[i];
            DrawControlPoint(drawingContext, roi, i, point, handleSize);
        }
    }

    /// <summary>
    /// 绘制单个控制点 - 新增方法，支持不同样式
    /// </summary>
    /// <param name="drawingContext">绘图上下文</param>
    /// <param name="roi">ROI对象</param>
    /// <param name="controlPointIndex">控制点索引</param>
    /// <param name="point">控制点位置</param>
    /// <param name="baseSize">基础尺寸</param>
    private void DrawControlPoint(DrawingContext drawingContext, Models.Roi.RoiBase roi, int controlPointIndex, Point point, double baseSize)
    {
        Brush fillBrush;
        Pen strokePen;
        double size = baseSize;

        // 检查是否为正在编辑的控制点
        bool isEditing = ViewModel!.EditingRoi == roi && ViewModel.EditingControlPointIndex == controlPointIndex;

        // 根据 ROI 类型和控制点索引确定样式
        switch (roi)
        {
            case Models.Roi.RectRoi _:
                if (controlPointIndex == 8) // 旋转控制点
                {
                    fillBrush = isEditing ? Brushes.Yellow : Brushes.LightBlue;
                    strokePen = new Pen(isEditing ? Brushes.Red : Brushes.Blue, isEditing ? 2.0 / ViewModel.Zoom : 1.5 / ViewModel.Zoom);
                    size = baseSize * 1.5;

                    // 绘制圆形旋转控制点
                    drawingContext.DrawEllipse(fillBrush, strokePen, point, size, size);

                    // 在旋转控制点中心绘制旋转图标（小十字）
                    var crossSize = size * 0.4;
                    var crossPen = new Pen(isEditing ? Brushes.Red : Brushes.DarkBlue, 1.0 / ViewModel.Zoom);
                    drawingContext.DrawLine(crossPen,
                        new Point(point.X - crossSize, point.Y),
                        new Point(point.X + crossSize, point.Y));
                    drawingContext.DrawLine(crossPen,
                        new Point(point.X, point.Y - crossSize),
                        new Point(point.X, point.Y + crossSize));

                    return;
                }
                else // 缩放控制点
                {
                    fillBrush = isEditing ? Brushes.Yellow : Brushes.White;
                    strokePen = new Pen(isEditing ? Brushes.Red : Brushes.Black, isEditing ? 2.0 / ViewModel.Zoom : 1.0 / ViewModel.Zoom);

                    // 角点使用较大的正方形
                    if (controlPointIndex % 2 == 0) // 0,2,4,6 是角点
                    {
                        size = baseSize * (isEditing ? 1.5 : 1.2);
                    }
                    else if (isEditing)
                    {
                        size = baseSize * 1.3; // 编辑状态下边中点也放大
                    }
                }
                break;

            case Models.Roi.EllipseRoi _ when roi is not Models.Roi.CircleRoi:
                if (controlPointIndex == 4) // 旋转控制点
                {
                    fillBrush = isEditing ? Brushes.Yellow : Brushes.LightBlue;
                    strokePen = new Pen(isEditing ? Brushes.Red : Brushes.Blue, isEditing ? 2.0 / ViewModel.Zoom : 1.5 / ViewModel.Zoom);
                    size = baseSize * 1.5;
                    drawingContext.DrawEllipse(fillBrush, strokePen, point, size, size);

                    // 在旋转控制点中心绘制旋转图标（小十字）
                    var crossSize = size * 0.4;
                    var crossPen = new Pen(isEditing ? Brushes.Red : Brushes.DarkBlue, 1.0 / ViewModel.Zoom);
                    drawingContext.DrawLine(crossPen,
                        new Point(point.X - crossSize, point.Y),
                        new Point(point.X + crossSize, point.Y));
                    drawingContext.DrawLine(crossPen,
                        new Point(point.X, point.Y - crossSize),
                        new Point(point.X, point.Y + crossSize));
                    return;
                }
                else // 轴向缩放控制点
                {
                    fillBrush = isEditing ? Brushes.Yellow : Brushes.LightCyan;
                    strokePen = new Pen(isEditing ? Brushes.Red : Brushes.DarkCyan, isEditing ? 2.0 / ViewModel.Zoom : 1.0 / ViewModel.Zoom);
                    if (isEditing)
                    {
                        size = baseSize * 1.3;
                    }
                }
                break;

            case Models.Roi.CircleRoi _:
                fillBrush = isEditing ? Brushes.Yellow : Brushes.LightGreen;
                strokePen = new Pen(isEditing ? Brushes.Red : Brushes.Green, isEditing ? 2.0 / ViewModel.Zoom : 1.0 / ViewModel.Zoom);
                break;

            case Models.Roi.PolygonRoi _:
                fillBrush = isEditing ? Brushes.Yellow : Brushes.Orange;
                strokePen = new Pen(isEditing ? Brushes.Red : Brushes.DarkOrange, isEditing ? 2.0 / ViewModel.Zoom : 1.0 / ViewModel.Zoom);
                // 多边形顶点使用圆形
                drawingContext.DrawEllipse(fillBrush, strokePen, point, isEditing ? size * 1.3 : size, isEditing ? size * 1.3 : size);
                return;

            case Models.Roi.SectorRoi _:
                // 扇形控制点根据类型使用不同样式
                if (controlPointIndex == 0) // 中心点
                {
                    fillBrush = isEditing ? Brushes.Yellow : Brushes.Purple;
                    strokePen = new Pen(isEditing ? Brushes.Red : Brushes.DarkMagenta, isEditing ? 2.0 / ViewModel.Zoom : 1.5 / ViewModel.Zoom);
                    size = baseSize * (isEditing ? 1.5 : 1.2);
                }
                else // 起始点和结束点
                {
                    fillBrush = isEditing ? Brushes.Yellow : Brushes.BlueViolet;
                    strokePen = new Pen(isEditing ? Brushes.Red : Brushes.Purple, isEditing ? 2.0 / ViewModel.Zoom : 1.0 / ViewModel.Zoom);
                    if (isEditing) size = baseSize * 1.3;
                }
                drawingContext.DrawEllipse(fillBrush, strokePen, point, size, size);
                return;

            default:
                fillBrush = isEditing ? Brushes.Yellow : Brushes.White;
                strokePen = new Pen(isEditing ? Brushes.Red : Brushes.Black, isEditing ? 2.0 / ViewModel.Zoom : 1.0 / ViewModel.Zoom);
                break;
        }

        // 绘制矩形控制点（默认样式）
        var handleRect = new Rect(
            point.X - size,
            point.Y - size,
            size * 2,
            size * 2
        );

        drawingContext.DrawRectangle(fillBrush, strokePen, handleRect);
    }

    /// <summary>
    /// 绘制多边形绘制过程中的辅助元素
    /// </summary>
    /// <param name="drawingContext">绘图上下文</param>
    private void DrawPolygonDrawingHelper(DrawingContext drawingContext)
    {
        var points = ViewModel!.PolygonPoints;
        if (points.Count == 0) return;

        var vertexBrush = Brushes.Orange;
        var vertexPen = new Pen(Brushes.DarkOrange, 1.5 / ViewModel.Zoom);
        var vertexSize = 3.0 / ViewModel.Zoom;

        // 绘制所有顶点
        foreach (var point in points)
        {
            var vertexRect = new Rect(
                point.X - vertexSize,
                point.Y - vertexSize,
                vertexSize * 2,
                vertexSize * 2
            );
            drawingContext.DrawEllipse(vertexBrush, vertexPen, point, vertexSize, vertexSize);
        }

        // 绘制当前鼠标位置到最后一个顶点的预览线
        if (points.Count > 0)
        {
            var lastPoint = points.Last();
            var currentPoint = ViewModel.CurrentMousePosition;
            var previewPen = new Pen(Brushes.Orange, 1.0 / ViewModel.Zoom);
            previewPen.DashStyle = DashStyles.Dot;

            drawingContext.DrawLine(previewPen, lastPoint, currentPoint);
        }
    }

    /// <summary>
    /// 绘制扇形绘制过程中的辅助元素 - 修改：支持新的分步骤绘制流程
    /// </summary>
    /// <param name="drawingContext">绘图上下文</param>
    /// <param name="sector">扇形对象</param>
    private void DrawSectorDrawingHelper(DrawingContext drawingContext, Models.Roi.SectorRoi sector)
    {
        var centerBrush = Brushes.BlueViolet;
        var centerSize = 3.0 / ViewModel!.Zoom;
        var helperPen = new Pen(Brushes.BlueViolet, 1.0 / ViewModel.Zoom);
        helperPen.DashStyle = DashStyles.Dot;

        // 绘制中心点
        drawingContext.DrawEllipse(centerBrush, helperPen, sector.Center, centerSize, centerSize);

        // 修复：根据绘制步骤显示不同的辅助元素
        if (ViewModel.SectorDrawingStep == 0)
        {
            // 第二步：绘制半径辅助线（从中心到当前鼠标位置）
            var radiusLine = new LineGeometry(sector.Center, ViewModel.CurrentMousePosition);
            drawingContext.DrawGeometry(null, helperPen, radiusLine);
        }
        else if (ViewModel.SectorDrawingStep == 1)
        {
            // 第四步：绘制固定的半径参考线（保留直线显示作为参考）
            var fixedRadiusEndPoint = new Point(
                sector.Center.X + ViewModel.SectorFixedRadius * Math.Cos(sector.StartAngle * Math.PI / 180),
                sector.Center.Y + ViewModel.SectorFixedRadius * Math.Sin(sector.StartAngle * Math.PI / 180)
            );
            var fixedRadiusLine = new LineGeometry(sector.Center, fixedRadiusEndPoint);
            var fixedRadiusPen = new Pen(Brushes.DarkBlue, 1.5 / ViewModel.Zoom);
            fixedRadiusPen.DashStyle = DashStyles.Dash;
            drawingContext.DrawGeometry(null, fixedRadiusPen, fixedRadiusLine);
        }
    }

    /// <summary>
    /// 绘制批量选择框
    /// </summary>
    /// <param name="drawingContext">绘图上下文</param>
    private void DrawBatchSelectionRect(DrawingContext drawingContext)
    {
        var rect = ViewModel!.BatchSelectRect;

        // 将图像坐标的选择框转换为屏幕坐标
        var topLeft = Services.TransformService.ImageToScreen(rect.TopLeft, ViewModel.Zoom, ViewModel.Pan);
        var bottomRight = Services.TransformService.ImageToScreen(rect.BottomRight, ViewModel.Zoom, ViewModel.Pan);
        var screenRect = new Rect(topLeft, bottomRight);

        // 绘制选择框
        var fillBrush = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)); // 半透明蓝色
        var strokePen = new Pen(Brushes.DodgerBlue, 1.5);
        strokePen.DashStyle = DashStyles.Dash;

        drawingContext.DrawRectangle(fillBrush, strokePen, screenRect);
    }

    /// <summary>
    /// 更新视口大小信息到 ViewModel - 保持现有功能
    /// </summary>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        // 这里可以将视口大小信息传递给 ViewModel，用于适合窗口功能
        if (ViewModel != null)
        {
            // 可以添加一个 ViewportSize 属性到 ViewModel
            // ViewModel.ViewportSize = new Size(ActualWidth, ActualHeight);
        }
    }

}