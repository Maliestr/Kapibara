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
                    Point screenPoint = new Point(
                        centerPoint.X * fromZoom + offsetX,
                        centerPoint.Y * fromZoom + offsetY
                    );
                    
                    double newOffsetX = screenPoint.X - centerPoint.X * targetZoom;
                    double newOffsetY = screenPoint.Y - centerPoint.Y * targetZoom;
                    
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
                    // Преобразуем координаты точки мыши из координат Canvas в координаты родителя
                    // centerPoint - это координаты относительно Canvas (до трансформаций)
                    // Нужно найти, где эта точка находится на экране с учетом текущих трансформаций
                    Point screenPoint = new Point(
                        centerPoint.X * oldZoom + offsetX,
                        centerPoint.Y * oldZoom + offsetY
                    );
                    
                    // Теперь вычисляем новое смещение так, чтобы после масштабирования
                    // эта же точка на экране соответствовала той же точке на Canvas
                    double newOffsetX = screenPoint.X - centerPoint.X * targetZoom;
                    double newOffsetY = screenPoint.Y - centerPoint.Y * targetZoom;
                    
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
        /// Масштабирование колесиком мыши
        /// </summary>
        /// <param name="delta">Изменение колесика (положительное - увеличение, отрицательное - уменьшение)</param>
        /// <param name="mousePosition">Позиция мыши относительно Canvas</param>
        public void ZoomWithMouseWheel(int delta, Point mousePosition)
        {
            if (delta > 0)
            {
                ZoomAtPoint(_currentZoom + ZoomStep, mousePosition);
            }
            else if (delta < 0)
            {
                ZoomAtPoint(_currentZoom - ZoomStep, mousePosition);
            }
        }
    }
}

