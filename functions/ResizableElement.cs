using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Diagnostics;

namespace Graphic_Editor
{
    public class ResizableElement
    {
        private UIElement element;
        private Canvas canvas;
        private Rectangle[] cornerHandles;
        private Line[] edgeLines;
        private bool isResizing = false;
        private ResizeDirection resizeDirection;
        private Point startPoint;
        private double startWidth, startHeight, startLeft, startTop;
        private FrameworkElement frameworkElement;
        private bool handlesVisible;
        private double startChildWidth, startChildHeight; // Начальные размеры внутреннего элемента
        private Func<Point, Point> snapToGridFunction; // Функция привязки к сетке
        private bool isSnapToGridEnabled; // Флаг включения привязки к сетке

        private enum ResizeDirection
        {
            None,
            Top,
            Right,
            Bottom,
            Left,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        public ResizableElement(UIElement element, Canvas canvas, Func<Point, Point> snapToGridFunction = null, bool isSnapToGridEnabled = false)
        {
            this.element = element;
            this.canvas = canvas;
            this.snapToGridFunction = snapToGridFunction;
            this.isSnapToGridEnabled = isSnapToGridEnabled;
            frameworkElement = element as FrameworkElement;
            AttachElementEvents();
            InitializeResizeHandles();
        }
        
        // Метод для обновления функции привязки к сетке
        public void UpdateSnapToGrid(Func<Point, Point> snapToGridFunction, bool isSnapToGridEnabled)
        {
            this.snapToGridFunction = snapToGridFunction;
            this.isSnapToGridEnabled = isSnapToGridEnabled;
        }

        private void AttachElementEvents()
        {
            if (frameworkElement != null)
            {
                frameworkElement.SizeChanged += Element_SizeChanged;
                frameworkElement.LayoutUpdated += Element_LayoutUpdated;
            }
        }

        private void Element_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            OnElementLayoutChanged();
        }

        private void Element_LayoutUpdated(object sender, EventArgs e)
        {
            OnElementLayoutChanged();
        }

        private void OnElementLayoutChanged()
        {
            if (handlesVisible && !isResizing)
            {
                UpdateResizeHandlesPosition();
            }
        }

        private void InitializeResizeHandles()
        {
            cornerHandles = new Rectangle[4];
            edgeLines = new Line[4];

            // Угловые маркеры
            for (int i = 0; i < 4; i++)
            {
                cornerHandles[i] = new Rectangle
                {
                    Fill = Brushes.Blue,
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    Width = 8,
                    Height = 8,
                    Visibility = Visibility.Collapsed
                };

                cornerHandles[i].MouseLeftButtonDown += CornerHandle_MouseLeftButtonDown;
                cornerHandles[i].MouseLeftButtonUp += ResizeHandle_MouseLeftButtonUp;
                cornerHandles[i].MouseMove += ResizeHandle_MouseMove;

                // Добавляем обработчик для обновления курсора при наведении
                cornerHandles[i].MouseEnter += Handle_MouseEnter;
                cornerHandles[i].MouseLeave += Handle_MouseLeave;

                canvas.Children.Add(cornerHandles[i]);
            }

            // Боковые линии
            for (int i = 0; i < 4; i++)
            {
                edgeLines[i] = new Line
                {
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 6,
                    Visibility = Visibility.Collapsed
                };

                edgeLines[i].MouseLeftButtonDown += EdgeLine_MouseLeftButtonDown;
                edgeLines[i].MouseLeftButtonUp += ResizeHandle_MouseLeftButtonUp;
                edgeLines[i].MouseMove += ResizeHandle_MouseMove;

                // Добавляем обработчик для обновления курсора при наведении
                edgeLines[i].MouseEnter += Handle_MouseEnter;
                edgeLines[i].MouseLeave += Handle_MouseLeave;

                canvas.Children.Add(edgeLines[i]);
            }
        }

