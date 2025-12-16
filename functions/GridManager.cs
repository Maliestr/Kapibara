using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    public class GridManager
    {
        #region Поля

        // Ссылки на элементы UI
        private Canvas mainCanvas;
        private Rectangle gridRectangle; // Используем Rectangle вместо Path для более быстрого рендеринга
        private MenuItem gridToggleMenuItem;
        private MenuItem snapToGridMenuItem;
        private DragManager dragManager;

        // Состояние сетки
        private bool isGridEnabled = false;
        private bool isSnapToGridEnabled = false;
        private bool isGridCreating = false; // Флаг для предотвращения множественных одновременных созданий
        private bool isGridInitialized = false; // Флаг для ленивой инициализации

        #endregion

        #region Константы

        private const double GridSize = 20; // Размер ячейки сетки в пикселях

        #endregion

        #region События

        // Делегаты для обратного вызова
        public Action<bool> OnGridToggled;
        public Action<bool> OnSnapToggled;

        #endregion

        #region Свойства

        public bool IsGridEnabled => isGridEnabled;
        public bool IsSnapToGridEnabled => isSnapToGridEnabled;
        public double GridCellSize => GridSize;

        #endregion

        #region Конструктор и инициализация

        public GridManager(
            Canvas canvas,
            MenuItem gridMenuItem,
            MenuItem snapMenuItem,
            DragManager dragManager = null)
        {
            mainCanvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            gridToggleMenuItem = gridMenuItem ?? throw new ArgumentNullException(nameof(gridMenuItem));
            snapToGridMenuItem = snapMenuItem ?? throw new ArgumentNullException(nameof(snapMenuItem));
            this.dragManager = dragManager;

            // Ленивая инициализация - не создаем gridPath до первого использования
            // InitializeGrid() будет вызван только когда сетка действительно понадобится
        }

        private void EnsureGridInitialized()
        {
            if (isGridInitialized)
                return;

            isGridInitialized = true;

            // Создаем DrawingBrush с паттерном сетки - это намного быстрее, чем создавать тысячи линий
            var gridBrush = CreateGridBrush();

            gridRectangle = new Rectangle
            {
                Fill = gridBrush,
                IsHitTestVisible = false
            };

            // Используем BitmapCache для кеширования сетки как растрового изображения
            var bitmapCache = new BitmapCache();
            bitmapCache.RenderAtScale = 1.0;
            bitmapCache.EnableClearType = false;
            bitmapCache.SnapsToDevicePixels = true;
            
            gridRectangle.CacheMode = bitmapCache;

            // Добавляем сетку на Canvas, но скрываем
            mainCanvas.Children.Add(gridRectangle);
            gridRectangle.Visibility = Visibility.Collapsed;
            
            // Устанавливаем Z-индекс, чтобы сетка была на заднем плане
            Canvas.SetZIndex(gridRectangle, -1);

            // Подписываемся на событие изменения размера только после инициализации
            mainCanvas.SizeChanged += (s, e) => UpdateGrid();
        }

        private DrawingBrush CreateGridBrush()
        {
            // Создаем маленький паттерн сетки (одна ячейка) - это намного быстрее, чем тысячи линий
            var geometry = new GeometryGroup();
            
            // Вертикальная линия справа (будет повторяться для всех вертикальных линий)
            var verticalLine = new LineGeometry(new Point(GridSize, 0), new Point(GridSize, GridSize));
            // Горизонтальная линия снизу (будет повторяться для всех горизонтальных линий)
            var horizontalLine = new LineGeometry(new Point(0, GridSize), new Point(GridSize, GridSize));
            
            geometry.Children.Add(verticalLine);
            geometry.Children.Add(horizontalLine);
            geometry.Freeze();
            
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, 128, 128, 128)), 0.5);
            pen.Freeze();
            
            var drawing = new GeometryDrawing(null, pen, geometry);
            drawing.Freeze();
            
            var drawingGroup = new DrawingGroup();
            drawingGroup.Children.Add(drawing);
            drawingGroup.Freeze();
            
            var brush = new DrawingBrush(drawingGroup)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, GridSize, GridSize),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None
            };
            
            brush.Freeze();
            return brush;
        }

        public void CreateGrid()
        {
            // Убеждаемся, что gridRectangle инициализирован
            EnsureGridInitialized();

            // Предотвращаем множественные одновременные создания
            if (isGridCreating)
                return;

            // Получаем размеры Canvas
            double canvasWidth = mainCanvas.Width > 0 ? mainCanvas.Width : mainCanvas.ActualWidth;
            double canvasHeight = mainCanvas.Height > 0 ? mainCanvas.Height : mainCanvas.ActualHeight;

            if (canvasWidth == 0 || canvasHeight == 0)
                return;

            // С DrawingBrush сетка создается мгновенно - просто обновляем размеры Rectangle
            if (gridRectangle != null)
            {
                Canvas.SetLeft(gridRectangle, 0);
                Canvas.SetTop(gridRectangle, 0);
                gridRectangle.Width = canvasWidth;
                gridRectangle.Height = canvasHeight;
            }
        }

        public void UpdateGrid()
        {
            if (isGridEnabled && isGridInitialized)
            {
                // С DrawingBrush обновление происходит мгновенно
                CreateGrid();
            }
        }

        public void ToggleGridVisibility()
        {
            // Не позволяем отключить сетку, если включена привязка
            if (isGridEnabled && isSnapToGridEnabled)
            {
                return;
            }

            isGridEnabled = !isGridEnabled;

            if (isGridEnabled)
            {
                // Инициализируем gridRectangle только при первом включении
                EnsureGridInitialized();
                
                // Создаем сетку мгновенно (DrawingBrush работает очень быстро)
                CreateGrid();
                
                // Показываем сетку
                gridRectangle.Visibility = Visibility.Visible;
                gridToggleMenuItem.IsChecked = true;
                gridToggleMenuItem.Header = "Скрыть сетку";
            }
            else
            {
                // Скрываем сетку (если она была инициализирована)
                if (isGridInitialized && gridRectangle != null)
                {
                    gridRectangle.Visibility = Visibility.Collapsed;
                }
                gridToggleMenuItem.IsChecked = false;
                gridToggleMenuItem.Header = "Показать сетку";
            }

            OnGridToggled?.Invoke(isGridEnabled);
        }

        public void ToggleSnapToGrid()
        {
            // Не позволяем включить привязку, если сетка не включена
            if (!isSnapToGridEnabled && !isGridEnabled)
            {
                // Сначала нужно включить сетку
                return;
            }

            isSnapToGridEnabled = !isSnapToGridEnabled;

            if (isSnapToGridEnabled)
            {
                snapToGridMenuItem.IsChecked = true;
                snapToGridMenuItem.Header = "Отключить привязку";

                // Автоматически включаем сетку если она выключена
                if (!isGridEnabled)
                {
                    isGridEnabled = true;
                    // Инициализируем gridRectangle только при первом включении
                    EnsureGridInitialized();
                    CreateGrid();
                    gridRectangle.Visibility = Visibility.Visible;
                    gridToggleMenuItem.IsChecked = true;
                    gridToggleMenuItem.Header = "Скрыть сетку";
                }

                // Привязываем все существующие элементы к сетке
                SnapAllElementsToGrid();

                if (dragManager != null)
                {
                    dragManager.SetSnapToGridEnabled(true);
                }
            }
            else
            {
                snapToGridMenuItem.IsChecked = false;
                snapToGridMenuItem.Header = "Привязка к сетке";

                if (dragManager != null)
                {
                    dragManager.SetSnapToGridEnabled(false);
                }
            }

            OnSnapToggled?.Invoke(isSnapToGridEnabled);
        }

        #endregion

        #region Привязка к сетке

        public Point SnapToGrid(Point originalPoint)
        {
            if (!isSnapToGridEnabled || GridSize <= 0) return originalPoint;

            double snappedX = Math.Round(originalPoint.X / GridSize) * GridSize;
            double snappedY = Math.Round(originalPoint.Y / GridSize) * GridSize;

            return new Point(snappedX, snappedY);
        }

        public void SnapAllElementsToGrid()
        {
            foreach (UIElement element in mainCanvas.Children)
            {
                // Пропускаем сетку и другие служебные элементы
                if ((isGridInitialized && element == gridRectangle) || element is Path)
                    continue;

                // Пропускаем элементы из панели нотации
                if (IsElementInNotation(element))
                    continue;

                // Обрабатываем Line элементы отдельно
                if (element is Line line)
                {
                    Point snappedStart = SnapToGrid(new Point(line.X1, line.Y1));
                    Point snappedEnd = SnapToGrid(new Point(line.X2, line.Y2));
                    line.X1 = snappedStart.X;
                    line.Y1 = snappedStart.Y;
                    line.X2 = snappedEnd.X;
                    line.Y2 = snappedEnd.Y;
                    continue;
                }

                // Обрабатываем Polyline элементы (стрелки)
                if (element is Polyline polyline && polyline.Points != null && polyline.Points.Count > 0)
                {
                    PointCollection snappedPoints = new PointCollection();
                    foreach (Point point in polyline.Points)
                    {
                        snappedPoints.Add(SnapToGrid(point));
                    }
                    polyline.Points = snappedPoints;
                    continue;
                }

                // Обрабатываем остальные элементы
                double currentLeft = Canvas.GetLeft(element);
                double currentTop = Canvas.GetTop(element);

                if (!double.IsNaN(currentLeft) && !double.IsNaN(currentTop))
                {
                    Point snappedPosition = SnapToGrid(new Point(currentLeft, currentTop));
                    Canvas.SetLeft(element, snappedPosition.X);
                    Canvas.SetTop(element, snappedPosition.Y);
                }
            }
        }

        private bool IsElementInNotation(UIElement element)
        {
            if (element == null) return false;

            // Ищем NotationCanvas среди родителей
            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is Canvas canvas && canvas.Name == "NotationCanvas")
                    return true;
                parent = VisualTreeHelper.GetParent(parent);
            }

            return false;
        }

        #endregion

        #region Управление зависимостями

        public void SetDragManager(DragManager manager)
        {
            dragManager = manager;
        }

        #endregion

        #region Обработка событий меню

        public void HandleGridToggleClick(object sender, RoutedEventArgs e)
        {
            // Сохраняем текущее состояние перед проверкой
            bool wasGridEnabled = isGridEnabled;

            // Не позволяем отключить сетку, если включена привязка
            if (wasGridEnabled && isSnapToGridEnabled)
            {
                // Отменяем изменение состояния меню
                if (gridToggleMenuItem != null)
                {
                    gridToggleMenuItem.IsChecked = true;
                }
                e.Handled = true;
                return;
            }

            ToggleGridVisibility();
        }

        public void HandleSnapToggleClick(object sender, RoutedEventArgs e)
        {
            // Сохраняем текущее состояние перед проверкой
            bool wasSnapEnabled = isSnapToGridEnabled;

            // Не позволяем включить привязку, если сетка не включена
            if (!wasSnapEnabled && !isGridEnabled)
            {
                // Отменяем изменение состояния меню
                if (snapToGridMenuItem != null)
                {
                    snapToGridMenuItem.IsChecked = false;
                }
                e.Handled = true;
                return;
            }

            ToggleSnapToGrid();
        }

        #endregion

        #region Вспомогательные методы

        public void UpdateResizableElements(Action<UIElement, bool> updateAction)
        {
            // Этот метод будет вызываться из MainWindow для обновления ResizableElement
        }

        #endregion
    }
}