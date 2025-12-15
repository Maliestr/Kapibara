using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Graphic_Editor
{
    /// <summary>
    /// Менеджер для управления масштабированием Canvas
    /// </summary>
    public class ZoomManager
    {
        private Canvas _canvas;
        private ScaleTransform _scaleTransform;
        private const double MinZoom = 0.1;  // Минимальный масштаб (10%)
        private const double MaxZoom = 5.0;   // Максимальный масштаб (500%)
        private const double ZoomStep = 0.1;  // Шаг изменения масштаба (10%)
        private double _currentZoom = 1.0;    // Текущий масштаб (100%)
        private bool _isAnimating = false;    // Флаг анимации

        /// <summary>
        /// Текущий уровень масштабирования
        /// </summary>
        public double CurrentZoom
        {
            get => _currentZoom;
            private set
            {
                _currentZoom = Math.Max(MinZoom, Math.Min(MaxZoom, value));
            }
        }

        /// <summary>
        /// Событие изменения масштаба
        /// </summary>
        public event Action<double> ZoomChanged;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="canvas">Canvas для управления масштабированием</param>
        public ZoomManager(Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            
            InitializeTransform();
        }

        /// <summary>
        /// Инициализация трансформации масштабирования
        /// </summary>
        private void InitializeTransform()
        {
            _scaleTransform = new ScaleTransform(1.0, 1.0);
            
            // Устанавливаем точку привязки масштабирования в центр Canvas
            _canvas.RenderTransformOrigin = new Point(0.5, 0.5);

            // Проверяем, есть ли уже RenderTransform
            if (_canvas.RenderTransform == null || !(_canvas.RenderTransform is TransformGroup))
            {
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(_scaleTransform);
                _canvas.RenderTransform = transformGroup;
            }
            else if (_canvas.RenderTransform is TransformGroup tg)
            {
                // Проверяем, есть ли уже ScaleTransform в группе
                bool hasScaleTransform = false;
                foreach (Transform transform in tg.Children)
                {
                    if (transform is ScaleTransform)
                    {
                        _scaleTransform = transform as ScaleTransform;
                        hasScaleTransform = true;
                        break;
                    }
                }

                // Если нет ScaleTransform, добавляем его в начало (масштабирование должно быть первым)
                if (!hasScaleTransform)
                {
                    tg.Children.Insert(0, _scaleTransform);
                }
            }
            else
            {
                // Если есть другая трансформация, создаем группу
                var existingTransform = _canvas.RenderTransform;
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(existingTransform);
                transformGroup.Children.Add(_scaleTransform);
                _canvas.RenderTransform = transformGroup;
            }
        }

        /// <summary>
        /// Получить центр видимой области
        /// </summary>
        private Point GetViewportCenter()
        {
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            if (parent != null)
            {
                // Центр видимой области в координатах родителя
                double centerX = parent.ActualWidth / 2.0;
                double centerY = parent.ActualHeight / 2.0;
                
                // Преобразуем в координаты Canvas
                // Учитываем текущее смещение и масштаб
                MatrixTransform matrixTransform = null;
                if (_canvas.RenderTransform is TransformGroup tg)
                {
                    foreach (Transform transform in tg.Children)
                    {
                        if (transform is MatrixTransform mt)
                        {
                            matrixTransform = mt;
                            break;
                        }
                    }
                }
                
                if (matrixTransform != null)
                {
                    Matrix matrix = matrixTransform.Matrix;
                    // Обратное преобразование: из координат экрана в координаты Canvas
                    double canvasX = (centerX - matrix.OffsetX) / _currentZoom;
                    double canvasY = (centerY - matrix.OffsetY) / _currentZoom;
                    return new Point(canvasX, canvasY);
                }
            }
            
            // Если нет родителя, возвращаем центр Canvas
            return new Point(_canvas.ActualWidth / 2.0, _canvas.ActualHeight / 2.0);
        }

        /// <summary>
        /// Увеличить масштаб
        /// </summary>
        /// <param name="animate">Применить плавную анимацию</param>
        public void ZoomIn(bool animate = true)
        {
            Point center = GetViewportCenter();
            ZoomAtPoint(_currentZoom + ZoomStep, center, animate);
        }

        /// <summary>
        /// Уменьшить масштаб
        /// </summary>
        /// <param name="animate">Применить плавную анимацию</param>
        public void ZoomOut(bool animate = true)
        {
            Point center = GetViewportCenter();
            ZoomAtPoint(_currentZoom - ZoomStep, center, animate);
        }

        /// <summary>
        /// Сбросить масштаб до 100%
        /// </summary>
        /// <param name="animate">Применить плавную анимацию</param>
        public void ResetZoom(bool animate = true)
        {
            Point center = GetViewportCenter();
            ZoomAtPoint(1.0, center, animate);
        }

        /// <summary>
        /// Установить масштаб
        /// </summary>
        /// <param name="zoom">Уровень масштабирования (1.0 = 100%)</param>
        /// <param name="animate">Применить плавную анимацию</param>
        public void SetZoom(double zoom, bool animate = false)
        {
            Point center = GetViewportCenter();
            ZoomAtPoint(zoom, center, animate);
        }

        /// <summary>
        /// Плавная анимация масштабирования
        /// </summary>
        private void AnimateZoom(double targetZoom)
        {
            if (_scaleTransform == null) return;

            _isAnimating = true;
            double fromZoom = _currentZoom;
            Point centerPoint = GetViewportCenter();

            // Вычисляем и применяем финальное смещение сразу
            MatrixTransform matrixTransform = null;
            
            if (_canvas.RenderTransform is TransformGroup tg)
            {
                foreach (Transform transform in tg.Children)
                {
                    if (transform is MatrixTransform mt)
                    {
                        matrixTransform = mt;
                        break;
                    }
                }
            }

            if (matrixTransform != null)
            {
                Matrix matrix = matrixTransform.Matrix;
                double offsetX = matrix.OffsetX;
                double offsetY = matrix.OffsetY;

                FrameworkElement parent = _canvas.Parent as FrameworkElement;
                
                if (parent != null)
                {
                    // Получаем размеры Canvas и viewport
                    double canvasWidth = _canvas.Width > 0 ? _canvas.Width : _canvas.ActualWidth;
                    double canvasHeight = _canvas.Height > 0 ? _canvas.Height : _canvas.ActualHeight;
                    double viewportWidth = parent.ActualWidth;
                    double viewportHeight = parent.ActualHeight;
                    
                    // Вычисляем центр Canvas и viewport
                    double canvasCenterX = canvasWidth / 2.0;
                    double canvasCenterY = canvasHeight / 2.0;
                    double viewportCenterX = viewportWidth / 2.0;
                    double viewportCenterY = viewportHeight / 2.0;
                    
                    // Преобразуем координаты точки Canvas в координаты viewport (до масштабирования)
                    double viewportX = (centerPoint.X - canvasCenterX) * fromZoom + viewportCenterX + offsetX;
                    double viewportY = (centerPoint.Y - canvasCenterY) * fromZoom + viewportCenterY + offsetY;
                    
                    // Вычисляем новое смещение для сохранения точки под курсором
                    double newOffsetX = viewportX - viewportCenterX - (centerPoint.X - canvasCenterX) * targetZoom;
                    double newOffsetY = viewportY - viewportCenterY - (centerPoint.Y - canvasCenterY) * targetZoom;
                    
                    matrix.OffsetX = newOffsetX;
                    matrix.OffsetY = newOffsetY;
                    matrixTransform.Matrix = matrix;
                }
            }

            var animationX = new DoubleAnimation
            {
                From = fromZoom,
                To = targetZoom,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var animationY = new DoubleAnimation
            {
                From = fromZoom,
                To = targetZoom,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animationX.Completed += (s, e) =>
            {
                _isAnimating = false;
                CurrentZoom = targetZoom;
                ZoomChanged?.Invoke(_currentZoom);
            };

            _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animationX);
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animationY);
        }

        /// <summary>
        /// Масштабирование относительно точки
        /// </summary>
        /// <param name="zoom">Уровень масштабирования</param>
        /// <param name="centerPoint">Точка, относительно которой происходит масштабирование (координаты относительно Canvas)</param>
        /// <param name="animate">Применить плавную анимацию</param>
        public void ZoomAtPoint(double zoom, Point centerPoint, bool animate = false)
        {
            if (_scaleTransform == null) return;

            // Вычисляем новое смещение для центрирования масштабирования
            double oldZoom = _currentZoom;
            double targetZoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            
            if (Math.Abs(oldZoom - targetZoom) < 0.001) return; // Нет изменений
            
            double zoomFactor = targetZoom / oldZoom;
            
            // Получаем текущее смещение из панорамирования (если есть)
            MatrixTransform matrixTransform = null;
            
            if (_canvas.RenderTransform is TransformGroup tg)
            {
                foreach (Transform transform in tg.Children)
                {
                    if (transform is MatrixTransform mt)
                    {
                        matrixTransform = mt;
                        break;
                    }
                }
            }

            if (matrixTransform != null)
            {
                Matrix matrix = matrixTransform.Matrix;
                double offsetX = matrix.OffsetX;
                double offsetY = matrix.OffsetY;

                // Получаем родительский контейнер для правильного расчета координат
                FrameworkElement parent = _canvas.Parent as FrameworkElement;
                
                if (parent != null)
                {
                    // Получаем размеры Canvas и viewport
                    double canvasWidth = _canvas.Width > 0 ? _canvas.Width : _canvas.ActualWidth;
                    double canvasHeight = _canvas.Height > 0 ? _canvas.Height : _canvas.ActualHeight;
                    double viewportWidth = parent.ActualWidth;
                    double viewportHeight = parent.ActualHeight;
                    
                    // Вычисляем центр Canvas и viewport
                    double canvasCenterX = canvasWidth / 2.0;
                    double canvasCenterY = canvasHeight / 2.0;
                    double viewportCenterX = viewportWidth / 2.0;
                    double viewportCenterY = viewportHeight / 2.0;
                    
                    // Преобразуем координаты точки Canvas в координаты viewport (до масштабирования)
                    // Формула: viewportPoint = (canvasPoint - canvasCenter) * zoom + viewportCenter + offset
                    double viewportX = (centerPoint.X - canvasCenterX) * oldZoom + viewportCenterX + offsetX;
                    double viewportY = (centerPoint.Y - canvasCenterY) * oldZoom + viewportCenterY + offsetY;
                    
                    // Теперь вычисляем новое смещение так, чтобы после масштабирования
                    // эта же точка в viewport соответствовала той же точке на Canvas
                    // Обратная формула: offset = viewportPoint - viewportCenter - (canvasPoint - canvasCenter) * newZoom
                    double newOffsetX = viewportX - viewportCenterX - (centerPoint.X - canvasCenterX) * targetZoom;
                    double newOffsetY = viewportY - viewportCenterY - (centerPoint.Y - canvasCenterY) * targetZoom;
                    
                    matrix.OffsetX = newOffsetX;
                    matrix.OffsetY = newOffsetY;
                }
                else
                {
                    // Альтернативный расчет, если нет родителя
                    // Используем более простую формулу для центрирования
                    double newOffsetX = centerPoint.X * (1 - zoomFactor) + offsetX * zoomFactor;
                    double newOffsetY = centerPoint.Y * (1 - zoomFactor) + offsetY * zoomFactor;
                    matrix.OffsetX = newOffsetX;
                    matrix.OffsetY = newOffsetY;
                }
                
                matrixTransform.Matrix = matrix;
            }

            // Обновляем ScaleTransform
            if (animate && !_isAnimating)
            {
                // Для анимации нужно обновить смещение сразу, а масштаб анимировать
                CurrentZoom = targetZoom;
                AnimateZoom(targetZoom);
            }
            else
            {
                CurrentZoom = targetZoom;
                _scaleTransform.ScaleX = _currentZoom;
                _scaleTransform.ScaleY = _currentZoom;
                ZoomChanged?.Invoke(_currentZoom);
            }
        }

        /// <summary>
        /// Масштабирование колесиком мыши (как в draw.io - относительно позиции курсора)
        /// </summary>
        /// <param name="delta">Изменение колесика (положительное - увеличение, отрицательное - уменьшение)</param>
        /// <param name="mousePosition">Позиция мыши относительно Canvas</param>
        public void ZoomWithMouseWheel(int delta, Point mousePosition)
        {
            // Получаем позицию мыши относительно родительского контейнера (viewport)
            // Это важно для правильного масштабирования относительно курсора
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            Point viewportPoint;
            
            if (parent != null)
            {
                // Получаем позицию мыши относительно viewport
                viewportPoint = Mouse.GetPosition(parent);
            }
            else
            {
                // Fallback на координаты Canvas
                viewportPoint = mousePosition;
            }
            
            // Преобразуем координаты viewport в координаты Canvas для масштабирования
            Point canvasPoint = ViewportToCanvasPoint(viewportPoint);
            
            if (delta > 0)
            {
                ZoomAtPoint(_currentZoom + ZoomStep, canvasPoint);
            }
            else if (delta < 0)
            {
                ZoomAtPoint(_currentZoom - ZoomStep, canvasPoint);
            }
        }
        
        /// <summary>
        /// Преобразование координат viewport в координаты Canvas
        /// </summary>
        private Point ViewportToCanvasPoint(Point viewportPoint)
        {
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            if (parent == null)
            {
                return viewportPoint;
            }
            
            // Получаем текущее смещение из панорамирования
            MatrixTransform matrixTransform = null;
            if (_canvas.RenderTransform is TransformGroup tg)
            {
                foreach (Transform transform in tg.Children)
                {
                    if (transform is MatrixTransform mt)
                    {
                        matrixTransform = mt;
                        break;
                    }
                }
            }
            
            if (matrixTransform != null)
            {
                Matrix matrix = matrixTransform.Matrix;
                double offsetX = matrix.OffsetX;
                double offsetY = matrix.OffsetY;
                
                // Получаем размеры Canvas и viewport
                double canvasWidth = _canvas.Width > 0 ? _canvas.Width : _canvas.ActualWidth;
                double canvasHeight = _canvas.Height > 0 ? _canvas.Height : _canvas.ActualHeight;
                double viewportWidth = parent.ActualWidth;
                double viewportHeight = parent.ActualHeight;
                
                // Вычисляем центр Canvas и viewport
                double canvasCenterX = canvasWidth / 2.0;
                double canvasCenterY = canvasHeight / 2.0;
                double viewportCenterX = viewportWidth / 2.0;
                double viewportCenterY = viewportHeight / 2.0;
                
                // Преобразуем координаты viewport в координаты Canvas
                // Обратная формула: canvasPoint = (viewportPoint - viewportCenter - offset) / zoom + canvasCenter
                double canvasX = (viewportPoint.X - viewportCenterX - offsetX) / _currentZoom + canvasCenterX;
                double canvasY = (viewportPoint.Y - viewportCenterY - offsetY) / _currentZoom + canvasCenterY;
                
                return new Point(canvasX, canvasY);
            }
            
            // Если нет трансформации, просто возвращаем координаты
            return viewportPoint;
        }
    }
}

