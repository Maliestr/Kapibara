using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Graphic_Editor
{
    /// <summary>
    /// Менеджер для управления масштабированием Canvas
    /// </summary>
    public class ZoomManager
    {
        private Canvas _canvas;
        private CanvasPanningManager _panningManager;
        private ScaleTransform _scaleTransform;
        private MatrixTransform _matrixTransform;
        private TransformGroup _transformGroup;
        
        private double _currentZoom = 1.0;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;
        private const double ZoomStep = 0.1;
        private const double ZoomWheelStep = 0.05;

        /// <summary>
        /// Текущий уровень масштабирования (1.0 = 100%)
        /// </summary>
        public double CurrentZoom
        {
            get => _currentZoom;
            private set
            {
                _currentZoom = Math.Max(MinZoom, Math.Min(MaxZoom, value));
                OnZoomChanged?.Invoke(_currentZoom);
            }
        }

        /// <summary>
        /// Событие изменения масштаба
        /// </summary>
        public event Action<double> OnZoomChanged;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="canvas">Canvas для управления</param>
        /// <param name="panningManager">Менеджер панорамирования для совместной работы</param>
        public ZoomManager(Canvas canvas, CanvasPanningManager panningManager)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _panningManager = panningManager ?? throw new ArgumentNullException(nameof(panningManager));
            
            InitializeTransform();
            AttachEventHandlers();
        }

        /// <summary>
        /// Инициализация трансформации для масштабирования
        /// </summary>
        private void InitializeTransform()
        {
            // Создаем ScaleTransform для масштабирования
            _scaleTransform = new ScaleTransform(1.0, 1.0);
            
            // Получаем существующую TransformGroup из Canvas или создаем новую
            if (_canvas.RenderTransform is TransformGroup existingGroup)
            {
                _transformGroup = existingGroup;
                
                // Ищем существующий MatrixTransform от панорамирования
                _matrixTransform = null;
                foreach (var transform in _transformGroup.Children)
                {
                    if (transform is MatrixTransform mt)
                    {
                        _matrixTransform = mt;
                        break;
                    }
                }
                
                // Если не нашли MatrixTransform, создаем новый
                if (_matrixTransform == null)
                {
                    _matrixTransform = new MatrixTransform(Matrix.Identity);
                    _transformGroup.Children.Add(_matrixTransform);
                }
                
                // Добавляем ScaleTransform в начало (масштаб применяется первым)
                _transformGroup.Children.Insert(0, _scaleTransform);
            }
            else
            {
                // Создаем новую TransformGroup
                _transformGroup = new TransformGroup();
                _transformGroup.Children.Add(_scaleTransform);
                
                // Добавляем MatrixTransform для панорамирования
                _matrixTransform = new MatrixTransform(Matrix.Identity);
                _transformGroup.Children.Add(_matrixTransform);
                
                _canvas.RenderTransform = _transformGroup;
            }
        }

        /// <summary>
        /// Прикрепление обработчиков событий
        /// </summary>
        private void AttachEventHandlers()
        {
            _canvas.PreviewMouseWheel += Canvas_PreviewMouseWheel;
        }

        /// <summary>
        /// Открепление обработчиков событий
        /// </summary>
        private void DetachEventHandlers()
        {
            _canvas.PreviewMouseWheel -= Canvas_PreviewMouseWheel;
        }

        /// <summary>
        /// Обработчик колесика мыши для масштабирования
        /// </summary>
        private void Canvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Масштабируем только при зажатом Ctrl
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Получаем позицию мыши относительно Canvas
                Point mousePosition = e.GetPosition(_canvas);
                
                // Определяем направление масштабирования
                double zoomFactor = e.Delta > 0 ? (1 + ZoomWheelStep) : (1 - ZoomWheelStep);
                
                // Выполняем масштабирование относительно позиции мыши
                ZoomAtPoint(mousePosition, zoomFactor);
                
                e.Handled = true;
            }
        }

        /// <summary>
        /// Масштабирование относительно определенной точки
        /// </summary>
        /// <param name="point">Точка относительно Canvas, относительно которой масштабируем</param>
        /// <param name="zoomFactor">Коэффициент масштабирования</param>
        private void ZoomAtPoint(Point point, double zoomFactor)
        {
            double newZoom = CurrentZoom * zoomFactor;
            
            // Ограничиваем масштаб
            newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));
            
            if (Math.Abs(newZoom - CurrentZoom) < 0.001)
                return;

            // Получаем текущее смещение панорамирования
            Point panningOffset = _panningManager.GetPanningOffset();
            
            // Вычисляем новое смещение для сохранения точки под курсором
            // Формула: newOffset = oldOffset + point * (oldZoom - newZoom)
            // Это сохраняет точку point на том же месте на экране после масштабирования
            double oldZoom = CurrentZoom;
            double offsetX = panningOffset.X + point.X * (oldZoom - newZoom);
            double offsetY = panningOffset.Y + point.Y * (oldZoom - newZoom);
            
            // Применяем новый масштаб
            CurrentZoom = newZoom;
            _scaleTransform.ScaleX = CurrentZoom;
            _scaleTransform.ScaleY = CurrentZoom;
            
            // Обновляем смещение панорамирования с учетом нового масштаба
            _panningManager.SetPanningOffset(new Point(offsetX, offsetY));
        }

        /// <summary>
        /// Увеличение масштаба
        /// </summary>
        public void ZoomIn()
        {
            // Масштабируем относительно центра видимой области
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            Point centerPoint;
            
            if (parent != null)
            {
                centerPoint = new Point(parent.ActualWidth / 2, parent.ActualHeight / 2);
            }
            else
            {
                centerPoint = new Point(_canvas.ActualWidth / 2, _canvas.ActualHeight / 2);
            }
            
            ZoomAtPoint(centerPoint, 1 + ZoomStep);
        }

        /// <summary>
        /// Уменьшение масштаба
        /// </summary>
        public void ZoomOut()
        {
            // Масштабируем относительно центра видимой области
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            Point centerPoint;
            
            if (parent != null)
            {
                centerPoint = new Point(parent.ActualWidth / 2, parent.ActualHeight / 2);
            }
            else
            {
                centerPoint = new Point(_canvas.ActualWidth / 2, _canvas.ActualHeight / 2);
            }
            
            ZoomAtPoint(centerPoint, 1 - ZoomStep);
        }

        /// <summary>
        /// Сброс масштаба до 100%
        /// </summary>
        public void ResetZoom()
        {
            // Сохраняем текущее смещение панорамирования
            Point panningOffset = _panningManager.GetPanningOffset();
            
            // Сбрасываем масштаб
            CurrentZoom = 1.0;
            _scaleTransform.ScaleX = 1.0;
            _scaleTransform.ScaleY = 1.0;
            
            // Корректируем смещение панорамирования
            // При уменьшении масштаба нужно скорректировать offset
            _panningManager.SetPanningOffset(panningOffset);
        }

        /// <summary>
        /// Установка конкретного уровня масштаба
        /// </summary>
        /// <param name="zoom">Уровень масштаба (1.0 = 100%)</param>
        public void SetZoom(double zoom)
        {
            if (zoom < MinZoom || zoom > MaxZoom)
                return;

            // Масштабируем относительно центра
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            Point centerPoint;
            
            if (parent != null)
            {
                centerPoint = new Point(parent.ActualWidth / 2, parent.ActualHeight / 2);
            }
            else
            {
                centerPoint = new Point(_canvas.ActualWidth / 2, _canvas.ActualHeight / 2);
            }
            
            double zoomFactor = zoom / CurrentZoom;
            ZoomAtPoint(centerPoint, zoomFactor);
        }

        /// <summary>
        /// Получение процента масштаба для отображения
        /// </summary>
        /// <returns>Процент масштаба (например, 100 для 100%)</returns>
        public int GetZoomPercentage()
        {
            return (int)Math.Round(CurrentZoom * 100);
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            DetachEventHandlers();
            _canvas = null;
            _panningManager = null;
        }
    }
}

