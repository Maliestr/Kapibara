using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Ink;
using System.Globalization;

namespace Graphic_Editor
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private UIElement draggedElement;
        private Point offset;
        private Transform originalTransform;
        private Point dragStartPoint;
        private string currentFilePath = null;
        private List<UIElement> canvasElements = new List<UIElement>();

        private FrameworkElement selectedElement;
        private FrameworkElement copiedElement;
        private Point lastMousePosition;
        private Stack<UIElement> undoStack = new Stack<UIElement>();
        private Stack<UIElement> redoStack = new Stack<UIElement>();

        public MainWindow()
        {
            InitializeComponent();
            MakeAllShapesDraggable();

            InitializeGrid(); // Инициализируем сетку
            InitializePanels();

            // Обработка горячих клавиш
            this.KeyDown += MainWindow_KeyDown;



        }

        // Переменные для масштабирования
        private double _currentZoom = 1.0;
        private const double ZoomStep = 0.1;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;

        private void ZoomCanvas(double delta)
        {
            double newZoom = _currentZoom + delta;

            // Ограничиваем масштаб
            if (newZoom < MinZoom) newZoom = MinZoom;
            if (newZoom > MaxZoom) newZoom = MaxZoom;

            if (newZoom != _currentZoom)
            {
                _currentZoom = newZoom;
                ApplyZoom();
            }
        }

        private void ApplyZoom()
        {
            if (CanvasScaleTransform != null)
            {
                CanvasScaleTransform.ScaleX = _currentZoom;
                CanvasScaleTransform.ScaleY = _currentZoom;

                // Обновляем отображение масштаба
                UpdateZoomText();
            }
        }

        private void ResetZoom()
        {
            _currentZoom = 1.0;
            ApplyZoom();
        }

        private void UpdateZoomText()
        {
            if (ZoomTextBlock != null)
            {
                ZoomTextBlock.Text = $"{_currentZoom * 100:0}%";
            }
        }

        // Масштабирование колесиком мыши
        private void MainCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
                ZoomCanvas(delta);
                e.Handled = true;

                // Показываем текст масштаба временно
                ShowZoomTextTemporarily();
            }
        }

        private void ShowZoomTextTemporarily()
        {
            if (ZoomTextBlock != null)
            {
                ZoomTextBlock.Visibility = Visibility.Visible;

                // Скрываем через 2 секунды
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (s, e) =>
                {
                    ZoomTextBlock.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
                timer.Start();
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
            MainCanvas.MouseWheel += MainCanvas_MouseWheel;
            UpdateZoomText();

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
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Add:
                    case Key.OemPlus:
                        ZoomCanvas(ZoomStep);
                        e.Handled = true;
                        break;
                    case Key.Subtract:
                    case Key.OemMinus:
                        ZoomCanvas(-ZoomStep);
                        e.Handled = true;
                        break;
                    case Key.D0:
                        ResetZoom();
                        e.Handled = true;
                        break;
                }
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
            ZoomCanvas(ZoomStep);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Уменьшить");
            ZoomCanvas(-ZoomStep);
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Сбросить масштаб");
            ResetZoom();
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

        /*private void Cut_Click(object sender, RoutedEventArgs e)
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
        }*/

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
            DeselectElement();
            
            selectedElement = element;
            ApplySelectionVisualFeedback(element, true);
            
            // Создаем контекстное меню для выделенного элемента
            CreateContextMenu(element);
        }

        // Снятие выделения
        private void DeselectElement()
        {
            if (selectedElement != null)
            {
                ApplySelectionVisualFeedback(selectedElement, false);
                if (selectedElement is FrameworkElement frameworkElement)
                {
                    frameworkElement.ContextMenu = null;
                }
                selectedElement = null;
            }
        }

        // Визуальное выделение элемента
        private void ApplySelectionVisualFeedback(UIElement element, bool isSelected)
        {
            if (isSelected)
            {
                if (element is Ellipse ellipse)
                {
                    ellipse.Stroke = Brushes.Blue;
                    ellipse.StrokeThickness += 2;
                }
                else if (element is Border border)
                {
                    border.BorderBrush = Brushes.Blue;
                    border.BorderThickness = new Thickness(4);
                }
                else if (element is Line line)
                {
                    line.Stroke = Brushes.Blue;
                    line.StrokeThickness += 2;
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
                    }
                    else // Круг
                    {
                        ellipse.Stroke = Brushes.Black;
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
                    line.StrokeThickness = Math.Max(line.StrokeThickness - 2, 1);
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
                
                DeselectElement();
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
                
                DeselectElement();
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
                }
            }
            else
            {
                MessageBox.Show("Нет действий для повтора", "Повтор", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Сохранение в стек отмены
        private void SaveToUndoStack(UIElement element)
        {
            undoStack.Push(CloneUIElement(element));
            // Очищаем стек повтора при новом действии
            redoStack.Clear();
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

        private void CreateShapeCopyOnCanvas(Point position)
        {
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
                canvasElements.Add(newShape);
            }
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

                offset = e.GetPosition(element);

                // Визуальный фидбэк
                ApplyCanvasDragVisualFeedback(element, true);

                e.Handled = true;
            }
        }

        private void CanvasShape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = draggedElement;
                Point currentPosition = e.GetPosition(MainCanvas);

                double newLeft = currentPosition.X - offset.X;
                double newTop = currentPosition.Y - offset.Y;

                Canvas.SetLeft(element, newLeft);
                Canvas.SetTop(element, newTop);
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
    }
}