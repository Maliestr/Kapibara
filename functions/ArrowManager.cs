using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    // Класс для хранения стрелки и её наконечника вместе
    public class ArrowContainer
    {
        public Polyline Line { get; set; }
        public Polygon Arrowhead { get; set; }
        public PointCollection OriginalPoints { get; set; }
        public Brush OriginalStroke { get; set; }
        public double OriginalThickness { get; set; }
        public Brush OriginalArrowheadFill { get; set; }
        public Brush OriginalArrowheadStroke { get; set; }
        public List<Ellipse> PointMarkers { get; set; } = new List<Ellipse>(); // Маркеры точек стрелки
    }

    // Менеджер для управления всеми операциями со стрелками
    public class ArrowManager
    {
        // Поля для состояния стрелок
        private bool isArrowModeActive = false;
        private Polyline currentArrow;
        private Point arrowStartPoint;
        private List<ArrowContainer> arrows = new List<ArrowContainer>();
        private ArrowContainer selectedArrowContainer;
        private Polygon draggedArrowhead;
        private PointCollection originalArrowPoints;
        private Dictionary<Polyline, Polygon> arrowToArrowheadMap = new Dictionary<Polyline, Polygon>();
        private List<Polyline> lines = new List<Polyline>();
        private List<Polygon> arrowheads = new List<Polygon>();
        private bool isArrowDragging = false;
        private ArrowContainer draggedArrowContainer;
        private Point arrowOffset;
        private Point arrowDragStartPoint;
        private bool isDraggingPoint = false; // Флаг для отслеживания перетаскивания точки

        // Зависимости (передаются из MainWindow)
        private Canvas mainCanvas;
        private Button pencilButton;
        private bool isDragging;
        private UIElement draggedElement;
        private Point dragStartPoint;
        private bool isSnapToGridEnabled;
        private double workAreaTop;
        private double workAreaBottom;

        // Делегаты для взаимодействия с MainWindow
        private Action<UIElement> bringToFrontAction;
        private Action<UIElement, UndoActionType> saveToUndoStackAction;
        private Action deselectAllAction;
        private Func<UIElement, bool> selectElementAction;
        private Func<Point, Point> snapToGridFunc;
        private Func<int> getMaxZIndexFunc;
        private Func<int> getMinZIndexFunc;
        private Action<ArrowContainer> updateArrowPropertiesInPanelsAction;
        private Func<double> getCurrentStrokeThicknessFunc; // Функция для получения текущей толщины из комбо-бокса
        
        // События для уведомления об изменениях
        public event Action OnArrowDeleted; // Вызывается при удалении выделенной стрелки
        public event Action OnArrowDeselected; // Вызывается при снятии выделения со стрелки

        public bool IsArrowModeActive => isArrowModeActive;
        public Polyline CurrentArrow => currentArrow;
        public List<ArrowContainer> Arrows => arrows;
        public ArrowContainer SelectedArrowContainer => selectedArrowContainer;

        // Конструктор
        public ArrowManager(
            Canvas canvas,
            Button pencil,
            bool isSnapToGridEnabled,
            double workAreaTop,
            double workAreaBottom,
            Action<UIElement> bringToFront,
            Action<UIElement, UndoActionType> saveToUndoStack,
            Action deselectAll,
            Func<UIElement, bool> selectElement,
            Func<Point, Point> snapToGrid,
            Func<int> getMaxZIndex,
            Func<int> getMinZIndex,
            Action<ArrowContainer> updateArrowProperties,
            Func<double> getCurrentStrokeThickness = null)
        {
            mainCanvas = canvas;
            pencilButton = pencil;
            this.isSnapToGridEnabled = isSnapToGridEnabled;
            this.workAreaTop = workAreaTop;
            this.workAreaBottom = workAreaBottom;
            bringToFrontAction = bringToFront;
            saveToUndoStackAction = saveToUndoStack;
            deselectAllAction = deselectAll;
            selectElementAction = selectElement;
            snapToGridFunc = snapToGrid;
            getMaxZIndexFunc = getMaxZIndex;
            getMinZIndexFunc = getMinZIndex;
            updateArrowPropertiesInPanelsAction = updateArrowProperties;
            getCurrentStrokeThicknessFunc = getCurrentStrokeThickness;
        }

        // Метод для обновления зависимостей (если они изменяются)
        public void UpdateDependencies(bool snapToGridEnabled)
        {
            isSnapToGridEnabled = snapToGridEnabled;
        }

        // Переключение режима рисования стрелок
        public void ToggleArrowMode()
        {
            isArrowModeActive = !isArrowModeActive;

            if (isArrowModeActive)
            {
                // Активируем режим рисования
                mainCanvas.Cursor = Cursors.Cross;
                pencilButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3939"));
            }
            else
            {
                // Деактивируем режим рисования
                mainCanvas.Cursor = Cursors.Arrow;
                pencilButton.Background = Brushes.Transparent;

                if (currentArrow != null)
                {
                    CompleteArrow();
                }
            }
        }

        // Методы для рисования стрелок
        public void StartArrowDrawing(Point startPoint)
        {
            arrowStartPoint = startPoint;

            // Получаем текущую толщину из комбо-бокса, если функция доступна
            double strokeThickness = 4; // Значение по умолчанию
            if (getCurrentStrokeThicknessFunc != null)
            {
                strokeThickness = getCurrentStrokeThicknessFunc();
            }

            currentArrow = new Polyline
            {
                Stroke = Brushes.White,
                StrokeThickness = strokeThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Triangle,
                Points = new PointCollection { startPoint, startPoint }
            };

            mainCanvas.Children.Add(currentArrow);
            Canvas.SetZIndex(currentArrow, 500);
        }

        // Метод обновления рисования ломаной стрелки
        public void UpdateArrowDrawing(Point currentPoint)
        {
            if (currentArrow != null && currentArrow.Points.Count >= 1)
            {
                // Для ломаной линии добавляем новую точку при перемещении мыши
                // или обновляем последнюю точку для плавного рисования

                if (currentArrow.Points.Count == 1)
                {
                    // Первое перемещение - добавляем вторую точку
                    currentArrow.Points.Add(currentPoint);
                }
                else
                {
                    // Обновляем последнюю точку для плавного рисования
                    currentArrow.Points[currentArrow.Points.Count - 1] = currentPoint;
                }
            }
        }

        // Метод добавления излома (нового сегмента)
        public void AddArrowSegment(Point point)
        {
            if (currentArrow != null && currentArrow.Points.Count >= 2)
            {
                // Добавляем новую точку для создания излома
                currentArrow.Points.Add(point);
            }
        }

        public void CompleteArrow()
        {
            if (currentArrow != null && currentArrow.Points.Count >= 2)
            {
                // Удаляем последнюю точку если она совпадает с предыдущей
                if (currentArrow.Points.Count > 2)
                {
                    Point lastPoint = currentArrow.Points[currentArrow.Points.Count - 1];
                    Point prevPoint = currentArrow.Points[currentArrow.Points.Count - 2];

                    if (lastPoint == prevPoint)
                    {
                        currentArrow.Points.RemoveAt(currentArrow.Points.Count - 1);
                    }
                }

                // Получаем точки для наконечника
                Point lineStart = currentArrow.Points[currentArrow.Points.Count - 2];
                Point lineEnd = currentArrow.Points[currentArrow.Points.Count - 1];

                // Создаем контейнер стрелки с наконечником
                var arrowContainer = new ArrowContainer
                {
                    Line = currentArrow,
                    Arrowhead = CreateArrowhead(lineStart, lineEnd, currentArrow.Stroke)
                };

                // Добавляем наконечник на холст
                if (arrowContainer.Arrowhead != null)
                {
                    mainCanvas.Children.Add(arrowContainer.Arrowhead);
                }

                // Регистрируем стрелку
                RegisterArrowContainer(arrowContainer);

                // Перемещаем стрелку и наконечник на передний план
                bringToFrontAction?.Invoke(currentArrow);
                if (arrowContainer.Arrowhead != null)
                {
                    bringToFrontAction?.Invoke(arrowContainer.Arrowhead);
                }

                // Сохраняем в стек отмены
                saveToUndoStackAction?.Invoke(currentArrow, UndoActionType.Add);

                currentArrow = null;
                ToggleArrowMode();
            }
        }

        private void RegisterArrowContainer(ArrowContainer arrowContainer)
        {
            if (arrowContainer == null || arrowContainer.Line == null) return;

            CaptureArrowOriginalAppearance(arrowContainer);
            arrows.Add(arrowContainer);
            MakeArrowDraggable(arrowContainer);
            MakeArrowPointsDraggable(arrowContainer);
        }

        private void CaptureArrowOriginalAppearance(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null) return;

            try
            {
                arrowContainer.OriginalStroke = arrowContainer.Line.Stroke?.CloneCurrentValue();
                arrowContainer.OriginalThickness = arrowContainer.Line.StrokeThickness;

                if (arrowContainer.Arrowhead != null)
                {
                    arrowContainer.OriginalArrowheadFill = arrowContainer.Arrowhead.Fill?.CloneCurrentValue();
                    arrowContainer.OriginalArrowheadStroke = arrowContainer.Arrowhead.Stroke?.CloneCurrentValue();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в CaptureArrowOriginalAppearance: {ex.Message}");
            }
        }

        private void ApplyArrowSelectionVisual(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null) return;

            try
            {
                // Оставляем оригинальный цвет стрелки, не меняем его на красный
                // Показываем маркеры точек при выделении
                ShowPointMarkers(arrowContainer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в ApplyArrowSelectionVisual: {ex.Message}");
            }
        }

        private void RestoreArrowAppearance(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null) return;

            try
            {
                if (arrowContainer.OriginalStroke != null)
                    arrowContainer.Line.Stroke = arrowContainer.OriginalStroke.CloneCurrentValue();
                else
                    arrowContainer.Line.Stroke = Brushes.Black; // Значение по умолчанию

                arrowContainer.Line.StrokeThickness = arrowContainer.OriginalThickness > 0 ? arrowContainer.OriginalThickness : 4;
                arrowContainer.Line.Opacity = 1.0;

                if (arrowContainer.Arrowhead != null)
                {
                    if (arrowContainer.OriginalArrowheadFill != null)
                        arrowContainer.Arrowhead.Fill = arrowContainer.OriginalArrowheadFill.CloneCurrentValue();
                    else
                        arrowContainer.Arrowhead.Fill = Brushes.Black;

                    if (arrowContainer.OriginalArrowheadStroke != null)
                        arrowContainer.Arrowhead.Stroke = arrowContainer.OriginalArrowheadStroke.CloneCurrentValue();
                    else
                        arrowContainer.Arrowhead.Stroke = Brushes.Black;

                    arrowContainer.Arrowhead.Opacity = 1.0;
                }

                // Скрываем маркеры точек при снятии выделения
                HidePointMarkers(arrowContainer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в RestoreArrowAppearance: {ex.Message}");
            }
        }

        public void DeselectArrow()
        {
            if (selectedArrowContainer != null)
            {
                RestoreArrowAppearance(selectedArrowContainer);
                selectedArrowContainer = null;
                // Уведомляем о снятии выделения
                OnArrowDeselected?.Invoke();
            }
        }

        private void SetArrowDragVisual(ArrowContainer arrowContainer, bool isDragging)
        {
            if (arrowContainer?.Line == null) return;

            if (isDragging)
            {
                // При перетаскивании оставляем оригинальный цвет, не меняем на красный
                // Визуальный фидбэк обеспечивается маркерами точек
            }
            else
            {
                if (selectedArrowContainer == arrowContainer)
                {
                    ApplyArrowSelectionVisual(arrowContainer);
                }
                else
                {
                    RestoreArrowAppearance(arrowContainer);
                }
            }
        }

        // Делаем стрелку перетаскиваемой
        private void MakeArrowDraggable(ArrowContainer arrowContainer)
        {
            if (arrowContainer == null || arrowContainer.Line == null) return;

            // Обработчики для линии стрелки
            arrowContainer.Line.MouseLeftButtonDown += Arrow_MouseLeftButtonDown;
            arrowContainer.Line.MouseMove += Arrow_MouseMove;
            arrowContainer.Line.MouseLeftButtonUp += Arrow_MouseLeftButtonUp;

            // Обработчики для наконечника
            if (arrowContainer.Arrowhead != null)
            {
                arrowContainer.Arrowhead.MouseLeftButtonDown += Arrowhead_MouseLeftButtonDown;
                arrowContainer.Arrowhead.MouseMove += Arrow_MouseMove;
                arrowContainer.Arrowhead.MouseLeftButtonUp += Arrow_MouseLeftButtonUp;
            }

            // Добавляем контекстное меню
            arrowContainer.Line.MouseRightButtonDown += (s, e) =>
            {
                CreateArrowContextMenu(arrowContainer);
                e.Handled = true;
            };
        }

        private void Arrowhead_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isArrowModeActive) return;

            var arrowhead = sender as Polygon;
            if (arrowhead == null) return;

            // Находим контейнер стрелки по наконечнику
            var arrowContainer = arrows?.FirstOrDefault(a => a?.Arrowhead == arrowhead);
            if (arrowContainer == null) return;

            // Выделяем стрелку
            SelectArrow(arrowContainer.Line);

            // Начинаем перетаскивание
            StartArrowDrag(arrowContainer, e);
            e.Handled = true;
        }

        // Обновленный метод для начала перетаскивания стрелки
        private void StartArrowDrag(ArrowContainer arrowContainer, MouseButtonEventArgs e)
        {
            if (arrowContainer == null || arrowContainer.Line == null || mainCanvas == null) return;

            try
            {
                // Сохраняем ссылку на перетаскиваемую стрелку
                draggedArrowContainer = arrowContainer;
                isArrowDragging = true;

                // Захватываем мышь
                arrowContainer.Line.CaptureMouse();
                if (arrowContainer.Arrowhead != null)
                {
                    arrowContainer.Arrowhead.CaptureMouse();
                }

                // Сохраняем начальную позицию
                arrowDragStartPoint = e.GetPosition(mainCanvas);

                // Сохраняем оригинальные точки стрелки
                arrowContainer.OriginalPoints = new PointCollection(arrowContainer.Line.Points);

                // Визуальный фидбэк - поднимаем наверх
                Canvas.SetZIndex(arrowContainer.Line, 1000);
                if (arrowContainer.Arrowhead != null)
                {
                    Canvas.SetZIndex(arrowContainer.Arrowhead, 1001);
                }

                // Визуальное выделение при перетаскивании
                SetArrowDragVisual(arrowContainer, true);

                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в StartArrowDrag: {ex.Message}");
                // Сбрасываем состояние в случае ошибки
                isArrowDragging = false;
                draggedArrowContainer = null;
            }
        }

        // Обработчики событий для перетаскивания стрелок
        private void Arrow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isArrowModeActive) return;

            try
            {
                var arrow = sender as Polyline;
                if (arrow == null) return;

                // Находим контейнер стрелки
                var arrowContainer = arrows?.FirstOrDefault(a => a?.Line == arrow);
                if (arrowContainer == null) return;

                // Выделяем стрелку
                SelectArrow(arrow);

                // Начинаем перетаскивание
                StartArrowDrag(arrowContainer, e);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в Arrow_MouseLeftButtonDown: {ex.Message}");
            }
        }

        private void Arrow_MouseMove(object sender, MouseEventArgs e)
        {
            // Не перетаскиваем всю стрелку, если перетаскивается отдельная точка
            if (isDraggingPoint) return;
            if (!isArrowDragging || draggedArrowContainer == null || draggedArrowContainer.Line == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            try
            {
                Point currentPosition = e.GetPosition(mainCanvas);

                // Вычисляем смещение от начальной точки
                double deltaX = currentPosition.X - arrowDragStartPoint.X;
                double deltaY = currentPosition.Y - arrowDragStartPoint.Y;

                // Проверяем что есть оригинальные точки
                if (draggedArrowContainer.OriginalPoints == null) return;

                // Создаем новую коллекцию точек с применением смещения
                var newPoints = new PointCollection();
                foreach (Point point in draggedArrowContainer.OriginalPoints)
                {
                    double newX = point.X + deltaX;
                    double newY = point.Y + deltaY;

                    // Ограничиваем перемещение рабочей областью
                    newX = Math.Max(0, Math.Min(newX, mainCanvas.ActualWidth - 10));
                    newY = Math.Max(workAreaTop, Math.Min(newY, mainCanvas.ActualHeight - workAreaBottom - 10));

                    // ПРИМЕНЯЕМ ПРИВЯЗКУ К СЕТКЕ К КАЖДОЙ ТОЧКЕ В РЕАЛЬНОМ ВРЕМЕНИ
                    if (snapToGridFunc != null)
                    {
                        Point snappedPoint = snapToGridFunc(new Point(newX, newY));
                        newX = snappedPoint.X;
                        newY = snappedPoint.Y;
                    }

                    newPoints.Add(new Point(newX, newY));
                }

                // Обновляем точки стрелки
                draggedArrowContainer.Line.Points = newPoints;

                // Обновляем позиции всех маркеров точек при перемещении стрелки
                if (draggedArrowContainer.PointMarkers != null && draggedArrowContainer.Line.Points != null)
                {
                    for (int i = 0; i < draggedArrowContainer.PointMarkers.Count && i < draggedArrowContainer.Line.Points.Count; i++)
                    {
                        var marker = draggedArrowContainer.PointMarkers[i];
                        Point pt = draggedArrowContainer.Line.Points[i];
                        Canvas.SetLeft(marker, pt.X - 5);
                        Canvas.SetTop(marker, pt.Y - 5);
                    }
                }

                // Синхронно обновляем позицию наконечника
                if (draggedArrowContainer.Arrowhead != null)
                {
                    UpdateArrowheadPosition(draggedArrowContainer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в Arrow_MouseMove: {ex.Message}");
            }
        }

        private void MakeArrowPointsDraggable(ArrowContainer arrowContainer)
        {
            // Очищаем старые маркеры если есть
            if (arrowContainer.PointMarkers != null)
            {
                foreach (var oldMarker in arrowContainer.PointMarkers)
                {
                    if (mainCanvas.Children.Contains(oldMarker))
                    {
                        mainCanvas.Children.Remove(oldMarker);
                    }
                }
            }
            arrowContainer.PointMarkers.Clear();

            // Создаем маркеры для всех точек стрелки (включая первую и последнюю)
            for (int i = 0; i < arrowContainer.Line.Points.Count; i++)
            {
                var pointMarker = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.Transparent,
                    Cursor = Cursors.SizeAll,
                    Tag = i, // Сохраняем индекс точки
                    Visibility = Visibility.Collapsed // По умолчанию скрыты, показываются при выделении
                };

                // Позиционируем маркер на точке
                Point point = arrowContainer.Line.Points[i];
                Canvas.SetLeft(pointMarker, point.X - 5);
                Canvas.SetTop(pointMarker, point.Y - 5);

                mainCanvas.Children.Add(pointMarker);
                arrowContainer.PointMarkers.Add(pointMarker);

                // Обработчики для перетаскивания точки
                pointMarker.MouseLeftButtonDown += (s, e) =>
                {
                    if (isArrowModeActive) return;

                    var marker = s as Ellipse;
                    if (marker != null && marker.Tag is int pointIndex)
                    {
                        // Предотвращаем перетаскивание всей стрелки при клике на маркер
                        e.Handled = true;
                        
                        isDraggingPoint = true; // Устанавливаем флаг перетаскивания точки
                        isDragging = true;
                        draggedElement = marker;
                        marker.CaptureMouse();
                        dragStartPoint = e.GetPosition(mainCanvas);

                        // Сохраняем оригинальные точки
                        arrowContainer.OriginalPoints = new PointCollection(arrowContainer.Line.Points);
                    }
                };

                pointMarker.MouseMove += (s, e) =>
                {
                    var marker = s as Ellipse;
                    if (isDragging && marker != null && marker.Tag is int pointIndex &&
                        arrowContainer.OriginalPoints != null)
                    {
                        Point currentPosition = e.GetPosition(mainCanvas);
                        
                        // Прикрепление к элементам только для крайних точек (первой и последней)
                        if (pointIndex == 0 || pointIndex == arrowContainer.Line.Points.Count - 1)
                        {
                            Point snappedToElement = SnapPointToElement(currentPosition, arrowContainer.Line);
                            if (snappedToElement != currentPosition)
                            {
                                currentPosition = snappedToElement;
                            }
                        }
                        
                        // Применяем привязку к сетке, если включена
                        if (snapToGridFunc != null)
                        {
                            currentPosition = snapToGridFunc(currentPosition);
                        }

                        // Обновляем конкретную точку в стрелке
                        var newPoints = new PointCollection(arrowContainer.Line.Points);
                        newPoints[pointIndex] = currentPosition;

                        arrowContainer.Line.Points = newPoints;

                        // Обновляем позицию маркера
                        Canvas.SetLeft(marker, currentPosition.X - 5);
                        Canvas.SetTop(marker, currentPosition.Y - 5);

                        // Обновляем наконечник если это последняя точка (Count - 1) или предпоследняя (Count - 2)
                        if (pointIndex == arrowContainer.Line.Points.Count - 1 || 
                            pointIndex == arrowContainer.Line.Points.Count - 2)
                        {
                            UpdateArrowheadPosition(arrowContainer);
                        }
                    }
                };

                pointMarker.MouseLeftButtonUp += (s, e) =>
                {
                    var marker = s as Ellipse;
                    if (marker != null && marker.Tag is int pointIndex)
                    {
                        isDraggingPoint = false; // Сбрасываем флаг перетаскивания точки
                        isDragging = false;
                        marker.ReleaseMouseCapture();
                        
                        bool wasSnappedToElement = false;
                        
                        // Прикрепление к элементам только для крайних точек (первой и последней) при завершении
                        if (pointIndex == 0 || pointIndex == arrowContainer.Line.Points.Count - 1)
                        {
                            Point currentPoint = arrowContainer.Line.Points[pointIndex];
                            Point snappedToElement = SnapPointToElement(currentPoint, arrowContainer.Line);
                            if (snappedToElement != currentPoint)
                            {
                                wasSnappedToElement = true;
                                var newPoints = new PointCollection(arrowContainer.Line.Points);
                                newPoints[pointIndex] = snappedToElement;
                                arrowContainer.Line.Points = newPoints;
                                Canvas.SetLeft(marker, snappedToElement.X - 5);
                                Canvas.SetTop(marker, snappedToElement.Y - 5);
                            }
                        }
                        
                        // Применяем привязку к сетке при завершении перетаскивания
                        // Для точек, прикрепленных к элементам, привязку к сетке не применяем, чтобы сохранить прикрепление
                        if (arrowContainer.Line.Points != null && snapToGridFunc != null && !wasSnappedToElement)
                        {
                            var newPoints = new PointCollection();
                            foreach (Point pt in arrowContainer.Line.Points)
                            {
                                newPoints.Add(snapToGridFunc(pt));
                            }
                            arrowContainer.Line.Points = newPoints;
                            
                            // Обновляем позицию маркера после привязки
                            Point snappedPoint = arrowContainer.Line.Points[pointIndex];
                            Canvas.SetLeft(marker, snappedPoint.X - 5);
                            Canvas.SetTop(marker, snappedPoint.Y - 5);
                        }
                        
                        // Обновляем наконечник если это последняя или предпоследняя точка
                        if (pointIndex == arrowContainer.Line.Points.Count - 1 || 
                            pointIndex == arrowContainer.Line.Points.Count - 2)
                        {
                            UpdateArrowheadPosition(arrowContainer);
                        }
                        
                        arrowContainer.OriginalPoints = null;
                        draggedElement = null;
                        e.Handled = true;
                    }
                };
            }
        }

        public void Arrow_MouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            if (!isArrowDragging || draggedArrowContainer == null) return;

            try
            {
                // Завершаем перетаскивание
                isArrowDragging = false;

                // Освобождаем захват мыши
                if (draggedArrowContainer.Line != null)
                {
                    draggedArrowContainer.Line.ReleaseMouseCapture();
                    // Возвращаем нормальный ZIndex
                    Canvas.SetZIndex(draggedArrowContainer.Line, 500);
                }

                if (draggedArrowContainer.Arrowhead != null)
                {
                    draggedArrowContainer.Arrowhead.ReleaseMouseCapture();
                    Canvas.SetZIndex(draggedArrowContainer.Arrowhead, 501);
                }

                // Применяем привязку к сетке при завершении перетаскивания
                if (draggedArrowContainer.Line.Points != null && snapToGridFunc != null)
                {
                    PointCollection snappedPoints = new PointCollection();
                    foreach (Point point in draggedArrowContainer.Line.Points)
                    {
                        snappedPoints.Add(snapToGridFunc(point));
                    }
                    draggedArrowContainer.Line.Points = snappedPoints;

                    // Синхронно обновляем позицию наконечника
                    if (draggedArrowContainer.Arrowhead != null)
                    {
                        UpdateArrowheadPosition(draggedArrowContainer);
                    }

                    // Обновляем позиции маркеров после привязки к сетке
                    UpdatePointMarkersPosition(draggedArrowContainer);
                }
                else
                {
                    // Обновляем позиции маркеров даже без привязки к сетке
                    UpdatePointMarkersPosition(draggedArrowContainer);
                }

                // Восстанавливаем визуальное оформление
                if (selectedArrowContainer == draggedArrowContainer)
                {
                    ApplyArrowSelectionVisual(draggedArrowContainer);
                }
                else
                {
                    RestoreArrowAppearance(draggedArrowContainer);
                }

                // Очищаем временные данные
                draggedArrowContainer.OriginalPoints = null;
                draggedArrowContainer = null;

                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в Arrow_MouseLeftButtonUp: {ex.Message}");
                // Принудительно сбрасываем состояние
                isArrowDragging = false;
                draggedArrowContainer = null;
            }
        }

        // Выделение стрелки
        public void SelectArrow(Polyline arrow)
        {
            if (arrow == null) return;

            // Снимаем выделение с других элементов
            deselectAllAction?.Invoke();
            DeselectArrow();

            // Находим контейнер стрелки
            var arrowContainer = arrows?.FirstOrDefault(a => a?.Line == arrow);
            if (arrowContainer == null) return;

            selectedArrowContainer = arrowContainer;
            ApplyArrowSelectionVisual(arrowContainer);

            // Создаем контекстное меню
            CreateArrowContextMenu(arrowContainer);

            // Показываем информацию о стрелке в панелях свойств
            UpdateArrowPropertiesInPanels(arrowContainer);
        }

        private void UpdateArrowPropertiesInPanels(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null) return;
            updateArrowPropertiesInPanelsAction?.Invoke(arrowContainer);
        }

        // Контекстное меню для стрелки
        private void CreateArrowContextMenu(ArrowContainer arrowContainer)
        {
            ContextMenu contextMenu = new ContextMenu
            {
                Background = Brushes.White,
                Foreground = Brushes.Black,
                FontSize = 12,
                Padding = new Thickness(5)
            };

            // Пункт "Удалить"
            MenuItem deleteMenuItem = new MenuItem
            {
                Header = "Удалить",
                Icon = new TextBlock { Text = "🗑", FontSize = 14 }
            };
            deleteMenuItem.Click += (s, e) => DeleteArrow(arrowContainer.Line);
            contextMenu.Items.Add(deleteMenuItem);

            contextMenu.Items.Add(new Separator());

            // Пункт "Перенести на передний план"
            MenuItem bringToFrontMenuItem = new MenuItem
            {
                Header = "Перенести на передний план",
                Icon = new TextBlock { Text = "⬆", FontSize = 14 }
            };
            bringToFrontMenuItem.Click += (s, e) => BringArrowToFront(arrowContainer);
            contextMenu.Items.Add(bringToFrontMenuItem);

            // Пункт "Перенести на задний план"
            MenuItem sendToBackMenuItem = new MenuItem
            {
                Header = "Перенести на задний план",
                Icon = new TextBlock { Text = "⬇", FontSize = 14 }
            };
            sendToBackMenuItem.Click += (s, e) => SendArrowToBack(arrowContainer);
            contextMenu.Items.Add(sendToBackMenuItem);

            arrowContainer.Line.ContextMenu = contextMenu;
        }

        // Изменение цвета стрелки
        private void ChangeArrowColor(ArrowContainer arrowContainer)
        {
            var dialog = new Window
            {
                Title = "Выбор цвета",
                Width = 300,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stackPanel = new StackPanel();

            // ComboBox с основными цветами
            var colorComboBox = new ComboBox
            {
                Margin = new Thickness(10),
                ItemsSource = new[]
                {
                    new { Name = "Красный", Color = Colors.Red },
                    new { Name = "Синий", Color = Colors.Blue },
                    new { Name = "Зеленый", Color = Colors.Green },
                    new { Name = "Черный", Color = Colors.Black },
                    new { Name = "Белый", Color = Colors.White }
                },
                DisplayMemberPath = "Name"
            };

            // Кнопки
            var okButton = new Button { Content = "OK", Width = 80 };
            var cancelButton = new Button { Content = "Отмена", Width = 80 };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(colorComboBox);
            stackPanel.Children.Add(buttonPanel);
            dialog.Content = stackPanel;

            okButton.Click += (s, e) =>
            {
                if (colorComboBox.SelectedValue != null)
                {
                    var selectedColor = (colorComboBox.SelectedValue as dynamic)?.Color;
                    Brush newBrush = new SolidColorBrush(selectedColor);

                    // Применяем цвет к линии
                    arrowContainer.Line.Stroke = newBrush;

                    // Применяем цвет к наконечнику
                    if (arrowContainer.Arrowhead != null)
                    {
                        arrowContainer.Arrowhead.Fill = newBrush;
                        arrowContainer.Arrowhead.Stroke = newBrush;
                    }

                    CaptureArrowOriginalAppearance(arrowContainer);

                    if (selectedArrowContainer == arrowContainer)
                    {
                        ApplyArrowSelectionVisual(arrowContainer);
                    }
                }
                dialog.DialogResult = true;
            };

            cancelButton.Click += (s, e) => dialog.DialogResult = false;

            dialog.ShowDialog();
        }

        // Удаление стрелки
        public void DeleteArrow(Polyline arrow)
        {
            if (arrow == null) return;

            try
            {
                var arrowContainer = arrows?.FirstOrDefault(a => a?.Line == arrow);
                if (arrowContainer == null) return;

                // Сохраняем в стек отмены
                SaveArrowToUndoStack(arrowContainer);

                bool wasSelected = selectedArrowContainer == arrowContainer;
                
                if (wasSelected)
                {
                    DeselectArrow();
                }

                // Удаляем с холста
                if (mainCanvas != null)
                {
                    if (mainCanvas.Children.Contains(arrowContainer.Line))
                    {
                        mainCanvas.Children.Remove(arrowContainer.Line);
                    }
                    if (arrowContainer.Arrowhead != null && mainCanvas.Children.Contains(arrowContainer.Arrowhead))
                    {
                        mainCanvas.Children.Remove(arrowContainer.Arrowhead);
                    }
                    // Удаляем маркеры точек
                    if (arrowContainer.PointMarkers != null)
                    {
                        foreach (var marker in arrowContainer.PointMarkers)
                        {
                            if (mainCanvas.Children.Contains(marker))
                            {
                                mainCanvas.Children.Remove(marker);
                            }
                        }
                        arrowContainer.PointMarkers.Clear();
                    }
                }

                arrows?.Remove(arrowContainer);
                
                // Уведомляем об удалении стрелки, если она была выделена
                if (wasSelected)
                {
                    OnArrowDeleted?.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в DeleteArrow: {ex.Message}");
            }
        }

        private void SaveArrowToUndoStack(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null) return;

            try
            {
                // Сохраняем стрелку как удаление
                saveToUndoStackAction?.Invoke(arrowContainer.Line, UndoActionType.Remove);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в SaveArrowToUndoStack: {ex.Message}");
            }
        }

        // Создание стрелки из данных файла
        public void CreateArrowFromData(string[] data)
        {
            if (data.Length >= 4)
            {
                var arrow = new Polyline
                {
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data[2])),
                    StrokeThickness = double.Parse(data[3]),
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Triangle,
                    Points = new PointCollection(),
                    Cursor = Cursors.Hand
                };

                // Парсим точки
                string[] pointStrings = data[1].Split(';');
                foreach (string pointStr in pointStrings)
                {
                    string[] coords = pointStr.Split(',');
                    if (coords.Length == 2)
                    {
                        arrow.Points.Add(new Point(double.Parse(coords[0]), double.Parse(coords[1])));
                    }
                }

                // Создаем контейнер стрелки
                var arrowContainer = new ArrowContainer { Line = arrow };

                // Создаем наконечник для загруженной стрелки
                if (arrow.Points.Count >= 2)
                {
                    Point lineStart = arrow.Points[arrow.Points.Count - 2];
                    Point lineEnd = arrow.Points[arrow.Points.Count - 1];
                    arrowContainer.Arrowhead = CreateArrowhead(lineStart, lineEnd, arrow.Stroke);
                }

                // Добавляем на холст
                mainCanvas.Children.Add(arrow);
                if (arrowContainer.Arrowhead != null)
                {
                    mainCanvas.Children.Add(arrowContainer.Arrowhead);
                }

                // Добавляем в список и делаем перетаскиваемой
                RegisterArrowContainer(arrowContainer);
            }
        }

        // Метод для создания треугольника-наконечника
        private Polygon CreateArrowhead(Point startPoint, Point endPoint, Brush color)
        {
            // Вычисляем направление стрелки
            double dx = endPoint.X - startPoint.X;
            double dy = endPoint.Y - startPoint.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length == 0) return null;

            // Нормализуем вектор направления
            dx /= length;
            dy /= length;

            // Размеры наконечника
            double arrowLength = 20;
            double arrowWidth = 12;

            // Вычисляем точки треугольника
            Point arrowTip = endPoint;
            Point arrowLeft = new Point(
                endPoint.X - dx * arrowLength + dy * arrowWidth,
                endPoint.Y - dy * arrowLength - dx * arrowWidth
            );
            Point arrowRight = new Point(
                endPoint.X - dx * arrowLength - dy * arrowWidth,
                endPoint.Y - dy * arrowLength + dx * arrowWidth
            );

            // Создаем треугольник
            Polygon arrowhead = new Polygon
            {
                Points = new PointCollection { arrowTip, arrowLeft, arrowRight },
                Fill = color,
                Stroke = color,
                StrokeThickness = 1,
                Cursor = Cursors.Hand
            };

            return arrowhead;
        }

        private void UpdateArrowheadPosition(ArrowContainer arrowContainer)
        {
            if (arrowContainer.Line.Points.Count < 2 || arrowContainer.Arrowhead == null) return;

            // Находим последний сегмент стрелки
            Point lineStart = arrowContainer.Line.Points[arrowContainer.Line.Points.Count - 2];
            Point lineEnd = arrowContainer.Line.Points[arrowContainer.Line.Points.Count - 1];

            // ОБНОВЛЯЕМ ГЕОМЕТРИЮ НАКОНЕЧНИКА НА МЕСТЕ
            UpdateArrowheadGeometry(arrowContainer.Arrowhead, lineStart, lineEnd, arrowContainer.Line.Stroke);
        }

        // Метод для обновления геометрии наконечника
        private void UpdateArrowheadGeometry(Polygon arrowhead, Point lineStart, Point lineEnd, Brush color)
        {
            // Вычисляем направление стрелки
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length == 0) return;

            // Нормализуем вектор направления
            dx /= length;
            dy /= length;

            // Размеры наконечника
            double arrowLength = 20;
            double arrowWidth = 12;

            // Вычисляем точки треугольника
            Point arrowTip = lineEnd;

            // Боковые точки наконечника
            Point arrowLeft = new Point(
                lineEnd.X - dx * arrowLength + dy * arrowWidth,
                lineEnd.Y - dy * arrowLength - dx * arrowWidth
            );
            Point arrowRight = new Point(
                lineEnd.X - dx * arrowLength - dy * arrowWidth,
                lineEnd.Y - dy * arrowLength + dx * arrowWidth
            );

            // Обновляем точки наконечника
            arrowhead.Points = new PointCollection { arrowTip, arrowLeft, arrowRight };
            arrowhead.Fill = color;
            arrowhead.Stroke = color;
        }

        // Показать маркеры точек стрелки
        private void ShowPointMarkers(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.PointMarkers == null) return;

            foreach (var marker in arrowContainer.PointMarkers)
            {
                marker.Visibility = Visibility.Visible;
                marker.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A90E2")); // Синий цвет для маркеров
                marker.Stroke = Brushes.White;
                marker.StrokeThickness = 1;
                // Убеждаемся, что маркеры поверх других элементов
                if (mainCanvas.Children.Contains(marker))
                {
                    Canvas.SetZIndex(marker, 2000);
                }
            }
        }

        // Скрыть маркеры точек стрелки
        private void HidePointMarkers(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.PointMarkers == null) return;

            foreach (var marker in arrowContainer.PointMarkers)
            {
                marker.Visibility = Visibility.Collapsed;
            }
        }

        // Обновить позиции маркеров точек стрелки
        private void UpdatePointMarkersPosition(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.PointMarkers == null || arrowContainer.Line?.Points == null) return;

            for (int i = 0; i < arrowContainer.PointMarkers.Count && i < arrowContainer.Line.Points.Count; i++)
            {
                var marker = arrowContainer.PointMarkers[i];
                Point pt = arrowContainer.Line.Points[i];
                Canvas.SetLeft(marker, pt.X - 5);
                Canvas.SetTop(marker, pt.Y - 5);
            }
        }

        // Прикрепление точки к ближайшему элементу
        private Point SnapPointToElement(Point point, Polyline currentArrowLine)
        {
            if (mainCanvas == null) return point;

            const double snapDistance = 20; // Максимальное расстояние для прикрепления в пикселях
            Point bestSnapPoint = point;
            double minDistance = snapDistance;

            // Находим контейнер текущей стрелки для исключения её маркеров
            ArrowContainer currentContainer = arrows?.FirstOrDefault(a => a?.Line == currentArrowLine);
            HashSet<UIElement> excludedElements = new HashSet<UIElement> { currentArrowLine };
            if (currentContainer?.Arrowhead != null)
            {
                excludedElements.Add(currentContainer.Arrowhead);
            }
            if (currentContainer?.PointMarkers != null)
            {
                foreach (var marker in currentContainer.PointMarkers)
                {
                    excludedElements.Add(marker);
                }
            }

            // Проходим по всем элементам на холсте
            foreach (UIElement element in mainCanvas.Children)
            {
                // Пропускаем саму стрелку, её наконечник и маркеры
                if (excludedElements.Contains(element))
                    continue;
                
                // Пропускаем другие наконечники стрелок (Polygon)
                if (element is Polygon)
                {
                    // Проверяем, не является ли это наконечником другой стрелки
                    bool isOtherArrowhead = arrows?.Any(a => a?.Arrowhead == element) ?? false;
                    if (isOtherArrowhead)
                        continue;
                }

                // Получаем границы элемента
                Rect elementBounds = GetElementBounds(element);
                if (elementBounds.IsEmpty) continue;

                // Находим ближайшую точку на границе элемента
                Point snapPoint = GetClosestPointOnBounds(point, elementBounds);
                double distance = Distance(point, snapPoint);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestSnapPoint = snapPoint;
                }
            }

            return bestSnapPoint;
        }

        // Получить границы элемента на холсте
        private Rect GetElementBounds(UIElement element)
        {
            if (element == null) return Rect.Empty;

            try
            {
                FrameworkElement fe = element as FrameworkElement;
                if (fe == null) return Rect.Empty;

                double left = Canvas.GetLeft(fe);
                double top = Canvas.GetTop(fe);

                // Если элемент не имеет позиции на Canvas, возвращаем пустой Rect
                if (double.IsNaN(left) || double.IsNaN(top))
                {
                    // Пытаемся получить через TransformToVisual
                    GeneralTransform transform = fe.TransformToVisual(mainCanvas);
                    if (transform != null)
                    {
                        Point topLeft = transform.Transform(new Point(0, 0));
                        left = topLeft.X;
                        top = topLeft.Y;
                    }
                    else
                    {
                        return Rect.Empty;
                    }
                }

                double width = fe.ActualWidth > 0 ? fe.ActualWidth : (fe.Width > 0 ? fe.Width : 0);
                double height = fe.ActualHeight > 0 ? fe.ActualHeight : (fe.Height > 0 ? fe.Height : 0);

                if (width <= 0 || height <= 0) return Rect.Empty;

                return new Rect(left, top, width, height);
            }
            catch
            {
                return Rect.Empty;
            }
        }

        // Найти ближайшую точку на границе прямоугольника к заданной точке
        private Point GetClosestPointOnBounds(Point point, Rect bounds)
        {
            // Вычисляем ближайшую точку на каждой стороне прямоугольника
            Point[] candidates = new Point[]
            {
                new Point(bounds.Left, point.Y), // Левая сторона
                new Point(bounds.Right, point.Y), // Правая сторона
                new Point(point.X, bounds.Top), // Верхняя сторона
                new Point(point.X, bounds.Bottom), // Нижняя сторона
                new Point(bounds.Left, bounds.Top), // Верхний левый угол
                new Point(bounds.Right, bounds.Top), // Верхний правый угол
                new Point(bounds.Left, bounds.Bottom), // Нижний левый угол
                new Point(bounds.Right, bounds.Bottom) // Нижний правый угол
            };

            // Ограничиваем точки кандидатов границами прямоугольника
            for (int i = 0; i < 4; i++) // Первые 4 - стороны
            {
                if (candidates[i].X < bounds.Left) candidates[i].X = bounds.Left;
                if (candidates[i].X > bounds.Right) candidates[i].X = bounds.Right;
                if (candidates[i].Y < bounds.Top) candidates[i].Y = bounds.Top;
                if (candidates[i].Y > bounds.Bottom) candidates[i].Y = bounds.Bottom;
            }

            // Находим ближайшую точку
            Point closest = candidates[0];
            double minDist = Distance(point, closest);

            for (int i = 1; i < candidates.Length; i++)
            {
                double dist = Distance(point, candidates[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = candidates[i];
                }
            }

            return closest;
        }

        // Вычислить расстояние между двумя точками
        private double Distance(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // Перенести стрелку на передний план (линия и наконечник вместе)
        private void BringArrowToFront(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null) return;

            int maxZIndex = getMaxZIndexFunc != null ? getMaxZIndexFunc() : 500;

            // Перемещаем линию на передний план
            if (mainCanvas.Children.Contains(arrowContainer.Line))
            {
                Canvas.SetZIndex(arrowContainer.Line, maxZIndex + 1);
                mainCanvas.Children.Remove(arrowContainer.Line);
                mainCanvas.Children.Add(arrowContainer.Line);
            }

            // Перемещаем наконечник на передний план (выше линии)
            if (arrowContainer.Arrowhead != null && mainCanvas.Children.Contains(arrowContainer.Arrowhead))
            {
                Canvas.SetZIndex(arrowContainer.Arrowhead, maxZIndex + 2);
                mainCanvas.Children.Remove(arrowContainer.Arrowhead);
                mainCanvas.Children.Add(arrowContainer.Arrowhead);
            }
        }

        // Перенести стрелку на задний план (линия и наконечник вместе)
        private void SendArrowToBack(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null) return;

            int minZIndex = getMinZIndexFunc != null ? getMinZIndexFunc() : 0;

            // Перемещаем наконечник на задний план (сначала наконечник, чтобы он был выше линии)
            if (arrowContainer.Arrowhead != null && mainCanvas.Children.Contains(arrowContainer.Arrowhead))
            {
                Canvas.SetZIndex(arrowContainer.Arrowhead, minZIndex - 1);
                mainCanvas.Children.Remove(arrowContainer.Arrowhead);
                mainCanvas.Children.Insert(0, arrowContainer.Arrowhead);
            }

            // Перемещаем линию на задний план
            if (mainCanvas.Children.Contains(arrowContainer.Line))
            {
                Canvas.SetZIndex(arrowContainer.Line, minZIndex - 2);
                mainCanvas.Children.Remove(arrowContainer.Line);
                mainCanvas.Children.Insert(0, arrowContainer.Line);
            }
        }

        // Очистка всех стрелок (для очистки холста)
        public void ClearAllArrows()
        {
            foreach (var arrowContainer in arrows.ToList())
            {
                DeleteArrow(arrowContainer.Line);
            }
            arrows.Clear();
            selectedArrowContainer = null;
            currentArrow = null;
        }
    }
}

