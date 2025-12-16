using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Graphic_Editor
{
    // Менеджер для управления масштабированием Canvas
    public class ZoomManager
    {
        #region Поля

        private Canvas _canvas;
        private CanvasPanningManager _panningManager;
        private ScaleTransform _scaleTransform;
        private MatrixTransform _matrixTransform;
        private TransformGroup _transformGroup;
        
        private double _currentZoom = 1.0;

        #endregion

        #region Константы

        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;
        private const double ZoomStep = 0.1;
        private const double ZoomWheelStep = 0.05;

        #endregion

        #region Свойства

        // Текущий уровень масштабирования (1.0 = 100%)
        public double CurrentZoom
        {
            get => _currentZoom;
            private set
            {
                _currentZoom = Math.Max(MinZoom, Math.Min(MaxZoom, value));
                OnZoomChanged?.Invoke(_currentZoom);
            }
        }

        #endregion

        #region События

        public event Action<double> OnZoomChanged;

        #endregion

        #region Конструктор и инициализация

        public ZoomManager(Canvas canvas, CanvasPanningManager panningManager)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _panningManager = panningManager ?? throw new ArgumentNullException(nameof(panningManager));
            
            InitializeTransform();
            AttachEventHandlers();
        }

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

        private void AttachEventHandlers()
        {
            _canvas.PreviewMouseWheel += Canvas_PreviewMouseWheel;
        }

        private void DetachEventHandlers()
        {
            _canvas.PreviewMouseWheel -= Canvas_PreviewMouseWheel;
        }

        #endregion

        #region Обработка событий

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

        #endregion

        #region Масштабирование

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

        // Увеличение масштаба относительно центра видимой области
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

        // Уменьшение масштаба относительно центра видимой области
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

        // Сброс масштаба к 100%
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

        // Установка конкретного значения масштаба
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

        #endregion

        #region Публичные методы

        // Получение текущего масштаба в процентах
        public int GetZoomPercentage()
        {
            return (int)Math.Round(CurrentZoom * 100);
        }

        public void Dispose()
        {
            DetachEventHandlers();
            _canvas = null;
            _panningManager = null;
        }

        #endregion
    }
}

