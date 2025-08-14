using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TROIEditor.Models;
using TROIEditor.Models.Roi;
using TROIEditor.Services;

namespace TROIEditor.ViewModels;

/// <summary>
/// ROI 编辑器主 ViewModel - 增强版本，支持实时绘制和批量选择等交互功能
/// </summary>
public partial class RoiEditorViewModel : ObservableObject
{
    #region Fields

    // ROI 集合
    private ObservableCollection<RoiBase> _rois = new();
    // 选中的 ROI 集合
    private ObservableCollection<RoiBase> _selectedRois = new();
    // 当前工具
    private RoiTool _currentTool = RoiTool.Select;
    // 背景图像源
    private ImageSource? _imageSource;
    // 图像尺寸
    private Size _imageSize;
    // 缩放比例
    private double _zoom = 1.0;
    // 最小缩放
    private double _minZoom = 0.01;
    // 最大缩放
    private double _maxZoom = 50.0;
    // 平移偏移
    private Point _pan = new();
    // 命中测试像素容差
    private double _hitTestPixelTolerance = 3.0;
    // 当前鼠标位置（图像坐标）
    private Point _currentMousePosition;
    // 状态信息
    private string _statusText = "就绪";
    // 是否正在绘制
    private bool _isDrawing;

    // 新增字段：支持实时绘制和交互增强
    // 正在创建的 ROI
    private RoiBase? _creatingRoi;
    // 绘制起始点
    private Point _drawStartPoint;
    // 实时绘制标记
    private bool _isRealTimeDrawing;
    // 批量选择状态
    private bool _isBatchSelecting;
    // 批量选择起始点
    private Point _batchSelectStartPoint;
    // 批量选择框
    private Rect _batchSelectRect;
    // 画布平移状态
    private bool _isCanvasPanning;
    // 平移起始点
    private Point _panStartPoint;
    // 原始平移值
    private Point _originalPan;
    // ROI 拖动状态
    private bool _isDraggingRoi;
    // 拖动偏移量
    private Vector _dragOffset;
    // 正在拖动的 ROI
    private RoiBase? _draggingRoi;
    // 正在编辑的 ROI
    private RoiBase? _editingRoi;
    // 编辑的控制点索引
    private int _editingControlPointIndex;
    // 上一个鼠标位置
    private Point _lastMousePosition;

    // 多边形绘制专用
    // 多边形绘制状态
    private bool _isPolygonDrawing;
    // 多边形临时顶点
    private List<Point> _polygonPoints = new();

    // 扇形绘制专用
    // 扇形绘制状态
    private bool _isSectorDrawing;
    // 扇形绘制步骤(0:设置中心点, 1:拖拽设置半径, 2:固定半径后设置角度)
    private int _sectorDrawingStep;
    // 固定的半径值（在第三步后保存）
    private double _sectorFixedRadius;
    // 半径起始点（用于显示半径参考线）
    private Point _sectorRadiusStartPoint;

    // 右键菜单相关
    // 右键点击的目标 ROI
    private RoiBase? _rightClickTargetRoi;

    #endregion

    #region Properties

    /// <summary>
    /// ROI 集合
    /// </summary>
    public ObservableCollection<RoiBase> Rois
    {
        get => _rois;
        set => SetProperty(ref _rois, value);
    }

    /// <summary>
    /// 选中的 ROI 集合
    /// </summary>
    public ObservableCollection<RoiBase> SelectedRois
    {
        get => _selectedRois;
        private set => SetProperty(ref _selectedRois, value);
    }

    /// <summary>
    /// 当前工具
    /// </summary>
    public RoiTool CurrentTool
    {
        get => _currentTool;
        set => SetProperty(ref _currentTool, value);
    }

    /// <summary>
    /// 背景图像源
    /// </summary>
    public ImageSource? ImageSource
    {
        get => _imageSource;
        set => SetProperty(ref _imageSource, value);
    }

    /// <summary>
    /// 图像尺寸
    /// </summary>
    public Size ImageSize
    {
        get => _imageSize;
        set => SetProperty(ref _imageSize, value);
    }

