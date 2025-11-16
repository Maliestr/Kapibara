using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Xml;
using System.Xml.Linq;

namespace Graphic_Editor
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private UIElement draggedElement;
        private Point offset;
        private UIElement shapeCopy;
        private Transform originalTransform;
        private Point dragStartPoint;
        private string currentFilePath = null;
        private List<UIElement> canvasElements = new List<UIElement>();

        private FrameworkElement selectedElement;
        private FrameworkElement copiedElement;
        private Point lastMousePosition;
        private Stack<UIElement> undoStack = new Stack<UIElement>();
        private Stack<UIElement> redoStack = new Stack<UIElement>();

        private bool isGridEnabled = false;
        private bool isSnapToGridEnabled = false;
        private System.Windows.Shapes.Path gridPath;
        private const double GridSize = 20; // Размер ячейки сетки
        private bool isTextModeActive = false;
        private TextBox textBox;

        private Dictionary<UIElement, Brush> originalStrokeColors = new Dictionary<UIElement, Brush>();
        private Dictionary<UIElement, Brush> originalFillColors = new Dictionary<UIElement, Brush>();
        private UIElement selectedShape;

        private bool arePanelsVisible = true;

        // Список всех плашек для управления видимостью
        private List<Border> panels = new List<Border>();

        private bool isCommentModeActive = false;
        private List<Comment> comments = new List<Comment>();

        public class Comment
        {
            public UIElement VisualElement { get; set; }
            public UIElement TextElement { get; set; }
            public string Text { get; set; }
            public Point Position { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            MakeAllShapesDraggable();

            InitializeGrid(); // Инициализируем сетку
            InitializePanels();

            // Обработка горячих клавиш
            this.KeyDown += MainWindow_KeyDown;

            // Добавляем обработчик клика по канвасу для сброса выделения
            MainCanvas.MouseLeftButtonDown += MainCanvas_MouseLeftButtonDown;

            // Очищаем комбо-боксы при запуске
            ClearColorComboBoxesOnStartup();

            // Добавляем обработчики для комментариев
            CommentModeButton.Click += CommentModeButton_Click;
        }

        // Обработчик клика по пустой области канваса
        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isCommentModeActive)
            {
                Point clickPosition = e.GetPosition(MainCanvas);

                // Проверяем, что клик не по другим элементам
                if (e.OriginalSource is System.Windows.Controls.Canvas)
                {
                    
                    e.Handled = true;
                    return; // Выходим, чтобы не выполнять остальной код
                }
            }
            // Проверяем, был ли клик непосредственно по канвасу, а не по дочернему элементу
            if (e.OriginalSource is System.Windows.Controls.Canvas)
            {
                DeselectAll();
                e.Handled = true;
            }
        }

        // Инициализация сетки
        private void InitializeGrid()
        {
            gridPath = new System.Windows.Shapes.Path
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
                // ОГРАНИЧИВАЕМ ПЕРЕМЕЩЕНИЕ РАБОЧЕЙ ОБЛАСТЬЮ
                double elementWidth = GetElementWidth(element);
                double elementHeight = GetElementHeight(element);

                // Вычисляем границы рабочей области
                
                double maxTop = MainCanvas.ActualHeight - elementHeight - WorkAreaBottom;

                // Ограничиваем координаты
                newLeft = Math.Max(0, Math.Min(newLeft, MainCanvas.ActualWidth - elementWidth)); newTop = Math.Max(WorkAreaTop, Math.Min(newTop, maxTop));
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
            else if (e.Key == Key.F12)
            {
                TogglePanelsVisibility();
                e.Handled = true;
            }
            else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+H - альтернативная горячая клавиша
                TogglePanelsVisibility();
                e.Handled = true;
            }
        }

        // Меню "Файл"
        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            // Очищаем холст
            MainCanvas.Children.Clear();
            canvasElements.Clear();
            currentFilePath = null;
            UpdateWindowTitle();
            MessageBox.Show("Создан новый файл", "Новый файл", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Graphic Editor Files (*.ged)|*.ged|All files (*.*)|*.*",
                Title = "Открыть файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    LoadFromFile(openFileDialog.FileName);
                    currentFilePath = openFileDialog.FileName;
                    UpdateWindowTitle();
                    MessageBox.Show("Файл успешно открыт", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveAsFile_Click(sender, e);
            }
            else
            {
                try
                {
                    SaveToFile(currentFilePath);
                    MessageBox.Show("Файл успешно сохранен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Graphic Editor Files (*.ged)|*.ged|All files (*.*)|*.*",
                Title = "Сохранить как",
                DefaultExt = ".ged"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    SaveToFile(saveFileDialog.FileName);
                    currentFilePath = saveFileDialog.FileName;
                    UpdateWindowTitle();
                    MessageBox.Show("Файл успешно сохранен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportToImage_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|All files (*.*)|*.*",
                Title = "Экспорт в изображение",
                DefaultExt = ".png"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExportCanvasToImage(saveFileDialog.FileName);
                    MessageBox.Show("Изображение успешно экспортировано", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Метод инициализации панелей
        private void InitializePanels()
        {
            // Находим все панели по именам
            var notationCard = FindName("NotationCard") as Border; //вопросы о панелях
            var dimensionsPanel = FindName("DimensionsPanel") as Border;
            var fillPanel = FindName("FillPanel") as Border;
            var strokePanel = FindName("StrokePanel") as Border;
            var textPanel = FindName("TextPanel") as Border;
            //var filePanel = FindName("Titl") as Border;

            // Добавляем панели в список
            if (notationCard != null) panels.Add(notationCard);
            if (dimensionsPanel != null) panels.Add(dimensionsPanel);
            if (fillPanel != null) panels.Add(fillPanel);
            if (strokePanel != null) panels.Add(strokePanel);
            if (textPanel != null) panels.Add(textPanel);
            //if (filePanel != null) panels.Add(filePanel);
        }

        // Обработчик кнопки скрытия/показа панелей
        private void TogglePanelsButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePanelsVisibility();
        }

        // Основной метод переключения видимости панелей
        private void TogglePanelsVisibility()
        {
            arePanelsVisible = !arePanelsVisible;

            if (arePanelsVisible)
            {
                ShowAllPanels();
            }
            else
            {
                HideAllPanels();
            }
        }

        // Скрытие всех панелей
        private void HideAllPanels()
        {
            foreach (var panel in panels)
            {
                if (panel != null)
                {
                    panel.Visibility = Visibility.Collapsed;
                }
            }

            // Также скрываем нижнюю панель инструментов
            var bottomToolbar = FindName("BottomToolbar") as Border;
            if (bottomToolbar != null)
            {
                bottomToolbar.Visibility = Visibility.Collapsed;
            }
        }

        // Показать все панели
        private void ShowAllPanels()
        {
            foreach (var panel in panels)
            {
                if (panel != null)
                {
                    panel.Visibility = Visibility.Visible;
                }
            }

            // Показываем нижнюю панель инструментов
            var bottomToolbar = FindName("BottomToolbar") as Border;
            if (bottomToolbar != null)
            {
                bottomToolbar.Visibility = Visibility.Visible;
            }
        }



        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Методы файловой системы
        private void SaveToFile(string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("GEDv1.0"); // Заголовок файла

                foreach (UIElement element in MainCanvas.Children)
                {
                    if (element is Ellipse ellipse)
                    {
                        double left = Canvas.GetLeft(ellipse);
                        double top = Canvas.GetTop(ellipse);
                        string fillColor = (ellipse.Fill as SolidColorBrush)?.Color.ToString() ?? "Transparent";
                        string strokeColor = (ellipse.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
                        double strokeThickness = ellipse.StrokeThickness;

                        writer.WriteLine($"ELLIPSE|{left}|{top}|{ellipse.Width}|{ellipse.Height}|{fillColor}|{strokeColor}|{strokeThickness}");
                    }
                    else if (element is Border border)
                    {
                        double left = Canvas.GetLeft(border);
                        double top = Canvas.GetTop(border);
                        string bgColor = (border.Background as SolidColorBrush)?.Color.ToString() ?? "Transparent";
                        string borderColor = (border.BorderBrush as SolidColorBrush)?.Color.ToString() ?? "Black";
                        double borderThickness = border.BorderThickness.Left;

                        // Определяем тип Border (квадрат или ромб)
                        string type = "SQUARE";
                        if (border.RenderTransform is RotateTransform rotateTransform && Math.Abs(rotateTransform.Angle - 45.354) < 0.1)
                        {
                            type = "DIAMOND";
                        }

                        writer.WriteLine($"{type}|{left}|{top}|{border.Width}|{border.Height}|{bgColor}|{borderColor}|{borderThickness}");
                    }
                    else if (element is Line line)
                    {
                        string strokeColor = (line.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
                        double strokeThickness = line.StrokeThickness;
                        double rotateAngle = 0;

                        if (line.RenderTransform is RotateTransform rotateTransform)
                        {
                            rotateAngle = rotateTransform.Angle;
                        }

                        writer.WriteLine($"LINE|{line.X1}|{line.Y1}|{line.X2}|{line.Y2}|{strokeColor}|{strokeThickness}|{rotateAngle}");
                    }
                }
            }
        }

        private void LoadFromFile(string filePath)
        {
            MainCanvas.Children.Clear();
            canvasElements.Clear();

            using (StreamReader reader = new StreamReader(filePath))
            {
                string header = reader.ReadLine();
                if (header != "GEDv1.0")
                {
                    throw new Exception("Неверный формат файла");
                }

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 2) continue;

                    switch (parts[0])
                    {
                        case "ELLIPSE":
                            CreateEllipseFromData(parts);
                            break;
                        case "SQUARE":
                            CreateSquareFromData(parts);
                            break;
                        case "DIAMOND":
                            CreateDiamondFromData(parts);
                            break;
                        case "LINE":
                            CreateLineFromData(parts);
                            break;
                    }
                }
            }
        }

        private void CreateEllipseFromData(string[] data)
        {
            var ellipse = new Ellipse
            {
                Width = double.Parse(data[3]),
                Height = double.Parse(data[4]),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data[5])),
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data[6])),
                StrokeThickness = double.Parse(data[7]),
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(ellipse, double.Parse(data[1]));
            Canvas.SetTop(ellipse, double.Parse(data[2]));
            MainCanvas.Children.Add(ellipse);
            MakeCanvasShapeDraggable(ellipse);
        }

        private void CreateSquareFromData(string[] data)
        {
            var square = new Border
            {
                Width = double.Parse(data[3]),
                Height = double.Parse(data[4]),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data[5])),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data[6])),
                BorderThickness = new Thickness(double.Parse(data[7])),
                CornerRadius = new CornerRadius(0),
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(square, double.Parse(data[1]));
            Canvas.SetTop(square, double.Parse(data[2]));
            MainCanvas.Children.Add(square);
            MakeCanvasShapeDraggable(square);
        }

        private void CreateDiamondFromData(string[] data)
        {
            var diamond = new Border
            {
                Width = double.Parse(data[3]),
                Height = double.Parse(data[4]),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data[5])),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data[6])),
                BorderThickness = new Thickness(double.Parse(data[7])),
                CornerRadius = new CornerRadius(0),
                Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            diamond.RenderTransform = new RotateTransform(45.354);
            Canvas.SetLeft(diamond, double.Parse(data[1]));
            Canvas.SetTop(diamond, double.Parse(data[2]));
            MainCanvas.Children.Add(diamond);
            MakeCanvasShapeDraggable(diamond);
        }

        private void CreateLineFromData(string[] data)
        {
            var line = new Line
            {
                X1 = double.Parse(data[1]),
                Y1 = double.Parse(data[2]),
                X2 = double.Parse(data[3]),
                Y2 = double.Parse(data[4]),
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data[5])),
                StrokeThickness = double.Parse(data[6]),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Cursor = Cursors.Hand
            };

            line.RenderTransform = new RotateTransform(double.Parse(data[7]));
            MainCanvas.Children.Add(line);
            MakeCanvasShapeDraggable(line);
        }

        private void ExportCanvasToImage(string filePath)
        {
            // Создаем RenderTargetBitmap с размерами холста
            var renderTargetBitmap = new RenderTargetBitmap(
                (int)MainCanvas.ActualWidth,
                (int)MainCanvas.ActualHeight,
                96, 96, PixelFormats.Pbgra32);

            renderTargetBitmap.Render(MainCanvas);

            // Создаем encoder в зависимости от расширения файла
            BitmapEncoder encoder;
            string extension = System.IO.Path.GetExtension(filePath).ToLower();

            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    encoder = new JpegBitmapEncoder();
                    break;
                case ".png":
                default:
                    encoder = new PngBitmapEncoder();
                    break;
            }

            encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

            // Сохраняем файл
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }

        private void UpdateWindowTitle()
        {
            string fileName = string.IsNullOrEmpty(currentFilePath) ? "Новый файл" : System.IO.Path.GetFileName(currentFilePath);
            this.Title = $"Graphic Editor - {fileName}";
        }

        // Меню "Вид"
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

        // Меню "Правка" - Обновленные
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            UndoLastAction();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            RedoLastAction();
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            CutSelectedElement();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedElement();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            PasteElement();
        }

        // Меню "Справка"
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Справка");
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("О программе");
        }

        // Кнопоки управления окном
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

        // Перетаскивание окна за заголовок
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

        // Новые методы для работы с фигурами

        // Обработка горячих клавиш
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        UndoLastAction();
                        e.Handled = true;
                        break;
                    case Key.Y:
                        RedoLastAction();
                        e.Handled = true;
                        break;
                    case Key.X:
                        CutSelectedElement();
                        e.Handled = true;
                        break;
                    case Key.C:
                        CopySelectedElement();
                        e.Handled = true;
                        break;
                    case Key.V:
                        PasteElement();
                        e.Handled = true;
                        break;
                    case Key.A:
                        SelectAllElements();
                        e.Handled = true;
                        break;
                }
            }

            // Удаление выделенного элемента по клавише Delete
            if (e.Key == Key.Delete && selectedElement != null)
            {
                DeleteSelectedElement();
                e.Handled = true;
            }
        }

        // Контекстное меню для фигур
        private void CreateContextMenu(FrameworkElement element)
        {
            ContextMenu contextMenu = new ContextMenu
            {
                Background = Brushes.White,
                Foreground = Brushes.Black,
                FontSize = 12,
                Padding = new Thickness(5)
            };

            // Пункт "Вырезать"
            MenuItem cutMenuItem = new MenuItem
            {
                Header = "Вырезать (Ctrl+X)",
                Icon = new TextBlock { Text = "✂", FontSize = 14 }
            };
            cutMenuItem.Click += (s, e) => CutSelectedElement();
            contextMenu.Items.Add(cutMenuItem);

            // Пункт "Копировать"
            MenuItem copyMenuItem = new MenuItem
            {
                Header = "Копировать (Ctrl+C)",
                Icon = new TextBlock { Text = "📄", FontSize = 14 }
            };
            copyMenuItem.Click += (s, e) => CopySelectedElement();
            contextMenu.Items.Add(copyMenuItem);

            // Пункт "Вставить"
            MenuItem pasteMenuItem = new MenuItem
            {
                Header = "Вставить (Ctrl+V)",
                Icon = new TextBlock { Text = "📋", FontSize = 14 }
            };
            pasteMenuItem.Click += (s, e) => PasteElement();
            contextMenu.Items.Add(pasteMenuItem);

            contextMenu.Items.Add(new Separator());

            // Пункт "Удалить"
            MenuItem deleteMenuItem = new MenuItem
            {
                Header = "Удалить (Delete)",
                Icon = new TextBlock { Text = "🗑", FontSize = 14 }
            };
            deleteMenuItem.Click += (s, e) => DeleteSelectedElement();
            contextMenu.Items.Add(deleteMenuItem);

            // Устанавливаем контекстное меню для элемента
            element.ContextMenu = contextMenu;
        }

        // Выделение элемента
        private void SelectElement(FrameworkElement element)
        {
            // Снимаем выделение с предыдущего элемента
            DeselectAll();

            selectedElement = element;
            ApplySelectionVisualFeedback(element, true);

            // Создаем контекстное меню для выделенного элемента
            CreateContextMenu(element);

            // Обновляем комбо-боксы в соответствии с цветами фигуры
            UpdateColorComboBoxesFromShape(element);
        }

        // Снятие выделения со всех элементов
        private void DeselectAll()
        {
            if (selectedElement != null)
            {
                ApplySelectionVisualFeedback(selectedElement, false);
                if (selectedElement is FrameworkElement frameworkElement)
                {
                    frameworkElement.ContextMenu = null;
                    Canvas.SetZIndex(frameworkElement, 1);
                }
                selectedElement = null;
            }

            if (selectedShape != null)
            {
                RemoveSelectionVisual(selectedShape);
                if (selectedShape is FrameworkElement shapeFrameworkElement)
                {
                    Canvas.SetZIndex(shapeFrameworkElement, 1);
                }
                selectedShape = null;
            }
            ClearColorComboBoxes();
        }
        private void ClearColorComboBoxes()
        {
            FillColorComboBox.SelectedIndex = -1;
            StrokeColorComboBox.SelectedIndex = -1;
            FillColorPreview.Background = Brushes.Transparent;
            StrokeColorPreview.Background = Brushes.Transparent;
        }
        // Визуальное выделение элемента
        private const double WorkAreaTop = 50;    // Отступ сверху (под меню)
        private const double WorkAreaBottom = 100; // Отступ снизу (над панелью инструментов)
        private void ApplySelectionVisualFeedback(UIElement element, bool isSelected)
        {
            // делаем фул ширину константной 
            if (isSelected)
            {
                if (element is Ellipse ellipse)
                {
                    ellipse.Stroke = Brushes.Blue;
                    
                }
                else if (element is Border border)
                {
                    border.BorderBrush = Brushes.Blue;
                    
                }
                else if (element is Line line)
                {
                    line.Stroke = Brushes.Blue;
                    
                }
                else if (element is TextBlock textBlock)
                {
                    textBlock.Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 255));
                }
            }
            else
            {
                // Возвращаем оригинальные цвета
                if (element is Ellipse ellipse)
                {
                    if (ellipse.Fill == Brushes.Transparent) // Кольцо
                    {
                        ellipse.Stroke = Brushes.White;
                        ellipse.StrokeThickness = 5;
                    }
                    else // Круг
                    {
                        ellipse.Stroke = Brushes.Black;
                        ellipse.StrokeThickness = 3;
                    }
                    ellipse.StrokeThickness = Math.Max(ellipse.StrokeThickness - 2, 1);
                }
                else if (element is Border border)
                {
                    border.BorderBrush = Brushes.White;
                    border.BorderThickness = new Thickness(2);

                }
                else if (element is Line line)
                {
                    line.Stroke = Brushes.White;
                    line.StrokeThickness = 3;
                }
                else if (element is TextBlock textBlock)
                {
                    textBlock.Background = Brushes.Transparent;
                }
            }
        }

        // Копирование выделенного элемента
        private void CopySelectedElement()
        {
            if (selectedElement != null)
            {
                copiedElement = CloneUIElement(selectedElement);
                MessageBox.Show("Элемент скопирован", "Копирование", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Сначала выделите элемент", "Копирование", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Вырезание выделенного элемента
        private void CutSelectedElement()
        {
            if (selectedElement != null)
            {
                // Сохраняем копию для буфера обмена
                copiedElement = CloneUIElement(selectedElement);

                // Сохраняем в стек отмены
                SaveToUndoStack(selectedElement);

                // Удаляем элемент
                MainCanvas.Children.Remove(selectedElement);
                canvasElements.Remove(selectedElement);

                DeselectAll();
                MessageBox.Show("Элемент вырезан", "Вырезание", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Сначала выделите элемент", "Вырезание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Вставка элемента
        private void PasteElement()
        {
            if (copiedElement != null)
            {
                // Создаем копию скопированного элемента со смещением
                UIElement newElement = CloneUIElement(copiedElement);

                // Смещаем новую копию на 20 пикселей
                double currentLeft = Canvas.GetLeft(newElement);
                double currentTop = Canvas.GetTop(newElement);

                Canvas.SetLeft(newElement, currentLeft + 20);
                Canvas.SetTop(newElement, currentTop + 20);

                // Добавляем на холст
                MainCanvas.Children.Add(newElement);
                canvasElements.Add(newElement);
                MakeCanvasShapeDraggable(newElement);

                // Выделяем вставленный элемент
                SelectElement(newElement as FrameworkElement);

                // Сохраняем в стек отмены
                SaveToUndoStack(newElement);

                MessageBox.Show("Элемент вставлен", "Вставка", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Нет скопированного элемента", "Вставка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Удаление выделенного элемента
        private void DeleteSelectedElement()
        {
            if (selectedElement != null)
            {
                // Сохраняем в стек отмены
                SaveToUndoStack(selectedElement);

                MainCanvas.Children.Remove(selectedElement);
                canvasElements.Remove(selectedElement);

                DeselectAll();
                MessageBox.Show("Элемент удален", "Удаление", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Сначала выделите элемент", "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Клонирование UI элемента
        private FrameworkElement CloneUIElement(UIElement original)
        {
            if (original is Ellipse originalEllipse)
            {
                var newEllipse = new Ellipse
                {
                    Width = originalEllipse.Width,
                    Height = originalEllipse.Height,
                    Fill = originalEllipse.Fill,
                    Stroke = originalEllipse.Stroke,
                    StrokeThickness = originalEllipse.StrokeThickness,
                    Cursor = Cursors.Hand
                };

                Canvas.SetLeft(newEllipse, Canvas.GetLeft(originalEllipse));
                Canvas.SetTop(newEllipse, Canvas.GetTop(originalEllipse));

                return newEllipse;
            }
            else if (original is Border originalBorder)
            {
                var newBorder = new Border
                {
                    Width = originalBorder.Width,
                    Height = originalBorder.Height,
                    Background = originalBorder.Background,
                    BorderBrush = originalBorder.BorderBrush,
                    BorderThickness = originalBorder.BorderThickness,
                    CornerRadius = originalBorder.CornerRadius,
                    Cursor = Cursors.Hand
                };

                // Копируем трансформации
                if (originalBorder.RenderTransform is RotateTransform originalRotate)
                {
                    newBorder.RenderTransform = new RotateTransform(originalRotate.Angle);
                    newBorder.RenderTransformOrigin = new Point(0.5, 0.5);
                }

                // Копируем позицию
                Canvas.SetLeft(newBorder, Canvas.GetLeft(originalBorder));
                Canvas.SetTop(newBorder, Canvas.GetTop(originalBorder));

                return newBorder;
            }
            else if (original is Line originalLine)
            {
                var newLine = new Line
                {
                    X1 = originalLine.X1,
                    Y1 = originalLine.Y1,
                    X2 = originalLine.X2,
                    Y2 = originalLine.Y2,
                    Stroke = originalLine.Stroke,
                    StrokeThickness = originalLine.StrokeThickness,
                    StrokeStartLineCap = originalLine.StrokeStartLineCap,
                    StrokeEndLineCap = originalLine.StrokeEndLineCap,
                    Cursor = Cursors.Hand
                };

                // Копируем трансформации
                if (originalLine.RenderTransform is RotateTransform originalRotate)
                {
                    newLine.RenderTransform = new RotateTransform(originalRotate.Angle);
                }

                return newLine;
            }

            return null;
        }

        // Отмена последнего действия
        private void UndoLastAction()
        {
            if (undoStack.Count > 0)
            {
                DeselectAll();
                UIElement element = undoStack.Pop();

                if (MainCanvas.Children.Contains(element))
                {
                    // Если элемент на холсте - удаляем его
                    MainCanvas.Children.Remove(element);
                    canvasElements.Remove(element);
                    redoStack.Push(element);

                    
                }
                else
                {
                    // Если элемента нет на холсте - восстанавливаем его
                    MainCanvas.Children.Add(element);
                    canvasElements.Add(element);
                    MakeCanvasShapeDraggable(element);
                    redoStack.Push(element);
                    RestoreOriginalAppearance(element);
                }
            }
            else
            {
                MessageBox.Show("Нет действий для отмены", "Отмена", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Повтор последнего действия
        private void RedoLastAction()
        {
            if (redoStack.Count > 0)
            {
                DeselectAll();
                UIElement element = redoStack.Pop();

                if (MainCanvas.Children.Contains(element))
                {
                    MainCanvas.Children.Remove(element);
                    canvasElements.Remove(element);
                    undoStack.Push(element);
                    
                    
                }
                else
                {
                    MainCanvas.Children.Add(element);
                    canvasElements.Add(element);
                    MakeCanvasShapeDraggable(element);
                    undoStack.Push(element);
                    RestoreOriginalAppearance(element);
                }
            }
            else
            {
                MessageBox.Show("Нет действий для повтора", "Повтор", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void RestoreOriginalAppearance(UIElement element)
        {
            if (element is Ellipse ellipse)
            {
                // Возвращаем фиксированную толщину обводки
                if (element == SimpleRing)
                {
                    ellipse.StrokeThickness = 5; // ФИКСИРОВАННАЯ для кольца
                }
                else
                {
                    ellipse.StrokeThickness = 3; // ФИКСИРОВАННАЯ для круга и овала
                }

                // Возвращаем оригинальные цвета
                if (ellipse.Fill == Brushes.Transparent) // Кольцо
                {
                    ellipse.Stroke = Brushes.White;
                }
                else // Круг
                {
                    ellipse.Stroke = Brushes.Black;
                }
                ellipse.Opacity = 1.0;
            }
            else if (element is Border border)
            {
                border.BorderBrush = Brushes.White;
                border.BorderThickness = new Thickness(2); // ФИКСИРОВАННАЯ толщина
                border.Opacity = 1.0;
            }
            else if (element is Line line)
            {
                line.Stroke = Brushes.White;
                line.StrokeThickness = 3; // ФИКСИРОВАННАЯ толщина
                line.Opacity = 1.0;
            }
            else if (element is TextBlock textBlock)
            {
                textBlock.Background = Brushes.Transparent;
                textBlock.Opacity = 1.0;
            }

            // Возвращаем нормальный ZIndex
            Canvas.SetZIndex(element, 1);
        }
        // Сохранение в стек отмены
        private void SaveToUndoStack(UIElement element)
        {
            UIElement elementCopy = CloneUIElement(element);
            SaveOriginalColorsForCopy(element, elementCopy);
            undoStack.Push(CloneUIElement(element));
            // Очищаем стек повтора при новом действии
            redoStack.Clear();
        }
        // Сохранение оригинальных цветов для копии элемента
        private void SaveOriginalColorsForCopy(UIElement original, UIElement copy)
        {
            if (original is Ellipse originalEllipse && copy is Ellipse copyEllipse)
            {
                if (originalStrokeColors.ContainsKey(original))
                {
                    originalStrokeColors[copy] = originalStrokeColors[original];
                }
                else
                {
                    originalStrokeColors[copy] = originalEllipse.Stroke;
                }

                if (originalFillColors.ContainsKey(original))
                {
                    originalFillColors[copy] = originalFillColors[original];
                }
                else
                {
                    originalFillColors[copy] = originalEllipse.Fill;
                }
            }
            else if (original is Border originalBorder && copy is Border copyBorder)
            {
                if (originalStrokeColors.ContainsKey(original))
                {
                    originalStrokeColors[copy] = originalStrokeColors[original];
                }
                else
                {
                    originalStrokeColors[copy] = originalBorder.BorderBrush;
                }

                if (originalFillColors.ContainsKey(original))
                {
                    originalFillColors[copy] = originalFillColors[original];
                }
                else
                {
                    originalFillColors[copy] = originalBorder.Background;
                }
            }
            else if (original is Line originalLine && copy is Line copyLine)
            {
                if (originalStrokeColors.ContainsKey(original))
                {
                    originalStrokeColors[copy] = originalStrokeColors[original];
                }
                else
                {
                    originalStrokeColors[copy] = originalLine.Stroke;
                }
            }
            else if (original is TextBlock originalText && copy is TextBlock copyText)
            {
                if (originalStrokeColors.ContainsKey(original))
                {
                    originalStrokeColors[copy] = originalStrokeColors[original];
                }
                else
                {
                    originalStrokeColors[copy] = originalText.Foreground;
                }
            }
        }
        // Восстановление оригинальных цветов элемента
        private void RestoreOriginalColors(UIElement element)
        {
            if (element is Ellipse ellipse)
            {
                if (originalStrokeColors.ContainsKey(element))
                {
                    ellipse.Stroke = originalStrokeColors[element];
                }
                if (originalFillColors.ContainsKey(element))
                {
                    ellipse.Fill = originalFillColors[element];
                }
            }
            else if (element is Border border)
            {
                if (originalStrokeColors.ContainsKey(element))
                {
                    border.BorderBrush = originalStrokeColors[element];
                }
                if (originalFillColors.ContainsKey(element))
                {
                    border.Background = originalFillColors[element];
                }
            }
            else if (element is Line line)
            {
                if (originalStrokeColors.ContainsKey(element))
                {
                    line.Stroke = originalStrokeColors[element];
                }
            }
            else if (element is TextBlock textBlock)
            {
                if (originalStrokeColors.ContainsKey(element))
                {
                    textBlock.Foreground = originalStrokeColors[element];
                }
            }
        }
        // Выделение всех элементов
        private void SelectAllElements()
        {
            // Пока просто выделяем первый элемент (можно расширить для множественного выделения)
            if (MainCanvas.Children.Count > 0)
            {
                SelectElement(MainCanvas.Children[0] as FrameworkElement);
            }
        }

        //ТЕКСТ

        // Обработчик для кнопки текста на панели инструментов
        private void TextButton_Click(object sender, RoutedEventArgs e)
        {
            if (isTextModeActive) return;
            ActivateTextMode();
        }

        // Активация режима ввода текста
        private void ActivateTextMode()
        {
            isTextModeActive = true;
            // Создаем текстовое поле с настройками
            textBox = new TextBox
            {
                Width = 200,
                Height = 30,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderBrush = Brushes.Gray,
                FontSize = 14,
                Text = "Введите текст...",
                CaretBrush = Brushes.White,
                FocusVisualStyle = null
            };
            // Центрируем текстовое поле на холсте
            double left = Math.Max(MainCanvas.ActualWidth / 2 - 100, 0);
            double top = Math.Max(MainCanvas.ActualHeight / 2 - 15, 0);
            // Устанавливаем позицию на холсте
            Canvas.SetLeft(textBox, left);
            Canvas.SetTop(textBox, top);
            // Добавляем текстовое поле на холст
            MainCanvas.Children.Add(textBox);
            // Устанавливаем фокус и выделяем весь текст для удобства редактирования
            textBox.Focus();
            textBox.SelectAll();
            // Обработчик нажатия клавиш в текстовом поле
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    DeactivateTextMode();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    isTextModeActive = false;
                    MainCanvas.Children.Remove(textBox);
                    textBox = null;
                    e.Handled = true;
                }
            };
            textBox.LostFocus += (s, e) => DeactivateTextMode();
        }

        // Деактивация режима ввода текста
        private void DeactivateTextMode()
        {
            if (textBox == null) return;
            // Сохраняем текст только если он не пустой
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                CreateTextElement(textBox.Text, Canvas.GetLeft(textBox), Canvas.GetTop(textBox));
            }
            // Удаляем временное текстовое поле
            MainCanvas.Children.Remove(textBox);
            textBox = null;
            isTextModeActive = false;
        }

        // Создание текстового элемента с цветом по умолчанию
        private void CreateTextElement(string text, double left, double top)
        {
            CreateTextElementWithColor(text, left, top, Brushes.White);
        }

        // Создание текстового элемента с указанным цветом
        private void CreateTextElementWithColor(string text, double left, double top, Brush color)
        {

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = 14,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null,
                Background = Brushes.Transparent // Явно указываем прозрачный фон
            };
            // Устанавливаем позицию на холсте
            Canvas.SetLeft(textBlock, left);
            Canvas.SetTop(textBlock, top);
            Canvas.SetZIndex(textBlock, 1000);

            // Сохраняем оригинальный цвет для возможного восстановления
            originalStrokeColors[textBlock] = color;

            // Добавляем текстовый элемент на холст
            MainCanvas.Children.Add(textBlock);

            textBlock.MouseLeftButtonDown += TextElement_MouseLeftButtonDown;
            SelectElement(textBlock);
        }

        // Обработчик нажатия левой кнопки мыши на текстовом элементе
        private void TextElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock == null) return;
            // Сохраняем оригинальный цвет если он еще не сохранен
            if (!originalStrokeColors.ContainsKey(textBlock))
            {
                originalStrokeColors[textBlock] = textBlock.Foreground;
            }
            SelectElement(textBlock);
            if (e.ClickCount == 2)
            {
                EditTextElement(textBlock);
                e.Handled = true;
            }
            else
            {
                StartTextDrag(textBlock, e);
            }
        }

        // Начало перемещения текстового элемента
        private void StartTextDrag(UIElement element, MouseButtonEventArgs e)
        {
            // Убедимся, что цвета сохранены перед началом перетаскивания
            if (!originalStrokeColors.ContainsKey(element) && element is TextBlock textBlock)
            {
                originalStrokeColors[element] = textBlock.Foreground;
            }

            isDragging = true;
            draggedElement = element;
            element.CaptureMouse();
            offset = e.GetPosition(element);

            if (element is TextBlock tb)
            {
                tb.Opacity = 0.7;
            }

            element.MouseMove += TextElement_MouseMove;
            element.MouseLeftButtonUp += TextElement_MouseLeftButtonUp;
        }

        // Обработчик перемещения мыши при перетаскивании текстового элемента
        private void TextElement_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(MainCanvas);
                // Вычисляем новую позицию элемента с учетом смещения
                double newLeft = currentPosition.X - offset.X;
                double newTop = currentPosition.Y - offset.Y;

                // Получаем размеры элемента для корректного ограничения перемещения
                double elementWidth = draggedElement.RenderSize.Width;
                double elementHeight = draggedElement.RenderSize.Height;

                if (elementWidth == 0 || double.IsNaN(elementWidth)) elementWidth = 100;
                if (elementHeight == 0 || double.IsNaN(elementHeight)) elementHeight = 30;
                
                double maxTop = MainCanvas.ActualHeight - elementHeight - WorkAreaBottom;

                // Определяем рабочую область 
                double workAreaLeft = 300;
                double workAreaTop = 26;
                double workAreaRight = MainCanvas.ActualWidth - 264;
                double workAreaBottom = MainCanvas.ActualHeight - 100;
                // Ограничиваем перемещение рабочей областью
                newLeft = Math.Max(workAreaLeft, Math.Min(newLeft, workAreaRight - elementWidth));
                newTop = Math.Max(workAreaTop, Math.Min(newTop, workAreaBottom - elementHeight));

                Canvas.SetLeft(draggedElement, newLeft);
                Canvas.SetTop(draggedElement, newTop);
            }
        }

        // Обработчик отпускания левой кнопки мыши при перетаскивании
        private void TextElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Восстанавливаем прозрачность текстового элемента
            if (draggedElement is TextBlock textBlock)
            {
                textBlock.Opacity = 1.0;
            }
            if (draggedElement != null)
            {
                draggedElement.MouseMove -= TextElement_MouseMove;
                draggedElement.MouseLeftButtonUp -= TextElement_MouseLeftButtonUp;
            }
            // Сбрасываем флаги состояния
            isDragging = false;
            draggedElement?.ReleaseMouseCapture();
            draggedElement = null;
        }

        // Редактирование существующего текстового элемента
        private void EditTextElement(TextBlock textBlock)
        {
            // Проверяем, что элемент существует на холсте
            if (MainCanvas.Children.Contains(textBlock))
            {
                // Получаем оригинальный цвет текста
                Brush originalStrokeColor = null;
                if (originalStrokeColors.ContainsKey(textBlock))
                {
                    originalStrokeColor = originalStrokeColors[textBlock];
                }
                else
                {
                    originalStrokeColor = textBlock.Foreground;
                }

                // Создаем TextBox для редактирования с видимым фоном
                var editTextBox = new TextBox
                {
                    Width = 200,
                    Height = 30,
                    Background = Brushes.Transparent, // Белый фон для видимости при редактировании
                    Foreground = originalStrokeColor, // Черный текст для контраста
                    BorderBrush = Brushes.Gray,
                    FontSize = 14,
                    Text = textBlock.Text,
                    CaretBrush = Brushes.White, // Черный курсор для видимости
                    FocusVisualStyle = null
                };

                double left = Canvas.GetLeft(textBlock);
                double top = Canvas.GetTop(textBlock);

                MainCanvas.Children.Remove(textBlock);
                Canvas.SetLeft(editTextBox, left);
                Canvas.SetTop(editTextBox, top);
                MainCanvas.Children.Add(editTextBox);
                editTextBox.Focus();
                editTextBox.SelectAll();

                // Обработчик клавиш при редактировании
                editTextBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        FinishEdit(editTextBox, left, top, originalStrokeColor); // Передаем оригинальный цвет
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        MainCanvas.Children.Remove(editTextBox);
                        CreateTextElementWithColor(textBlock.Text, left, top, originalStrokeColor); // Передаем оригинальный цвет
                        e.Handled = true;
                    }
                };
                // Обработчик потери фокуса - сохраняем изменения
                editTextBox.LostFocus += (s, e) => FinishEdit(editTextBox, left, top, originalStrokeColor); // Передаем оригинальный цвет
            }
        }

        // Завершение редактирования текстового элемента
        private void FinishEdit(TextBox editTextBox, double left, double top, Brush originalColor)
        {
            if (MainCanvas.Children.Contains(editTextBox))
            {
                string newText = string.IsNullOrWhiteSpace(editTextBox.Text) ? "Текст" : editTextBox.Text;
                MainCanvas.Children.Remove(editTextBox);
                CreateTextElementWithColor(newText, left, top, originalColor);
            }
        }
        private void MakeAllShapesDraggable()
        {
            // Добавляем обработчики для всех фигур в нотации
            AddDragHandlers(Circle);
            AddDragHandlers(SimpleRing);
            AddDragHandlers(Square);
            AddDragHandlers(ResizableSquare);
            AddDragHandlers(Rectangle);
            AddDragHandlers(SuperEllipse);
            AddDragHandlers(Ellipse);
            AddDragHandlers(UmlInitialNode);
            AddDragHandlers(stick);
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
                // Выделяем фигуру в нотации
                SelectShape(element);

                isDragging = true;
                draggedElement = element;
                element.CaptureMouse();

                // Сразу создаем полупрозрачную копию для перетаскивания
                shapeCopy = CreateShapeCopy(element);
                if (shapeCopy != null)
                {
                    Canvas.SetZIndex(shapeCopy, 1000);

                    // Позиционируем копию так, чтобы центр фигуры был под курсором
                    Point startPosition = e.GetPosition(MainCanvas);
                    Canvas.SetLeft(shapeCopy, startPosition.X - (GetElementWidth(shapeCopy) / 2));
                    Canvas.SetTop(shapeCopy, startPosition.Y - (GetElementHeight(shapeCopy) / 2));

                    MainCanvas.Children.Add(shapeCopy);
                    shapeCopy.Opacity = 0.7;
                }

                // Визуальный фидбэк - делаем оригинал полупрозрачным
                element.Opacity = 0.5;

                originalTransform = element.RenderTransform;
                dragStartPoint = e.GetPosition(MainCanvas);
                offset = e.GetPosition(element);
                e.Handled = true;
            }
        }

        private void Shape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && shapeCopy != null && e.LeftButton == MouseButtonState.Pressed)
            {
                // Перемещаем копию фигуры за курсором
                Point currentPosition = e.GetPosition(MainCanvas);
                if (currentPosition.Y > WorkAreaTop && currentPosition.Y < MainCanvas.ActualHeight - WorkAreaBottom)
                {
                    double newLeft = currentPosition.X - (GetElementWidth(shapeCopy) / 2);
                    double newTop = currentPosition.Y - (GetElementHeight(shapeCopy) / 2);
                    // Дополнительные ограничения для копии
                    double elementWidth = GetElementWidth(shapeCopy);
                    double elementHeight = GetElementHeight(shapeCopy);
                    
                    double maxTop = MainCanvas.ActualHeight - elementHeight - WorkAreaBottom;

                    newLeft = Math.Max(0, Math.Min(newLeft, MainCanvas.ActualWidth - elementWidth)); newTop = Math.Max(WorkAreaTop, Math.Min(newTop, maxTop));
                    Canvas.SetLeft(shapeCopy, newLeft);
                    Canvas.SetTop(shapeCopy, newTop);
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

                // Возвращаем оригиналу полную прозрачность
                element.Opacity = 1.0;

                if (shapeCopy != null)
                {
                    Point dropPosition = e.GetPosition(MainCanvas);

                    // ЕСЛИ БРОСИЛИ ФИГУРУ НАД МЕНЮ - УДАЛЯЕМ ЕЕ
                    if (dropPosition.Y <= WorkAreaTop ||
                dropPosition.Y >= MainCanvas.ActualHeight - WorkAreaBottom)
                    {
                        MainCanvas.Children.Remove(shapeCopy);
                    }
                    else
                    {
                        // Если копия была создана, делаем ее полностью видимой и добавляем функционал перетаскивания
                        shapeCopy.Opacity = 1.0;

                        // ВОЗВРАЩАЕМ НОРМАЛЬНЫЙ ZIndex ДЛЯ ФИГУРЫ НА ХОЛСТЕ
                        Canvas.SetZIndex(shapeCopy, 1);

                        MakeCanvasShapeDraggable(shapeCopy);

                        // Добавляем эффект появления
                        AnimateShapeAppearance(shapeCopy);

                        // ВЫДЕЛЯЕМ НОВУЮ ФИГУРУ ПОСЛЕ ПЕРЕТАСКИВАНИЯ
                        SelectElement(shapeCopy as FrameworkElement);

                        if (IsShapeFromNotation(element))
                        {
                            string shapeType = GetShapeType(element);
                        }
                    }
                }

                draggedElement = null;
                shapeCopy = null;
                e.Handled = true;
            }
        }

        private bool IsShapeFromNotation(UIElement element)
        {
            // Проверяем, находится ли фигура в панели нотации
            return NotationCanvas?.Children.Contains(element) == true;
        }

        private string GetShapeType(UIElement element)
        {
            if (element == Circle) return "Circle";
            if (element == SimpleRing) return "SimpleRing";
            if (element == Square) return "Square";
            if (element == ResizableSquare) return "ResizableSquare";
            if (element == Rectangle) return "Rectangle";
            if (element == SuperEllipse) return "SuperEllipse";
            if (element == Ellipse) return "Ellipse";
            if (element == UmlInitialNode) return "UmlInitialNode";

            if (element == stick) return "stick";
            return "Unknown";
        }

        private UIElement CreateShapeCopy(UIElement original)
        {
            // Создаем копию фигуры в зависимости от типа оригинала
            if (original == Circle)
            {
                return new Ellipse
                {
                    Width = GetElementWidth(Circle),
                    Height = GetElementHeight(Circle),
                    Fill = (Circle as Ellipse).Fill,
                    Stroke = (Circle as Ellipse).Stroke,
                    StrokeThickness = 3,
                    Cursor = Cursors.Hand
                };
            }
            else if (original == UmlInitialNode)
            {
                var originalBorder = UmlInitialNode as Border;
                var innerCircle = originalBorder.Child as Ellipse;

                // Получаем размеры из оригинала
                double containerWidth = GetElementWidth(UmlInitialNode);
                double containerHeight = GetElementHeight(UmlInitialNode);

                // Создаем внешний Border (кольцо)
                var border = new Border
                {
                    Width = containerWidth,
                    Height = containerHeight,
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(3),
                    CornerRadius = new CornerRadius(containerWidth / 2), // Делаем круглым
                    Cursor = Cursors.Hand
                };

                // Создаем внутренний круг
                var circle = new Ellipse
                {
                    Width = GetElementWidth(innerCircle),
                    Height = GetElementHeight(innerCircle),
                    Fill = Brushes.White,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Добавляем круг внутрь Border'а
                border.Child = circle;
                return border;
            }

            else if (original == SimpleRing)
            {
                return new Ellipse
                {
                    Width = GetElementWidth(SimpleRing),
                    Height = GetElementHeight(SimpleRing),
                    Stroke = (SimpleRing as Ellipse).Stroke,
                    StrokeThickness = 5,
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
            }
            else if (original == Ellipse)
            {
                return new Ellipse
                {
                    Width = GetElementWidth(Ellipse),
                    Height = GetElementHeight(Ellipse),
                    Stroke = (Ellipse as Ellipse).Stroke,
                    StrokeThickness = 3,
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
            }

            else if (original == Square)
            {
                var originalBorder = Square as Border;
                return new Border
                {
                    Width = GetElementWidth(originalBorder),
                    Height = GetElementHeight(originalBorder),
                    Background = originalBorder.Background,
                    BorderBrush = originalBorder.BorderBrush,
                    BorderThickness = new Thickness(2),
                    CornerRadius = originalBorder.CornerRadius,
                    Cursor = Cursors.Hand
                };
            }
            else if (original == ResizableSquare)
            {
                var originalDiamond = ResizableSquare as Border;
                var diamond = new Border
                {
                    Width = GetElementWidth(originalDiamond),
                    Height = GetElementHeight(originalDiamond),
                    Background = originalDiamond.Background,
                    BorderBrush = originalDiamond.BorderBrush,
                    BorderThickness = new Thickness(2),
                    CornerRadius = originalDiamond.CornerRadius,
                    Cursor = Cursors.Hand,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                // Применяем ТОЛЬКО поворот на 45 градусов как у оригинала
                diamond.RenderTransform = new RotateTransform(45);
                return diamond;
            }
            else if (original == Rectangle)
            {
                var originalBorder = Rectangle as Border;
                return new Border
                {
                    Width = GetElementWidth(originalBorder),
                    Height = GetElementHeight(originalBorder),
                    Background = originalBorder.Background,
                    BorderBrush = originalBorder.BorderBrush,
                    BorderThickness = new Thickness(2),
                    CornerRadius = originalBorder.CornerRadius,
                    Cursor = Cursors.Hand
                };
            }
            else if (original == stick)
            {
                var originalBorder = stick as Border;
                return new Border
                {
                    Width = GetElementWidth(originalBorder),
                    Height = GetElementHeight(originalBorder),
                    Background = originalBorder.Background,
                    BorderBrush = originalBorder.BorderBrush,
                    BorderThickness = new Thickness(2),
                    CornerRadius = originalBorder.CornerRadius,
                    Cursor = Cursors.Hand
                };
            }
            else if (original == SuperEllipse)
            {
                var originalBorder = SuperEllipse as Border;
                return new Border
                {
                    Width = GetElementWidth(originalBorder),
                    Height = GetElementHeight(originalBorder),
                    Background = originalBorder.Background,
                    BorderBrush = originalBorder.BorderBrush,
                    BorderThickness = new Thickness(2),
                    CornerRadius = originalBorder.CornerRadius,
                    Cursor = Cursors.Hand
                };
            }
            return null;
        }

        private void MakeCanvasShapeDraggable(UIElement shape)
        {
            shape.MouseLeftButtonDown += CanvasShape_MouseLeftButtonDown;
            shape.MouseMove += CanvasShape_MouseMove;
            shape.MouseLeftButtonUp += CanvasShape_MouseLeftButtonUp;

            // Обработчик пкм для контекстного меню
            shape.MouseRightButtonDown += (s, e) =>
            {
                SelectElement(shape as FrameworkElement);
                e.Handled = true;
            };
        }

        private void CanvasShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                // Выделяем элемент при щелчке лкм
                SelectElement(element as FrameworkElement);
                isDragging = true;
                draggedElement = element;
                element.CaptureMouse();

                // Рассчитываем смещение так, чтобы центр фигуры был под курсором
                offset = new Point(
                    GetElementWidth(element) / 2,
                    GetElementHeight(element) / 2
                );

                // ПОДНИМАЕМ ЭЛЕМЕНТ НАВЕРХ ПРИ ЗАХВАТЕ (ZIndex = 1000)
                Canvas.SetZIndex(element, 1000);

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
                // ВЫДЕЛЯЕМ фигуру при клике (но не начинаем новое перетаскивание)
                

                isDragging = false;
                element.ReleaseMouseCapture();

                // ФИКСИРУЕМ ТРАНСФОРМАЦИЮ ДЛЯ РОМБА
                if (element is Border border && border.RenderTransform is RotateTransform rotateTransform)
                {
                    // Убеждаемся что ромб сохраняет поворот на 45 градусов
                    rotateTransform.Angle = 45;
                }

                // ВОЗВРАЩАЕМ НОРМАЛЬНЫЙ ZIndex ПОСЛЕ ПЕРЕТАСКИВАНИЯ
                Canvas.SetZIndex(element, 1000); // Оставляем выделенный элемент поверх других

                // Возвращаем исходный вид
                ApplyCanvasDragVisualFeedback(element, false);

                draggedElement = null;
                e.Handled = true;
            }
        }

        private void ApplyCanvasDragVisualFeedback(UIElement element, bool isDragging)
        {
            if (isDragging)
            {
                // Только визуальные эффекты без изменения реальной толщины
                element.Opacity = 0.7;
                // НЕ меняем StrokeThickness при перетаскивании
            }
            else
            {
                // Возвращаем оригинальные настройки
                element.Opacity = 1.0;
                // Толщина остается фиксированной
            }
        }

        // Метод для выделения фигуры из нотации
        public void SelectShape(UIElement shape)
        {
            // Снимаем выделение с предыдущей фигуры
            if (selectedShape != null)
            {
                RemoveSelectionVisual(selectedShape);
            }

            // Сохраняем оригинальные цвета перед выделением
            SaveOriginalColors(shape);

            // Выделяем новую фигуру
            selectedShape = shape;
            ApplySelectionVisual(selectedShape);

            // Обновляем комбо-боксы в соответствии с цветами фигуры
            UpdateColorComboBoxesFromShape(selectedShape);
        }

        // Сохранение оригинальных цветов
        private void SaveOriginalColors(UIElement shape)
        {
            if (shape is Ellipse ellipse)
            {
                if (!originalStrokeColors.ContainsKey(shape))
                {
                    originalStrokeColors[shape] = ellipse.Stroke;
                }
                if (!originalFillColors.ContainsKey(shape))
                {
                    originalFillColors[shape] = ellipse.Fill;
                }
            }
            else if (shape is Border border)
            {
                if (!originalStrokeColors.ContainsKey(shape))
                {
                    originalStrokeColors[shape] = border.BorderBrush;
                }
                if (!originalFillColors.ContainsKey(shape))
                {
                    originalFillColors[shape] = border.Background;
                }
            }
            else if (shape is Line line)
            {
                if (!originalStrokeColors.ContainsKey(shape))
                {
                    originalStrokeColors[shape] = line.Stroke;
                }
            }
            else if (shape is TextBlock textBlock)
            {
                if (!originalStrokeColors.ContainsKey(shape))
                {
                    originalStrokeColors[shape] = textBlock.Foreground;
                }
            }
        }

        // Метод применения цвета к выделенной фигуре
        private void ApplyColorToSelectedShape(string hexColor)
        {
            if (selectedElement != null)
            {
                Color newColor = (Color)ColorConverter.ConvertFromString("#" + hexColor);
                Brush newBrush = new SolidColorBrush(newColor);

                ApplyFillColorToShape(selectedElement, newBrush);

                // Сохраняем новый цвет как оригинальный
                if (originalFillColors.ContainsKey(selectedElement))
                {
                    originalFillColors[selectedElement] = newBrush;
                }
            }
        }

        // Метод обновления комбо-бокса на основе цвета фигуры
        private void UpdateColorComboBoxFromShape(UIElement shape)
        {
            if (shape == null) return;

            Brush fillBrush = null;
            Brush strokeBrush = null;

            if (shape is Ellipse ellipse)
            {
                fillBrush = ellipse.Fill;
                strokeBrush = ellipse.Stroke;
            }
            else if (shape is Border border)
            {
                fillBrush = border.Background;
                strokeBrush = border.BorderBrush;
            }
            else if (shape is Line line)
            {
                strokeBrush = line.Stroke; // У линии только обводка
            }
            else if (shape is TextBlock textBlock)
            {
                strokeBrush = textBlock.Foreground; // У текста цвет - это обводка
            }

            // Обновляем комбо-бокс заливки
            if (fillBrush is SolidColorBrush fillSolidBrush && fillBrush != Brushes.Transparent)
            {
                string fillHexColor = fillSolidBrush.Color.ToString().Substring(3);
                UpdateComboBoxSelection(FillColorComboBox, fillHexColor);
            }

            // Обновляем комбо-бокс обводки
            if (strokeBrush is SolidColorBrush strokeSolidBrush)
            {
                string strokeHexColor = strokeSolidBrush.Color.ToString().Substring(3);
                UpdateComboBoxSelection(StrokeColorComboBox, strokeHexColor);
            }
        }

        // Визуальное выделение фигуры
        private void ApplySelectionVisual(UIElement element)
        {
            // Добавляем эффект выделения без изменения реальных цветов
            if (element is Ellipse ellipse)
            {
                ellipse.StrokeThickness += 2;
                ellipse.Stroke = Brushes.Blue;
                ellipse.Opacity = 0.8;
            }
            else if (element is Border border)
            {
                border.BorderThickness = new Thickness(4);
                border.BorderBrush = Brushes.Blue;
                border.Opacity = 0.8;
            }
            else if (element is Line line)
            {
                line.StrokeThickness += 2;
                line.Stroke = Brushes.Blue;
                line.Opacity = 0.8;
            }
        }

        // Удаление визуального выделения
        private void RemoveSelectionVisual(UIElement element)
        {
            if (element is Ellipse ellipse)
            {
                ellipse.StrokeThickness -= 2;
                ellipse.Opacity = 1.0;
                // Возвращаем оригинальный цвет
                if (originalStrokeColors.ContainsKey(element))
                {
                    ellipse.Stroke = originalStrokeColors[element];
                }
            }
            else if (element is Border border)
            {
                border.BorderThickness = new Thickness(2);
                border.Opacity = 1.0;
                // Возвращаем оригинальный цвет
                if (originalStrokeColors.ContainsKey(element))
                {
                    border.BorderBrush = originalStrokeColors[element];
                }
            }
            else if (element is Line line)
            {
                line.StrokeThickness -= 2;
                line.Opacity = 1.0;
                // Возвращаем оригинальный цвет
                if (originalStrokeColors.ContainsKey(element))
                {
                    line.Stroke = originalStrokeColors[element];
                }
            }
            else if (element is TextBlock textBlock)
            {
                textBlock.Effect = null;
            }
        }

        // ОБРАБОТЧИК СОБЫТИЯ ДЛЯ COMBOBOX
        private void FillColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FillColorComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string hexColor = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(hexColor))
                {
                    try
                    {
                        // Обновляем превью цвета
                        FillColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + hexColor));

                        // Применяем цвет к выделенной фигуре
                        ApplyColorToSelectedShape(hexColor);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка применения цвета: {ex.Message}");
                    }
                }
            }
        }

        // Вспомогательный метод для применения цвета к конкретной фигуре
        private void ApplyColorToShape(UIElement shape, Brush brush)
        {
            if (shape is Ellipse ellipse)
            {
                if (ellipse.Fill == Brushes.Transparent) // Кольцо
                {
                    ellipse.Stroke = brush;
                }
                else // Круг
                {
                    ellipse.Fill = brush;
                }
            }
            else if (shape is Border border)
            {
                border.Background = brush;
            }
            else if (shape is Line line)
            {
                line.Stroke = brush;
            }
        }

        // ОБРАБОТЧИК СОБЫТИЯ ДЛЯ COMBOBOX ОБВОДКИ
        private void StrokeColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StrokeColorComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string hexColor = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(hexColor))
                {
                    try
                    {
                        // Обновляем превью цвета обводки
                        StrokeColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + hexColor));

                        // Применяем цвет обводки к выделенной фигуре
                        ApplyStrokeColorToSelectedShape(hexColor);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка применения цвета обводки: {ex.Message}");
                    }
                }
            }
        }

        // Метод применения цвета обводки к выделенной фигуре
        private void ApplyStrokeColorToSelectedShape(string hexColor)
        {
            if (selectedElement != null)
            {
                Color newColor = (Color)ColorConverter.ConvertFromString("#" + hexColor);
                Brush newBrush = new SolidColorBrush(newColor);

                ApplyStrokeColorToShape(selectedElement, newBrush);

                // Сохраняем новый цвет как оригинальный
                if (originalStrokeColors.ContainsKey(selectedElement))
                {
                    originalStrokeColors[selectedElement] = newBrush;
                }
            }
        }

        // Вспомогательный метод для применения цвета обводки к конкретной фигуре
        private void ApplyStrokeColorToShape(UIElement shape, Brush brush)
        {
            if (shape is Ellipse ellipse)
            {
                // Для эллипсов меняем Stroke (контур)
                ellipse.Stroke = brush;
            }
            else if (shape is Border border)
            {
                // Для Border меняем BorderBrush (рамку)
                border.BorderBrush = brush;
            }
            else if (shape is Line line)
            {
                // Для линии меняем Stroke (цвет линии)
                line.Stroke = brush;
            }
            else if (shape is TextBlock textBlock)
            {
                // Для текста меняем Foreground (цвет текста)
                textBlock.Foreground = brush;
            }
        }

        // Вспомогательный метод для обновления комбо-бокса
        private void UpdateComboBoxSelection(ComboBox comboBox, string hexColor)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag as string == hexColor)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }
        // Метод для очистки комбо-боксов при запуске программы
        private void ClearColorComboBoxesOnStartup()
        {
            FillColorComboBox.SelectedIndex = -1;
            StrokeColorComboBox.SelectedIndex = -1;
            FillColorPreview.Background = Brushes.Transparent;
            StrokeColorPreview.Background = Brushes.Transparent;
        }

        // Вспомогательный метод для применения цвета заливки
        private void ApplyFillColorToShape(UIElement shape, Brush brush)
        {
            if (shape is Ellipse ellipse)
            {
                // Для эллипсов меняем Fill (заливку), только если это не кольцо
                if (ellipse.Fill != Brushes.Transparent)
                {
                    ellipse.Fill = brush;
                }
            }
            else if (shape is Border border)
            {
                // Для Border меняем Background (заливку)
                border.Background = brush;
            }
            // Для Line и TextBlock заливка не применяется
        }

        // Метод обновления комбо-боксов на основе цветов фигуры
        private void UpdateColorComboBoxesFromShape(UIElement shape)
        {
            if (shape == null) return;

            Brush fillBrush = null;
            Brush strokeBrush = null;

            // Используем оригинальные цвета если они сохранены
            if (originalFillColors.ContainsKey(shape))
            {
                fillBrush = originalFillColors[shape];
            }
            if (originalStrokeColors.ContainsKey(shape))
            {
                strokeBrush = originalStrokeColors[shape];
            }

            // Если оригинальные цвета не сохранены, берем текущие
            if (fillBrush == null)
            {
                if (shape is Ellipse ellipse)
                {
                    fillBrush = ellipse.Fill;
                }
                else if (shape is Border border)
                {
                    fillBrush = border.Background;
                }
            }

            if (strokeBrush == null)
            {
                if (shape is Ellipse ellipse)
                {
                    strokeBrush = ellipse.Stroke;
                }
                else if (shape is Border border)
                {
                    strokeBrush = border.BorderBrush;
                }
                else if (shape is Line line)
                {
                    strokeBrush = line.Stroke;
                }
                else if (shape is TextBlock textBlock)
                {
                    strokeBrush = textBlock.Foreground;
                }
            }

            // Обновляем комбо-бокс заливки
            if (fillBrush is SolidColorBrush fillSolidBrush && fillBrush != Brushes.Transparent)
            {
                string fillHexColor = fillSolidBrush.Color.ToString().Substring(3);
                UpdateComboBoxSelection(FillColorComboBox, fillHexColor);
            }

            // Обновляем комбо-бокс обводки
            if (strokeBrush is SolidColorBrush strokeSolidBrush)
            {
                string strokeHexColor = strokeSolidBrush.Color.ToString().Substring(3);
                UpdateComboBoxSelection(StrokeColorComboBox, strokeHexColor);
            }
        }

        private void AnimateShapeAppearance(UIElement shape)
        {
            // Сохраняем оригинальную трансформацию перед анимацией
            Transform originalTransform = shape.RenderTransform;

            var scaleAnimation = new DoubleAnimation
            {
                From = 0.5,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(originalTransform); // Оригинальная трансформация
            transformGroup.Children.Add(new ScaleTransform());

            shape.RenderTransform = transformGroup;
            shape.RenderTransformOrigin = new Point(0.5, 0.5);

            transformGroup.Children[1].BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            transformGroup.Children[1].BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }
        // Обработчик кнопки комментариев
        private void CommentModeButton_Click(object sender, RoutedEventArgs e)
        {
            isCommentModeActive = CommentModeButton.IsChecked == true;

            if (isCommentModeActive)
            {
                MainCanvas.Cursor = Cursors.Hand;
            }
            else
            {
                MainCanvas.Cursor = Cursors.Arrow;
            }
        }
        // Обработчик клика по холсту в режиме комментариев
        private void MainCanvas_MouseLeftButtonDown_Comment(object sender, MouseButtonEventArgs e)
        {
            if (!isCommentModeActive) return;

            Point clickPosition = e.GetPosition(MainCanvas);

            // Проверяем, что клик не по другим элементам
            if (e.OriginalSource is System.Windows.Controls.Canvas)
            {

                e.Handled = true;
            }
        }


        private double GetElementWidth(UIElement element)
        {
            if (element is FrameworkElement fe)
                return fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
            return 0;
        }

        private double GetElementHeight(UIElement element)
        {
            if (element is FrameworkElement fe)
                return fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;
            return 0;
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}