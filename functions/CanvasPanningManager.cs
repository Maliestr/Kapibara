using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Graphic_Editor
{
    /// Менеджер для управления перемещением (панорамированием) Canvas
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

        /// Тип панорамирования
        public enum PanningMode
        {
            MiddleButton,      // Средняя кнопка мыши (колесико)
            RightButton,       // Правая кнопка мыши
            CtrlRightButton    // Ctrl + Правая кнопка мыши
        }

        /// Текущий режим панорамирования
        public PanningMode Mode { get; set; } = PanningMode.MiddleButton;

        /// Включено ли панорамирование
        public bool IsEnabled { get; set; } = true;

        /// Событие изменения позиции панорамирования
        public event Action<Point> PanningChanged;

        /// Конструктор
        /// <param name="canvas">Canvas для управления</param>
        public CanvasPanningManager(Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            
            InitializeTransform();
            AttachEventHandlers();
        }


        /// Инициализация трансформации для перемещения
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

        /// Прикрепление обработчиков событий Canvas
        private void AttachEventHandlers()
        {
            // Используем обычные события мыши
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseUp += Canvas_MouseUp;
            _canvas.MouseLeave += Canvas_MouseLeave;
        }


        /// Открепление обработчиков событий
        private void DetachEventHandlers()
        {
            _canvas.MouseDown -= Canvas_MouseDown;
            _canvas.MouseMove -= Canvas_MouseMove;
            _canvas.MouseUp -= Canvas_MouseUp;
            _canvas.MouseLeave -= Canvas_MouseLeave;
        }

        /// Проверка, должна ли начаться панорамирование
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

        /// Проверка, должна ли прекратиться панорамирование
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

        /// Обработчик нажатия кнопки мыши на Canvas (обычное событие)
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Если уже начали панорамирование, не обрабатываем снова
            if (_isPanning) return;
            
            if (ShouldStartPanning(e))
            {
                Point mousePos = e.GetPosition(_canvas);
                StartPanning(mousePos);
                e.Handled = true; // Предотвращаем дальнейшую обработку другими менеджерами
            }
        }



        /// Проверка, должна ли продолжаться панорамирование
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

        /// Обработчик движения мыши на Canvas
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsEnabled || !_isPanning) return;

            // Проверяем, что кнопка мыши все еще нажата
            if (!ShouldContinuePanning())
            {
                StopPanning();
                return;
            }

            // Используем координаты относительно Canvas (они будут преобразованы в UpdatePanning)
            Point currentPosition = e.GetPosition(_canvas);
            UpdatePanning(currentPosition);
            e.Handled = true;
        }

        /// Обработчик отпускания кнопки мыши на Canvas
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ShouldStopPanning(e))
            {
                StopPanning();
                e.Handled = true;
            }
        }

        /// Обработчик выхода мыши за пределы Canvas
        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                StopPanning();
            }
        }


        /// Начало панорамирования
        private void StartPanning(Point mousePosition)
        {
            if (_isPanning) return;

            _isPanning = true;
            
            // Используем координаты относительно родительского контейнера для стабильности
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

        /// Обновление позиции панорамирования
        private void UpdatePanning(Point currentMousePosition)
        {
            if (!_isPanning) return;

            // Получаем текущую позицию мыши относительно родительского контейнера
            // Это важно для стабильного отслеживания, так как Canvas может быть трансформирован
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            Point currentScreenPosition;
            
            if (parent != null)
            {
                currentScreenPosition = Mouse.GetPosition(parent);
            }
            else
            {
                currentScreenPosition = currentMousePosition;
            }

            // Вычисляем смещение относительно предыдущей позиции
            double deltaX = currentScreenPosition.X - _lastMousePosition.X;
            double deltaY = currentScreenPosition.Y - _lastMousePosition.Y;

            // Игнорируем очень маленькие смещения для уменьшения дрожания
            if (Math.Abs(deltaX) < 0.1 && Math.Abs(deltaY) < 0.1)
            {
                return;
            }

            // Вычисляем новые координаты смещения
            double newOffsetX = _transformMatrix.OffsetX + deltaX;
            double newOffsetY = _transformMatrix.OffsetY + deltaY;

            // Ограничиваем смещение границами Canvas
            newOffsetX = ClampPanningOffsetX(newOffsetX);
            newOffsetY = ClampPanningOffsetY(newOffsetY);

            // Обновляем матрицу трансформации
            _transformMatrix.OffsetX = newOffsetX;
            _transformMatrix.OffsetY = newOffsetY;

            // Применяем трансформацию
            _matrixTransform.Matrix = _transformMatrix;

            // Вызываем событие
            PanningChanged?.Invoke(new Point(_transformMatrix.OffsetX, _transformMatrix.OffsetY));

            // Сохраняем текущую позицию для следующего обновления
            _lastMousePosition = currentScreenPosition;
        }

        /// Ограничение смещения по X границами Canvas
        private double ClampPanningOffsetX(double offsetX)
        {
            // Получаем размеры Canvas
            double canvasWidth = _canvas.Width > 0 ? _canvas.Width : _canvas.ActualWidth;

            // Получаем размеры видимой области (контейнера Canvas)
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            if (parent == null) return offsetX;

            double viewportWidth = parent.ActualWidth;

            // Если Canvas меньше или равен видимой области, не позволяем перемещение
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

        /// Ограничение смещения по Y границами Canvas
        private double ClampPanningOffsetY(double offsetY)
        {
            // Получаем размеры Canvas
            double canvasHeight = _canvas.Height > 0 ? _canvas.Height : _canvas.ActualHeight;

            // Получаем размеры видимой области (контейнера Canvas)
            FrameworkElement parent = _canvas.Parent as FrameworkElement;
            if (parent == null) return offsetY;

            double viewportHeight = parent.ActualHeight;

            // Если Canvas меньше или равен видимой области, не позволяем перемещение
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

        /// Остановка панорамирования
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

        /// Сброс позиции панорамирования (возврат к исходной позиции)
        public void ResetPanning()
        {
            _transformMatrix.OffsetX = 0;
            _transformMatrix.OffsetY = 0;
            _matrixTransform.Matrix = _transformMatrix;
            
            PanningChanged?.Invoke(new Point(0, 0));
        }

        /// Установка позиции панорамирования
        /// <param name="offset">Смещение относительно исходной позиции</param>
        public void SetPanningOffset(Point offset)
        {
            _transformMatrix.OffsetX = ClampPanningOffsetX(offset.X);
            _transformMatrix.OffsetY = ClampPanningOffsetY(offset.Y);
            _matrixTransform.Matrix = _transformMatrix;
            
            PanningChanged?.Invoke(new Point(_transformMatrix.OffsetX, _transformMatrix.OffsetY));
        }

        /// Получение текущего смещения панорамирования
        /// <returns>Текущее смещение</returns>
        public Point GetPanningOffset()
        {
            return new Point(_transformMatrix.OffsetX, _transformMatrix.OffsetY);
        }

        /// Включение/выключение панорамирования
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            
            if (!enabled && _isPanning)
            {
                StopPanning();
            }
        }

        /// Освобождение ресурсов
        public void Dispose()
        {
            StopPanning();
            DetachEventHandlers();
            _canvas = null;
        }
    }
}