        private void Handle_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement resizeElement && resizeElement.Tag is ResizeDirection direction)
            {
                UpdateCursorForDirection(direction);
            }
        }

        // Обработчик выхода мыши с элемента - ДОБАВЛЕНО
        private void Handle_MouseLeave(object sender, MouseEventArgs e)
        {
            ResetCursor();
        }

        private void UpdateCursorForDirection(ResizeDirection direction)
        {
            Cursor cursor = Cursors.Arrow;

            switch (direction)
            {
                case ResizeDirection.TopLeft:
                case ResizeDirection.BottomRight:
                    cursor = Cursors.SizeNWSE;
                    break;
                case ResizeDirection.TopRight:
                case ResizeDirection.BottomLeft:
                    cursor = Cursors.SizeNESW;
                    break;
                case ResizeDirection.Top:
                case ResizeDirection.Bottom:
                    cursor = Cursors.SizeNS;
                    break;
                case ResizeDirection.Left:
                case ResizeDirection.Right:
                    cursor = Cursors.SizeWE;
                    break;
            }

            // Принудительно устанавливаем курсор для всего приложения
            Mouse.OverrideCursor = cursor;
        }

        private void ResetCursor()
        {
            // Сбрасываем OverrideCursor только если не происходит изменение размера
            if (!isResizing)
            {
                Mouse.OverrideCursor = null;
            }
        }

        public void ShowResizeHandles()
        {
            if (element == null) return;

            UpdateResizeHandlesPosition();
            handlesVisible = true;

            foreach (var handle in cornerHandles)
            {
                handle.Visibility = Visibility.Visible;
                Canvas.SetZIndex(handle, 1001);
            }

            foreach (var line in edgeLines)
            {
                line.Visibility = Visibility.Visible;
                Canvas.SetZIndex(line, 1001);
            }
        }

        public void HideResizeHandles()
        {
            handlesVisible = false;
            if (cornerHandles != null)
            {
                foreach (var handle in cornerHandles)
                {
                    if (handle != null)
                        handle.Visibility = Visibility.Collapsed;
                }
            }

            if (edgeLines != null)
            {
                foreach (var line in edgeLines)
                {
                    if (line != null)
                        line.Visibility = Visibility.Collapsed;
                }
            }

            // Сбрасываем курсор при скрытии ручек
            ResetCursor();
        }

        private void UpdateResizeHandlesPosition()
        {
            if (element == null) return;

            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            double width = GetElementWidth();
            double height = GetElementHeight();

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            // Угловые маркеры
            PositionCornerHandle(cornerHandles[0], left - 4, top - 4, ResizeDirection.TopLeft);
            PositionCornerHandle(cornerHandles[1], left + width - 4, top - 4, ResizeDirection.TopRight);
            PositionCornerHandle(cornerHandles[2], left + width - 4, top + height - 4, ResizeDirection.BottomRight);
            PositionCornerHandle(cornerHandles[3], left - 4, top + height - 4, ResizeDirection.BottomLeft);

            // Боковые линии
            PositionEdgeLine(edgeLines[0],
                left, top - 3,
                left + width, top - 3,
                ResizeDirection.Top);

            PositionEdgeLine(edgeLines[1],
                left + width + 3, top,
                left + width + 3, top + height,
                ResizeDirection.Right);

            PositionEdgeLine(edgeLines[2],
                left, top + height + 3,
                left + width, top + height + 3,
                ResizeDirection.Bottom);

            PositionEdgeLine(edgeLines[3],
                left - 3, top,
                left - 3, top + height,
                ResizeDirection.Left);
        }

        private void PositionCornerHandle(Rectangle handle, double left, double top, ResizeDirection direction)
        {
            Canvas.SetLeft(handle, left);
            Canvas.SetTop(handle, top);
            handle.Tag = direction;
        }

        private void PositionEdgeLine(Line line, double x1, double y1, double x2, double y2, ResizeDirection direction)
        {
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
            line.Tag = direction;
        }

        private double GetElementWidth()
        {
            if (element is FrameworkElement fe)
            {
                if (fe.ActualWidth > 0)
                    return fe.ActualWidth;
                if (fe.RenderSize.Width > 0)
                    return fe.RenderSize.Width;
                if (!double.IsNaN(fe.Width))
                    return fe.Width;
            }
            return 50;
        }

        private double GetElementHeight()
        {
            if (element is FrameworkElement fe)
            {
                if (fe.ActualHeight > 0)
                    return fe.ActualHeight;
                if (fe.RenderSize.Height > 0)
                    return fe.RenderSize.Height;
                if (!double.IsNaN(fe.Height))
                    return fe.Height;
            }
            return 50;
        }

        private void CornerHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StartResizing(sender, e);
        }

        private void EdgeLine_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StartResizing(sender, e);
        }

        private void StartResizing(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement resizeElement && e is MouseButtonEventArgs mouseArgs)
            {
                isResizing = true;
                resizeDirection = (ResizeDirection)resizeElement.Tag;
                startPoint = mouseArgs.GetPosition(canvas);

                startWidth = GetElementWidth();
                startHeight = GetElementHeight();
                startLeft = Canvas.GetLeft(element);
                startTop = Canvas.GetTop(element);

                if (double.IsNaN(startLeft)) startLeft = 0;
                if (double.IsNaN(startTop)) startTop = 0;

                // Сохраняем начальные размеры внутреннего элемента (если есть)
                if (element is Border border && border.Child is FrameworkElement child)
                {
                    startChildWidth = child.Width;
                    startChildHeight = child.Height;
                    if (double.IsNaN(startChildWidth)) startChildWidth = child.ActualWidth;
                    if (double.IsNaN(startChildHeight)) startChildHeight = child.ActualHeight;
                }
                else
                {
                    startChildWidth = 0;
                    startChildHeight = 0;
                }

                resizeElement.CaptureMouse();
                e.Handled = true;

                Debug.WriteLine($"Start resizing: {resizeDirection}");
            }
        }

        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizing && e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement)
            {
                Point currentPoint = e.GetPosition(canvas);
                double deltaX = currentPoint.X - startPoint.X;
                double deltaY = currentPoint.Y - startPoint.Y;

                ResizeElement(deltaX, deltaY);
                UpdateResizeHandlesPosition();
            }
            
        }

        private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isResizing = false;
            if (sender is FrameworkElement resizeElement)
            {
                resizeElement.ReleaseMouseCapture();
            }
            ResetCursor();
        }

        private void ResizeElement(double deltaX, double deltaY)
        {
            double newWidth = startWidth;
            double newHeight = startHeight;
            double newLeft = startLeft;
            double newTop = startTop;

            const double minSize = 20;

            switch (resizeDirection)
            {
                case ResizeDirection.Right:
                    newWidth = Math.Max(minSize, startWidth + deltaX);
                    break;

                case ResizeDirection.Bottom:
                    newHeight = Math.Max(minSize, startHeight + deltaY);
                    break;

                case ResizeDirection.Left:
                    newWidth = Math.Max(minSize, startWidth - deltaX);
                    // Сохраняем правую сторону на месте: newLeft + newWidth = startLeft + startWidth
                    newLeft = startLeft + startWidth - newWidth;
                    break;

                case ResizeDirection.Top:
                    newHeight = Math.Max(minSize, startHeight - deltaY);
                    // Сохраняем нижнюю сторону на месте: newTop + newHeight = startTop + startHeight
                    newTop = startTop + startHeight - newHeight;
                    break;

                case ResizeDirection.TopRight:
                    newWidth = Math.Max(minSize, startWidth + deltaX);
                    newHeight = Math.Max(minSize, startHeight - deltaY);
                    // Сохраняем нижнюю левую точку на месте: newTop + newHeight = startTop + startHeight
                    newTop = startTop + startHeight - newHeight;
                    break;

                case ResizeDirection.BottomRight:
                    newWidth = Math.Max(minSize, startWidth + deltaX);
                    newHeight = Math.Max(minSize, startHeight + deltaY);
                    break;

                case ResizeDirection.TopLeft:
                    newWidth = Math.Max(minSize, startWidth - deltaX);
                    newHeight = Math.Max(minSize, startHeight - deltaY);
                    // Сохраняем правую нижнюю точку на месте
                    newLeft = startLeft + startWidth - newWidth;
                    newTop = startTop + startHeight - newHeight;
                    break;

                case ResizeDirection.BottomLeft:
                    newWidth = Math.Max(minSize, startWidth - deltaX);
                    newHeight = Math.Max(minSize, startHeight + deltaY);
                    // Сохраняем правую верхнюю точку на месте
                    newLeft = startLeft + startWidth - newWidth;
                    break;
            }

            // Проверяем, чтобы элемент не вышел за допустимые границы
            if (newTop < 0)
            {
                newHeight += newTop;
                newTop = 0;
            }

            if (newLeft < 0)
            {
                newWidth += newLeft;
                newLeft = 0;
            }

            // Гарантируем минимальный размер
            newWidth = Math.Max(minSize, newWidth);
            newHeight = Math.Max(minSize, newHeight);
            
            // Применяем привязку к сетке если включена
            if (isSnapToGridEnabled && snapToGridFunction != null)
            {
                // Привязываем позицию к сетке
                Point snappedPosition = snapToGridFunction(new Point(newLeft, newTop));
                newLeft = snappedPosition.X;
                newTop = snappedPosition.Y;
                
                // Привязываем размеры к сетке (округление до ближайшего размера сетки)
                // Используем ту же логику, что и для позиции
                Point snappedSize = snapToGridFunction(new Point(newWidth, newHeight));
                newWidth = snappedSize.X;
                newHeight = snappedSize.Y;
                
                // Гарантируем минимальный размер после привязки
                newWidth = Math.Max(minSize, newWidth);
                newHeight = Math.Max(minSize, newHeight);
            }

            ApplyNewSize(newWidth, newHeight, newLeft, newTop);
        }

        private void ApplyNewSize(double width, double height, double left, double top)
        {
            if (element is FrameworkElement frameworkElement)
            {
                // Для TextBlock устанавливаем размеры
                if (element is System.Windows.Controls.TextBlock textBlock)
                {
                    // TextBlock может не иметь фиксированных размеров, устанавливаем их явно
                    frameworkElement.Width = Math.Max(20, width);
                    frameworkElement.Height = Math.Max(20, height);
                    // Для TextBlock также можно использовать TextWrapping для лучшего отображения
                    if (textBlock.TextWrapping == TextWrapping.NoWrap)
                    {
                        textBlock.TextWrapping = TextWrapping.Wrap;
                    }
                }
                else if (element is Border border)
                {
                    // Для Border устанавливаем размеры
                    frameworkElement.Width = width;
                    frameworkElement.Height = height;
                    
                    // Масштабируем внутренний элемент пропорционально
                    if (border.Child is FrameworkElement child && startChildWidth > 0 && startChildHeight > 0 && startWidth > 0 && startHeight > 0)
                    {
                        double scaleX = width / startWidth;
                        double scaleY = height / startHeight;
                        
                        double newChildWidth = startChildWidth * scaleX;
                        double newChildHeight = startChildHeight * scaleY;
                        
                        child.Width = newChildWidth;
                        child.Height = newChildHeight;
                        
                        // Обновляем CornerRadius для Border, если это круг (UmlInitialNode)
                        if (border.CornerRadius.TopLeft > 0)
                        {
                            double newRadius = Math.Min(width, height) / 2;
                            border.CornerRadius = new CornerRadius(newRadius);
                        }
                    }
                }
                else
                {
                    frameworkElement.Width = width;
                    frameworkElement.Height = height;
                }
            }

            Canvas.SetLeft(element, left);
            Canvas.SetTop(element, top);

            UpdateDimensionTextboxes(width, height);
        }

        private void UpdateDimensionTextboxes(double width, double height)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.UpdateDimensions(width, height);
            }
        }

        public void Dispose()
        {
            handlesVisible = false;
            if (frameworkElement != null)
            {
                frameworkElement.SizeChanged -= Element_SizeChanged;
                frameworkElement.LayoutUpdated -= Element_LayoutUpdated;
            }

            if (cornerHandles != null)
            {
                foreach (var handle in cornerHandles)
                {
                    if (handle != null && canvas.Children.Contains(handle))
                    {
                        canvas.Children.Remove(handle);
                    }
                }
            }

            if (edgeLines != null)
            {
                foreach (var line in edgeLines)
                {
                    if (line != null && canvas.Children.Contains(line))
                    {
                        canvas.Children.Remove(line);
                    }
                }
            }

            ResetCursor();
        }
    }
}