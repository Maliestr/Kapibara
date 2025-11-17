using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    public class ResizableElement
    {
        private UIElement element;
        private Canvas canvas;
        private Rectangle[] resizeHandles;
        private bool isResizing = false;
        private ResizeDirection resizeDirection;
        private Point startPoint;
        private double startWidth, startHeight, startLeft, startTop;

        private enum ResizeDirection
        {
            None, Top, Right, Bottom, Left,
            TopLeft, TopRight, BottomLeft, BottomRight
        }

        public ResizableElement(UIElement element, Canvas canvas)
        {
            this.element = element;
            this.canvas = canvas;
            InitializeResizeHandles();
        }

        private void InitializeResizeHandles()
        {
            resizeHandles = new Rectangle[8];

            // Создаем ручки для всех сторон и углов
            for (int i = 0; i < 8; i++)
            {
                resizeHandles[i] = new Rectangle
                {
                    Fill = Brushes.Blue,
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    Width = 8,
                    Height = 8,
                    Visibility = Visibility.Collapsed,
                    Cursor = GetCursorForDirection((ResizeDirection)i)
                };

                resizeHandles[i].MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;
                resizeHandles[i].MouseLeftButtonUp += ResizeHandle_MouseLeftButtonUp;
                resizeHandles[i].MouseMove += ResizeHandle_MouseMove;

                canvas.Children.Add(resizeHandles[i]);
            }
        }

        private Cursor GetCursorForDirection(ResizeDirection direction)
        {
            switch (direction)
            {
                case ResizeDirection.Top:
                    return Cursors.SizeNS;
                case ResizeDirection.Bottom:
                    return Cursors.SizeNS;
                case ResizeDirection.Left:
                    return Cursors.SizeWE;
                case ResizeDirection.Right:
                    return Cursors.SizeWE;
                case ResizeDirection.TopLeft:
                    return Cursors.SizeNWSE;
                case ResizeDirection.BottomRight:
                    return Cursors.SizeNWSE;
                case ResizeDirection.TopRight:
                    return Cursors.SizeNESW;
                case ResizeDirection.BottomLeft:
                    return Cursors.SizeNESW;
                default:
                    return Cursors.Arrow;
            }
        }

        public void ShowResizeHandles()
        {
            if (element == null) return;

            UpdateResizeHandlesPosition();

            foreach (var handle in resizeHandles)
            {
                handle.Visibility = Visibility.Visible;
                Canvas.SetZIndex(handle, 1001);
            }
        }

        public void HideResizeHandles()
        {
            if (resizeHandles == null) return;

            foreach (var handle in resizeHandles)
            {
                if (handle != null)
                    handle.Visibility = Visibility.Collapsed;
            }
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

            // Угловые ручки
            PositionHandle(resizeHandles[0], left - 4, top - 4, ResizeDirection.TopLeft);
            PositionHandle(resizeHandles[1], left + width / 2 - 4, top - 4, ResizeDirection.Top);
            PositionHandle(resizeHandles[2], left + width - 4, top - 4, ResizeDirection.TopRight);
            PositionHandle(resizeHandles[3], left + width - 4, top + height / 2 - 4, ResizeDirection.Right);
            PositionHandle(resizeHandles[4], left + width - 4, top + height - 4, ResizeDirection.BottomRight);
            PositionHandle(resizeHandles[5], left + width / 2 - 4, top + height - 4, ResizeDirection.Bottom);
            PositionHandle(resizeHandles[6], left - 4, top + height - 4, ResizeDirection.BottomLeft);
            PositionHandle(resizeHandles[7], left - 4, top + height / 2 - 4, ResizeDirection.Left);
        }

        private void PositionHandle(Rectangle handle, double left, double top, ResizeDirection direction)
        {
            Canvas.SetLeft(handle, left);
            Canvas.SetTop(handle, top);
            handle.Tag = direction;
        }

        private double GetElementWidth()
        {
            if (element is FrameworkElement fe)
            {
                if (fe.ActualWidth > 0)
                    return fe.ActualWidth;
                if (!double.IsNaN(fe.Width))
                    return fe.Width;
            }
            return 50; // Значение по умолчанию
        }

        private double GetElementHeight()
        {
            if (element is FrameworkElement fe)
            {
                if (fe.ActualHeight > 0)
                    return fe.ActualHeight;
                if (!double.IsNaN(fe.Height))
                    return fe.Height;
            }
            return 50; // Значение по умолчанию
        }

        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle handle)
            {
                isResizing = true;
                resizeDirection = (ResizeDirection)handle.Tag;
                startPoint = e.GetPosition(canvas);

                startWidth = GetElementWidth();
                startHeight = GetElementHeight();
                startLeft = Canvas.GetLeft(element);
                startTop = Canvas.GetTop(element);

                if (double.IsNaN(startLeft)) startLeft = 0;
                if (double.IsNaN(startTop)) startTop = 0;

                handle.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizing && e.LeftButton == MouseButtonState.Pressed && sender is Rectangle)
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
            if (sender is Rectangle handle)
            {
                handle.ReleaseMouseCapture();
            }
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
                    newLeft = startLeft + deltaX;
                    break;

                case ResizeDirection.Top:
                    newHeight = Math.Max(minSize, startHeight - deltaY);
                    newTop = startTop + deltaY;
                    break;

                case ResizeDirection.TopRight:
                    newWidth = Math.Max(minSize, startWidth + deltaX);
                    newHeight = Math.Max(minSize, startHeight - deltaY);
                    newTop = startTop + deltaY;
                    break;

                case ResizeDirection.BottomRight:
                    newWidth = Math.Max(minSize, startWidth + deltaX);
                    newHeight = Math.Max(minSize, startHeight + deltaY);
                    break;

                case ResizeDirection.TopLeft:
                    newWidth = Math.Max(minSize, startWidth - deltaX);
                    newHeight = Math.Max(minSize, startHeight - deltaY);
                    newLeft = startLeft + deltaX;
                    newTop = startTop + deltaY;
                    break;

                case ResizeDirection.BottomLeft:
                    newWidth = Math.Max(minSize, startWidth - deltaX);
                    newHeight = Math.Max(minSize, startHeight + deltaY);
                    newLeft = startLeft + deltaX;
                    break;
            }

            ApplyNewSize(newWidth, newHeight, newLeft, newTop);
        }

        private void ApplyNewSize(double width, double height, double left, double top)
        {
            if (element is FrameworkElement frameworkElement)
            {
                frameworkElement.Width = width;
                frameworkElement.Height = height;
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
            if (resizeHandles != null)
            {
                foreach (var handle in resizeHandles)
                {
                    if (handle != null && canvas.Children.Contains(handle))
                    {
                        canvas.Children.Remove(handle);
                    }
                }
            }
        }
    }
}