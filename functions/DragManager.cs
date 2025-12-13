using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    public class DragManager
    {
        // Состояние перетаскивания
        private bool isDragging = false;
        private UIElement draggedElement;
        private Point offset;
        private UIElement shapeCopy;
        private Transform originalTransform;
        private Point dragStartPoint;

        // Константы для ограничения перемещения
        private const double WorkAreaTop = 40;
        private const double WorkAreaBottom = 100;

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
        public Action<UIElement, UIElement> SetOriginalStyles { get; set; }
        public Action<UIElement> AnimateShapeAppearance { get; set; }
        public Func<UIElement, bool> IsShapeFromNotation { get; set; }
        public Func<UIElement, string> GetShapeType { get; set; }

        // Добавляем ссылку на ResizableElement
        public Func<UIElement, bool> GetIsDragging { get; set; }

        // Canvas для работы
        private Canvas mainCanvas;
        private Canvas notationCanvas;

        // Дополнительные свойства
        private bool isSnapToGridEnabled = false;

        public bool IsDragging => isDragging;
        public UIElement DraggedElement => draggedElement;

        // Конструктор
        public DragManager(Canvas mainCanvas, Canvas notationCanvas)
        {
            this.mainCanvas = mainCanvas;
            this.notationCanvas = notationCanvas;
        }

        // Установка состояния привязки к сетке
        public void SetSnapToGridEnabled(bool enabled)
        {
            isSnapToGridEnabled = enabled;
        }

        // Делает фигуры в панели инструментов перетаскиваемыми
        public void MakeAllShapesDraggable(params UIElement[] shapes)
        {
            foreach (var element in shapes)
            {
                AddDragHandlers(element);
            }
        }

        // Добавляет обработчики перетаскивания
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

        // Обработчик начала перетаскивания из панели инструментов
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

        // Обработчик перемещения при перетаскивании из панели
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

        // Обработчик завершения перетаскивания из панели
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

        // Обработчик начала перетаскивания элемента на Canvas
        public void CanvasShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                // Выделяем элемент
                SelectElement?.Invoke(element);
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

                // Поднимаем элемент наверх
                Canvas.SetZIndex(element, 1000);

                // Визуальный фидбэк
                ApplyCanvasDragVisualFeedback(element, true);
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
                    double deltaX = newLeft - lineLeft;
                    double deltaY = newTop - lineTop;

                    double newX1 = currentX1 + deltaX;
                    double newY1 = currentY1 + deltaY;
                    double newX2 = currentX2 + deltaX;
                    double newY2 = currentY2 + deltaY;

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
            }
        }

        // Обработчик завершения перетаскивания элемента на Canvas
        public void CanvasShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging && draggedElement != null)
            {
                // Применяем привязку к сетке при завершении
                if (isSnapToGridEnabled && SnapToGrid != null)
                {
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

                // Возвращаем исходный вид
                ApplyCanvasDragVisualFeedback(draggedElement, false);

                draggedElement = null;
                e.Handled = true;
            }
        }

        // Визуальный фидбэк при перетаскивании
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

        // Создание копии фигуры
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
                SetOriginalStyles?.Invoke(copy, original);
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
                SetOriginalStyles?.Invoke(border, original);
                return border;
            }
            else if (elementName == "SimpleRing")
            {
                var copy = new Ellipse
                {
                    Width = 110,
                    Height = 110,
                    Stroke = Brushes.White,
                    StrokeThickness = 5,
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                SetOriginalStyles?.Invoke(copy, original);
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
                SetOriginalStyles?.Invoke(copy, original);
                return copy;
            }
            else if (elementName == "Square")
            {
                var copy = new Border
                {
                    Width = 100,
                    Height = 100,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(0),
                    Cursor = Cursors.Hand
                };
                SetOriginalStyles?.Invoke(copy, original);
                return copy;
            }
            else if (elementName == "ResizableSquare")
            {
                var copy = new Border
                {
                    Width = 70,
                    Height = 71,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(0),
                    Cursor = Cursors.Hand,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                copy.RenderTransform = new RotateTransform(45);
                SetOriginalStyles?.Invoke(copy, original);
                return copy;
            }
            else if (elementName == "Rectangle")
            {
                var copy = new Border
                {
                    Width = 150,
                    Height = 80,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(0),
                    Cursor = Cursors.Hand
                };
                SetOriginalStyles?.Invoke(copy, original);
                return copy;
            }
            else if (elementName == "stick")
            {
                var copy = new Border
                {
                    Width = 200,
                    Height = 30,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(0),
                    Cursor = Cursors.Hand
                };
                SetOriginalStyles?.Invoke(copy, original);
                return copy;
            }
            else if (elementName == "SuperEllipse")
            {
                var copy = new Border
                {
                    Width = 120,
                    Height = 80,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(10),
                    Cursor = Cursors.Hand
                };
                SetOriginalStyles?.Invoke(copy, original);
                return copy;
            }

            return null;
        }

        // Остановка перетаскивания
        public void StopDragging()
        {
            if (isDragging && draggedElement != null)
            {
                isDragging = false;
                draggedElement.ReleaseMouseCapture();
                draggedElement = null;
            }
        }

        // Проверка состояния перетаскивания (для ShowResizeHandlesForElement)
        public bool GetIsDraggingState()
        {
            return isDragging;
        }
    }
}