using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    // Менеджер для управления перетаскиванием элементов из панели инструментов и на холсте
    public class DragManager
    {
        #region Поля

        // Состояние перетаскивания
        private bool isDragging = false;
        private UIElement draggedElement;
        private Point offset;
        private UIElement shapeCopy;
        private Transform originalTransform;
        private Point dragStartPoint;
        private Point draggedElementStartPosition; // Начальная позиция перетаскиваемого элемента

        // Canvas для работы
        private Canvas mainCanvas;
        private Canvas notationCanvas;
        
        // Состояние для группового перемещения
        private Dictionary<UIElement, Point> selectedElementsStartPositions = new Dictionary<UIElement, Point>();

        // Дополнительные свойства
        private bool isSnapToGridEnabled = false;

        // Ссылка на FillStrokeManager для сохранения оригинальных стилей
        private FillStrokeManager fillStrokeManager;

        #endregion

        #region Константы

        // Константы для ограничения перемещения
        private const double WorkAreaTop = 40;
        private const double WorkAreaBottom = 100;

        #endregion

        #region Свойства

        public bool IsDragging => isDragging;
        public UIElement DraggedElement => draggedElement;

        #endregion

        #region Делегаты

        // Делегаты для взаимодействия с основным окном
        public Func<Point, Point> SnapToGrid { get; set; }
        public Action<FrameworkElement> SelectElement { get; set; }
        public Action<UIElement> SelectShape { get; set; }
        public Action<UIElement, bool> ApplySelectionVisualFeedback { get; set; }
        public Func<UIElement, bool> IsElementInNotation { get; set; }
        public Func<UIElement, double> GetElementWidth { get; set; }
        public Func<UIElement, double> GetElementHeight { get; set; }
        public Action<FrameworkElement> BringToFront { get; set; }
        public Action<UIElement> MakeCanvasShapeDraggable { get; set; }
        public Action<UIElement, UIElement> SetOriginalStyles { get; set; } // Оставлено для обратной совместимости
        public Action<UIElement> AnimateShapeAppearance { get; set; }
        public Func<UIElement, bool> IsShapeFromNotation { get; set; }
        public Func<UIElement, string> GetShapeType { get; set; }
        public Action<UIElement, Point> OnElementMoved { get; set; } // Обработчик перемещения элемента
        public Func<List<UIElement>> GetSelectedElements { get; set; } // Получение списка выделенных элементов
        public Action<UIElement> HideResizeHandlesForElement { get; set; } // Скрытие ручек изменения размера
        public Action<UIElement> ShowResizeHandlesForElement { get; set; } // Показ ручек изменения размера
        
        #endregion

        #region Конструктор и инициализация

        public DragManager(Canvas mainCanvas, Canvas notationCanvas)
        {
            this.mainCanvas = mainCanvas;
            this.notationCanvas = notationCanvas;
        }

        public void SetFillStrokeManager(FillStrokeManager manager)
        {
            fillStrokeManager = manager;
        }

        public void SetSnapToGridEnabled(bool enabled)
        {
            isSnapToGridEnabled = enabled;
        }

        #endregion

        #region Перетаскивание из панели инструментов

        // Делает все указанные фигуры перетаскиваемыми из панели инструментов
        public void MakeAllShapesDraggable(params UIElement[] shapes)
        {
            foreach (var element in shapes)
            {
                AddDragHandlers(element);
            }
        }

        // Добавление обработчиков событий перетаскивания для элемента
        private void AddDragHandlers(UIElement element)
        {
            if (element == null) return;

            element.MouseLeftButtonDown += Shape_MouseLeftButtonDown;
            element.MouseMove += Shape_MouseMove;
            element.MouseLeftButtonUp += Shape_MouseLeftButtonUp;

            if (element is FrameworkElement fe)
            {
                fe.Cursor = Cursors.Hand;
            }
        }

        // Обработчик начала перетаскивания фигуры из панели инструментов
        private void Shape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                // Выделяем фигуру в нотации
                if (element is FrameworkElement frameworkElement)
                {
                    SelectElement?.Invoke(frameworkElement);
                }
                SelectShape?.Invoke(element);

                isDragging = true;
                draggedElement = element;
                element.CaptureMouse();

                // Создаем полупрозрачную копию для перетаскивания
                shapeCopy = CreateShapeCopy(element);
                if (shapeCopy != null)
                {
                    Canvas.SetZIndex(shapeCopy, 1000);

                    // Позиционируем копию
                    Point startPosition = e.GetPosition(mainCanvas);

                    // Привязываем начальную позицию к сетке
                    if (isSnapToGridEnabled && SnapToGrid != null)
                    {
                        startPosition = SnapToGrid(startPosition);
                    }

                    double startLeft = startPosition.X - (GetElementWidth(shapeCopy) / 2);
                    double startTop = startPosition.Y - (GetElementHeight(shapeCopy) / 2);

                    // Дополнительная привязка к сетке
                    if (isSnapToGridEnabled && SnapToGrid != null)
                    {
                        Point snappedStart = SnapToGrid(new Point(startLeft, startTop));
                        startLeft = snappedStart.X;
                        startTop = snappedStart.Y;
                    }

                    Canvas.SetLeft(shapeCopy, startLeft);
                    Canvas.SetTop(shapeCopy, startTop);

                    mainCanvas.Children.Add(shapeCopy);
                    shapeCopy.Opacity = 0.7;
                }

                // Визуальный фидбэк
                element.Opacity = 0.5;

                originalTransform = element.RenderTransform;
                dragStartPoint = e.GetPosition(mainCanvas);
                offset = e.GetPosition(element);
                e.Handled = true;
            }
        }

        // Обработчик перемещения мыши при перетаскивании фигуры из панели инструментов
        private void Shape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && shapeCopy != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(mainCanvas);

                // Применяем привязку к сетке
                if (isSnapToGridEnabled && SnapToGrid != null)
                {
                    currentPosition = SnapToGrid(currentPosition);
                }

                if (currentPosition.Y > WorkAreaTop && currentPosition.Y < mainCanvas.ActualHeight - WorkAreaBottom)
                {
                    double newLeft = currentPosition.X - (GetElementWidth(shapeCopy) / 2);
                    double newTop = currentPosition.Y - (GetElementHeight(shapeCopy) / 2);

                    double elementWidth = GetElementWidth(shapeCopy);
                    double elementHeight = GetElementHeight(shapeCopy);
                    double maxTop = mainCanvas.ActualHeight - elementHeight - WorkAreaBottom;

                    newLeft = Math.Max(0, Math.Min(newLeft, mainCanvas.ActualWidth - elementWidth));
                    newTop = Math.Max(WorkAreaTop, Math.Min(newTop, maxTop));

                    // Дополнительная привязка к сетке
                    if (isSnapToGridEnabled && SnapToGrid != null)
                    {
                        Point snappedPosition = SnapToGrid(new Point(newLeft, newTop));
                        newLeft = snappedPosition.X;
                        newTop = snappedPosition.Y;
                    }

                    Canvas.SetLeft(shapeCopy, newLeft);
                    Canvas.SetTop(shapeCopy, newTop);
                }
            }
        }

        // Обработчик завершения перетаскивания фигуры из панели инструментов и добавления на холст
        private void Shape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                isDragging = false;
                element.ReleaseMouseCapture();
                element.Opacity = 1.0;

                if (shapeCopy != null)
                {
                    Point dropPosition = e.GetPosition(mainCanvas);

                    double elementWidth = GetElementWidth(shapeCopy);
                    double elementHeight = GetElementHeight(shapeCopy);
                    double snappedLeft = dropPosition.X - (elementWidth / 2);
                    double snappedTop = dropPosition.Y - (elementHeight / 2);

                    // Ограничиваем координаты
                    double maxTop = mainCanvas.ActualHeight - elementHeight - WorkAreaBottom;
                    snappedLeft = Math.Max(0, Math.Min(snappedLeft, mainCanvas.ActualWidth - elementWidth));
                    snappedTop = Math.Max(WorkAreaTop, Math.Min(snappedTop, maxTop));

                    // Применяем привязку к сетке
                    if (isSnapToGridEnabled && SnapToGrid != null)
                    {
                        Point snappedPosition = SnapToGrid(new Point(snappedLeft, snappedTop));
                        snappedLeft = snappedPosition.X;
                        snappedTop = snappedPosition.Y;
                    }

                    Canvas.SetLeft(shapeCopy, snappedLeft);
                    Canvas.SetTop(shapeCopy, snappedTop);

                    // Удаляем если бросили вне рабочей области
                    if (dropPosition.Y <= WorkAreaTop ||
                        dropPosition.Y >= mainCanvas.ActualHeight - WorkAreaBottom)
                    {
                        mainCanvas.Children.Remove(shapeCopy);
                    }
                    else
                    {
                        // Делаем копию полностью видимой
                        shapeCopy.Opacity = 1.0;

                        // Перемещаем на передний план
                        if (shapeCopy is FrameworkElement frameworkElement)
                        {
                            BringToFront?.Invoke(frameworkElement);
                        }

                        MakeCanvasShapeDraggable?.Invoke(shapeCopy);

                        // Анимация появления
                        AnimateShapeAppearance?.Invoke(shapeCopy);

                        // Выделяем новую фигуру
                        if (shapeCopy is FrameworkElement fe)
                        {
                            SelectElement?.Invoke(fe);
                        }
                        SelectShape?.Invoke(shapeCopy);

                        if (IsShapeFromNotation != null && IsShapeFromNotation(element))
                        {
                            string shapeType = GetShapeType?.Invoke(element) ?? "Unknown";
                        }
                    }
                }

                // Снимаем выделение с фигуры в нотации
                ApplySelectionVisualFeedback?.Invoke(element, false);

                draggedElement = null;
                shapeCopy = null;
                e.Handled = true;
            }
        }

        #endregion

        #region Перетаскивание элементов на Canvas

        public void CanvasShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                // Проверяем, выделен ли элемент уже
                bool isAlreadySelected = false;
                if (GetSelectedElements != null)
                {
                    var selectedElements = GetSelectedElements();
                    isAlreadySelected = selectedElements.Contains(element);
                }
                
                // Выделяем элемент только если он еще не выделен
                // Это предотвращает очистку других выделений при начале перетаскивания
                if (!isAlreadySelected)
                {
                    SelectElement?.Invoke(element);
                }
                SelectShape?.Invoke(element);

                isDragging = true;
                draggedElement = element;
                element.CaptureMouse();

                // Получаем текущую позицию элемента
                double currentLeft = Canvas.GetLeft(element);
                double currentTop = Canvas.GetTop(element);
                if (double.IsNaN(currentLeft)) currentLeft = 0;
                if (double.IsNaN(currentTop)) currentTop = 0;

                // Получаем позицию курсора
                Point canvasPos = e.GetPosition(mainCanvas);
                if (isSnapToGridEnabled && SnapToGrid != null)
                {
                    canvasPos = SnapToGrid(canvasPos);
                }

                // Вычисляем offset
                offset = new Point(
                    canvasPos.X - currentLeft,
                    canvasPos.Y - currentTop
                );

                // Сохраняем начальную позицию перетаскиваемого элемента
                draggedElementStartPosition = new Point(currentLeft, currentTop);

                // Сохраняем начальные позиции всех выделенных элементов для группового перемещения
                selectedElementsStartPositions.Clear();
                if (GetSelectedElements != null)
                {
                    var selectedElements = GetSelectedElements();
                    foreach (var selectedElement in selectedElements)
                    {
                        if (selectedElement != null)
                        {
                            double left = Canvas.GetLeft(selectedElement);
                            double top = Canvas.GetTop(selectedElement);
                            if (double.IsNaN(left)) left = 0;
                            if (double.IsNaN(top)) top = 0;
                            
                            // Для Line элементов используем координаты точек
                            if (selectedElement is Line line)
                            {
                                left = Math.Min(line.X1, line.X2);
                                top = Math.Min(line.Y1, line.Y2);
                            }
                            
                            selectedElementsStartPositions[selectedElement] = new Point(left, top);
                        }
                    }
                }

                // Поднимаем элемент наверх
                Canvas.SetZIndex(element, 1000);

                // Визуальный фидбэк для всех выделенных элементов
                ApplyCanvasDragVisualFeedback(element, true);
                if (GetSelectedElements != null)
                {
                    var selectedElements = GetSelectedElements();
                    foreach (var selectedElement in selectedElements)
                    {
                        if (selectedElement != element)
                        {
                            ApplyCanvasDragVisualFeedback(selectedElement, true);
                        }
                    }
                }
                
                // Скрываем ручки изменения размера для всех выделенных элементов
                if (GetSelectedElements != null && HideResizeHandlesForElement != null)
                {
                    var selectedElements = GetSelectedElements();
                    foreach (var selectedElement in selectedElements)
                    {
                        HideResizeHandlesForElement(selectedElement);
                    }
                }
                
                e.Handled = true;
            }
        }

        // Обработчик перемещения элемента на Canvas
        public void CanvasShape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = draggedElement;

                // Привязываем позицию курсора к сетке
                Point currentPosition = e.GetPosition(mainCanvas);
                if (isSnapToGridEnabled && SnapToGrid != null)
                {
                    currentPosition = SnapToGrid(currentPosition);
                }

                // Вычисляем новую позицию с учетом offset
                double newLeft = currentPosition.X - offset.X;
                double newTop = currentPosition.Y - offset.Y;

                double elementWidth = GetElementWidth(element);
                double elementHeight = GetElementHeight(element);
                double maxTop = mainCanvas.ActualHeight - elementHeight - WorkAreaBottom;

                newLeft = Math.Max(0, Math.Min(newLeft, mainCanvas.ActualWidth - elementWidth));
                newTop = Math.Max(WorkAreaTop, Math.Min(newTop, maxTop));

                // Вычисляем дельту ДО привязки к сетке для группового перемещения
                double deltaX = newLeft - draggedElementStartPosition.X;
                double deltaY = newTop - draggedElementStartPosition.Y;

                // Дополнительная привязка к сетке
                if (isSnapToGridEnabled && SnapToGrid != null)
                {
                    Point snappedPosition = SnapToGrid(new Point(newLeft, newTop));
                    newLeft = snappedPosition.X;
                    newTop = snappedPosition.Y;
                }

                // Обработка Line элементов отдельно
                if (element is Line line)
                {
                    double currentX1 = line.X1;
                    double currentY1 = line.Y1;
                    double currentX2 = line.X2;
                    double currentY2 = line.Y2;

                    double lineLeft = Math.Min(currentX1, currentX2);
                    double lineTop = Math.Min(currentY1, currentY2);
                    double lineDeltaX = newLeft - lineLeft;
                    double lineDeltaY = newTop - lineTop;

                    double newX1 = currentX1 + lineDeltaX;
                    double newY1 = currentY1 + lineDeltaY;
                    double newX2 = currentX2 + lineDeltaX;
                    double newY2 = currentY2 + lineDeltaY;

                    // Применяем привязку к сетке к точкам линии
                    if (isSnapToGridEnabled && SnapToGrid != null)
                    {
                        Point snappedStart = SnapToGrid(new Point(newX1, newY1));
                        Point snappedEnd = SnapToGrid(new Point(newX2, newY2));
                        newX1 = snappedStart.X;
                        newY1 = snappedStart.Y;
                        newX2 = snappedEnd.X;
                        newY2 = snappedEnd.Y;
                    }

                    line.X1 = newX1;
                    line.Y1 = newY1;
                    line.X2 = newX2;
                    line.Y2 = newY2;
                }
                else
                {
                    // Для остальных элементов
                    Canvas.SetLeft(element, newLeft);
                    Canvas.SetTop(element, newTop);
                }

                // Перемещаем все выделенные элементы вместе
                if (GetSelectedElements != null)
                {
                    var selectedElements = GetSelectedElements();
                    
                    // Используем дельту, вычисленную ДО привязки к сетке

                    // Перемещаем все выделенные элементы, включая перетаскиваемый (если он в списке)
                    foreach (var selectedElement in selectedElements)
                    {
                        // Пропускаем перетаскиваемый элемент, он уже перемещен выше
                        if (selectedElement == element) continue;
                        
                        // Пропускаем null элементы
                        if (selectedElement == null) continue;
                        
                        // Получаем начальную позицию из словаря или текущую позицию
                        Point startPos;
                        if (selectedElementsStartPositions.ContainsKey(selectedElement))
                        {
                            startPos = selectedElementsStartPositions[selectedElement];
                        }
                        else
                        {
                            // Если позиция не была сохранена, получаем текущую
                            double left = Canvas.GetLeft(selectedElement);
                            double top = Canvas.GetTop(selectedElement);
                            if (double.IsNaN(left)) left = 0;
                            if (double.IsNaN(top)) top = 0;
                            
                            if (selectedElement is Line lineForPos)
                            {
                                left = Math.Min(lineForPos.X1, lineForPos.X2);
                                top = Math.Min(lineForPos.Y1, lineForPos.Y2);
                            }
                            
                            startPos = new Point(left, top);
                            selectedElementsStartPositions[selectedElement] = startPos;
                        }
                        
                        double newSelectedLeft = startPos.X + deltaX;
                        double newSelectedTop = startPos.Y + deltaY;
                        
                        // Ограничиваем координаты
                        double selectedElementWidth = GetElementWidth(selectedElement);
                        double selectedElementHeight = GetElementHeight(selectedElement);
                        double selectedMaxTop = mainCanvas.ActualHeight - selectedElementHeight - WorkAreaBottom;
                        
                        newSelectedLeft = Math.Max(0, Math.Min(newSelectedLeft, mainCanvas.ActualWidth - selectedElementWidth));
                        newSelectedTop = Math.Max(WorkAreaTop, Math.Min(newSelectedTop, selectedMaxTop));
                        
                        // Применяем привязку к сетке
                        if (isSnapToGridEnabled && SnapToGrid != null)
                        {
                            Point snappedPosition = SnapToGrid(new Point(newSelectedLeft, newSelectedTop));
                            newSelectedLeft = snappedPosition.X;
                            newSelectedTop = snappedPosition.Y;
                        }
                        
                        // Обработка Line элементов отдельно
                        if (selectedElement is Line selectedLine)
                        {
                            double lineDeltaX = newSelectedLeft - startPos.X;
                            double lineDeltaY = newSelectedTop - startPos.Y;
                            
                            double currentX1 = selectedLine.X1;
                            double currentY1 = selectedLine.Y1;
                            double currentX2 = selectedLine.X2;
                            double currentY2 = selectedLine.Y2;
                            
                            double newX1 = currentX1 + lineDeltaX;
                            double newY1 = currentY1 + lineDeltaY;
                            double newX2 = currentX2 + lineDeltaX;
                            double newY2 = currentY2 + lineDeltaY;
                            
                            // Применяем привязку к сетке к точкам линии
                            if (isSnapToGridEnabled && SnapToGrid != null)
                            {
                                Point snappedStart = SnapToGrid(new Point(newX1, newY1));
                                Point snappedEnd = SnapToGrid(new Point(newX2, newY2));
                                newX1 = snappedStart.X;
                                newY1 = snappedStart.Y;
                                newX2 = snappedEnd.X;
                                newY2 = snappedEnd.Y;
                            }
                            
                            selectedLine.X1 = newX1;
                            selectedLine.Y1 = newY1;
                            selectedLine.X2 = newX2;
                            selectedLine.Y2 = newY2;
                        }
                        else
                        {
                            Canvas.SetLeft(selectedElement, newSelectedLeft);
                            Canvas.SetTop(selectedElement, newSelectedTop);
                        }
                        
                        // Вызываем обработчик перемещения элемента (для прикрепления к пулам)
                        if (OnElementMoved != null)
                        {
                            Point elementPosition = new Point(newSelectedLeft, newSelectedTop);
                            OnElementMoved(selectedElement, elementPosition);
                        }
                    }
                }

                // Вызываем обработчик перемещения элемента (для прикрепления к пулам)
                if (OnElementMoved != null)
                {
                    Point elementPosition = new Point(newLeft, newTop);
                    OnElementMoved(element, elementPosition);
                }
            }
        }

        // Обработчик завершения перетаскивания элемента на Canvas
        public void CanvasShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging && draggedElement != null)
            {
                // Применяем привязку к сетке при завершении для всех выделенных элементов
                if (isSnapToGridEnabled && SnapToGrid != null)
                {
                    // Привязка для перетаскиваемого элемента
                    if (draggedElement is Line line)
                    {
                        Point snappedStart = SnapToGrid(new Point(line.X1, line.Y1));
                        Point snappedEnd = SnapToGrid(new Point(line.X2, line.Y2));
                        line.X1 = snappedStart.X;
                        line.Y1 = snappedStart.Y;
                        line.X2 = snappedEnd.X;
                        line.Y2 = snappedEnd.Y;
                    }
                    else
                    {
                        double currentLeft = Canvas.GetLeft(draggedElement);
                        double currentTop = Canvas.GetTop(draggedElement);

                        if (!double.IsNaN(currentLeft) && !double.IsNaN(currentTop))
                        {
                            Point snappedPosition = SnapToGrid(new Point(currentLeft, currentTop));
                            Canvas.SetLeft(draggedElement, snappedPosition.X);
                            Canvas.SetTop(draggedElement, snappedPosition.Y);
                        }
                    }
                    
                    // Привязка для остальных выделенных элементов
                    if (GetSelectedElements != null && selectedElementsStartPositions.Count > 0)
                    {
                        var selectedElements = GetSelectedElements();
                        foreach (var selectedElement in selectedElements)
                        {
                            if (selectedElement != draggedElement)
                            {
                                if (selectedElement is Line selectedLine)
                                {
                                    Point snappedStart = SnapToGrid(new Point(selectedLine.X1, selectedLine.Y1));
                                    Point snappedEnd = SnapToGrid(new Point(selectedLine.X2, selectedLine.Y2));
                                    selectedLine.X1 = snappedStart.X;
                                    selectedLine.Y1 = snappedStart.Y;
                                    selectedLine.X2 = snappedEnd.X;
                                    selectedLine.Y2 = snappedEnd.Y;
                                }
                                else
                                {
                                    double currentLeft = Canvas.GetLeft(selectedElement);
                                    double currentTop = Canvas.GetTop(selectedElement);

                                    if (!double.IsNaN(currentLeft) && !double.IsNaN(currentTop))
                                    {
                                        Point snappedPosition = SnapToGrid(new Point(currentLeft, currentTop));
                                        Canvas.SetLeft(selectedElement, snappedPosition.X);
                                        Canvas.SetTop(selectedElement, snappedPosition.Y);
                                    }
                                }
                            }
                        }
                    }
                }

                isDragging = false;
                draggedElement.ReleaseMouseCapture();

                // Фиксируем трансформацию для ромба
                if (draggedElement is Border border && border.RenderTransform is RotateTransform rotateTransform)
                {
                    rotateTransform.Angle = 45;
                }

                // Возвращаем нормальный ZIndex
                Canvas.SetZIndex(draggedElement, 1);

                // Возвращаем исходный вид для всех выделенных элементов
                ApplyCanvasDragVisualFeedback(draggedElement, false);
                if (GetSelectedElements != null)
                {
                    var selectedElements = GetSelectedElements();
                    foreach (var selectedElement in selectedElements)
                    {
                        if (selectedElement != draggedElement)
                        {
                            ApplyCanvasDragVisualFeedback(selectedElement, false);
                        }
                    }
                }

                // Показываем ручки изменения размера для всех выделенных элементов
                if (GetSelectedElements != null && ShowResizeHandlesForElement != null)
                {
                    var selectedElements = GetSelectedElements();
                    foreach (var selectedElement in selectedElements)
                    {
                        ShowResizeHandlesForElement(selectedElement);
                    }
                }

                selectedElementsStartPositions.Clear();
                draggedElement = null;
                e.Handled = true;
            }
        }

        // Применение визуального обратного отклика при перетаскивании элемента на холсте
        private void ApplyCanvasDragVisualFeedback(UIElement element, bool isDragging)
        {
            if (isDragging)
            {
                element.Opacity = 0.7;
                if (element is Ellipse ellipse)
                {
                    ellipse.StrokeThickness += 1;
                }
                else if (element is Border border)
                {
                    border.BorderThickness = new Thickness(3);
                }
                else if (element is Line line)
                {
                    line.StrokeThickness += 1;
                }
            }
            else
            {
                element.Opacity = 1.0;
                if (element is Ellipse ellipse)
                {
                    if (ellipse.Fill == Brushes.Transparent)
                    {
                        ellipse.Stroke = Brushes.White;
                    }
                    else
                    {
                        ellipse.Stroke = Brushes.Black;
                    }
                    ellipse.StrokeThickness = 3;
                }
                else if (element is Border border)
                {
                    border.BorderThickness = new Thickness(2);
                }
                else if (element is Line line)
                {
                    if (line.StrokeThickness > 1)
                    {
                        line.StrokeThickness -= 1;
                    }
                }
            }
        }

        // Создание визуальной копии фигуры для перетаскивания
        private UIElement CreateShapeCopy(UIElement original)
        {
            if (original == null) return null;

            // Проверяем имя элемента для определения типа
            var elementName = (original as FrameworkElement)?.Name;

            // Создаем копии в зависимости от типа
            if (elementName == "Circle" || original is Ellipse && elementName != "SimpleRing" && elementName != "Ellipse")
            {
                var copy = new Ellipse
                {
                    Width = 110,
                    Height = 110,
                    Fill = Brushes.White,
                    Stroke = Brushes.White,
                    StrokeThickness = 3,
                    Cursor = Cursors.Hand
                };
                // Используем FillStrokeManager для сохранения оригинальных стилей, если он доступен
                if (fillStrokeManager != null)
                {
                    fillStrokeManager.SetOriginalStylesFromCopy(copy, original);
                }
                else
                {
                    // Fallback на делегат для обратной совместимости
                    SetOriginalStyles?.Invoke(copy, original);
                }
                return copy;
            }
            else if (elementName == "UmlInitialNode")
            {
                var border = new Border
                {
                    Width = 110,
                    Height = 110,
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(3),
                    CornerRadius = new CornerRadius(55),
                    Cursor = Cursors.Hand
                };

                var circle = new Ellipse
                {
                    Width = 80,
                    Height = 80,
                    Fill = Brushes.White,
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                border.Child = circle;
                // Используем FillStrokeManager для сохранения оригинальных стилей, если он доступен
                if (fillStrokeManager != null)
                {
                    fillStrokeManager.SetOriginalStylesFromCopy(border, original);
                }
                else
                {
                    // Fallback на делегат для обратной совместимости
                    SetOriginalStyles?.Invoke(border, original);
                }
                return border;
            }
            else if (elementName == "SimpleRing")
            {
                var copy = new Ellipse
                {
                    Width = 110,
                    Height = 110,
                    Stroke = Brushes.White,
                    StrokeThickness = 3,
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                // Используем FillStrokeManager для сохранения оригинальных стилей, если он доступен
                if (fillStrokeManager != null)
                {
                    fillStrokeManager.SetOriginalStylesFromCopy(copy, original);
                }
                else
                {
                    // Fallback на делегат для обратной совместимости
                    SetOriginalStyles?.Invoke(copy, original);
                }
                return copy;
            }
            else if (elementName == "Ellipse")
            {
                var copy = new Ellipse
                {
                    Width = 130,
                    Height = 80,
                    Stroke = Brushes.White,
                    StrokeThickness = 3,
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                // Используем FillStrokeManager для сохранения оригинальных стилей, если он доступен
                if (fillStrokeManager != null)
                {
                    fillStrokeManager.SetOriginalStylesFromCopy(copy, original);
                }
                else
                {
                    // Fallback на делегат для обратной совместимости
                    SetOriginalStyles?.Invoke(copy, original);
                }
                return copy;
            }
            else if (elementName == "Square")
            {
                // Получаем оригинальный цвет заливки
                Brush originalBackground = Brushes.Transparent;
                if (original is Border originalBorder)
                {
                    originalBackground = originalBorder.Background ?? Brushes.Transparent;
                }
                
                var copy = new Border
                {
                    Width = 100,
                    Height = 100,
                    Background = originalBackground,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(3),
                    CornerRadius = new CornerRadius(0),
                    Cursor = Cursors.Hand
                };
                // Используем FillStrokeManager для сохранения оригинальных стилей, если он доступен
                if (fillStrokeManager != null)
                {
                    fillStrokeManager.SetOriginalStylesFromCopy(copy, original);
                }
                else
                {
                    // Fallback на делегат для обратной совместимости
                    SetOriginalStyles?.Invoke(copy, original);
                }
                return copy;
            }
            else if (elementName == "ResizableSquare")
            {
                // Получаем оригинальный цвет заливки
                Brush originalBackground = Brushes.Transparent;
                if (original is Border originalBorder)
                {
                    originalBackground = originalBorder.Background ?? Brushes.Transparent;
                }
                
                var copy = new Border
                {
                    Width = 70,
                    Height = 71,
                    Background = originalBackground,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(3),
                    CornerRadius = new CornerRadius(0),
                    Cursor = Cursors.Hand,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                copy.RenderTransform = new RotateTransform(45);
                // Используем FillStrokeManager для сохранения оригинальных стилей, если он доступен
                if (fillStrokeManager != null)
                {
                    fillStrokeManager.SetOriginalStylesFromCopy(copy, original);
                }
                else
                {
                    // Fallback на делегат для обратной совместимости
                    SetOriginalStyles?.Invoke(copy, original);
                }
                return copy;
            }
            else if (elementName == "Rectangle")
            {
                // Получаем оригинальный цвет заливки
                Brush originalBackground = Brushes.Transparent;
                if (original is Border originalBorder)
                {
                    originalBackground = originalBorder.Background ?? Brushes.Transparent;
                }
                
                var copy = new Border
                {
                    Width = 150,
                    Height = 80,
                    Background = originalBackground,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(3),
                    CornerRadius = new CornerRadius(0),
                    Cursor = Cursors.Hand
                };
                // Используем FillStrokeManager для сохранения оригинальных стилей, если он доступен
                if (fillStrokeManager != null)
                {
                    fillStrokeManager.SetOriginalStylesFromCopy(copy, original);
                }
                else
                {
                    // Fallback на делегат для обратной совместимости
                    SetOriginalStyles?.Invoke(copy, original);
                }
                return copy;
            }
            else if (elementName == "stick")
            {
                // Получаем оригинальный цвет заливки
                Brush originalBackground = Brushes.White;
                if (original is Border originalBorder)
                {
                    originalBackground = originalBorder.Background ?? Brushes.White;
                }
                
                var copy = new Border
                {
                    Width = 200,
                    Height = 30,
                    Background = originalBackground,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(3),
                    CornerRadius = new CornerRadius(0),
                    Cursor = Cursors.Hand
                };
                // Используем FillStrokeManager для сохранения оригинальных стилей, если он доступен
                if (fillStrokeManager != null)
                {
                    fillStrokeManager.SetOriginalStylesFromCopy(copy, original);
                }
                else
                {
                    // Fallback на делегат для обратной совместимости
                    SetOriginalStyles?.Invoke(copy, original);
                }
                return copy;
            }
            else if (elementName == "SuperEllipse")
            {
                // Получаем оригинальный цвет заливки
                Brush originalBackground = Brushes.Transparent;
                if (original is Border originalBorder)
                {
                    originalBackground = originalBorder.Background ?? Brushes.Transparent;
                }
                
                var copy = new Border
                {
                    Width = 120,
                    Height = 80,
                    Background = originalBackground,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(3),
                    CornerRadius = new CornerRadius(10),
                    Cursor = Cursors.Hand
                };
                // Используем FillStrokeManager для сохранения оригинальных стилей, если он доступен
                if (fillStrokeManager != null)
                {
                    fillStrokeManager.SetOriginalStylesFromCopy(copy, original);
                }
                else
                {
                    // Fallback на делегат для обратной совместимости
                    SetOriginalStyles?.Invoke(copy, original);
                }
                return copy;
            }

            return null;
        }

        #endregion

        #region Вспомогательные методы

        public void StopDragging()
        {
            if (isDragging && draggedElement != null)
            {
                isDragging = false;
                draggedElement.ReleaseMouseCapture();
                draggedElement = null;
            }
        }

        public bool GetIsDraggingState()
        {
            return isDragging;
        }

        #endregion
    }
}