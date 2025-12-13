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
        // Ссылки на элементы UI
        private Canvas mainCanvas;
        private Path gridPath;
        private MenuItem gridToggleMenuItem;
        private MenuItem snapToGridMenuItem;
        private DragManager dragManager;

        // Состояние сетки
        private bool isGridEnabled = false;
        private bool isSnapToGridEnabled = false;
        private const double GridSize = 20; // Размер ячейки сетки в пикселях

        // Делегаты для обратного вызова
        public Action<bool> OnGridToggled;
        public Action<bool> OnSnapToggled;

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

            InitializeGrid();
        }

        private void InitializeGrid()
        {
            gridPath = new Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
                StrokeThickness = 0.5,
                IsHitTestVisible = false
            };

            // Добавляем сетку на Canvas, но скрываем
            mainCanvas.Children.Add(gridPath);
            gridPath.Visibility = Visibility.Collapsed;

            // Подписываемся на событие изменения размера
            mainCanvas.SizeChanged += (s, e) => UpdateGrid();
        }

        public void CreateGrid()
        {
            if (mainCanvas.ActualWidth == 0 || mainCanvas.ActualHeight == 0)
                return;

            var geometryGroup = new GeometryGroup();

            // Вертикальные линии
            for (double x = 0; x < mainCanvas.ActualWidth; x += GridSize)
            {
                geometryGroup.Children.Add(new LineGeometry(
                    new Point(x, 0),
                    new Point(x, mainCanvas.ActualHeight)));
            }

            // Горизонтальные линии
            for (double y = 0; y < mainCanvas.ActualHeight; y += GridSize)
            {
                geometryGroup.Children.Add(new LineGeometry(
                    new Point(0, y),
                    new Point(mainCanvas.ActualWidth, y)));
            }

            gridPath.Data = geometryGroup;
        }

        public void UpdateGrid()
        {
            if (isGridEnabled)
            {
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
                // Создаем и показываем сетку
                CreateGrid();
                gridPath.Visibility = Visibility.Visible;
                gridToggleMenuItem.IsChecked = true;
                gridToggleMenuItem.Header = "Скрыть сетку";
            }
            else
            {
                // Скрываем сетку
                gridPath.Visibility = Visibility.Collapsed;
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
                    CreateGrid();
                    gridPath.Visibility = Visibility.Visible;
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
                if (element == gridPath || element is Path)
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

        // Свойства для доступа к состоянию
        public bool IsGridEnabled => isGridEnabled;
        public bool IsSnapToGridEnabled => isSnapToGridEnabled;
        public double GridCellSize => GridSize;

        // Методы для установки зависимостей
        public void SetDragManager(DragManager manager)
        {
            dragManager = manager;
        }

        // Методы для обработки событий из меню
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

        // Метод для обновления привязки в существующих ResizableElement (если нужно)
        public void UpdateResizableElements(Action<UIElement, bool> updateAction)
        {
            // Этот метод будет вызываться из MainWindow для обновления ResizableElement
        }
    }
}