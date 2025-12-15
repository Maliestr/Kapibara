using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Graphic_Editor
{
    /// <summary>
    /// Менеджер для управления перемещением (панорамированием) Canvas
    /// </summary>
    public class CanvasPanningManager
    {
        private Canvas _canvas;
        private bool _isPanning = false;
        private Point _lastMousePosition;
        private TranslateTransform _translateTransform;
        private MatrixTransform _matrixTransform;
        private Matrix _transformMatrix;
        private CacheMode _originalCanvasCacheMode;
        private bool _hasOriginalCacheMode = false;

        /// <summary>
        /// Тип панорамирования
        /// </summary>
        public enum PanningMode
        {
            MiddleButton,      // Средняя кнопка мыши (колесико)
            RightButton,       // Правая кнопка мыши
            CtrlRightButton    // Ctrl + Правая кнопка мыши
        }

        /// <summary>
        /// Текущий режим панорамирования
        /// </summary>
        public PanningMode Mode { get; set; } = PanningMode.MiddleButton;

        /// <summary>
        /// Включено ли панорамирование
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Делегат для получения текущего масштаба
        /// </summary>
        public Func<double> GetCurrentZoom { get; set; }

        /// <summary>
        /// Событие изменения позиции панорамирования
        /// </summary>
        public event Action<Point> PanningChanged;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="canvas">Canvas для управления</param>
        public CanvasPanningManager(Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            
            InitializeTransform();
            AttachEventHandlers();
        }


        /// <summary>
        /// Инициализация трансформации для перемещения
        /// </summary>
        private void InitializeTransform()
        {
            // Используем MatrixTransform для более гибкого управления
            _transformMatrix = Matrix.Identity;
            _matrixTransform = new MatrixTransform(_transformMatrix);

            // Проверяем, есть ли уже RenderTransform
            if (_canvas.RenderTransform == null || !(_canvas.RenderTransform is TransformGroup))
            {
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(_matrixTransform);
                _canvas.RenderTransform = transformGroup;
            }
            else if (_canvas.RenderTransform is TransformGroup tg)
            {
                tg.Children.Add(_matrixTransform);
            }
            else
            {
                // Если есть другая трансформация, создаем группу
                var existingTransform = _canvas.RenderTransform;
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(existingTransform);
                transformGroup.Children.Add(_matrixTransform);
                _canvas.RenderTransform = transformGroup;
            }
        }

        /// <summary>
        /// Прикрепление обработчиков событий Canvas
        /// </summary>
        private void AttachEventHandlers()
        {
            // Используем обычные события мыши
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseUp += Canvas_MouseUp;
            _canvas.MouseLeave += Canvas_MouseLeave;
        }


        /// <summary>
        /// Открепление обработчиков событий
        /// </summary>
        private void DetachEventHandlers()
        {
            _canvas.MouseDown -= Canvas_MouseDown;
            _canvas.MouseMove -= Canvas_MouseMove;
            _canvas.MouseUp -= Canvas_MouseUp;
            _canvas.MouseLeave -= Canvas_MouseLeave;
        }

        /// <summary>
        /// Проверка, должна ли начаться панорамирование
        /// </summary>
        private bool ShouldStartPanning(MouseButtonEventArgs e)
        {
            if (!IsEnabled || _isPanning) return false;

            switch (Mode)
            {
                case PanningMode.MiddleButton:
                    return e.ChangedButton == MouseButton.Middle;
                case PanningMode.RightButton:
                    return e.ChangedButton == MouseButton.Right && 
                           Keyboard.Modifiers == ModifierKeys.None;
                case PanningMode.CtrlRightButton:
                    return e.ChangedButton == MouseButton.Right && 
                           Keyboard.Modifiers == ModifierKeys.Control;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Проверка, должна ли прекратиться панорамирование
        /// </summary>
        private bool ShouldStopPanning(MouseButtonEventArgs e)
        {
            if (!_isPanning) return false;

            switch (Mode)
            {
                case PanningMode.MiddleButton:
                    return e.ChangedButton == MouseButton.Middle &&
                           e.MiddleButton == MouseButtonState.Released;
                case PanningMode.RightButton:
                case PanningMode.CtrlRightButton:
                    return e.ChangedButton == MouseButton.Right &&
                           e.RightButton == MouseButtonState.Released;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки мыши на Canvas (обычное событие)
        /// </summary>
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Если уже начали панорамирование, не обрабатываем снова
            if (_isPanning) return;
            
            if (ShouldStartPanning(e))
            {
                // Получаем позицию мыши относительно родителя сразу, чтобы избежать "прыжков"
                FrameworkElement parent = _canvas.Parent as FrameworkElement;
                Point mousePos = parent != null ? e.GetPosition(parent) : e.GetPosition(_canvas);
                StartPanning(mousePos);
                e.Handled = true; // Предотвращаем дальнейшую обработку другими менеджерами
            }
        }



        /// <summary>
        /// Проверка, должна ли продолжаться панорамирование
        /// </summary>
        private bool ShouldContinuePanning()
        {
            if (!_isPanning) return false;

            switch (Mode)
            {
                case PanningMode.MiddleButton:
                    return Mouse.MiddleButton == MouseButtonState.Pressed;
                case PanningMode.RightButton:
                    return Mouse.RightButton == MouseButtonState.Pressed &&
                           Keyboard.Modifiers == ModifierKeys.None;
                case PanningMode.CtrlRightButton:
                    return Mouse.RightButton == MouseButtonState.Pressed &&
                           Keyboard.Modifiers == ModifierKeys.Control;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Обработчик движения мыши на Canvas
        /// </summary>
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsEnabled || !_isPanning) return;

            // Проверяем, что кнопка мыши все еще нажата
            if (!ShouldContinuePanning())
            {
                StopPanning();
                return;
            }

            // Получаем позицию мыши относительно родителя для согласованности с StartPanning
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            Point currentPosition = parent != null ? Mouse.GetPosition(parent) : e.GetPosition(_canvas);
            UpdatePanning(currentPosition);
            e.Handled = true;
        }

        /// <summary>
        /// Обработчик отпускания кнопки мыши на Canvas
        /// </summary>
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ShouldStopPanning(e))
            {
                StopPanning();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Обработчик выхода мыши за пределы Canvas
        /// </summary>
        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                StopPanning();
            }
        }


        /// <summary>
        /// Начало панорамирования
        /// </summary>
        private void StartPanning(Point mousePosition)
        {
            if (_isPanning) return;

            _isPanning = true;
            
            // ВАЖНО: Синхронизируем _transformMatrix с реальным состоянием MatrixTransform
            // Это необходимо, так как ZoomManager может обновлять MatrixTransform напрямую
            // и _transformMatrix может содержать устаревшие значения
            if (_matrixTransform != null)
            {
                _transformMatrix = _matrixTransform.Matrix;
            }
            
            // Получаем актуальную позицию мыши СРАЗУ после синхронизации матрицы
            // Это гарантирует, что координаты синхронизированы с текущим состоянием Canvas
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            if (parent != null)
            {
                _lastMousePosition = Mouse.GetPosition(parent);
            }
            else
            {
                _lastMousePosition = mousePosition;
            }
            
            _canvas.Cursor = Cursors.Hand;
            
            // Оптимизация рендеринга: сохраняем оригинальный CacheMode и включаем BitmapCache
            _originalCanvasCacheMode = _canvas.CacheMode;
            _hasOriginalCacheMode = true;
            
            // Включаем BitmapCache для улучшения производительности при панорамировании
            var bitmapCache = new BitmapCache();
            bitmapCache.RenderAtScale = 1.0;
            bitmapCache.EnableClearType = false;
            bitmapCache.SnapsToDevicePixels = true;
            _canvas.CacheMode = bitmapCache;
            
            // Оптимизируем RenderOptions для Canvas
            RenderOptions.SetBitmapScalingMode(_canvas, BitmapScalingMode.LowQuality);
            RenderOptions.SetEdgeMode(_canvas, EdgeMode.Aliased);
            
            // Захватываем мышь на Canvas для стабильного отслеживания
            try
            {
                if (!_canvas.IsMouseCaptured)
                {
                    _canvas.CaptureMouse();
                }
            }
            catch
            {
                // Игнорируем ошибки захвата мыши
            }
        }

        /// <summary>
        /// Обновление позиции панорамирования
        /// </summary>
        private void UpdatePanning(Point currentMousePosition)
        {
            if (!_isPanning) return;

            // Получаем текущую позицию мыши относительно родительского контейнера
            // Это важно для стабильного отслеживания, так как Canvas может быть трансформирован
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            Point currentScreenPosition;
            
            if (parent != null)
            {
                // Используем Mouse.GetPosition для получения актуальной позиции мыши
                // Это гарантирует согласованность с StartPanning
                currentScreenPosition = Mouse.GetPosition(parent);
            }
            else
            {
                currentScreenPosition = currentMousePosition;
            }

            // Вычисляем смещение относительно предыдущей позиции (в пикселях экрана)
            double deltaX = currentScreenPosition.X - _lastMousePosition.X;
            double deltaY = currentScreenPosition.Y - _lastMousePosition.Y;

            // Игнорируем очень маленькие смещения для уменьшения дрожания
            if (Math.Abs(deltaX) < 0.1 && Math.Abs(deltaY) < 0.1)
            {
                return;
            }
            
            // Защита от резких "прыжков" при первом движении после начала панорамирования
            // Если смещение слишком большое (больше 20 пикселей), это может быть ошибка синхронизации
            // В этом случае обновляем _lastMousePosition без применения смещения
            // Это предотвращает "телепортацию" Canvas после масштабирования
            if (Math.Abs(deltaX) > 20 || Math.Abs(deltaY) > 20)
            {
                _lastMousePosition = currentScreenPosition;
                return;
            }

            // Получаем текущий масштаб (если доступен)
            double currentZoom = GetCurrentZoom?.Invoke() ?? 1.0;

            // В draw.io панорамирование работает так: при перемещении мыши на N пикселей
            // холст перемещается на те же N пикселей в экранных координатах, независимо от масштаба.
            // Это означает, что смещение применяется напрямую к offset, без деления на масштаб.
            // Смещение уже в экранных координатах, поэтому применяем его напрямую.
            double newOffsetX = _transformMatrix.OffsetX + deltaX;
            double newOffsetY = _transformMatrix.OffsetY + deltaY;

            // Ограничиваем смещение границами Canvas (с учетом масштаба)
            newOffsetX = ClampPanningOffsetX(newOffsetX, currentZoom);
            newOffsetY = ClampPanningOffsetY(newOffsetY, currentZoom);

            // Обновляем матрицу трансформации
            _transformMatrix.OffsetX = newOffsetX;
            _transformMatrix.OffsetY = newOffsetY;

            // Применяем трансформацию
            // ВАЖНО: Обновляем Matrix напрямую, чтобы сохранить другие компоненты матрицы (масштаб и т.д.)
            Matrix newMatrix = _matrixTransform.Matrix;
            newMatrix.OffsetX = newOffsetX;
            newMatrix.OffsetY = newOffsetY;
            _matrixTransform.Matrix = newMatrix;
            
            // Синхронизируем _transformMatrix с реальным состоянием
            _transformMatrix = newMatrix;

            // Вызываем событие
            PanningChanged?.Invoke(new Point(_transformMatrix.OffsetX, _transformMatrix.OffsetY));

            // Сохраняем текущую позицию для следующего обновления
            _lastMousePosition = currentScreenPosition;
        }

        /// <summary>
        /// Ограничение смещения по X границами Canvas
        /// </summary>
        private double ClampPanningOffsetX(double offsetX, double zoom = 1.0)
        {
            // Получаем размеры Canvas с учетом масштаба
            double canvasWidth = (_canvas.Width > 0 ? _canvas.Width : _canvas.ActualWidth) * zoom;

            // Получаем размеры видимой области (контейнера Canvas)
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            if (parent == null) return offsetX;

            double viewportWidth = parent.ActualWidth;

            // Если масштабированный Canvas меньше или равен видимой области, не позволяем перемещение
            if (canvasWidth <= viewportWidth || viewportWidth <= 0 || canvasWidth <= 0)
            {
                return 0;
            }

            // Canvas центрирован в контейнере
            // При центрировании: левая граница Canvas = (viewportWidth - canvasWidth) / 2
            // RenderTransform применяется относительно центра Canvas
            
            // Когда offsetX = 0, Canvas находится в центре
            // offsetX > 0 сдвигает Canvas вправо (левая граница увеличивается)
            // offsetX < 0 сдвигает Canvas влево (левая граница уменьшается)
            
            // Левая граница Canvas после трансформации: (viewportWidth - canvasWidth) / 2 + offsetX
            // Левая граница не должна быть меньше 0
            // (viewportWidth - canvasWidth) / 2 + offsetX >= 0
            // offsetX >= -(viewportWidth - canvasWidth) / 2
            // offsetX >= (canvasWidth - viewportWidth) / 2
            double minOffset = -(canvasWidth - viewportWidth) / 2;  // отрицательное значение

            // Правая граница Canvas после трансформации: (viewportWidth - canvasWidth) / 2 + offsetX + canvasWidth
            // Правая граница не должна быть больше viewportWidth
            // (viewportWidth - canvasWidth) / 2 + offsetX + canvasWidth <= viewportWidth
            // offsetX <= (viewportWidth - canvasWidth) / 2
            // offsetX <= -(canvasWidth - viewportWidth) / 2 - неправильно, пересчитаем:
            // (viewportWidth - canvasWidth) / 2 + offsetX + canvasWidth <= viewportWidth
            // offsetX <= viewportWidth - (viewportWidth - canvasWidth) / 2 - canvasWidth
            // offsetX <= (2*viewportWidth - viewportWidth + canvasWidth - 2*canvasWidth) / 2
            // offsetX <= (viewportWidth - canvasWidth) / 2 - неправильно!
            // Правильно: (viewportWidth - canvasWidth) / 2 + offsetX + canvasWidth <= viewportWidth
            // offsetX <= viewportWidth - (viewportWidth - canvasWidth) / 2 - canvasWidth
            // offsetX <= (2*viewportWidth - viewportWidth + canvasWidth - 2*canvasWidth) / 2
            // offsetX <= (viewportWidth - canvasWidth) / 2
            // Но это отрицательное значение. Давайте проще:
            // Чтобы правая граница была ровно viewportWidth:
            // (viewportWidth - canvasWidth) / 2 + offsetX + canvasWidth = viewportWidth
            // offsetX = viewportWidth - canvasWidth - (viewportWidth - canvasWidth) / 2
            // offsetX = (2*viewportWidth - 2*canvasWidth - viewportWidth + canvasWidth) / 2
            // offsetX = (viewportWidth - canvasWidth) / 2
            // А чтобы левая граница была 0:
            // (viewportWidth - canvasWidth) / 2 + offsetX = 0
            // offsetX = -(viewportWidth - canvasWidth) / 2 = (canvasWidth - viewportWidth) / 2
            double maxOffset = (canvasWidth - viewportWidth) / 2;  // положительное значение

            // minOffset (отрицательное) <= offsetX <= maxOffset (положительное)
            return Math.Max(minOffset, Math.Min(maxOffset, offsetX));
        }

        /// <summary>
        /// Ограничение смещения по Y границами Canvas
        /// </summary>
        private double ClampPanningOffsetY(double offsetY, double zoom = 1.0)
        {
            // Получаем размеры Canvas с учетом масштаба
            double canvasHeight = (_canvas.Height > 0 ? _canvas.Height : _canvas.ActualHeight) * zoom;

            // Получаем размеры видимой области (контейнера Canvas)
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            if (parent == null) return offsetY;

            double viewportHeight = parent.ActualHeight;

            // Если масштабированный Canvas меньше или равен видимой области, не позволяем перемещение
            if (canvasHeight <= viewportHeight || viewportHeight <= 0 || canvasHeight <= 0)
            {
                return 0;
            }

            // Canvas центрирован в контейнере
            // При центрировании: верхняя граница Canvas = (viewportHeight - canvasHeight) / 2
            // RenderTransform применяется относительно центра Canvas
            
            // Когда offsetY = 0, Canvas находится в центре
            // offsetY > 0 сдвигает Canvas вниз (верхняя граница увеличивается)
            // offsetY < 0 сдвигает Canvas вверх (верхняя граница уменьшается)
            
            // Верхняя граница Canvas после трансформации: (viewportHeight - canvasHeight) / 2 + offsetY
            // Верхняя граница не должна быть меньше 0
            // (viewportHeight - canvasHeight) / 2 + offsetY >= 0
            // offsetY >= -(viewportHeight - canvasHeight) / 2
            // offsetY >= (canvasHeight - viewportHeight) / 2 - неправильно!
            // Правильно: offsetY >= -(viewportHeight - canvasHeight) / 2 = (canvasHeight - viewportHeight) / 2
            // Но (canvasHeight - viewportHeight) / 2 положительное, а нам нужно отрицательное
            // offsetY >= -(viewportHeight - canvasHeight) / 2 = -(canvasHeight - viewportHeight) / 2
            double minOffset = -(canvasHeight - viewportHeight) / 2;  // отрицательное значение

            // Нижняя граница Canvas после трансформации: (viewportHeight - canvasHeight) / 2 + offsetY + canvasHeight
            // Нижняя граница не должна быть больше viewportHeight
            // (viewportHeight - canvasHeight) / 2 + offsetY + canvasHeight <= viewportHeight
            // offsetY <= viewportHeight - (viewportHeight - canvasHeight) / 2 - canvasHeight
            // offsetY <= (2*viewportHeight - viewportHeight + canvasHeight - 2*canvasHeight) / 2
            // offsetY <= (viewportHeight - canvasHeight) / 2 - это отрицательное
            // Чтобы верхняя граница была 0:
            // (viewportHeight - canvasHeight) / 2 + offsetY = 0
            // offsetY = -(viewportHeight - canvasHeight) / 2 = (canvasHeight - viewportHeight) / 2
            double maxOffset = (canvasHeight - viewportHeight) / 2;  // положительное значение

            // minOffset (отрицательное) <= offsetY <= maxOffset (положительное)
            return Math.Max(minOffset, Math.Min(maxOffset, offsetY));
        }

        /// <summary>
        /// Остановка панорамирования
        /// </summary>
        private void StopPanning()
        {
            if (!_isPanning) return;

            _isPanning = false;
            _canvas.Cursor = Cursors.Arrow;
            
            // Восстанавливаем оригинальные настройки рендеринга
            if (_hasOriginalCacheMode)
            {
                _canvas.CacheMode = _originalCanvasCacheMode;
                _hasOriginalCacheMode = false;
            }
            
            // Восстанавливаем оригинальные RenderOptions
            RenderOptions.SetBitmapScalingMode(_canvas, BitmapScalingMode.Unspecified);
            RenderOptions.SetEdgeMode(_canvas, EdgeMode.Unspecified);
            
            // Освобождаем захват мыши
            if (_canvas.IsMouseCaptured)
            {
                _canvas.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// Сброс позиции панорамирования (возврат к исходной позиции)
        /// </summary>
        public void ResetPanning()
        {
            _transformMatrix.OffsetX = 0;
            _transformMatrix.OffsetY = 0;
            _matrixTransform.Matrix = _transformMatrix;
            
            PanningChanged?.Invoke(new Point(0, 0));
        }

        /// <summary>
        /// Установка позиции панорамирования
        /// </summary>
        /// <param name="offset">Смещение относительно исходной позиции</param>
        public void SetPanningOffset(Point offset)
        {
            _transformMatrix.OffsetX = ClampPanningOffsetX(offset.X);
            _transformMatrix.OffsetY = ClampPanningOffsetY(offset.Y);
            _matrixTransform.Matrix = _transformMatrix;
            
            PanningChanged?.Invoke(new Point(_transformMatrix.OffsetX, _transformMatrix.OffsetY));
        }

        /// <summary>
        /// Получение текущего смещения панорамирования
        /// </summary>
        /// <returns>Текущее смещение</returns>
        public Point GetPanningOffset()
        {
            return new Point(_transformMatrix.OffsetX, _transformMatrix.OffsetY);
        }

        /// <summary>
        /// Включение/выключение панорамирования
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            
            if (!enabled && _isPanning)
            {
                StopPanning();
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            StopPanning();
            DetachEventHandlers();
            _canvas = null;
        }
    }
}