    /// <summary>
    /// 缩放比例
    /// </summary>
    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, value);
    }

    /// <summary>
    /// 最小缩放
    /// </summary>
    public double MinZoom
    {
        get => _minZoom;
        set => SetProperty(ref _minZoom, value);
    }

    /// <summary>
    /// 最大缩放
    /// </summary>
    public double MaxZoom
    {
        get => _maxZoom;
        set => SetProperty(ref _maxZoom, value);
    }

    /// <summary>
    /// 平移偏移
    /// </summary>
    public Point Pan
    {
        get => _pan;
        set => SetProperty(ref _pan, value);
    }

    /// <summary>
    /// 命中测试像素容差
    /// </summary>
    public double HitTestPixelTolerance
    {
        get => _hitTestPixelTolerance;
        set => SetProperty(ref _hitTestPixelTolerance, value);
    }

    /// <summary>
    /// 当前鼠标位置（图像坐标）
    /// </summary>
    public Point CurrentMousePosition
    {
        get => _currentMousePosition;
        set => SetProperty(ref _currentMousePosition, value);
    }

    /// <summary>
    /// 状态信息
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// 是否正在绘制
    /// </summary>
    public bool IsDrawing
    {
        get => _isDrawing;
        set => SetProperty(ref _isDrawing, value);
    }

    // 新增属性：支持新的交互状态

    /// <summary>
    /// 正在创建的 ROI（公开访问，替代私有字段）
    /// </summary>
    public RoiBase? CreatingRoi
    {
        get => _creatingRoi;
        set => SetProperty(ref _creatingRoi, value);
    }

    /// <summary>
    /// 是否正在实时绘制
    /// </summary>
    public bool IsRealTimeDrawing
    {
        get => _isRealTimeDrawing;
        set => SetProperty(ref _isRealTimeDrawing, value);
    }

    /// <summary>
    /// 是否正在批量选择
    /// </summary>
    public bool IsBatchSelecting
    {
        get => _isBatchSelecting;
        set => SetProperty(ref _isBatchSelecting, value);
    }

    /// <summary>
    /// 批量选择框
    /// </summary>
    public Rect BatchSelectRect
    {
        get => _batchSelectRect;
        set => SetProperty(ref _batchSelectRect, value);
    }

    /// <summary>
    /// 是否正在平移画布
    /// </summary>
    public bool IsCanvasPanning
    {
        get => _isCanvasPanning;
        set => SetProperty(ref _isCanvasPanning, value);
    }

    /// <summary>
    /// 是否正在拖动 ROI
    /// </summary>
    public bool IsDraggingRoi
    {
        get => _isDraggingRoi;
        set => SetProperty(ref _isDraggingRoi, value);
    }

    /// <summary>
    /// 正在拖动的 ROI
    /// </summary>
    public RoiBase? DraggingRoi
    {
        get => _draggingRoi;
        set => SetProperty(ref _draggingRoi, value);
    }

    /// <summary>
    /// 是否正在绘制多边形
    /// </summary>
    public bool IsPolygonDrawing
    {
        get => _isPolygonDrawing;
        set => SetProperty(ref _isPolygonDrawing, value);
    }

    /// <summary>
    /// 多边形临时顶点列表
    /// </summary>
    public List<Point> PolygonPoints
    {
        get => _polygonPoints;
        set => SetProperty(ref _polygonPoints, value);
    }

    /// <summary>
    /// 是否正在绘制扇形
    /// </summary>
    public bool IsSectorDrawing
    {
        get => _isSectorDrawing;
        set => SetProperty(ref _isSectorDrawing, value);
    }

    /// <summary>
    /// 扇形绘制步骤
    /// </summary>
    public int SectorDrawingStep
    {
        get => _sectorDrawingStep;
        set => SetProperty(ref _sectorDrawingStep, value);
    }

    /// <summary>
    /// 扇形固定半径值
    /// </summary>
    public double SectorFixedRadius
    {
        get => _sectorFixedRadius;
        set => SetProperty(ref _sectorFixedRadius, value);
    }

    /// <summary>
    /// 扇形半径起始点
    /// </summary>
    public Point SectorRadiusStartPoint
    {
        get => _sectorRadiusStartPoint;
        set => SetProperty(ref _sectorRadiusStartPoint, value);
    }

    /// <summary>
    /// 正在编辑的 ROI
    /// </summary>
    public RoiBase? EditingRoi
    {
        get => _editingRoi;
        set => SetProperty(ref _editingRoi, value);
    }

    /// <summary>
    /// 正在编辑的控制点索引
    /// </summary>
    public int EditingControlPointIndex
    {
        get => _editingControlPointIndex;
        set => SetProperty(ref _editingControlPointIndex, value);
    }

    #endregion

    #region Commands

    /// <summary>
    /// 加载图像命令
    /// </summary>
    [RelayCommand]
    private async Task LoadImage()
    {
        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "选择图像文件",
                Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif|所有文件|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await LoadImageFromPath(openFileDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"加载图像失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 选择工具命令
    /// </summary>
    [RelayCommand]
    private void SelectTool()
    {
        CurrentTool = RoiTool.Select;
        CancelAllOperations();
    }

    /// <summary>
    /// 平移工具命令
    /// </summary>
    [RelayCommand]
    private void PanTool()
    {
        CurrentTool = RoiTool.Pan;
        CancelAllOperations();
    }

    /// <summary>
    /// 新建矩形命令
    /// </summary>
    [RelayCommand]
    private void NewRectangle()
    {
        CurrentTool = RoiTool.Rectangle;
        CancelAllOperations();
    }

    /// <summary>
    /// 新建椭圆命令
    /// </summary>
    [RelayCommand]
    private void NewEllipse()
    {
        CurrentTool = RoiTool.Ellipse;
        CancelAllOperations();
    }

    /// <summary>
    /// 新建圆形命令
    /// </summary>
    [RelayCommand]
    private void NewCircle()
    {
        CurrentTool = RoiTool.Circle;
        CancelAllOperations();
    }

    /// <summary>
    /// 新建多边形命令
    /// </summary>
    [RelayCommand]
    private void NewPolygon()
    {
        CurrentTool = RoiTool.Polygon;
        CancelAllOperations();
    }

    /// <summary>
    /// 新建扇形命令
    /// </summary>
    [RelayCommand]
    private void NewSector()
    {
        CurrentTool = RoiTool.Sector;
        CancelAllOperations();
    }

    /// <summary>
    /// 并集运算命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteBooleanOperation))]
    private void Union()
    {
        try
        {
            var result = GeometryBooleanService.Union(SelectedRois);
            Rois.Add(result);
            StatusText = $"并集运算完成，生成新的 ROI";

            // 发送消息通知布尔运算完成
            WeakReferenceMessenger.Default.Send(new BooleanOperationCompletedMessage(result, RoiCombineMode.Union));
        }
        catch (Exception ex)
        {
            StatusText = $"并集运算失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 交集运算命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteBooleanOperation))]
    private void Intersect()
    {
        try
        {
            var result = GeometryBooleanService.Intersect(SelectedRois);
            Rois.Add(result);
            StatusText = $"交集运算完成，生成新的 ROI";

            WeakReferenceMessenger.Default.Send(new BooleanOperationCompletedMessage(result, RoiCombineMode.Intersect));
        }
        catch (Exception ex)
        {
            StatusText = $"交集运算失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 差集运算命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteBooleanOperation))]
    private void Subtract()
    {
        try
        {
            var result = GeometryBooleanService.Subtract(SelectedRois);
            Rois.Add(result);
            StatusText = $"差集运算完成，生成新的 ROI";

            WeakReferenceMessenger.Default.Send(new BooleanOperationCompletedMessage(result, RoiCombineMode.Subtract));
        }
        catch (Exception ex)
        {
            StatusText = $"差集运算失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 删除选中的 ROI 命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedRois))]
    private void DeleteSelected()
    {
        var toDelete = SelectedRois.ToList();
        foreach (var roi in toDelete)
        {
            Rois.Remove(roi);
        }
        SelectedRois.Clear();
        StatusText = $"删除了 {toDelete.Count} 个 ROI";
    }

    /// <summary>
    /// 删除 ROI 命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteRoi))]
    private void DeleteRoi()
    {
        if (_rightClickTargetRoi != null)
        {
            // 删除右键点击的 ROI
            Rois.Remove(_rightClickTargetRoi);
            SelectedRois.Remove(_rightClickTargetRoi);
            StatusText = $"删除了 ROI: {_rightClickTargetRoi.Name}";
            _rightClickTargetRoi = null;
        }
        else if (SelectedRois.Count > 0)
        {
            // 删除当前选中的 ROI
            DeleteSelected();
        }
        // 更新界面
    }

    /// <summary>
    /// 清空所有 ROI 命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasAnyRois))]
    private void ClearAll()
    {
        var count = Rois.Count;
        Rois.Clear();
        SelectedRois.Clear();
        StatusText = $"清空了 {count} 个 ROI";
    }

    /// <summary>
    /// 全选 ROI 命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasAnyRois))]
    private void SelectAll()
    {
        SelectedRois.Clear();
        foreach (var roi in Rois)
        {
            roi.IsSelected = true;
            SelectedRois.Add(roi);
        }
        StatusText = $"选择了 {SelectedRois.Count} 个 ROI";
    }

    /// <summary>
    /// 放大命令
    /// </summary>
    [RelayCommand]
    private void ZoomIn()
    {
        Zoom = Math.Min(MaxZoom, Zoom * 1.2);
    }

    /// <summary>
    /// 缩小命令
    /// </summary>
    [RelayCommand]
    private void ZoomOut()
    {
        Zoom = Math.Max(MinZoom, Zoom / 1.2);
    }

    /// <summary>
    /// 重置缩放命令
    /// </summary>
    [RelayCommand]
    private void ZoomReset()
    {
        Zoom = 1.0;
        Pan = new Point(0, 0);
        StatusText = "缩放已重置";
    }

    /// <summary>
    /// 适合窗口命令
    /// </summary>
    [RelayCommand]
    private void ZoomToFit()
    {
        if (ImageSize.Width > 0 && ImageSize.Height > 0)
        {
            // 这个需要从控件获取当前视口大小，暂时使用默认值
            var viewportSize = new Size(800, 600);
            var (zoom, pan) = TransformService.CalculateFitToView(ImageSize, viewportSize);
            Zoom = zoom;
            Pan = pan;
            StatusText = "已适配窗口";
        }
    }

    /// <summary>
    /// 导出掩码命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasAnyRois))]
    private async Task ExportMask()
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "导出掩码",
                Filter = "PNG 图像|*.png|BMP 位图|*.bmp|JPEG 图像|*.jpg|TIFF 图像|*.tiff",
                DefaultExt = ".png"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await Task.Run(() =>
                {
                    var imageSize = ImageSize.Width > 0 && ImageSize.Height > 0 ? ImageSize : new Size(800, 600);
                    MaskExportService.ExportMask(Rois, imageSize, saveFileDialog.FileName);
                });

                StatusText = $"掩码已导出到: {saveFileDialog.FileName}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"导出掩码失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 保存 ROI 到 JSON 命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasAnyRois))]
    private async Task SaveRoisToJson()
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "保存 ROI 数据",
                Filter = "JSON 文件|*.json|所有文件|*.*",
                DefaultExt = ".json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await SaveRoisToJsonFile(saveFileDialog.FileName);
                StatusText = $"ROI 数据已保存到: {saveFileDialog.FileName}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"保存 ROI 失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 从 JSON 加载 ROI 命令
    /// </summary>
    [RelayCommand]
    private async Task LoadRoisFromJson()
    {
        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "加载 ROI 数据",
                Filter = "JSON 文件|*.json|所有文件|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await LoadRoisFromJsonFile(openFileDialog.FileName);
                StatusText = $"已从 {openFileDialog.FileName} 加载 {Rois.Count} 个 ROI";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"加载 ROI 失败: {ex.Message}";
        }
    }

    #endregion

    #region Public Methods - 鼠标事件处理（增强版）

    /// <summary>
    /// 处理鼠标按下事件 - 增强版本支持所有新交互功能
    /// </summary>
    /// <param name="point">点击点（屏幕坐标）</param>
    /// <param name="button">鼠标按钮</param>
    public void HandleMouseDown(Point point, MouseButton button)
    {
        // 转换为图像坐标
        var imagePoint = TransformService.ScreenToImage(point, Zoom, Pan);
        CurrentMousePosition = imagePoint;
        _lastMousePosition = imagePoint;

        if (button == MouseButton.Left)
        {
            HandleLeftMouseDown(imagePoint);
        }
        else if (button == MouseButton.Right)
        {
            HandleRightMouseDown(imagePoint);

        }
        else if (button == MouseButton.Middle)
        {
            HandleMiddleMouseDown(imagePoint);
        }

    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="imagePoint"></param>
    private void HandleMiddleMouseDown(Point imagePoint)
    {
        StartCanvasPan(imagePoint);
    }

    /// <summary>
    /// 处理鼠标移动事件 - 增强版本支持实时绘制和交互
    /// </summary>
    /// <param name="point">鼠标位置（屏幕坐标）</param>
    public void HandleMouseMove(Point point, MouseEventArgs e)
    {
        var imagePoint = TransformService.ScreenToImage(point, Zoom, Pan);
        CurrentMousePosition = imagePoint;
        if (IsCanvasPanning && e.MiddleButton != MouseButtonState.Pressed)
        {
            FinishCanvasPan();
        }
        // 根据当前状态处理鼠标移动
        if (IsRealTimeDrawing && CreatingRoi != null)
        {
            // 实时更新正在绘制的 ROI
            UpdateRealTimeDrawing(imagePoint);
        }
        else if (IsBatchSelecting)
        {
            // 更新批量选择框
            UpdateBatchSelection(imagePoint);
        }
        else if (IsCanvasPanning)
        {
            // 更新画布平移
            UpdateCanvasPan(imagePoint);
        }
        else if (IsDraggingRoi && DraggingRoi != null)
        {
            // 更新 ROI 拖动 - 修复：确保实时更新拖动效果和流畅的刷新
            UpdateRoiDrag(imagePoint);
        }
        else if (EditingRoi != null)
        {
            // 更新控制点编辑
            UpdateControlPointEdit(imagePoint);
        }
        else if (IsSectorDrawing && CreatingRoi is SectorRoi)
        {
            // 修复：处理扇形绘制的不同阶段
            if (SectorDrawingStep == 0)
            {
                // 第二步：拖拽时实时更新半径
                UpdateSectorRadiusDrawing(imagePoint);
            }
            else if (SectorDrawingStep == 1)
            {
                // 第四步：实时更新扇形角度
                UpdateSectorAngleDrawing(imagePoint);
            }
        }

        // 更新状态文本显示鼠标坐标和当前操作状态
        UpdateStatusText();
        _lastMousePosition = imagePoint;
    }

    /// <summary>
    /// 处理鼠标抬起事件 - 增强版本
    /// </summary>
    /// <param name="point">点击点（屏幕坐标）</param>
    /// <param name="button">鼠标按钮</param>
    public void HandleMouseUp(Point point, MouseButton button)
    {
        var imagePoint = TransformService.ScreenToImage(point, Zoom, Pan);

        if (button == MouseButton.Left)
        {
            HandleLeftMouseUp(imagePoint);
        }
        else if (button == MouseButton.Right)
        {
            HandleRightMouseUp(imagePoint);
        }
        else if (button == MouseButton.Middle)
        {
            HandleMiddleMouseUp();

        }
    }

    private void HandleMiddleMouseUp()
    {
        if (IsCanvasPanning)
        {
            // 完成画布平移
            FinishCanvasPan();
        }
    }

    /// <summary>
    /// 处理双击事件 - 保持原有功能
    /// </summary>
    /// <param name="point">点击点（屏幕坐标）</param>
    public void HandleMouseDoubleClick(Point point)
    {
        var imagePoint = TransformService.ScreenToImage(point, Zoom, Pan);

        if (CurrentTool == RoiTool.Select)
        {
            // 双击空白区域重置视图
            var (hitRoi, _) = HitTestService.HitTest(Rois, imagePoint, HitTestPixelTolerance, Zoom);
            if (hitRoi == null)
            {
                ZoomReset();
            }
        }
    }

    /// <summary>
    /// 处理键盘按下事件 - 增强版本
    /// </summary>
    /// <param name="key">按键</param>
    public void HandleKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Delete:
                if (SelectedRois.Count > 0)
                    DeleteSelected();
                break;
            case Key.Escape:
                CancelAllOperations();
                break;
            case Key.A when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                if (Rois.Count > 0)
                    SelectAll();
                break;
        }
    }

    /// <summary>
    /// 命中测试
    /// </summary>
    /// <param name="point">测试点（图像坐标）</param>
    /// <returns>命中结果</returns>
    public (RoiBase? roi, RoiHitTestResult result) HitTest(Point point)
    {
        return HitTestService.HitTest(Rois, point, HitTestPixelTolerance, Zoom);
    }

    #endregion

    #region Private Methods - 鼠标事件具体处理

    /// <summary>
    /// 处理左键按下事件
    /// </summary>
    /// <param name="imagePoint">图像坐标点</param>
    private void HandleLeftMouseDown(Point imagePoint)
    {
        switch (CurrentTool)
        {
            case RoiTool.Select:
                HandleSelectLeftMouseDown(imagePoint);
                break;
            case RoiTool.Rectangle:
            case RoiTool.Ellipse:
            case RoiTool.Circle:
                StartRealTimeShapeDrawing(imagePoint);
                break;
            case RoiTool.Polygon:
                HandlePolygonLeftMouseDown(imagePoint);
                break;
            case RoiTool.Sector:  // 修改：将Arc改为Sector
                HandleSectorLeftMouseDown(imagePoint);
                break;
        }
    }

    /// <summary>
    /// 处理右键按下事件
    /// </summary>
    /// <param name="imagePoint">图像坐标点</param>
    private void HandleRightMouseDown(Point imagePoint)
    {

        if (IsPolygonDrawing)
        {
            // 多边形绘制时，右键结束绘制
            FinishPolygonDrawing();
        }
        else if (IsSectorDrawing)
        {
            // 扇形绘制时，右键完成绘制 - 修改：支持在角度设置阶段完成绘制
            FinishSectorDrawing();
        }
    }

    /// <summary>
    /// 处理左键抬起事件
    /// </summary>
    /// <param name="imagePoint">图像坐标点</param>
    private void HandleLeftMouseUp(Point imagePoint)
    {
        if (IsRealTimeDrawing)
        {
            // 完成实时形状绘制
            FinishRealTimeShapeDrawing(imagePoint);
        }
        else if (IsBatchSelecting)
        {
            // 完成批量选择
            FinishBatchSelection();
        }
        else if (IsDraggingRoi)
        {
            // 完成 ROI 拖动
            FinishRoiDrag();
        }
        else if (_editingRoi != null)
        {
            // 完成控制点编辑
            FinishControlPointEdit();
        }
        else if (IsSectorDrawing && SectorDrawingStep == 0)
        {
            // 第三步：扇形绘制 - 松开左键固定半径，进入角度设置阶段
            FixSectorRadius();
        }
    }

    /// <summary>
    /// 处理右键抬起事件
    /// </summary>
    /// <param name="imagePoint">图像坐标点</param>
    private void HandleRightMouseUp(Point imagePoint)
    {

    }

    /// <summary>
    /// 处理选择工具的左键按下 - 增强版本
    /// </summary>
    /// <param name="imagePoint">图像坐标点</param>
    private void HandleSelectLeftMouseDown(Point imagePoint)
    {
        var (hitRoi, hitResult) = HitTestService.HitTest(Rois, imagePoint, HitTestPixelTolerance, Zoom);

        if (hitRoi != null)
        {
            // 点击到 ROI
            if (hitResult.HitType == RoiHitType.Body && hitRoi.IsSelected)
            {
                // 点击选中的 ROI 主体，开始拖动
                StartRoiDrag(hitRoi, imagePoint);
            }
            else if (hitResult.HitType == RoiHitType.ScaleHandle || hitResult.HitType == RoiHitType.RotateHandle || hitResult.HitType == RoiHitType.Vertex)
            {
                // 点击控制点，开始编辑
                StartControlPointEdit(hitRoi, hitResult);
            }
            else
            {
                // 单选 ROI
                SelectSingleRoi(hitRoi);
            }
        }
        else
        {
            // 点击空白处，开始批量选择
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                ClearSelection();
            }
            StartBatchSelection(imagePoint);
        }
    }

    /// <summary>
    /// 开始实时形状绘制（矩形、椭圆、圆）
    /// </summary>
    /// <param name="startPoint">起始点</param>
    private void StartRealTimeShapeDrawing(Point startPoint)
    {
        _drawStartPoint = startPoint;
        IsRealTimeDrawing = true;
        IsDrawing = true;

        // 创建对应的 ROI 对象
        CreatingRoi = CurrentTool switch
        {
            RoiTool.Rectangle => new RectRoi(startPoint, 0, 0),
            RoiTool.Ellipse => new EllipseRoi(startPoint, 0, 0),
            RoiTool.Circle => new CircleRoi(startPoint, 0),
            _ => null
        };

        StatusText = $"开始绘制{GetToolName()}";
    }

    /// <summary>
    /// 更新实时绘制
    /// </summary>
    /// <param name="currentPoint">当前鼠标点</param>
    private void UpdateRealTimeDrawing(Point currentPoint)
    {
        if (CreatingRoi == null) return;

        var delta = currentPoint - _drawStartPoint;

        // 根据 ROI 类型更新参数
        switch (CreatingRoi)
        {
            case RectRoi rect:
                var width = Math.Abs(delta.X);
                var height = Math.Abs(delta.Y);
                rect.Width = width;
                rect.Height = height;
                rect.Center = new Point(
                    _drawStartPoint.X + delta.X / 2,
                    _drawStartPoint.Y + delta.Y / 2
                );
                break;

            case EllipseRoi ellipse when ellipse is not CircleRoi:
                ellipse.RadiusX = Math.Abs(delta.X) / 2;
                ellipse.RadiusY = Math.Abs(delta.Y) / 2;
                ellipse.Center = new Point(
                    _drawStartPoint.X + delta.X / 2,
                    _drawStartPoint.Y + delta.Y / 2
                );
                break;

            case CircleRoi circle:
                var radius = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
                circle.Radius = radius;
                break;
        }

        // 触发重绘
        OnPropertyChanged(nameof(CreatingRoi));
    }

    /// <summary>
    /// 完成实时形状绘制
    /// </summary>
    /// <param name="endPoint">结束点</param>
    private void FinishRealTimeShapeDrawing(Point endPoint)
    {
        if (CreatingRoi != null)
        {
            // 检查是否有效（大小大于最小阈值）
            var isValid = CreatingRoi switch
            {
                RectRoi rect => rect.Width > 5 && rect.Height > 5,
                EllipseRoi ellipse when ellipse is not CircleRoi => ellipse.RadiusX > 2.5 && ellipse.RadiusY > 2.5,
                CircleRoi circle => circle.Radius > 2.5,
                _ => true
            };

            if (isValid)
            {
                Rois.Add(CreatingRoi);
                StatusText = $"创建了新的{GetToolName()}";
            }
            else
            {
                StatusText = $"{GetToolName()}太小，已取消创建";
            }

            CreatingRoi = null;
        }

        IsRealTimeDrawing = false;
        IsDrawing = false;
    }

    /// <summary>
    /// 处理多边形左键按下
    /// </summary>
    /// <param name="imagePoint">图像坐标点</param>
    private void HandlePolygonLeftMouseDown(Point imagePoint)
    {
        if (!IsPolygonDrawing)
        {
            // 开始新的多边形
            StartPolygonDrawing(imagePoint);
        }
        else
        {
            // 添加新顶点
            AddPolygonPoint(imagePoint);
        }
    }

    /// <summary>
    /// 开始多边形绘制
    /// </summary>
    /// <param name="startPoint">起始点</param>
    private void StartPolygonDrawing(Point startPoint)
    {
        IsPolygonDrawing = true;
        IsDrawing = true;
        PolygonPoints.Clear();
        PolygonPoints.Add(startPoint);

        // 创建多边形 ROI
        CreatingRoi = new PolygonRoi { IsClosed = false };
        ((PolygonRoi)CreatingRoi).AddPoint(startPoint);

        StatusText = "多边形绘制中，右键结束绘制";
    }

    /// <summary>
    /// 添加多边形顶点
    /// </summary>
    /// <param name="point">顶点坐标</param>
    private void AddPolygonPoint(Point point)
    {
        PolygonPoints.Add(point);

        if (CreatingRoi is PolygonRoi polygon)
        {
            polygon.AddPoint(point);
        }

        OnPropertyChanged(nameof(CreatingRoi));
        OnPropertyChanged(nameof(PolygonPoints));
    }

    /// <summary>
    /// 完成多边形绘制
    /// </summary>
    private void FinishPolygonDrawing()
    {
        if (IsPolygonDrawing && CreatingRoi is PolygonRoi polygon)
        {
            if (PolygonPoints.Count >= 3)
            {
                polygon.IsClosed = true;
                Rois.Add(polygon);
                StatusText = $"多边形创建完成，共 {PolygonPoints.Count} 个顶点";
            }
            else
            {
                StatusText = "顶点数不足3个，多边形绘制已取消";
            }
        }

        ClearPolygonDrawing();
    }

    /// <summary>
    /// 清除多边形绘制状态
    /// </summary>
    private void ClearPolygonDrawing()
    {
        IsPolygonDrawing = false;
        IsDrawing = false;
        PolygonPoints.Clear();
        CreatingRoi = null;
    }

    /// <summary>
    /// 处理圆弧左键按下
    /// </summary>
    /// <param name="imagePoint">图像坐标点</param>
    private void HandleSectorLeftMouseDown(Point imagePoint)
    {
        if (!IsSectorDrawing)
        {
            // 第一步：开始扇形绘制，设定中心点
            StartSectorDrawing(imagePoint);
        }
        else if (SectorDrawingStep == 1)
        {
            // 第五步：左键单击确定最终扇形角度并完成绘制
            FinishSectorDrawing();
        }
        // 注意：第二步（拖拽半径）和第四步（设置角度）通过鼠标移动和抬起处理，不需要在这里处理左键按下
    }

    /// <summary>
    /// 开始扇形绘制 - 修复：第一步设定中心点
    /// </summary>
    /// <param name="centerPoint">中心点</param>
    private void StartSectorDrawing(Point centerPoint)
    {
        IsSectorDrawing = true;
        IsDrawing = true;
        SectorDrawingStep = 0; // 0: 设置中心点已完成，准备拖拽设置半径

        // 创建扇形 ROI，设定中心点
        CreatingRoi = new SectorRoi { Center = centerPoint, Radius = 0 };
        SectorRadiusStartPoint = centerPoint; // 保存半径起始点用于参考线显示

        StatusText = "扇形绘制：中心已设定，拖动鼠标设置半径，松开左键固定半径";
    }

    /// <summary>
    /// 更新扇形半径绘制 - 新增：第二步拖拽时实时计算和显示半径
    /// </summary>
    /// <param name="currentPoint">当前鼠标位置</param>
    private void UpdateSectorRadiusDrawing(Point currentPoint)
    {
        if (CreatingRoi is SectorRoi sector)
        {
            // 实时计算半径
            var radius = (currentPoint - sector.Center).Length;
            sector.Radius = radius;

            // 触发重绘
            OnPropertyChanged(nameof(CreatingRoi));
        }
    }

    /// <summary>
    /// 固定扇形半径 - 新增：第三步固定半径后进入角度设置
    /// </summary>
    private void FixSectorRadius()
    {
        if (CreatingRoi is SectorRoi sector)
        {
            SectorFixedRadius = sector.Radius; // 保存当前半径为固定值
            SectorDrawingStep = 1; // 进入角度设置阶段

            // 设置扇形的起始角度为当前鼠标方向
            var currentVector = CurrentMousePosition - sector.Center;
            var startAngle = Math.Atan2(currentVector.Y, currentVector.X) * 180 / Math.PI;
            sector.StartAngle = startAngle;
            sector.EndAngle = startAngle; // 初始时起始角度和结束角度相同

            StatusText = "扇形绘制：半径已固定，移动鼠标设置扇形角度，左键或右键完成绘制";
        }
    }

    /// <summary>
    /// 更新扇形角度绘制 - 新增：第四步实时更新扇形角度
    /// </summary>
    /// <param name="currentPoint">当前鼠标位置</param>
    private void UpdateSectorAngleDrawing(Point currentPoint)
    {
        if (CreatingRoi is SectorRoi sector)
        {
            // 计算当前鼠标位置相对于圆心的角度
            var currentVector = currentPoint - sector.Center;
            var currentAngle = Math.Atan2(currentVector.Y, currentVector.X) * 180 / Math.PI;

            // 更新结束角度，起始角度保持不变
            sector.EndAngle = currentAngle;

            // 触发重绘
            OnPropertyChanged(nameof(CreatingRoi));
        }
    }

    /// <summary>
    /// 完成扇形绘制 - 修复：支持新的绘制流程
    /// </summary>
    private void FinishSectorDrawing()
    {
        if (IsSectorDrawing && CreatingRoi is SectorRoi sector)
        {
            if (sector.Radius > 5)
            {
                Rois.Add(sector);
                StatusText = "扇形创建完成";
            }
            else
            {
                StatusText = "扇形半径太小，已取消创建";
            }
        }

        ClearSectorDrawing();
    }

    /// <summary>
    /// 清除扇形绘制状态 - 修复：清除所有新增状态
    /// </summary>
    private void ClearSectorDrawing()
    {
        IsSectorDrawing = false;
        IsDrawing = false;
        SectorDrawingStep = 0;
        SectorFixedRadius = 0;
        SectorRadiusStartPoint = new Point();
        CreatingRoi = null;
    }

    /// <summary>
    /// 开始批量选择
    /// </summary>
    /// <param name="startPoint">起始点</param>
    private void StartBatchSelection(Point startPoint)
    {
        IsBatchSelecting = true;
        _batchSelectStartPoint = startPoint;
        BatchSelectRect = new Rect(startPoint, startPoint);
        StatusText = "批量选择中...";
    }

    /// <summary>
    /// 更新批量选择框
    /// </summary>
    /// <param name="currentPoint">当前鼠标点</param>
    private void UpdateBatchSelection(Point currentPoint)
    {
        var rect = new Rect(_batchSelectStartPoint, currentPoint);
        BatchSelectRect = rect;

        // 实时更新选中的 ROI
        UpdateBatchSelectedRois();
    }

    /// <summary>
    /// 更新批量选择的 ROI
    /// </summary>
    private void UpdateBatchSelectedRois()
    {
        // 获取与选择框相交的所有 ROI
        var intersectingRois = HitTestService.GetRoisInRect(Rois, BatchSelectRect, true);

        // 更新选中状态
        foreach (var roi in Rois)
        {
            var shouldSelect = intersectingRois.Contains(roi);
            if (roi.IsSelected != shouldSelect)
            {
                roi.IsSelected = shouldSelect;
                if (shouldSelect && !SelectedRois.Contains(roi))
                {
                    SelectedRois.Add(roi);
                }
                else if (!shouldSelect && SelectedRois.Contains(roi))
                {
                    SelectedRois.Remove(roi);
                }
            }
        }
    }

    /// <summary>
    /// 完成批量选择
    /// </summary>
    private void FinishBatchSelection()
    {
        IsBatchSelecting = false;
        BatchSelectRect = Rect.Empty;
        StatusText = $"批量选择完成，选中 {SelectedRois.Count} 个 ROI";
    }

    /// <summary>
    /// 开始画布平移
    /// </summary>
    /// <param name="startPoint">起始点</param>
    private void StartCanvasPan(Point startPoint)
    {
        IsCanvasPanning = true;
        _panStartPoint = startPoint;
        _originalPan = Pan;
        StatusText = "画布平移中...";
    }

    /// <summary>
    /// 更新画布平移
    /// </summary>
    /// <param name="currentPoint">当前鼠标点</param>
    private void UpdateCanvasPan(Point currentPoint)
    {
        var screenStartPoint = TransformService.ImageToScreen(_panStartPoint, Zoom, _originalPan);
        var screenCurrentPoint = TransformService.ImageToScreen(currentPoint, Zoom, _originalPan);
        var delta = screenCurrentPoint - screenStartPoint;

        Pan = new Point(_originalPan.X + delta.X, _originalPan.Y + delta.Y);
    }

    /// <summary>
    /// 完成画布平移
    /// </summary>
    private void FinishCanvasPan()
    {
        IsCanvasPanning = false;
        StatusText = "画布平移完成";
    }

    /// <summary>
    /// 开始 ROI 拖动
    /// </summary>
    /// <param name="roi">要拖动的 ROI</param>
    /// <param name="startPoint">起始点</param>
    private void StartRoiDrag(RoiBase roi, Point startPoint)
    {
        IsDraggingRoi = true;
        DraggingRoi = roi;
        _dragOffset = new Vector(0, 0);
        StatusText = $"拖动 {roi.GetType().Name} 中...";
    }

    /// <summary>
    /// 更新 ROI 拖动
    /// </summary>
    /// <param name="currentPoint">当前鼠标点</param>
    private void UpdateRoiDrag(Point currentPoint)
    {
        if (DraggingRoi != null)
        {
            var delta = currentPoint - _lastMousePosition;
            DraggingRoi.Move(delta);
            _dragOffset += delta;

            // 修复：立即触发界面刷新，确保拖动效果流畅实时更新
            OnPropertyChanged(nameof(Rois));
            OnPropertyChanged(nameof(DraggingRoi));
        }
    }

    /// <summary>
    /// 完成 ROI 拖动
    /// </summary>
    private void FinishRoiDrag()
    {
        IsDraggingRoi = false;
        var draggedRoi = DraggingRoi;
        DraggingRoi = null;
        StatusText = $"{draggedRoi?.GetType().Name} 拖动完成";
    }

    /// <summary>
    /// 开始控制点编辑
    /// </summary>
    /// <param name="roi">要编辑的 ROI</param>
    /// <param name="hitResult">命中测试结果</param>
    private void StartControlPointEdit(RoiBase roi, RoiHitTestResult hitResult)
    {
        EditingRoi = roi;
        EditingControlPointIndex = hitResult.VertexIndex; // 使用 VertexIndex 而不是 ControlPointIndex
        StatusText = $"编辑 {roi.GetType().Name} 控制点...";
    }

    /// <summary>
    /// 更新控制点编辑 - 新增方法
    /// </summary>
    /// <param name="currentPoint">当前鼠标位置</param>
    private void UpdateControlPointEdit(Point currentPoint)
    {
        if (EditingRoi != null && EditingControlPointIndex >= 0)
        {
            // 使用 HitTestService 的更新方法处理控制点更新
            HitTestService.UpdateRoiControlPoint(EditingRoi, EditingControlPointIndex, currentPoint);

            // 触发界面刷新，确保编辑过程实时可见
            OnPropertyChanged(nameof(Rois));
            OnPropertyChanged(nameof(EditingRoi));
        }
    }

    /// <summary>
    /// 完成控制点编辑
    /// </summary>
    private void FinishControlPointEdit()
    {
        var editedRoi = EditingRoi;
        EditingRoi = null;
        EditingControlPointIndex = -1;
        StatusText = $"{editedRoi?.GetType().Name} 编辑完成";

        // 确保最终状态的界面刷新
        OnPropertyChanged(nameof(Rois));
    }

    /// <summary>
    /// 单选 ROI - 修复：立即更新选中状态并刷新界面
    /// </summary>
    /// <param name="roi">要选中的 ROI</param>
    private void SelectSingleRoi(RoiBase roi)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ClearSelection();
        }

        if (roi.IsSelected)
        {
            roi.IsSelected = false;
            SelectedRois.Remove(roi);
        }
        else
        {
            roi.IsSelected = true;
            SelectedRois.Add(roi);
        }

        StatusText = $"选中 {SelectedRois.Count} 个 ROI";

        // 修复：立即触发界面刷新，确保选中效果立刻可见
        OnPropertyChanged(nameof(Rois));
        OnPropertyChanged(nameof(SelectedRois));
    }

    /// <summary>
    /// 清除选择
    /// </summary>
    private void ClearSelection()
    {
        foreach (var roi in SelectedRois)
        {
            roi.IsSelected = false;
        }
        SelectedRois.Clear();
    }

    /// <summary>
    /// 取消所有操作
    /// </summary>
    private void CancelAllOperations()
    {
        IsRealTimeDrawing = false;
        IsDrawing = false;
        IsBatchSelecting = false;
        IsCanvasPanning = false;
        IsDraggingRoi = false;
        CreatingRoi = null;
        DraggingRoi = null;
        EditingRoi = null;
        EditingControlPointIndex = -1;
        BatchSelectRect = Rect.Empty;
        ClearPolygonDrawing();
        ClearSectorDrawing();  // 修改：将ClearArcDrawing改为ClearSectorDrawing
        StatusText = "已取消当前操作";
    }

    /// <summary>
    /// 更新状态文本
    /// </summary>
    private void UpdateStatusText()
    {
        var coord = $"鼠标位置: ({CurrentMousePosition.X:F1}, {CurrentMousePosition.Y:F1})";
        var zoom = $"缩放: {Zoom:F2}x";
        var roiCount = $"ROI: {Rois.Count}";
        var selected = $"选中: {SelectedRois.Count}";

        var operation = GetCurrentOperationText();

        StatusText = $"{coord} | {zoom} | {roiCount} | {selected}{operation}";
    }

    /// <summary>
    /// 获取当前操作状态文本
    /// </summary>
    /// <returns>操作状态描述</returns>
    private string GetCurrentOperationText()
    {
        if (IsRealTimeDrawing) return $" | 绘制{GetToolName()}中";
        if (IsPolygonDrawing) return " | 多边形绘制中";
        if (IsSectorDrawing) return " | 扇形绘制中";  // 修改：将圆弧改为扇形
        if (IsBatchSelecting) return " | 批量选择中";
        if (IsCanvasPanning) return " | 画布平移中";
        if (IsDraggingRoi) return " | ROI拖动中";
        if (EditingRoi != null)
        {
            var controlType = EditingControlPointIndex == 8 ? "旋转" : "缩放";
            return $" | {controlType}控制点编辑中";
        }
        return "";
    }

    /// <summary>
    /// 获取工具名称
    /// </summary>
    /// <returns>当前工具的中文名称</returns>
    private string GetToolName()
    {
        return CurrentTool switch
        {
            RoiTool.Rectangle => "矩形",
            RoiTool.Ellipse => "椭圆",
            RoiTool.Circle => "圆形",
            RoiTool.Polygon => "多边形",
            RoiTool.Sector => "扇形",  // 修改：将Arc改为Sector
            _ => "形状"
        };
    }

    #endregion

    #region Private Methods - 原有功能保持不变

    /// <summary>
    /// 从路径加载图像
    /// </summary>
    private async Task LoadImageFromPath(string filePath)
    {
        await Task.Run(() =>
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            Application.Current.Dispatcher.Invoke(() =>
            {
                ImageSource = bitmap;
                ImageSize = new Size(bitmap.PixelWidth, bitmap.PixelHeight);
                StatusText = $"图像已加载: {bitmap.PixelWidth} x {bitmap.PixelHeight}";
            });
        });
    }

    /// <summary>
    /// 保存 ROI 到 JSON 文件
    /// </summary>
    private async Task SaveRoisToJsonFile(string filePath)
    {
        var roiDataList = Rois.Select(roi => roi.ToJsonData()).ToList();
        var json = JsonSerializer.Serialize(roiDataList, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// 从 JSON 文件加载 ROI
    /// </summary>
    private async Task LoadRoisFromJsonFile(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var roiDataList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

        if (roiDataList != null)
        {
            Rois.Clear();
            SelectedRois.Clear();

            foreach (var roiData in roiDataList)
            {
                if (roiData.TryGetValue("Type", out var typeValue) && typeValue is string typeName)
                {
                    RoiBase? roi = typeName switch
                    {
                        "RectRoi" => new RectRoi(),
                        "EllipseRoi" => new EllipseRoi(),
                        "CircleRoi" => new CircleRoi(),
                        "PolygonRoi" => new PolygonRoi(),
                        "SectorRoi" => new SectorRoi(),  // 修改：将ArcRoi改为SectorRoi
                        "GeometryRoi" => new GeometryRoi(),
                        _ => null
                    };

                    if (roi != null)
                    {
                        roi.FromJsonData(roiData);
                        Rois.Add(roi);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 是否可以执行布尔运算
    /// </summary>
    private bool CanExecuteBooleanOperation() => SelectedRois.Count >= 2;

    /// <summary>
    /// 是否有选中的 ROI
    /// </summary>
    private bool HasSelectedRois() => SelectedRois.Count > 0;

    /// <summary>
    /// 是否有任何 ROI
    /// </summary>
    private bool HasAnyRois() => true;

    /// <summary>
    /// 是否可以删除 ROI - 新增方法
    /// </summary>
    private bool CanDeleteRoi() => IsDrawing == false && SelectedRois.Count > 0;

    /// <summary>
    /// 更新右键菜单状态 - 新增方法，根据右键位置设置菜单项状态
    /// </summary>
    /// <param name="rightClickPoint">右键点击位置（图像坐标）</param>
    public void UpdateContextMenuState(Point rightClickPoint)
    {
        _rightClickTargetRoi = null;

        if (SelectedRois.Count > 0)
        {
            // 如果有选中的 ROI，删除菜单始终可用

            return;
        }

        // 如果没有选中的 ROI，检查右键位置是否有 ROI
        var (hitRoi, hitResult) = HitTestService.HitTest(Rois, rightClickPoint, HitTestPixelTolerance, Zoom);
        if (hitRoi != null)
        {
            // 设置右键目标 ROI 并选中它
            _rightClickTargetRoi = hitRoi;
            SelectSingleRoi(hitRoi);
        }

        // 触发命令的 CanExecute 重新评估
        DeleteRoiCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 尝试把当前工具切换为选择工具
    /// </summary>
    /// <returns></returns>
    public bool TryToSelect()
    {
        if (this.IsDrawing == true)
        {
            return false;
        }
        if (this.IsBatchSelecting == true)
        {
            return false;
        }
        if (this.IsDraggingRoi == true)
        {
            return false;
        }
        this.CurrentTool = RoiTool.Select;
        return true;
    }

    #endregion
}

/// <summary>
/// 布尔运算完成消息
/// </summary>
public class BooleanOperationCompletedMessage
{
    /// <summary>
    /// 运算结果 ROI
    /// </summary>
    public GeometryRoi Result { get; }
    /// <summary>
    /// 运算类型
    /// </summary>
    public RoiCombineMode Operation { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="result">运算结果 ROI</param>
    /// <param name="operation">运算类型</param>
    public BooleanOperationCompletedMessage(GeometryRoi result, RoiCombineMode operation)
    {
        Result = result;
        Operation = operation;
    }
}