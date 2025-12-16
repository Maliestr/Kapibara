using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    // Менеджер для управления пулами (swimlanes) в UML Activity диаграммах
    public class PoolManager
    {
        #region Поля

        private Canvas _mainCanvas;
        private bool _isPoolModeActive = false;
        private List<Pool> _pools = new List<Pool>();
        private Button _poolButton;
        private bool _isSnapToGridEnabled;
        private Func<Point, Point> _snapToGridFunc;
        private Action<UIElement, UndoActionType> _saveToUndoStackAction;
        private Dictionary<UIElement, ResizableElement> _resizableElements = new Dictionary<UIElement, ResizableElement>(); // ResizableElement для пулов
        private Dictionary<UIElement, Pool> _elementToPoolMap = new Dictionary<UIElement, Pool>(); // Карта элементов к пулам

        #endregion

        #region Константы

        // Константы для пулов
        private const double DefaultPoolWidth = 150; // Ширина вертикального пула
        private const double DefaultPoolHeight = 300; // Начальная высота вертикального пула
        private const double DefaultPoolHorizontalWidth = 400; // Начальная ширина горизонтального пула
        private const double DefaultPoolHorizontalHeight = 100; // Высота горизонтального пула
        private const double MinPoolWidth = 80;
        private const double MinPoolHeight = 100; // Минимальная высота (включая заголовок)
        private const double HeaderHeight = 30; // Высота заголовка пула

        #endregion

        #region Свойства

        public bool IsPoolModeActive => _isPoolModeActive;
        public List<Pool> Pools => _pools;

        #endregion

        #region События

        public event Action<bool> PoolModeChanged;
        public event Action<Pool> PoolCreated;
        public event Action<Pool> PoolDeleted;

        #endregion

        #region Вспомогательные классы

        // Класс для представления прикрепленного элемента
        public class AttachedElement
        {
            public UIElement Element { get; set; }
            public Point RelativePosition { get; set; } // Относительная позиция относительно пула
        }

        // Класс для представления пула
        public class Pool
        {
            public Border Container { get; set; } // Основной контейнер пула
            public Border Header { get; set; } // Заголовок пула
            public TextBlock HeaderText { get; set; } // Текст заголовка
            public Border Body { get; set; } // Тело пула (область для элементов)
            public Line Separator { get; set; } // Разделительная линия
            public bool IsVertical { get; set; } = true; // Вертикальный (true) или горизонтальный (false)
            public string Name { get; set; } = "Участник";
            public Point Position { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public List<AttachedElement> AttachedElements { get; set; } = new List<AttachedElement>(); // Прикрепленные элементы
        }

        public class PoolData
        {
            public double PositionX { get; set; }
            public double PositionY { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public bool IsVertical { get; set; }
            public string Name { get; set; }
        }

        #endregion

        #region Конструктор и инициализация

        public PoolManager(
            Canvas mainCanvas,
            Button poolButton,
            bool isSnapToGridEnabled,
            Func<Point, Point> snapToGrid,
            Action<UIElement, UndoActionType> saveToUndoStack)
        {
            _mainCanvas = mainCanvas ?? throw new ArgumentNullException(nameof(mainCanvas));
            _poolButton = poolButton ?? throw new ArgumentNullException(nameof(poolButton));
            _isSnapToGridEnabled = isSnapToGridEnabled;
            _snapToGridFunc = snapToGrid;
            _saveToUndoStackAction = saveToUndoStack;

            InitializePoolButton();
        }

        private void InitializePoolButton()
        {
            _poolButton.Click += PoolButton_Click;
        }

        private void PoolButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePoolMode();
        }

        #endregion

        #region Управление режимом создания пулов

        public void TogglePoolMode()
        {
            _isPoolModeActive = !_isPoolModeActive;

            if (_isPoolModeActive)
            {
                _mainCanvas.Cursor = Cursors.Cross;
                _poolButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3939"));
            }
            else
            {
                _mainCanvas.Cursor = Cursors.Arrow;
                _poolButton.Background = Brushes.Transparent;
            }

            PoolModeChanged?.Invoke(_isPoolModeActive);
        }

        #endregion

        #region Создание пулов

        public bool HandleMouseClick(Point clickPosition, object originalSource)
        {
            if (!_isPoolModeActive) return false;

            // Проверяем, что клик не по другим элементам
            if (originalSource is Canvas)
            {
                // Применяем привязку к сетке, если включена
                Point position = _isSnapToGridEnabled && _snapToGridFunc != null
                    ? _snapToGridFunc(clickPosition)
                    : clickPosition;

                var pool = CreatePoolAtPosition(position);
                EditPoolName(pool);
                
                // Автоматически отключаем режим создания пулов после создания одного пула
                TogglePoolMode();
                
                return true; // Обработано
            }

            return false; // Не обработано
        }

        public Pool CreatePoolAtPosition(Point position, bool isVertical = true)
        {
            // Создаем основной контейнер в стиле программы
            var container = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#343434")), // Стиль панелей программы
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")), // Границы в стиле программы
                BorderThickness = new Thickness(2), // Более заметная обводка
                CornerRadius = new CornerRadius(0), // Квадратные углы вместо круглых
                Cursor = Cursors.SizeAll,
                ClipToBounds = true // Обрезаем содержимое по границам
            };

            // Создаем заголовок пула в стиле программы
            var header = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#424242")), // Темнее основного фона
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555")), // Более темная граница между header и body
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = HeaderHeight,
                Margin = new Thickness(0),
                CornerRadius = new CornerRadius(0) // Без скругления, так как скругление на контейнере
            };

            var headerText = new TextBlock
            {
                Text = "Участник",
                Foreground = Brushes.White, // Белый текст как в программе
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Gotham Pro"), // Шрифт программы
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            header.Child = headerText;

            // Создаем тело пула
            var body = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#343434")), // Тот же цвет что и контейнер
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0),
                ClipToBounds = true // Обрезаем содержимое по границам
            };

            // Разделительная линия больше не нужна - убрана
            Line separator = null;

            // Настраиваем размеры и позицию в зависимости от ориентации
            double poolWidth;
            double poolHeight;
            
            if (isVertical)
            {
                poolWidth = DefaultPoolWidth;
                poolHeight = DefaultPoolHeight; // Фиксированная начальная высота
                container.Width = poolWidth;
                container.Height = poolHeight;
                // Header и body будут управляться Grid, не устанавливаем фиксированные размеры
                header.Width = double.NaN; // Auto - Grid будет управлять
                body.Width = double.NaN; // Auto - Grid будет управлять
                body.Height = double.NaN; // Auto - Grid будет управлять через RowDefinition
            }
            else
            {
                poolWidth = DefaultPoolHorizontalWidth; // Фиксированная начальная ширина
                poolHeight = DefaultPoolHorizontalHeight;
                container.Width = poolWidth;
                container.Height = poolHeight;
                // Header и body будут управляться Grid, не устанавливаем фиксированные размеры
                header.Height = HeaderHeight; // Фиксированная высота для header
                header.Width = double.NaN; // Auto - Grid будет управлять
                body.Width = double.NaN; // Auto - Grid будет управлять через ColumnDefinition
                body.Height = double.NaN; // Auto - Grid будет управлять
            }

            // Устанавливаем позицию контейнера
            Canvas.SetLeft(container, position.X);
            Canvas.SetTop(container, position.Y);

            // Создаем структуру с правильным порядком для скругления углов
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = true // Обрезаем содержимое по границам, чтобы не было видно круглых углов сзади
            };
            
            // Настраиваем Grid для правильного размещения header и body
            if (isVertical)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderHeight) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                
                // Устанавливаем HorizontalAlignment для растягивания по ширине
                header.HorizontalAlignment = HorizontalAlignment.Stretch;
                body.HorizontalAlignment = HorizontalAlignment.Stretch;
                
                Grid.SetRow(header, 0);
                Grid.SetRow(body, 1);
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HeaderHeight) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                // Устанавливаем VerticalAlignment для растягивания по высоте
                header.VerticalAlignment = VerticalAlignment.Stretch;
                body.VerticalAlignment = VerticalAlignment.Stretch;
                
                Grid.SetColumn(header, 0);
                Grid.SetColumn(body, 1);
            }
            
            grid.Children.Add(body); // Тело сначала (фон)
            grid.Children.Add(header); // Заголовок сверху
            container.Child = grid;
            
            // Квадратные углы для тела пула (без скругления)
            body.CornerRadius = new CornerRadius(0);

            // Добавляем на холст
            _mainCanvas.Children.Add(container);

            // Устанавливаем Z-индекс (пулы должны быть на заднем плане)
            Canvas.SetZIndex(container, 0);

            // Создаем объект Pool
            var pool = new Pool
            {
                Container = container,
                Header = header,
                HeaderText = headerText,
                Body = body,
                Separator = null, // Разделительная линия больше не используется
                IsVertical = isVertical,
                Position = position,
                Width = poolWidth,
                Height = poolHeight
            };

            _pools.Add(pool);

            // Настраиваем обработчики событий
            SetupPoolEventHandlers(pool);

            // Создаем ResizableElement для пула
            CreateResizableElementForPool(pool);

            PoolCreated?.Invoke(pool);
            _saveToUndoStackAction?.Invoke(container, UndoActionType.Add);

            return pool;
        }

        #endregion

        #region Управление размерами пулов

        private void CreateResizableElementForPool(Pool pool)
        {
            if (!_resizableElements.ContainsKey(pool.Container))
            {
                var resizableElement = new ResizableElement(
                    pool.Container,
                    _mainCanvas,
                    _snapToGridFunc,
                    _isSnapToGridEnabled
                );

                // Подписываемся на событие изменения размера для обновления разделительной линии и Grid
                if (pool.Container is FrameworkElement fe)
                {
                    // Отключаем автоматическое масштабирование Grid - мы будем обновлять его вручную
                    fe.SizeChanged += (s, e) => 
                    {
                        // Небольшая задержка, чтобы ResizableElement успел применить размеры
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => OnPoolSizeChanged(pool)), 
                            System.Windows.Threading.DispatcherPriority.Background);
                    };
                }

                _resizableElements[pool.Container] = resizableElement;
            }
        }

        private void OnPoolSizeChanged(Pool pool)
        {
            if (pool.Container is FrameworkElement container)
            {
                double newWidth = container.ActualWidth;
                double newHeight = container.ActualHeight;

                // Применяем минимальные размеры пула
                if (newWidth < MinPoolWidth)
                {
                    newWidth = MinPoolWidth;
                    container.Width = MinPoolWidth;
                }
                if (newHeight < MinPoolHeight)
                {
                    newHeight = MinPoolHeight;
                    container.Height = MinPoolHeight;
                }

                // Обновляем размеры пула
                pool.Width = newWidth;
                pool.Height = newHeight;

                // Предотвращаем масштабирование Grid - восстанавливаем его размеры
                var grid = pool.Container.Child as Grid;
                if (grid != null)
                {
                    // Сбрасываем размеры Grid, если они были изменены ResizableElement
                    if (grid is FrameworkElement gridFe)
                    {
                        gridFe.Width = double.NaN; // Auto
                        gridFe.Height = double.NaN; // Auto
                    }
                }

                // Предотвращаем масштабирование Grid и его дочерних элементов ResizableElement
                if (grid != null)
                {
                    // Сбрасываем размеры Grid, если они были изменены ResizableElement
                    if (grid is FrameworkElement gridFe)
                    {
                        gridFe.Width = double.NaN; // Auto
                        gridFe.Height = double.NaN; // Auto
                    }
                    
                    // Сбрасываем размеры дочерних элементов Grid, если они были изменены ResizableElement
                    foreach (UIElement child in grid.Children)
                    {
                        if (child is FrameworkElement childFe)
                        {
                            // Сбрасываем Width для header и body, чтобы Grid управлял размерами
                            if (pool.IsVertical)
                            {
                                childFe.Width = double.NaN; // Auto - Grid будет управлять шириной
                            }
                            else
                            {
                                childFe.Height = double.NaN; // Auto - Grid будет управлять высотой
                            }
                        }
                    }
                }

                // Обновляем Grid definitions для правильного отображения
                if (pool.IsVertical)
                {
                    // Обновляем Grid row definition для body
                    if (grid != null && grid.RowDefinitions.Count > 1)
                    {
                        grid.RowDefinitions[1].Height = new GridLength(newHeight - HeaderHeight);
                    }
                    
                    // Header имеет фиксированную высоту, body будет растягиваться через Grid
                    // Ширина будет управляться Grid через ColumnDefinitions (если есть) или через контейнер
                }
                else
                {
                    // Обновляем Grid column definition для body
                    if (grid != null && grid.ColumnDefinitions.Count > 1)
                    {
                        grid.ColumnDefinitions[1].Width = new GridLength(newWidth - HeaderHeight);
                    }
                    
                    // Header имеет фиксированную ширину, body будет растягиваться через Grid
                    // Высота будет управляться Grid через RowDefinitions (если есть) или через контейнер
                }
            }
        }

        #endregion

        #region Обработчики событий

        private void SetupPoolEventHandlers(Pool pool)
        {
            // Обработчик двойного клика для редактирования названия
            pool.Header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    EditPoolName(pool);
                    e.Handled = true;
                }
            };

            // Обработчик для перемещения пула и показа ручек изменения размера
            pool.Container.MouseLeftButtonDown += (s, e) =>
            {
                // Проверяем, не кликнули ли по ручкам изменения размера
                if (e.OriginalSource is Rectangle || e.OriginalSource is Line)
                {
                    // Это клик по ручке - не обрабатываем, пусть ResizableElement обработает
                    return;
                }

                // Показываем ручки изменения размера при клике на пул
                ShowResizeHandlesForPool(pool);
                
                if (e.ClickCount == 1 && !_isDragging && !_isMouseDown)
                {
                    _isMouseDown = true;
                    _potentialDragPool = pool;
                    _dragStartPoint = e.GetPosition(_mainCanvas);
                    _poolStartPosition = new Point(Canvas.GetLeft(pool.Container), Canvas.GetTop(pool.Container));
                    
                    // Подписываемся на события самого контейнера, а не Canvas
                    // Это предотвратит срабатывание при минимальном движении мыши
                    pool.Container.MouseMove += PoolContainer_MouseMove;
                    pool.Container.MouseLeftButtonUp += PoolContainer_MouseLeftButtonUp;
                    pool.Container.LostMouseCapture += PoolContainer_LostMouseCapture;
                    
                    e.Handled = true;
                }
            };

            // Изменение размера теперь обрабатывается через ResizableElement
        }

        private bool _isDragging = false;
        private Pool _draggedPool;
        private Point _dragStartPoint;
        private Point _poolStartPosition;
        private bool _isMouseDown = false;
        private Pool _potentialDragPool;
        private const double DragThreshold = 10.0; // Минимальное расстояние для начала перетаскивания

        #endregion

        #region Перетаскивание пулов

        private void PoolContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || _potentialDragPool == null || _isDragging) return;
            
            // Проверяем, что кнопка мыши все еще нажата
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                CancelPotentialDrag();
                return;
            }
            
            Point currentPoint = e.GetPosition(_mainCanvas);
            double distance = Math.Sqrt(
                Math.Pow(currentPoint.X - _dragStartPoint.X, 2) + 
                Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2)
            );

            // Если мышь переместилась на достаточное расстояние, начинаем перетаскивание
            if (distance >= DragThreshold)
            {
                // Отписываемся от временных обработчиков контейнера
                if (_potentialDragPool != null && _potentialDragPool.Container != null)
                {
                    _potentialDragPool.Container.MouseMove -= PoolContainer_MouseMove;
                    _potentialDragPool.Container.MouseLeftButtonUp -= PoolContainer_MouseLeftButtonUp;
                    _potentialDragPool.Container.LostMouseCapture -= PoolContainer_LostMouseCapture;
                }
                
                // Начинаем перетаскивание
                StartPoolDrag(_potentialDragPool, _dragStartPoint);
                _isMouseDown = false;
                _potentialDragPool = null;
            }
        }

        private void PoolContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CancelPotentialDrag();
        }

        private void PoolContainer_LostMouseCapture(object sender, MouseEventArgs e)
        {
            CancelPotentialDrag();
        }

        private void CancelPotentialDrag()
        {
            if (_isMouseDown)
            {
                _isMouseDown = false;
                
                // Отписываемся от обработчиков контейнера
                if (_potentialDragPool != null && _potentialDragPool.Container != null)
                {
                    _potentialDragPool.Container.MouseMove -= PoolContainer_MouseMove;
                    _potentialDragPool.Container.MouseLeftButtonUp -= PoolContainer_MouseLeftButtonUp;
                    _potentialDragPool.Container.LostMouseCapture -= PoolContainer_LostMouseCapture;
                }
                
                // Если это был простой клик (не перетаскивание), показываем ручки изменения размера
                if (_potentialDragPool != null)
                {
                    ShowResizeHandlesForPool(_potentialDragPool);
                }
                
                _potentialDragPool = null;
            }
        }

        private void StartPoolDrag(Pool pool, Point startPoint)
        {
            // Проверяем, не идет ли уже перетаскивание
            if (_isDragging) return;

            _isDragging = true;
            _draggedPool = pool;
            _dragStartPoint = startPoint;
            _poolStartPosition = new Point(Canvas.GetLeft(pool.Container), Canvas.GetTop(pool.Container));

            _mainCanvas.MouseMove += PoolDrag_MouseMove;
            _mainCanvas.MouseLeftButtonUp += PoolDrag_MouseLeftButtonUp;
            _mainCanvas.LostMouseCapture += PoolDrag_LostMouseCapture;
            
            if (_mainCanvas.CaptureMouse())
            {
                // Захват успешен
            }
        }

        private void PoolDrag_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _draggedPool == null) return;
            
            // Проверяем, что кнопка мыши все еще нажата
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndPoolDrag();
                return;
            }

            Point currentPoint = e.GetPosition(_mainCanvas);
            Point delta = new Point(
                currentPoint.X - _dragStartPoint.X,
                currentPoint.Y - _dragStartPoint.Y
            );

            Point newPosition = new Point(
                _poolStartPosition.X + delta.X,
                _poolStartPosition.Y + delta.Y
            );

            // Применяем привязку к сетке, если включена
            if (_isSnapToGridEnabled && _snapToGridFunc != null)
            {
                newPosition = _snapToGridFunc(newPosition);
            }

            // Ограничиваем позицию границами холста
            newPosition.X = Math.Max(0, newPosition.X);
            newPosition.Y = Math.Max(0, newPosition.Y);

            Canvas.SetLeft(_draggedPool.Container, newPosition.X);
            Canvas.SetTop(_draggedPool.Container, newPosition.Y);
            
            // Обновляем позицию пула
            _draggedPool.Position = newPosition;
            
            // Перемещаем все прикрепленные элементы вместе с пулом
            UpdateAttachedElementsPosition(_draggedPool);
        }

        #endregion

        #region Прикрепление элементов к пулам

        private void UpdateAttachedElementsPosition(Pool pool)
        {
            if (pool == null) return;

            double poolLeft = Canvas.GetLeft(pool.Container);
            double poolTop = Canvas.GetTop(pool.Container);

            foreach (var attached in pool.AttachedElements)
            {
                if (attached.Element != null && _mainCanvas.Children.Contains(attached.Element))
                {
                    Canvas.SetLeft(attached.Element, poolLeft + attached.RelativePosition.X);
                    Canvas.SetTop(attached.Element, poolTop + attached.RelativePosition.Y);
                }
            }
        }

        private bool IsElementInsidePool(UIElement element, Pool pool)
        {
            if (element == null || pool == null || pool.Container == null) return false;

            double elementLeft = Canvas.GetLeft(element);
            double elementTop = Canvas.GetTop(element);
            
            if (double.IsNaN(elementLeft) || double.IsNaN(elementTop)) return false;

            double poolLeft = Canvas.GetLeft(pool.Container);
            double poolTop = Canvas.GetTop(pool.Container);
            
            if (double.IsNaN(poolLeft) || double.IsNaN(poolTop)) return false;

            // Получаем размеры элемента
            double elementWidth = 0;
            double elementHeight = 0;
            if (element is FrameworkElement fe)
            {
                elementWidth = fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
                elementHeight = fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;
                
                if (double.IsNaN(elementWidth) || elementWidth <= 0) return false;
                if (double.IsNaN(elementHeight) || elementHeight <= 0) return false;
            }
            else
            {
                return false;
            }

            // Проверяем, попадает ли центр элемента в границы пула (исключая header)
            double elementCenterX = elementLeft + elementWidth / 2;
            double elementCenterY = elementTop + elementHeight / 2;

            double poolRight = poolLeft + pool.Width;
            double poolBottom = poolTop + pool.Height;

            // Для вертикального пула проверяем, что элемент находится в body (не в header)
            if (pool.IsVertical)
            {
                double bodyTop = poolTop + HeaderHeight;
                return elementCenterX >= poolLeft && elementCenterX <= poolRight &&
                       elementCenterY >= bodyTop && elementCenterY <= poolBottom;
            }
            else
            {
                // Для горизонтального пула проверяем, что элемент находится в body (не в header)
                double bodyLeft = poolLeft + HeaderHeight;
                return elementCenterX >= bodyLeft && elementCenterX <= poolRight &&
                       elementCenterY >= poolTop && elementCenterY <= poolBottom;
            }
        }

        private void AttachElementToPool(UIElement element, Pool pool)
        {
            if (element == null || pool == null) return;

            // Проверяем, не прикреплен ли уже элемент к другому пулу
            if (_elementToPoolMap.ContainsKey(element))
            {
                DetachElementFromPool(element);
            }

            double elementLeft = Canvas.GetLeft(element);
            double elementTop = Canvas.GetTop(element);
            double poolLeft = Canvas.GetLeft(pool.Container);
            double poolTop = Canvas.GetTop(pool.Container);

            // Вычисляем относительную позицию
            Point relativePosition = new Point(
                elementLeft - poolLeft,
                elementTop - poolTop
            );

            var attached = new AttachedElement
            {
                Element = element,
                RelativePosition = relativePosition
            };

            pool.AttachedElements.Add(attached);
            _elementToPoolMap[element] = pool;
        }

        private void DetachElementFromPool(UIElement element)
        {
            if (element == null || !_elementToPoolMap.ContainsKey(element)) return;

            Pool pool = _elementToPoolMap[element];
            var attached = pool.AttachedElements.FirstOrDefault(a => a.Element == element);
            
            if (attached != null)
            {
                pool.AttachedElements.Remove(attached);
            }

            _elementToPoolMap.Remove(element);
        }

        public void HandleElementMove(UIElement element, Point newPosition)
        {
            if (element == null) return;

            // Проверяем, не является ли элемент частью пула
            foreach (var pool in _pools)
            {
                if (element == pool.Container || element == pool.Header || element == pool.Body || 
                    element == pool.Separator || element == pool.HeaderText)
                {
                    return; // Не прикрепляем элементы пула к самому пулу
                }
            }

            // Проверяем, прикреплен ли элемент к пулу
            if (_elementToPoolMap.ContainsKey(element))
            {
                Pool currentPool = _elementToPoolMap[element];
                
                // Обновляем относительную позицию
                double poolLeft = Canvas.GetLeft(currentPool.Container);
                double poolTop = Canvas.GetTop(currentPool.Container);
                
                Point relativePosition = new Point(
                    newPosition.X - poolLeft,
                    newPosition.Y - poolTop
                );

                var attached = currentPool.AttachedElements.FirstOrDefault(a => a.Element == element);
                if (attached != null)
                {
                    attached.RelativePosition = relativePosition;
                }

                // Проверяем, все еще ли элемент в границах пула
                if (!IsElementInsidePool(element, currentPool))
                {
                    DetachElementFromPool(element);
                }
            }
            else
            {
                // Проверяем, попадает ли элемент в какой-либо пул
                foreach (var pool in _pools)
                {
                    if (IsElementInsidePool(element, pool))
                    {
                        AttachElementToPool(element, pool);
                        break;
                    }
                }
            }
        }

        private void PoolDrag_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndPoolDrag();
        }

        private void PoolDrag_LostMouseCapture(object sender, MouseEventArgs e)
        {
            EndPoolDrag();
        }

        private void EndPoolDrag()
        {
            if (_isDragging)
            {
                _isDragging = false;
                _isMouseDown = false;
                _mainCanvas.MouseMove -= PoolDrag_MouseMove;
                _mainCanvas.MouseLeftButtonUp -= PoolDrag_MouseLeftButtonUp;
                _mainCanvas.LostMouseCapture -= PoolDrag_LostMouseCapture;
                
                if (_mainCanvas.IsMouseCaptured)
                {
                    _mainCanvas.ReleaseMouseCapture();
                }
                
                _draggedPool = null;
                _potentialDragPool = null;
            }
        }

        #endregion

        #region Управление ручками изменения размера

        public void ShowResizeHandlesForPool(Pool pool)
        {
            if (pool != null && pool.Container != null)
            {
                // Убеждаемся, что ResizableElement создан
                if (!_resizableElements.ContainsKey(pool.Container))
                {
                    CreateResizableElementForPool(pool);
                }

                if (_resizableElements.TryGetValue(pool.Container, out var resizable))
                {
                    resizable.ShowResizeHandles();
                }
            }
        }

        public void HideResizeHandlesForPool(Pool pool)
        {
            if (pool != null && _resizableElements.TryGetValue(pool.Container, out var resizable))
            {
                resizable.HideResizeHandles();
            }
        }

        #endregion

        #region Редактирование пулов

        public void EditPoolName(Pool pool)
        {
            var editTextBox = new TextBox
            {
                Width = pool.IsVertical ? pool.Container.Width - 10 : 200,
                Height = HeaderHeight - 5,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525")), // Темный фон как у программы
                Foreground = Brushes.White, // Белый текст
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")), // Границы в стиле программы
                BorderThickness = new Thickness(1),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Gotham Pro"), // Шрифт программы
                Text = pool.Name,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                CaretBrush = Brushes.White // Белый курсор
            };

            // Позиционируем поверх заголовка
            double left = Canvas.GetLeft(pool.Container) + 5;
            double top = Canvas.GetTop(pool.Container) + 2.5;
            Canvas.SetLeft(editTextBox, left);
            Canvas.SetTop(editTextBox, top);
            Canvas.SetZIndex(editTextBox, 1000);

            _mainCanvas.Children.Add(editTextBox);
            editTextBox.Focus();
            editTextBox.SelectAll();

            void FinishEditing()
            {
                string newName = editTextBox.Text.Trim();
                if (string.IsNullOrEmpty(newName))
                {
                    newName = "Участник";
                }

                pool.Name = newName;
                pool.HeaderText.Text = newName;

                _mainCanvas.Children.Remove(editTextBox);
            }

            editTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Escape)
                {
                    FinishEditing();
                    e.Handled = true;
                }
            };

            editTextBox.LostFocus += (s, e) => FinishEditing();
        }

        #endregion

        #region Удаление пулов

        public void DeletePool(Pool pool)
        {
            // Удаляем ResizableElement
            if (pool.Container != null && _resizableElements.TryGetValue(pool.Container, out var resizable))
            {
                resizable.Dispose();
                _resizableElements.Remove(pool.Container);
            }

            if (pool.Container != null)
            {
                _mainCanvas.Children.Remove(pool.Container);
            }
            // Разделительная линия больше не используется
            _pools.Remove(pool);
            PoolDeleted?.Invoke(pool);
        }

        public void ClearAllPools()
        {
            foreach (var pool in _pools.ToArray())
            {
                DeletePool(pool);
            }
        }

        #endregion

        #region Поиск пулов

        public Pool GetPoolByElement(UIElement element)
        {
            foreach (var pool in _pools)
            {
                if (pool.Container == element || pool.Header == element || 
                    pool.Body == element)
                {
                    return pool;
                }
            }
            return null;
        }

        #endregion

        #region Привязка к сетке

        public void UpdateSnapToGrid(bool enabled)
        {
            _isSnapToGridEnabled = enabled;
            
            // Обновляем все ResizableElement
            foreach (var resizable in _resizableElements.Values)
            {
                resizable.UpdateSnapToGrid(_snapToGridFunc, enabled);
            }
        }

        #endregion

        #region Сериализация

        public List<PoolData> GetPoolsData()
        {
            var data = new List<PoolData>();
            foreach (var pool in _pools)
            {
                data.Add(new PoolData
                {
                    PositionX = Canvas.GetLeft(pool.Container),
                    PositionY = Canvas.GetTop(pool.Container),
                    Width = pool.Width,
                    Height = pool.Height,
                    IsVertical = pool.IsVertical,
                    Name = pool.Name
                });
            }
            return data;
        }

        public void LoadPools(List<PoolData> poolsData)
        {
            ClearAllPools();

            foreach (var data in poolsData)
            {
                var position = new Point(data.PositionX, data.PositionY);
                var pool = CreatePoolAtPosition(position, data.IsVertical);
                pool.Width = data.Width;
                pool.Height = data.Height;
                pool.Name = data.Name;
                pool.HeaderText.Text = data.Name;

                // Обновляем размеры
                if (pool.IsVertical)
                {
                    pool.Container.Width = data.Width;
                    pool.Header.Width = data.Width;
                    pool.Body.Width = data.Width;
                    pool.Body.Height = data.Height - HeaderHeight;
                    // Разделительная линия больше не используется
                }
                else
                {
                    pool.Container.Height = data.Height;
                    pool.Body.Height = data.Height - HeaderHeight;
                    // Разделительная линия больше не используется
                }
            }
        }

        #endregion
    }
}

