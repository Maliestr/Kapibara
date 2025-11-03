using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;



namespace Graphic_Editor
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private UIElement draggedElement;
        private Point offset;
        private Transform originalTransform;
        private Point dragStartPoint;

        private bool isGridEnabled = false;
        private bool isSnapToGridEnabled = false;
        private Path gridPath;
        private const double GridSize = 20; // Размер ячейки сетки
        public MainWindow()
        {
            InitializeComponent();
            MakeAllShapesDraggable();

            InitializeGrid(); // Инициализируем сетку

            // Обработка горячих клавиш
            this.KeyDown += MainWindow_KeyDown;
        }

        // Инициализация сетки
        private void InitializeGrid()
        {
            gridPath = new Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)), // Полупрозрачный серый
                StrokeThickness = 0.5,
                IsHitTestVisible = false // Сетка не должна мешать взаимодействию
            };

            // Добавляем сетку на канвас, но скрываем
            MainCanvas.Children.Add(gridPath);
            gridPath.Visibility = Visibility.Collapsed;
        }

        // Создание сетки
        private void CreateGrid()
        {
            if (MainCanvas.ActualWidth == 0 || MainCanvas.ActualHeight == 0)
                return;

            var geometryGroup = new GeometryGroup();

            // Вертикальные линии
            for (double x = 0; x < MainCanvas.ActualWidth; x += GridSize)
            {
                geometryGroup.Children.Add(new LineGeometry(
                    new Point(x, 0),
                    new Point(x, MainCanvas.ActualHeight)));
            }

            // Горизонтальные линии
            for (double y = 0; y < MainCanvas.ActualHeight; y += GridSize)
            {
                geometryGroup.Children.Add(new LineGeometry(
                    new Point(0, y),
                    new Point(MainCanvas.ActualWidth, y)));
            }

            gridPath.Data = geometryGroup;
        }

        // Обработчик переключения сетки из меню
        private void GridToggleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleGridVisibility();
        }

        // Обработчик переключения привязки к сетке
        private void SnapToGridMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleSnapToGrid();
        }

        // Метод переключения сетки
        private void ToggleGridVisibility()
        {
            isGridEnabled = !isGridEnabled;

            if (isGridEnabled)
            {
                // Создаем и показываем сетку
                CreateGrid();
                gridPath.Visibility = Visibility.Visible;
                GridToggleMenuItem.IsChecked = true;
                GridToggleMenuItem.Header = "Скрыть сетку";
            }
            else
            {
                // Скрываем сетку
                gridPath.Visibility = Visibility.Collapsed;
                GridToggleMenuItem.IsChecked = false;
                GridToggleMenuItem.Header = "Показать сетку";
            }
        }

        // Метод переключения привязки к сетке
        private void ToggleSnapToGrid()
        {
            isSnapToGridEnabled = !isSnapToGridEnabled;

            if (isSnapToGridEnabled)
            {
                SnapToGridMenuItem.IsChecked = true;
                SnapToGridMenuItem.Header = "Отключить привязку";

                // Автоматически включаем сетку если она выключена
                if (!isGridEnabled)
                {
                    ToggleGridVisibility();
                }
            }
            else
            {
                SnapToGridMenuItem.IsChecked = false;
                SnapToGridMenuItem.Header = "Привязка к сетке";
            }
        }

        // Метод для привязки точки к сетке
        private Point SnapToGrid(Point originalPoint)
        {
            if (!isSnapToGridEnabled) return originalPoint;

            double snappedX = Math.Round(originalPoint.X / GridSize) * GridSize;
            double snappedY = Math.Round(originalPoint.Y / GridSize) * GridSize;

            return new Point(snappedX, snappedY);
        }

        // Обновите метод создания копий фигур для привязки к сетке
        private void CreateShapeCopyOnCanvas(Point position)
        {
            // Применяем привязку к сетке если включена
            if (isSnapToGridEnabled)
            {
                position = SnapToGrid(position);
            }

            UIElement newShape = null;

            // Определяем тип фигуры и создаем копию с оригинальными цветами
            if (draggedElement == Circle)
            {
                newShape = CreateCircleCopy(position);
            }
            else if (draggedElement == SimpleRing)
            {
                newShape = CreateRingCopy(position);
            }
            else if (draggedElement == Square)
            {
                newShape = CreateSquareCopy(position);
            }
            else if (draggedElement == ResizableSquare)
            {
                newShape = CreateDiamondCopy(position);
            }
            else if (draggedElement == Line)
            {
                newShape = CreateLineCopy(position);
            }

            if (newShape != null)
            {
                MainCanvas.Children.Add(newShape);
                MakeCanvasShapeDraggable(newShape);
            }
        }

        // Обновите метод перемещения фигур на канвасе
        private void CanvasShape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = draggedElement;
                Point currentPosition = e.GetPosition(MainCanvas);

                // Применяем привязку к сетке если включена
                if (isSnapToGridEnabled)
                {
                    currentPosition = SnapToGrid(currentPosition);
                }

                double newLeft = currentPosition.X - offset.X;
                double newTop = currentPosition.Y - offset.Y;

                Canvas.SetLeft(element, newLeft);
                Canvas.SetTop(element, newTop);
            }
        }

        // Обновляем сетку при изменении размера окна
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (isGridEnabled && gridPath != null)
            {
                // Небольшая задержка для корректного обновления размеров
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CreateGrid();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        // Обновляем сетку когда канвас полностью загружен и проинициализирован
        private void MainCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (isGridEnabled)
            {
                CreateGrid();
            }
        }

        // Добавьте горячие клавиши для удобства (опционально)
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.G && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ToggleGridVisibility();
                e.Handled = true;
            }
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ToggleSnapToGrid();
                e.Handled = true;
            }
        }

        // File menu handlers
        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Новый файл");
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Открыть файл");
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Сохранить файл");
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Сохранить как");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // View menu handlers
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Увеличить");
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Уменьшить");
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Сбросить масштаб");
        }

        // Edit menu handlers
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Отменить");
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Повторить");
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вырезать");
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Копировать");
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вставить");
        }

        // Help menu handlers
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Справка");
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("О программе");
        }

        // Window control buttons
        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Window dragging
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeBtn_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }

        private void MakeAllShapesDraggable()
        {
            // Добавляем обработчики для всех фигур в нотации
            AddDragHandlers(Circle);
            AddDragHandlers(SimpleRing);
            AddDragHandlers(Square);
            AddDragHandlers(ResizableSquare);
            AddDragHandlers(Line);
        }

        private void AddDragHandlers(UIElement element)
        {
            if (element == null) return;

            element.MouseLeftButtonDown += Shape_MouseLeftButtonDown;
            element.MouseMove += Shape_MouseMove;
            element.MouseLeftButtonUp += Shape_MouseLeftButtonUp;

            // Устанавливаем курсор руку для всех элементов
            if (element is FrameworkElement fe)
            {
                fe.Cursor = Cursors.Hand;
            }
        }

        private void Shape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                isDragging = true;
                draggedElement = element;
                element.CaptureMouse();

                // Сохраняем оригинальный трансформ
                originalTransform = element.RenderTransform;

                // Получаем позицию относительно MainCanvas для правильного позиционирования
                dragStartPoint = e.GetPosition(MainCanvas);
                offset = e.GetPosition(element);

                // Визуальный фидбэк при захвате
                ApplyDragVisualFeedback(element, true);

                e.Handled = true;
            }
        }

        private void Shape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = draggedElement;

                // Получаем текущую позицию мыши относительно MainCanvas
                Point currentPosition = e.GetPosition(MainCanvas);

                // Проверяем, вышли ли мы за пределы нотации
                if (IsOverMainCanvas(currentPosition))
                {
                    // Создаем копию фигуры на MainCanvas
                    CreateShapeCopyOnCanvas(currentPosition);
                    isDragging = false;
                    element.ReleaseMouseCapture();

                    // Возвращаем исходный вид оригинальной фигуре
                    ApplyDragVisualFeedback(element, false);

                    // Сбрасываем оригинальный трансформ у оригинальной фигуры
                    element.RenderTransform = originalTransform;

                    draggedElement = null;
                }
                else
                {
                    // Перемещаем оригинальную фигуру внутри нотации
                    MoveShapeWithinNotation(element, currentPosition);
                }
            }
        }

        private void Shape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                isDragging = false;
                element.ReleaseMouseCapture();
                // Возвращаем исходный вид
                ApplyDragVisualFeedback(element, false);

                // Сбрасываем трансформ если не создали копию
                if (draggedElement == element)
                {
                    element.RenderTransform = originalTransform;
                }

                draggedElement = null;
                e.Handled = true;
            }
        }

        private bool IsOverMainCanvas(Point position)
        {
            // Проверяем, находится ли позиция над MainCanvas
            return position.X > 0 && position.Y > 0 &&
                   position.X < MainCanvas.ActualWidth &&
                   position.Y < MainCanvas.ActualHeight;
        }

        private void MoveShapeWithinNotation(UIElement element, Point currentPosition)
        {
            // Вычисляем смещение от начальной точки
            double deltaX = currentPosition.X - dragStartPoint.X;
            double deltaY = currentPosition.Y - dragStartPoint.Y;

            // Создаем трансформ для перемещения
            var translateTransform = new TranslateTransform(deltaX, deltaY);

            // Создаем новую группу трансформов
            var newTransform = new TransformGroup();

            // Добавляем оригинальные трансформы (если есть)
            if (originalTransform != null)
            {
                if (originalTransform is TransformGroup originalGroup)
                {
                    foreach (var transform in originalGroup.Children)
                    {
                        newTransform.Children.Add(transform);
                    }
                }
                else
                {
                    newTransform.Children.Add(originalTransform);
                }
            }

            // Добавляем трансформ перемещения
            newTransform.Children.Add(translateTransform);

            // Применяем к элементу
            element.RenderTransform = newTransform;
        }

        private Ellipse CreateCircleCopy(Point position)
        {
            var circle = new Ellipse
            {
                Width = 110,
                Height = 110,
                Fill = Brushes.White, // Оригинальный цвет из нотации
                Stroke = Brushes.Black, // Оригинальный цвет из нотации
                StrokeThickness = 3, // Оригинальная толщина
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(circle, position.X - 55);
            Canvas.SetTop(circle, position.Y - 55);

            return circle;
        }

        private Ellipse CreateRingCopy(Point position)
        {
            var ring = new Ellipse
            {
                Width = 110,
                Height = 110,
                Stroke = Brushes.White, // Оригинальный цвет из нотации
                StrokeThickness = 5, // Оригинальная толщина
                Fill = Brushes.Transparent, // Оригинальный цвет из нотации
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(ring, position.X - 55);
            Canvas.SetTop(ring, position.Y - 55);
            return ring;
        }

        private Border CreateSquareCopy(Point position)
        {
            var square = new Border
            {
                Width = 100,
                Height = 100,
                Background = new SolidColorBrush(Color.FromRgb(52, 52, 52)), // Оригинальный цвет #343434
                BorderBrush = Brushes.White, // Оригинальный цвет из нотации
                BorderThickness = new Thickness(2), // Оригинальная толщина
                CornerRadius = new CornerRadius(0),
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(square, position.X - 50);
            Canvas.SetTop(square, position.Y - 50);

            return square;
        }

        private Border CreateDiamondCopy(Point position)
        {
            var diamond = new Border
            {
                Width = 70,
                Height = 71,
                Background = new SolidColorBrush(Color.FromRgb(52, 52, 52)), // Оригинальный цвет #343434
                BorderBrush = Brushes.White, // Оригинальный цвет из нотации
                BorderThickness = new Thickness(2), // Оригинальная толщина
                CornerRadius = new CornerRadius(0),
                Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            diamond.RenderTransform = new RotateTransform(45.354); // Оригинальный угол вращения

            Canvas.SetLeft(diamond, position.X - 35);
            Canvas.SetTop(diamond, position.Y - 35);

            return diamond;
        }

        private Line CreateLineCopy(Point position)
        {
            var line = new Line
            {
                X1 = position.X - 25,
                Y1 = position.Y,
                X2 = position.X + 25,
                Y2 = position.Y,
                Stroke = Brushes.White, // Оригинальный цвет из нотации
                StrokeThickness = 3, // Оригинальная толщина
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Cursor = Cursors.Hand
            };

            // Применяем оригинальное вращение линии
            line.RenderTransform = new RotateTransform(93.201); // Оригинальный угол

            return line;
        }

        private void MakeCanvasShapeDraggable(UIElement shape)
        {
            shape.MouseLeftButtonDown += CanvasShape_MouseLeftButtonDown;
            shape.MouseMove += CanvasShape_MouseMove;
            shape.MouseLeftButtonUp += CanvasShape_MouseLeftButtonUp;
        }

        private void CanvasShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                isDragging = true;
                draggedElement = element;
                element.CaptureMouse();

                offset = e.GetPosition(element);

                // Визуальный фидбэк
                ApplyCanvasDragVisualFeedback(element, true);

                e.Handled = true;
            }
        }

        private void CanvasShape_MouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                isDragging = false;
                element.ReleaseMouseCapture();
                draggedElement = null;

                // Возвращаем исходный вид
                ApplyCanvasDragVisualFeedback(element, false);
                e.Handled = true;
            }
        }

        private void ApplyDragVisualFeedback(UIElement element, bool isDragging)
        {
            if (isDragging)
            {
                element.Opacity = 0.7;
            }
            else
            {
                element.Opacity = 1.0;
            }
        }

        private void ApplyCanvasDragVisualFeedback(UIElement element, bool isDragging)
        {
            if (isDragging)
            {
                if (element is Ellipse ellipse)
                {
                    ellipse.Stroke = Brushes.Red;
                    ellipse.StrokeThickness += 2;
                }
                else if (element is Border border)
                {
                    border.BorderBrush = Brushes.Red;
                    border.BorderThickness = new Thickness(4);
                }
                else if (element is Line line)
                {
                    line.Stroke = Brushes.Red;
                    line.StrokeThickness += 2;
                }
            }
            else
            {
                // Возвращаем оригинальные цвета из нотации
                if (element is Ellipse ellipse)
                {
                    if (ellipse.Fill == Brushes.Transparent) // Кольцо
                    {
                        ellipse.Stroke = Brushes.White;
                    }
                    else // Круг
                    {
                        ellipse.Stroke = Brushes.Black;
                    }
                    ellipse.StrokeThickness -= 2;
                }
                else if (element is Border border)
                {
                    border.BorderBrush = Brushes.White;
                    border.BorderThickness = new Thickness(2);
                }
                else if (element is Line line)
                {
                    line.Stroke = Brushes.White;
                    line.StrokeThickness -= 2;
                }
            }
        }
        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // закрытие окна
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}