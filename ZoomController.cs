using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Graphic_Editor
{
    public class ZoomController
    {
        private Canvas canvas;
        private TransformGroup transformGroup;
        private ScaleTransform scaleTransform;
        private TranslateTransform translateTransform;
        private double currentZoom = 1.0;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;
        private const double ZoomStep = 0.1;
        private const double DefaultZoom = 1.0;

        public double CurrentZoom
        {
            get { return currentZoom; }
        }

        public ZoomController(Canvas canvas)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            this.canvas = canvas;
            InitializeTransform();
        }

        /// Инициализирует трансформации для масштабирования
        private void InitializeTransform()
        {
            // Создаем группу трансформаций
            transformGroup = new TransformGroup();
            scaleTransform = new ScaleTransform(currentZoom, currentZoom);
            translateTransform = new TranslateTransform(0, 0);

            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);

            // Применяем трансформацию к Canvas
            canvas.RenderTransform = transformGroup;
            canvas.RenderTransformOrigin = new Point(0, 0);
        }

        /// Увеличивает масштаб
        public void ZoomIn()
        {
            SetZoom(currentZoom + ZoomStep);
        }

        /// Уменьшает масштаб
        public void ZoomOut()
        {
            SetZoom(currentZoom - ZoomStep);
        }

        /// Сбрасывает масштаб до значения по умолчанию
        public void ResetZoom()
        {
            SetZoom(DefaultZoom);
        }

        /// Устанавливает масштаб с проверкой границ
        public void SetZoom(double zoom)
        {
            // Ограничиваем масштаб минимальным и максимальным значениями
            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));

            if (Math.Abs(currentZoom - zoom) < 0.001)
                return; // Масштаб не изменился

            double oldZoom = currentZoom;
            currentZoom = zoom;

            // Обновляем ScaleTransform
            if (scaleTransform != null)
            {
                scaleTransform.ScaleX = currentZoom;
                scaleTransform.ScaleY = currentZoom;
            }

            // Вызываем событие изменения масштаба
            ZoomChanged?.Invoke(currentZoom, oldZoom);
        }

        /// Устанавливает масштаб с центрированием на указанной точке
        public void SetZoomToPoint(double zoom, Point centerPoint)
        {
            if (canvas == null) return;

            // Получаем текущую позицию центра относительно Canvas
            Point canvasPoint = centerPoint;

            // Сохраняем старый масштаб
            double oldZoom = currentZoom;

            // Устанавливаем новый масштаб
            SetZoom(zoom);

            // Корректируем смещение для сохранения позиции центра
            if (oldZoom > 0 && currentZoom > 0)
            {
                double scaleFactor = currentZoom / oldZoom;
                
                // Вычисляем новое смещение для сохранения позиции центра
                double deltaX = canvasPoint.X * (1 - scaleFactor);
                double deltaY = canvasPoint.Y * (1 - scaleFactor);

                if (translateTransform != null)
                {
                    translateTransform.X += deltaX;
                    translateTransform.Y += deltaY;
                }
            }
        }

        /// Увеличивает масштаб с центрированием на указанной точке
        public void ZoomInToPoint(Point centerPoint)
        {
            SetZoomToPoint(currentZoom + ZoomStep, centerPoint);
        }

        /// Уменьшает масштаб с центрированием на указанной точке
        public void ZoomOutToPoint(Point centerPoint)
        {
            SetZoomToPoint(currentZoom - ZoomStep, centerPoint);
        }

        /// Масштабирует относительно центра Canvas
        public void ZoomInToCenter()
        {
            if (canvas != null)
            {
                Point center = new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2);
                ZoomInToPoint(center);
            }
            else
            {
                ZoomIn();
            }
        }

        /// Уменьшает масштаб относительно центра Canvas
        public void ZoomOutToCenter()
        {
            if (canvas != null)
            {
                Point center = new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2);
                ZoomOutToPoint(center);
            }
            else
            {
                ZoomOut();
            }
        }

        /// Устанавливает смещение (для панорамирования)
        public void SetOffset(double x, double y)
        {
            if (translateTransform != null)
            {
                translateTransform.X = x;
                translateTransform.Y = y;
            }
        }

        /// Получает текущее смещение
        public Point GetOffset()
        {
            if (translateTransform != null)
            {
                return new Point(translateTransform.X, translateTransform.Y);
            }
            return new Point(0, 0);
        }

        /// Сбрасывает смещение
        public void ResetOffset()
        {
            SetOffset(0, 0);
        }

        /// Полностью сбрасывает масштаб и смещение
        public void Reset()
        {
            ResetZoom();
            ResetOffset();
        }

        /// Преобразует координаты точки с экрана в координаты Canvas с учетом масштаба
        public Point ScreenToCanvas(Point screenPoint)
        {
            if (canvas == null || scaleTransform == null || translateTransform == null)
                return screenPoint;

            // Учитываем масштаб и смещение
            Point canvasPoint = new Point(
                (screenPoint.X - translateTransform.X) / scaleTransform.ScaleX,
                (screenPoint.Y - translateTransform.Y) / scaleTransform.ScaleY
            );

            return canvasPoint;
        }

        /// Преобразует координаты точки из Canvas в координаты экрана с учетом масштаба
        public Point CanvasToScreen(Point canvasPoint)
        {
            if (canvas == null || scaleTransform == null || translateTransform == null)
                return canvasPoint;

            // Учитываем масштаб и смещение
            Point screenPoint = new Point(
                canvasPoint.X * scaleTransform.ScaleX + translateTransform.X,
                canvasPoint.Y * scaleTransform.ScaleY + translateTransform.Y
            );

            return screenPoint;
        }

        /// Событие изменения масштаба
        public event Action<double, double> ZoomChanged;
    }
}

