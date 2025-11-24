using System;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Xml;
using System.Xml.Linq;

namespace Graphic_Editor
{
    public partial class MainWindow : Window
    {
        //Состояние перетаскивания
        private bool isDragging = false; //Идёт ли процесс перетаскивания - нет
        private UIElement draggedElement; //Элемент, который сейчас перетаскивается
        private Point offset; //Смещение курсора относительно элемента при захвате
        private UIElement shapeCopy; //Копия элемента при перетаскивании из панели
        private Transform originalTransform; //Исходная трансформация элемента для восстановления
        private Point dragStartPoint; //Начальная точка перетаскивания

        //Управление элементами холста
        private string currentFilePath = null; //Путь к текущему файлу (null - новый файл)
        private List<UIElement> canvasElements = new List<UIElement>(); //Список всех элементов на холсте

        //Система выделения
        private FrameworkElement selectedElement; //Текущий выделенный элемент (для операций)
        private UIElement selectedShape; //Выделенная фигура (альтернативная система выделения)
        private FrameworkElement copiedElement; //Элемент в буфере обмена для копирования/вставки
        private Point lastMousePosition; //Последняя позиция мыши (для отслеживания движения)

        //Система прямоугольного выделения
        private bool isSelecting = false; //Идёт ли процесс выделения - нет
        private Point selectionStart; //Начальная точка выделения
        private Rectangle selectionRect; //Визуальный прямоугольник выделения
        private List<UIElement> selectedElements = new List<UIElement>(); //Список выделенных элементов
        private Dictionary<UIElement, ResizableElement> resizableElements = 
            new Dictionary<UIElement, ResizableElement>(); //Хранит связь между элементами на холсте и их контроллерами изменения размера
        private ResizableElement currentResizableElement; //Текущий активный изменяемый в размерах элемент

        //Система отмены/повтора
        private Stack<UIElement> undoStack = new Stack<UIElement>(); //Стек для отмены действий
        private Stack<UIElement> redoStack = new Stack<UIElement>(); //Стек для повтора действий

        //Система сетки
        private bool isGridEnabled = false; //Отображение сетки - нет
        private bool isSnapToGridEnabled = false; //Привязка к сетке - нет
        private System.Windows.Shapes.Path gridPath; //Отрисовка линий сетки
        private const double GridSize = 20; //Размер ячейки сетки в пикселях

        //Режим текста
        private bool isTextModeActive = false; //Активация режима ввода текста - нет
        private TextBox textBox; //Временное текстовое поле для ввода
        private Dictionary<UIElement, TextFormatting> textFormatting = new Dictionary<UIElement, TextFormatting>(); //Система форматирования текста (хранит шрифт, размер, стиль)

        //Временные настройки текста
        private string currentFontFamily = "Gotham Pro"; //Текущий шрифт
        private double currentFontSize = 14; //Текущий размер шрифта
        private FontStyle currentFontStyle = FontStyles.Normal; //Текущий стиль
        private FontWeight currentFontWeight = FontWeights.Normal; //Текущая насыщенность

        //Система сохранения оригинальных стилей
        private Dictionary<UIElement, Brush> originalStrokeColors = new Dictionary<UIElement, Brush>(); //Исходные цвета контуров
        private Dictionary<UIElement, Brush> originalFillColors = new Dictionary<UIElement, Brush>(); //Исходные цвета заливки

        //Управление интерфейсом
        private bool arePanelsVisible = true; //Видимость всех панелей инструментов
        private List<Border> panels = new List<Border>(); //Список всех плашек для управления видимостью

        //Система комментариев
        private bool isCommentModeActive = false; //Активность режима добавления комментариев - нет
        private List<Comment> comments = new List<Comment>(); //Список всех комментариев на холсте

        //Система рисования стрелок
        private bool isArrowModeActive = false; //Активен ли режим рисования стрелок - нет
        private Polyline currentArrow; //Текущая рисуемая стрелка
        private Point arrowStartPoint; //Начальная точка стрелки
        private List<ArrowContainer> arrows = new List<ArrowContainer>(); //Все стрелки на холсте
        private ArrowContainer selectedArrowContainer; //Выделенная стрелка
        private Polygon draggedArrowhead; //Перетаскиваемый наконечник
        private PointCollection originalArrowPoints; //Оригинальные точки стрелки перед перетаскиванием
        private Dictionary<Polyline, Polygon> arrowToArrowheadMap = new Dictionary<Polyline, Polygon>(); //Связь стрелка-наконечник

        //Нужно разобраться
        private List<Polyline> lines = new List<Polyline>(); //Список линий
        private List<Polygon> arrowheads = new List<Polygon>(); //Список наконечников
        //private List<UIElement> arrows = new List<UIElement>(); //Хранение всех стрелок на холсте в виде коллекции UIElement

        public class TextFormatting //Класс для форматирования текста
        {
            public FontFamily FontFamily { get; set; }
            public double FontSize { get; set; }
            public FontStyle FontStyle { get; set; }
            public FontWeight FontWeight { get; set; }
        }

        public class Comment //Класс для комментирования
        {
            public UIElement VisualElement { get; set; }
            public UIElement TextElement { get; set; }
            public string Text { get; set; }
            public Point Position { get; set; }
        }

        public class ArrowContainer //Класс для хранения стрелки и её наконечника вместе
        {
            public Polyline Line { get; set; }
            public Polygon Arrowhead { get; set; }
            public PointCollection OriginalPoints { get; set; }
            public Brush OriginalStroke { get; set; }
            public double OriginalThickness { get; set; }
            public Brush OriginalArrowheadFill { get; set; }
            public Brush OriginalArrowheadStroke { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            MakeAllShapesDraggable();

            InitializeGrid(); // Инициализируем сетку
            InitializePanels();
            InitializeSelectionSystem(); // Инициализация системы выделения

            // Обработка горячих клавиш
            this.KeyDown += MainWindow_KeyDown;

            // Добавляем обработчик клика по канвасу для сброса выделения
            MainCanvas.MouseLeftButtonDown += MainCanvas_MouseLeftButtonDown;

            // Очищаем комбо-боксы при запуске
            ClearColorComboBoxesOnStartup();

            // Добавляем обработчики для комментариев
            if (comment != null)
            {
                comment.Click += CommentButton_Click;
            }

            // Сбрасываем комбо-боксы текста при запуске
            ResetTextComboBoxesToEmpty();

            // Обработчики для текстовых полей размеров
            WidthTextBox.LostFocus += DimensionTextBox_LostFocus;
            HeightTextBox.LostFocus += DimensionTextBox_LostFocus;
            WidthTextBox.KeyDown += DimensionTextBox_KeyDown;
            HeightTextBox.KeyDown += DimensionTextBox_KeyDown;

            // Кнопка пера включает/выключает режим рисования стрелок
            pencil.Click += PenButton_Click;
        }

        // Обработчики для ручного ввода размеров
        private void DimensionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyDimensionsFromTextboxes();
        }

        public bool IsElementInNotation(UIElement element)
        {
            if (element == null || NotationCanvas == null) return false;

            // Проверяем, содержится ли элемент непосредственно в NotationCanvas
            if (NotationCanvas.Children.Contains(element))
                return true;

            // Проверяем родителей элемента
            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent == NotationCanvas)
                    return true;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return false;
        }

        private void DimensionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyDimensionsFromTextboxes();
                e.Handled = true;
            }
        }

        private void ApplyDimensionsFromTextboxes()
        {
            if (selectedShape == null) return;

            if (double.TryParse(WidthTextBox.Text, out double width) &&
                double.TryParse(HeightTextBox.Text, out double height))
            {
                // Применяем новые размеры
                if (selectedShape is FrameworkElement frameworkElement)
                {
                    frameworkElement.Width = Math.Max(20, width);
                    frameworkElement.Height = Math.Max(20, height);
                }

                // Обновляем позицию ручек изменения размера
                if (resizableElements.ContainsKey(selectedShape))
                {
                    resizableElements[selectedShape].ShowResizeHandles();
                }
            }
        }

        // Для выделения кнопок
        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    // Сбрасываем выделение у всех кнопок
        //    cursor.Tag = null;
        //    pencil.Tag = null;
        //    rectangle.Tag = null;
        //    text.Tag = null;
        //    comment.Tag = null;
        //    dash.Tag = null;

        //    // Выделяем нажатую кнопку
        //    ((Button)sender).Tag = "Selected";
        //}

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
            // Обработка Escape для завершения стрелки
            if (e.Key == Key.Escape && isArrowModeActive && currentArrow != null)
            {
                CompleteArrow();
                e.Handled = true;
                return; // return чтобы не мешать другим обработкам
            }
            else if (e.Key == Key.G && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
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
                    else if (element is Polyline arrow)
                    {
                        string strokeColor = (arrow.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
                        double strokeThickness = arrow.StrokeThickness;

                        // Сохраняем все точки стрелки
                        string points = string.Join(";", arrow.Points.Select(p => $"{p.X},{p.Y}"));
                        writer.WriteLine($"ARROW|{points}|{strokeColor}|{strokeThickness}");
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
            arrows.Clear();

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
                        case "ARROW":
                            CreateArrowFromData(parts);
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

            // Создаем ResizableElement
            resizableElements[ellipse] = new ResizableElement(ellipse, MainCanvas);
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

            // Создаем ResizableElement
            resizableElements[square] = new ResizableElement(square, MainCanvas);
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

            // Создаем ResizableElement
            resizableElements[diamond] = new ResizableElement(diamond, MainCanvas);
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

            // Создаем ResizableElement
            resizableElements[line] = new ResizableElement(line, MainCanvas);
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
        // Новые методы для работы с множественным выделением
        private void CutSelectedElements()
        {
            if (selectedElements.Count > 0)
            {
                // Сохраняем копии для буфера обмена
                var elementsToCut = selectedElements.ToList();

                foreach (var element in elementsToCut)
                {
                    // Сохраняем в стек отмены
                    SaveToUndoStack(element);

                    // Удаляем элемент
                    MainCanvas.Children.Remove(element);
                    canvasElements.Remove(element);
                }

                ClearSelection();
                MessageBox.Show($"Вырезано {elementsToCut.Count} элементов", "Вырезание", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Сначала выделите элементы", "Вырезание", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        

        // Снятие выделения со всех элементов
        
        
            
        
        // Визуальное выделение элемента
        private const double WorkAreaTop = 50;    // Отступ сверху (под меню)
        private const double WorkAreaBottom = 100; // Отступ снизу (над панелью инструментов)


        // Словарь для хранения оригинальной толщины обводки
        private Dictionary<UIElement, double> originalStrokeThickness = new Dictionary<UIElement, double>();

        // Метод для показа/скрытия панели толщины обводки
        private void UpdateStrokeWidthPanelVisibility()
        {
            if (selectedElement != null)
            {
                // Показываем панель толщины только для элементов, у которых есть обводка
                bool hasStroke = selectedElement is Ellipse || selectedElement is Line ||
                                selectedElement is Border || selectedElement is TextBlock;
                StrokeWidthPanel.Visibility = hasStroke ? Visibility.Visible : Visibility.Collapsed;

                // Обновляем комбо-бокс толщины текущим значением
                UpdateStrokeWidthComboBoxFromShape(selectedElement);
            }
            else
            {
                StrokeWidthPanel.Visibility = Visibility.Collapsed;
            }
        }

        // Обработчик изменения толщины обводки
        private void StrokeWidthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StrokeWidthComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string thicknessValue = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(thicknessValue))
                {
                    try
                    {
                        double thickness = double.Parse(thicknessValue);
                        ApplyStrokeThicknessToSelectedShape(thickness);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка применения толщины: {ex.Message}");
                    }
                }
            }
        }

        // Метод применения толщины обводки к выделенной фигуре
        private void ApplyStrokeThicknessToSelectedShape(double thickness)
        {
            if (selectedElement != null)
            {
                ApplyStrokeThicknessToShape(selectedElement, thickness);

                // Сохраняем новую толщину как оригинальную
                originalStrokeThickness[selectedElement] = thickness;
                if (selectedShape != null)
                {
                    originalStrokeThickness[selectedShape] = thickness;
                }
            }
        }

        // Вспомогательный метод для применения толщины обводки к конкретной фигуре
        private void ApplyStrokeThicknessToShape(UIElement shape, double thickness)
        {
            UpdateStrokeThicknessInAllReferences(shape, thickness);
            if (shape is Ellipse ellipse)
            {
                ellipse.StrokeThickness = thickness;
            }
            else if (shape is Border border)
            {
                border.BorderThickness = new Thickness(thickness);
            }
            else if (shape is Line line)
            {
                line.StrokeThickness = thickness;
            }
            else if (shape is TextBlock textBlock)
            {
                // Для текста толщина не применяется
            }

            // Сохраняем толщину в словарь оригинальных толщин
            originalStrokeThickness[shape] = thickness;
        }

        // Метод обновления комбо-бокса толщины на основе фигуры
        private void UpdateStrokeWidthComboBoxFromShape(UIElement shape)
        {
            if (shape == null) return;

            double currentThickness = 0;

            if (shape is Ellipse ellipse)
            {
                currentThickness = ellipse.StrokeThickness;
            }
            else if (shape is Border border)
            {
                currentThickness = border.BorderThickness.Left;
            }
            else if (shape is Line line)
            {
                currentThickness = line.StrokeThickness;
            }

            // Обновляем комбо-бокс
            UpdateStrokeWidthComboBoxSelection(currentThickness);
        }

        // Вспомогательный метод для обновления комбо-бокса толщины
        private void UpdateStrokeWidthComboBoxSelection(double thickness)
        {
            foreach (ComboBoxItem item in StrokeWidthComboBox.Items)
            {
                if (item.Tag != null && double.Parse(item.Tag as string) == thickness)
                {
                    StrokeWidthComboBox.SelectedItem = item;
                    return;
                }
            }

            // Если точное значение не найдено, выбираем ближайшее
            StrokeWidthComboBox.SelectedIndex = 0;
        }

        // Сохранение оригинальных цветов и толщины
        private void SaveOriginalColors(UIElement shape)
        {
            if (shape == null) return;
            
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
                if (!originalStrokeThickness.ContainsKey(shape))
                {
                    originalStrokeThickness[shape] = ellipse.StrokeThickness;
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
                if (!originalStrokeThickness.ContainsKey(shape))
                {
                    originalStrokeThickness[shape] = border.BorderThickness.Left;
                }
            }
            else if (shape is Line line)
            {
                if (!originalStrokeColors.ContainsKey(shape))
                {
                    originalStrokeColors[shape] = line.Stroke;
                }
                if (!originalStrokeThickness.ContainsKey(shape))
                {
                    originalStrokeThickness[shape] = line.StrokeThickness;
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

        // Обновите метод RestoreOriginalAppearance для восстановления толщины
        private void RestoreOriginalAppearance(UIElement element)
        {
            if (element is Ellipse ellipse)
            {
                // Возвращаем сохраненный цвет обводки
                if (originalStrokeColors.ContainsKey(element))
                {
                    ellipse.Stroke = originalStrokeColors[element];
                }
                else
                {
                    if (ellipse.Fill == Brushes.Transparent)
                    {
                        ellipse.Stroke = Brushes.White;
                    }
                    else
                    {
                        ellipse.Stroke = Brushes.Black;
                    }
                }
                // Возвращаем сохраненную толщину
                if (originalStrokeThickness.ContainsKey(element))
                {
                    ellipse.StrokeThickness = originalStrokeThickness[element];
                }
                else
                {
                    ellipse.StrokeThickness = ellipse == SimpleRing ? 5 : 3;
                }
                ellipse.Opacity = 1.0;
            }
            else if (element is Border border)
            {
                if (originalStrokeColors.ContainsKey(element))
                {
                    border.BorderBrush = originalStrokeColors[element];
                }
                else
                {
                    border.BorderBrush = Brushes.White;
                }
                // Возвращаем сохраненную толщину
                if (originalStrokeThickness.ContainsKey(element))
                {
                    border.BorderThickness = new Thickness(originalStrokeThickness[element]);
                }
                else
                {
                    border.BorderThickness = new Thickness(2);
                }
                border.Opacity = 1.0;
            }
            else if (element is Line line)
            {
                if (originalStrokeColors.ContainsKey(element))
                {
                    line.Stroke = originalStrokeColors[element];
                }
                else
                {
                    line.Stroke = Brushes.White;
                }
                // Возвращаем сохраненную толщину
                if (originalStrokeThickness.ContainsKey(element))
                {
                    line.StrokeThickness = originalStrokeThickness[element];
                }
                else
                {
                    line.StrokeThickness = 3;
                }
                line.Opacity = 1.0;
            }
            else if (element is TextBlock textBlock)
            {
                if (originalStrokeColors.ContainsKey(element))
                {
                    textBlock.Foreground = originalStrokeColors[element];
                }
                textBlock.Background = Brushes.Transparent;
                textBlock.Opacity = 1.0;
            }

            Canvas.SetZIndex(element, 1);
        }

        private void SelectElement(FrameworkElement element)
        {
            if (element == null) return;

            // УБИРАЕМ проверку на Ctrl - выделение всегда добавляется
            // Снимаем выделение с предыдущих элементов только если не зажат Ctrl
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                DeselectAll();
            }

            // Если элемент уже выделен и нажат Ctrl - снимаем выделение (ОСТАВЛЯЕМ эту логику)
            if (selectedElements.Contains(element) && Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                DeselectElement(element);
                return;
            }

            // Если элемент не выделен - выделяем его
            if (!selectedElements.Contains(element))
            {
                SaveOriginalColors(element);
                selectedElement = element;
                ApplySelectionVisualFeedback(element, true);

                // Создаем контекстное меню для выделенного элемента
                CreateContextMenu(element);

                // Обновляем комбо-боксы в соответствии с цветами фигуры
                UpdateColorComboBoxesFromShape(element);

                // ПОКАЗЫВАЕМ ПАНЕЛЬ ТОЛЩИНЫ ОБВОДКИ
                UpdateStrokeWidthPanelVisibility();
                // Обновляем панель текста, если элемент является текстовым
                UpdateTextPanelForSelectedElement(element);

                selectedElements.Add(element);

                // Показываем ручки изменения размера
                ShowResizeHandlesForElement(element);
            }
        }

        private void DeselectAll()
        {
            // Снимаем выделение со всех элементов
            foreach (var element in selectedElements.ToList())
            {
                ApplySelectionVisualFeedback(element, false);
                if (element is FrameworkElement frameworkElement)
                {
                    frameworkElement.ContextMenu = null;
                    Canvas.SetZIndex(frameworkElement, 1);
                }

                // Скрываем ручки изменения размера
                if (resizableElements.ContainsKey(element))
                {
                    resizableElements[element].HideResizeHandles();
                }
            }

            selectedElements.Clear();
            selectedElement = null;

            // СКРЫВАЕМ ПАНЕЛЬ ТОЛЩИНЫ ОБВОДКИ
            StrokeWidthPanel.Visibility = Visibility.Collapsed;

            // Очищаем комбо-боксы
            ClearColorComboBoxes();

            // Сбрасываем комбо-боксы текста при снятии выделения
            ResetTextComboBoxesToEmpty();

            // Снимаем выделение со стрелки если есть
            DeselectArrow();
        }
        private void ClearColorComboBoxes()
        {
            FillColorComboBox.SelectedIndex = -1;
            StrokeColorComboBox.SelectedIndex = -1;
            FillColorPreview.Background = Brushes.Transparent;
            StrokeColorPreview.Background = Brushes.Transparent;
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

                // Создаем ResizableElement для нового элемента
                resizableElements[newElement] = new ResizableElement(newElement, MainCanvas);

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

                // Удаляем ResizableElement если есть
                if (resizableElements.ContainsKey(selectedElement))
                {
                    resizableElements[selectedElement].Dispose();
                    resizableElements.Remove(selectedElement);
                }

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
            if (original is Polyline originalArrow)
            {
                var newArrow = new Polyline
                {
                    Stroke = originalArrow.Stroke,
                    StrokeThickness = originalArrow.StrokeThickness,
                    StrokeLineJoin = originalArrow.StrokeLineJoin,
                    StrokeStartLineCap = originalArrow.StrokeStartLineCap,
                    StrokeEndLineCap = originalArrow.StrokeEndLineCap,
                    Points = new PointCollection(originalArrow.Points),
                    Cursor = Cursors.Hand
                };

                return newArrow;
            }



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
        // Восстановление оригинального вида элемента (без выделения)
        
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
            if (original == null || copy == null) return;
            
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
            if (element == null) return;
            
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
            // Пока просто выделяем все элементы (расширено для множественного выделения)
            ClearSelection();
            foreach (UIElement element in MainCanvas.Children)
            {
                if (element != selectionRect) // Исключаем прямоугольник выделения
                {
                    SelectElement(element as FrameworkElement);
                }
            }
        }
        //ИЗНАЧАЛЬНАЯ ВЕРСИЯ:
        /*private void SelectAllElements()
        {
            // Пока просто выделяем первый элемент (можно расширить для множественного выделения)
            if (MainCanvas.Children.Count > 0)
            {
                SelectElement(MainCanvas.Children[0] as FrameworkElement);
            }
        }*/

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

            // Сбрасываем комбо-боксы текста после создания текстового элемента
            ResetTextComboBoxesToEmpty();
        }

        // Создание текстового элемента с цветом по умолчанию
        private void CreateTextElement(string text, double left, double top)
        {
            CreateTextElementWithColor(text, left, top, Brushes.White);
        }
        // Создание текстового элемента с указанным цветом
        private void CreateTextElementWithColor(string text, double left, double top, Brush color)
        {
            // Используем значения по умолчанию если текущие не установлены
            string fontFamily = string.IsNullOrEmpty(currentFontFamily) ? "Gotham Pro" : currentFontFamily;
            double fontSize = currentFontSize == 0 ? 14 : currentFontSize;
            FontStyle fontStyle = currentFontStyle;
            FontWeight fontWeight = currentFontWeight;

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontFamily = new FontFamily(fontFamily),
                FontSize = fontSize,
                FontStyle = fontStyle,
                FontWeight = fontWeight,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null,
                Background = Brushes.Transparent,
                // Устанавливаем начальные размеры для возможности изменения размера
                Width = double.NaN, // Автоматический размер
                Height = double.NaN // Автоматический размер
            };

            // Устанавливаем позицию на холсте
            Canvas.SetLeft(textBlock, left);
            Canvas.SetTop(textBlock, top);
            Canvas.SetZIndex(textBlock, 1000);

            // Сохраняем оригинальные настройки
            originalStrokeColors[textBlock] = color;
            SaveTextFormatting(textBlock);

            // Добавляем текстовый элемент на холст
            MainCanvas.Children.Add(textBlock);

            textBlock.MouseLeftButtonDown += TextElement_MouseLeftButtonDown;
            MakeCanvasShapeDraggable(textBlock);
            SelectElement(textBlock);
            SelectShape(textBlock);
        }

        // Сохранение форматирования текстового элемента
        private void SaveTextFormatting(TextBlock textBlock)
        {
            if (!textFormatting.ContainsKey(textBlock))
            {
                textFormatting[textBlock] = new TextFormatting
                {
                    FontFamily = textBlock.FontFamily,
                    FontSize = textBlock.FontSize,
                    FontStyle = textBlock.FontStyle,
                    FontWeight = textBlock.FontWeight
                };
            }
        }
        // Обработчик нажатия левой кнопки мыши на текстовом элементе
        private void TextElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock == null) return;
            DeselectAll();
            // Сохраняем оригинальный цвет если он еще не сохранен
            if (!originalStrokeColors.ContainsKey(textBlock))
            {
                originalStrokeColors[textBlock] = textBlock.Foreground;
            }
            SelectElement(textBlock);
            SelectShape(textBlock);
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

            // СКРЫВАЕМ РУЧКИ ИЗМЕНЕНИЯ РАЗМЕРА ДЛЯ ТЕКСТА
            if (resizableElements.ContainsKey(element))
            {
                resizableElements[element].HideResizeHandles();
            }

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

                // ПОКАЗЫВАЕМ РУЧКИ ИЗМЕНЕНИЯ РАЗМЕРА ДЛЯ ТЕКСТА
                if (resizableElements.ContainsKey(draggedElement))
                {
                    resizableElements[draggedElement].ShowResizeHandles();
                }
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

                // Создаем TextBox для редактирования с сохранением форматирования
                var editTextBox = new TextBox
                {
                    Width = Math.Max(textBlock.ActualWidth, 200),
                    Height = Math.Max(textBlock.ActualHeight, 30),
                    Background = Brushes.White,
                    Foreground = Brushes.Black,
                    BorderBrush = Brushes.Gray,
                    FontFamily = textBlock.FontFamily,
                    FontSize = textBlock.FontSize,
                    FontStyle = textBlock.FontStyle,
                    FontWeight = textBlock.FontWeight,
                    Text = textBlock.Text,
                    CaretBrush = Brushes.Black,
                    FocusVisualStyle = null,
                    TextWrapping = TextWrapping.Wrap
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
                        FinishEditWithFormatting(editTextBox, left, top, originalStrokeColor);
                        FinishEdit(editTextBox, left, top, originalStrokeColor); // Передаем оригинальный цвет
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        CancelTextEditing(editTextBox, textBlock.Text, left, top, originalStrokeColor);
                        MainCanvas.Children.Remove(editTextBox);
                        CreateTextElementWithColor(textBlock.Text, left, top, originalStrokeColor); // Передаем оригинальный цвет
                        e.Handled = true;
                    }
                };
                editTextBox.LostFocus += (s, e) => FinishEdit(editTextBox, left, top, originalStrokeColor); // Передаем оригинальный цвет

                // Обработчик потери фокуса - сохраняем изменения
                editTextBox.LostFocus += (s, e) => FinishEditWithFormatting(editTextBox, left, top, originalStrokeColor);
            }
        }
        // Завершение редактирования с сохранением форматирования
        private void FinishEditWithFormatting(TextBox editTextBox, double left, double top, Brush originalColor)
        {
            if (MainCanvas.Children.Contains(editTextBox))
            {
                string newText = string.IsNullOrWhiteSpace(editTextBox.Text) ? "Текст" : editTextBox.Text;
                MainCanvas.Children.Remove(editTextBox);

                // Создаем новый текстовый элемент с форматированием из TextBox
                var textBlock = new TextBlock
                {
                    Text = newText,
                    Foreground = originalColor,
                    FontFamily = editTextBox.FontFamily,
                    FontSize = editTextBox.FontSize,
                    FontStyle = editTextBox.FontStyle,
                    FontWeight = editTextBox.FontWeight,
                    Cursor = Cursors.Hand,
                    FocusVisualStyle = null,
                    Background = Brushes.Transparent,
                    // Устанавливаем начальные размеры для возможности изменения размера
                    Width = double.NaN, // Автоматический размер
                    Height = double.NaN // Автоматический размер
                };

                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, top);
                Canvas.SetZIndex(textBlock, 1000);

                originalStrokeColors[textBlock] = originalColor;
                SaveTextFormatting(textBlock);

                MainCanvas.Children.Add(textBlock);
                textBlock.MouseLeftButtonDown += TextElement_MouseLeftButtonDown;
                MakeCanvasShapeDraggable(textBlock);
                SelectElement(textBlock);
                SelectShape(textBlock);
            }
        }

        // Отмена редактирования
        private void CancelTextEditing(TextBox editTextBox, string originalText, double left, double top, Brush originalColor)
        {
            if (MainCanvas.Children.Contains(editTextBox))
            {
                MainCanvas.Children.Remove(editTextBox);
                CreateTextElementWithColor(originalText, left, top, originalColor);
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


        // Обработчик изменения шрифта
        private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
            {
                currentFontFamily = selectedItem.Content.ToString();
                ApplyTextFormattingToSelected();
            }
            else if (FontComboBox.SelectedItem == null)
            {
                // Не сбрасываем currentFontFamily, если элемент уже выбран
                // Это позволяет сохранить текущее значение при программном изменении
            }
        }

        // Обработчик изменения стиля шрифта
        private void FontStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontStyleComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
            {
                switch (selectedItem.Content.ToString())
                {
                    case "Regular":
                        currentFontStyle = FontStyles.Normal;
                        currentFontWeight = FontWeights.Normal;
                        break;
                    case "Bold":
                        currentFontStyle = FontStyles.Normal;
                        currentFontWeight = FontWeights.Bold;
                        break;
                    case "Italic":
                        currentFontStyle = FontStyles.Italic;
                        currentFontWeight = FontWeights.Normal;
                        break;
                    case "Bold Italic":
                        currentFontStyle = FontStyles.Italic;
                        currentFontWeight = FontWeights.Bold;
                        break;
                }
                ApplyTextFormattingToSelected();
            }
            else if (FontStyleComboBox.SelectedItem == null)
            {
                // Не сбрасываем значения при программном изменении
            }
        }

        // Обработчик изменения размера шрифта
        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSizeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
            {
                if (double.TryParse(selectedItem.Content.ToString(), out double size))
                {
                    currentFontSize = size;
                    ApplyTextFormattingToSelected();
                }
            }
            else if (FontSizeComboBox.SelectedItem == null)
            {
                // Не сбрасываем значение при программном изменении
            }
        }

        // Применение форматирования к выбранному текстовому элементу
        private void ApplyTextFormattingToSelected()
        {
            // Применяем изменения только если есть выбранный текстовый элемент
            if (selectedElement is TextBlock textBlock)
            {
                // Применяем шрифт, если он выбран
                if (!string.IsNullOrEmpty(currentFontFamily))
                {
                    try
                    {
                        textBlock.FontFamily = new FontFamily(currentFontFamily);
                    }
                    catch
                    {
                        // Игнорируем ошибки при создании FontFamily
                    }
                }

                // Применяем размер шрифта, если он выбран
                if (currentFontSize > 0)
                {
                    textBlock.FontSize = currentFontSize;
                }

                // Применяем стиль и вес шрифта
                textBlock.FontStyle = currentFontStyle;
                textBlock.FontWeight = currentFontWeight;

                // Сохраняем новые настройки
                SaveCurrentTextFormatting(textBlock);
            }
        }
        // Обновление панели текста для выбранного элемента
        private void UpdateTextPanelForSelectedElement(FrameworkElement element)
        {
            if (element == null) return;
            
            if (element is TextBlock textBlock)
            {
                // Показываем панель текста
                TextPanel.Visibility = Visibility.Visible;

                // Обновляем комбо-боксы значениями из текстового элемента
                UpdateTextComboBoxes(textBlock);

                // Сохраняем текущие настройки для применения изменений
                SaveCurrentTextFormatting(textBlock);
            }
            else
            {
                // Скрываем панель текста для не-текстовых элементов
                TextPanel.Visibility = Visibility.Collapsed;

                // Сбрасываем комбо-боксы к пустым значениям
                ResetTextComboBoxesToEmpty();
            }
        }
        // Обновление комбо-боксов текста значениями из TextBlock
        private void UpdateTextComboBoxes(TextBlock textBlock)
        {
            if (textBlock == null) return;
            
            // Обновляем шрифт
            string fontFamilySource = textBlock.FontFamily?.Source ?? "";
            foreach (ComboBoxItem item in FontComboBox.Items)
            {
                if (item.Content != null && item.Content.ToString() == fontFamilySource)
                {
                    // Временно отключаем обработчик, чтобы избежать рекурсии
                    FontComboBox.SelectionChanged -= FontComboBox_SelectionChanged;
                    FontComboBox.SelectedItem = item;
                    FontComboBox.SelectionChanged += FontComboBox_SelectionChanged;
                    currentFontFamily = fontFamilySource;
                    break;
                }
            }
            
            // Если шрифт не найден в списке, устанавливаем значение напрямую
            if (string.IsNullOrEmpty(currentFontFamily) && !string.IsNullOrEmpty(fontFamilySource))
            {
                currentFontFamily = fontFamilySource;
            }

            // Обновляем размер шрифта
            bool fontSizeFound = false;
            foreach (ComboBoxItem item in FontSizeComboBox.Items)
            {
                if (item.Content != null && double.TryParse(item.Content.ToString(), out double size) &&
                    Math.Abs(size - textBlock.FontSize) < 0.1)
                {
                    // Временно отключаем обработчик, чтобы избежать рекурсии
                    FontSizeComboBox.SelectionChanged -= FontSizeComboBox_SelectionChanged;
                    FontSizeComboBox.SelectedItem = item;
                    FontSizeComboBox.SelectionChanged += FontSizeComboBox_SelectionChanged;
                    currentFontSize = textBlock.FontSize;
                    fontSizeFound = true;
                    break;
                }
            }

            // Если размер не найден в списке, добавляем его
            if (!fontSizeFound && textBlock.FontSize > 0)
            {
                var newItem = new ComboBoxItem { Content = textBlock.FontSize.ToString("0") };
                FontSizeComboBox.Items.Add(newItem);
                // Временно отключаем обработчик
                FontSizeComboBox.SelectionChanged -= FontSizeComboBox_SelectionChanged;
                FontSizeComboBox.SelectedItem = newItem;
                FontSizeComboBox.SelectionChanged += FontSizeComboBox_SelectionChanged;
                currentFontSize = textBlock.FontSize;
            }
            else if (!fontSizeFound && textBlock.FontSize > 0)
            {
                currentFontSize = textBlock.FontSize;
            }

            // Обновляем стиль шрифта
            string styleName = GetFontStyleName(textBlock.FontStyle, textBlock.FontWeight);
            foreach (ComboBoxItem item in FontStyleComboBox.Items)
            {
                if (item.Content != null && item.Content.ToString() == styleName)
                {
                    // Временно отключаем обработчик, чтобы избежать рекурсии
                    FontStyleComboBox.SelectionChanged -= FontStyleComboBox_SelectionChanged;
                    FontStyleComboBox.SelectedItem = item;
                    FontStyleComboBox.SelectionChanged += FontStyleComboBox_SelectionChanged;
                    currentFontStyle = textBlock.FontStyle;
                    currentFontWeight = textBlock.FontWeight;
                    break;
                }
            }
        }

        // Получение имени стиля для комбо-бокса
        private string GetFontStyleName(FontStyle style, FontWeight weight)
        {
            if (style == FontStyles.Italic && weight == FontWeights.Bold)
                return "Bold Italic";
            else if (weight == FontWeights.Bold)
                return "Bold";
            else if (style == FontStyles.Italic)
                return "Italic";
            else
                return "Regular";
        }
        // Сохранение текущего форматирования текстового элемента
        private void SaveCurrentTextFormatting(TextBlock textBlock)
        {
            if (!textFormatting.ContainsKey(textBlock))
            {
                textFormatting[textBlock] = new TextFormatting();
            }

            textFormatting[textBlock].FontFamily = textBlock.FontFamily;
            textFormatting[textBlock].FontSize = textBlock.FontSize;
            textFormatting[textBlock].FontStyle = textBlock.FontStyle;
            textFormatting[textBlock].FontWeight = textBlock.FontWeight;
        }

        // Сброс комбо-боксов текста к пустым значениям
        private void ResetTextComboBoxesToEmpty()
        {
            // Сбрасываем выбранные элементы
            FontComboBox.SelectedItem = null;
            FontStyleComboBox.SelectedItem = null;
            FontSizeComboBox.SelectedItem = null;

            // Очищаем текущие значения
            currentFontFamily = null;
            currentFontSize = 0;
            currentFontStyle = FontStyles.Normal;
            currentFontWeight = FontWeights.Normal;

            // Скрываем панель текста
            TextPanel.Visibility = Visibility.Collapsed;
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
                SelectElement(element as FrameworkElement);
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
                //ApplyDragVisualFeedback(element, true);
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

        private void ApplySelectionVisualFeedback(UIElement element, bool isSelected)
        {
            if (element == null) return;

            if (isSelected)
            {
                // ВЫДЕЛЕНИЕ СИНИМ ЦВЕТОМ ДЛЯ ВСЕХ ВЫДЕЛЕННЫХ ЭЛЕМЕНТОВ
                if (selectedElements.Contains(element))
                {
                    if (element is Ellipse ellipseElement)
                    {
                        ellipseElement.Stroke = Brushes.Blue;
                        ellipseElement.StrokeThickness += 2;
                    }
                    else if (element is Border borderElement)
                    {
                        borderElement.BorderBrush = Brushes.Blue;
                        borderElement.BorderThickness = new Thickness(borderElement.BorderThickness.Left + 2);
                    }
                    else if (element is Line lineElement)
                    {
                        lineElement.Stroke = Brushes.Blue;
                        lineElement.StrokeThickness += 2;
                    }
                    else if (element is TextBlock textBlockElement)
                    {
                        textBlockElement.Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 255));
                    }
                }
            }
            else
            {
                // ВОЗВРАЩАЕМ ОРИГИНАЛЬНЫЕ ЗНАЧЕНИЯ ТОЛЬКО ЕСЛИ ЭЛЕМЕНТ НЕ ВЫДЕЛЕН
                if (!selectedElements.Contains(element))
                {
                    if (element is Ellipse ellipseElement)
                    {
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            ellipseElement.Stroke = originalStrokeColors[element];
                        }
                        else
                        {
                            // Если цвет не сохранен, используем логику по умолчанию
                            if (ellipseElement.Fill == Brushes.Transparent) // Кольцо
                            {
                                ellipseElement.Stroke = Brushes.White;
                            }
                            else // Круг
                            {
                                ellipseElement.Stroke = Brushes.Black;
                            }
                        }

                        // Восстанавливаем толщину
                        if (originalStrokeThickness.ContainsKey(element))
                        {
                            ellipseElement.StrokeThickness = originalStrokeThickness[element];
                        }
                    }
                    else if (element is Border borderElement)
                    {
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            borderElement.BorderBrush = originalStrokeColors[element];
                        }
                        else
                        {
                            borderElement.BorderBrush = Brushes.White;
                        }

                        // Восстанавливаем толщину
                        if (originalStrokeThickness.ContainsKey(element))
                        {
                            borderElement.BorderThickness = new Thickness(originalStrokeThickness[element]);
                        }
                    }
                    else if (element is Line lineElement)
                    {
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            lineElement.Stroke = originalStrokeColors[element];
                        }
                        else
                        {
                            lineElement.Stroke = Brushes.White;
                        }

                        // Восстанавливаем толщину
                        if (originalStrokeThickness.ContainsKey(element))
                        {
                            lineElement.StrokeThickness = originalStrokeThickness[element];
                        }
                    }
                    else if (element is TextBlock textBlockElement)
                    {
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            textBlockElement.Foreground = originalStrokeColors[element];
                        }
                        textBlockElement.Background = Brushes.Transparent;
                    }
                }
            }
        }

        // Метод для обновления текстовых полей размеров
        public void UpdateDimensions(double width, double height)
        {
            // Обновляем текстовые поля размеров если они существуют
            if (WidthTextBox != null && !double.IsNaN(width))
            {
                WidthTextBox.Text = Math.Round(width).ToString();
            }

            if (HeightTextBox != null && !double.IsNaN(height))
            {
                HeightTextBox.Text = Math.Round(height).ToString();
            }
        }

        private void Shape_MouseLeftButtonUp(object sender, MouseEventArgs e)
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

                    // ЕСЛИ БРОСИЛИ ФИГУРУ ВНЕ РАБОЧЕЙ ОБЛАСТИ - УДАЛЯЕМ ЕЕ
                    if (dropPosition.Y <= WorkAreaTop ||
                        dropPosition.Y >= MainCanvas.ActualHeight - WorkAreaBottom)
                    {
                        MainCanvas.Children.Remove(shapeCopy);
                    }
                    else
                    {
                        // Если копия была создана в рабочей области, делаем ее полностью видимой
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

                // СНИМАЕМ ВЫДЕЛЕНИЕ С ФИГУРЫ В НОТАЦИИ
                ApplySelectionVisualFeedback(element, false);

                draggedElement = null;
                shapeCopy = null;
                e.Handled = true;
            }
        }
        // Метод для принудительного обновления толщины во всех связанных элементах
        private void UpdateStrokeThicknessInAllReferences(UIElement element, double thickness)
        {
            // Обновляем толщину в основном элементе
            if (element is Ellipse ellipse)
            {
                ellipse.StrokeThickness = thickness;
            }
            else if (element is Border border)
            {
                border.BorderThickness = new Thickness(thickness);
            }
            else if (element is Line line)
            {
                line.StrokeThickness = thickness;
            }

            // Сохраняем в словарь
            originalStrokeThickness[element] = thickness;

            // Если это selectedElement, обновляем и в selectedShape
            if (selectedElement == element && selectedShape != null)
            {
                originalStrokeThickness[selectedShape] = thickness;
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
            // Создаем копию фигуры в зависимости от типа оригинала с фиксированными размерами и толщиной
            if (original == Circle)
            {
                var copy = new Ellipse
                {
                    Width = 110,
                    Height = 110,
                    Fill = (Circle as Ellipse).Fill,
                    Stroke = (Circle as Ellipse).Stroke,
                    StrokeThickness = 3, // ФИКСИРОВАННАЯ толщина
                    Cursor = Cursors.Hand
                };
                originalStrokeThickness[copy] = 3;
                return copy;
            }
            else if (original == UmlInitialNode)
            {
                var border = new Border
                {
                    Width = 110,
                    Height = 110,
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(3), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = new CornerRadius(55),
                    Cursor = Cursors.Hand
                };

                var circle = new Ellipse
                {
                    Width = 80,
                    Height = 80,
                    Fill = Brushes.White,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1, // ФИКСИРОВАННАЯ толщина
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                border.Child = circle;
                originalStrokeThickness[border] = 3;
                originalStrokeThickness[circle] = 1;
                return border;
            }
            else if (original == SimpleRing)
            {
                var copy = new Ellipse
                {
                    Width = 110,
                    Height = 110,
                    Stroke = (SimpleRing as Ellipse).Stroke,
                    StrokeThickness = 5, // ФИКСИРОВАННАЯ толщина
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                originalStrokeThickness[copy] = 5;
                return copy;
            }
            else if (original == Ellipse)
            {
                var copy = new Ellipse
                {
                    Width = 130,
                    Height = 80,
                    Stroke = (Ellipse as Ellipse).Stroke,
                    StrokeThickness = 3, // ФИКСИРОВАННАЯ толщина
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                originalStrokeThickness[copy] = 3;
                return copy;
            }
            else if (original == Square)
            {
                var copy = new Border
                {
                    Width = 100,
                    Height = 100,
                    Background = (Square as Border).Background,
                    BorderBrush = (Square as Border).BorderBrush,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (Square as Border).CornerRadius,
                    Cursor = Cursors.Hand
                };
                originalStrokeThickness[copy] = 2;
                return copy;
            }
            else if (original == ResizableSquare)
            {
                var copy = new Border
                {
                    Width = 70,
                    Height = 71,
                    Background = (ResizableSquare as Border).Background,
                    BorderBrush = (ResizableSquare as Border).BorderBrush,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (ResizableSquare as Border).CornerRadius,
                    Cursor = Cursors.Hand,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                copy.RenderTransform = new RotateTransform(45);
                originalStrokeThickness[copy] = 2;
                return copy;
            }
            else if (original == Rectangle)
            {
                var copy = new Border
                {
                    Width = 150,
                    Height = 80,
                    Background = (Rectangle as Border).Background,
                    BorderBrush = (Rectangle as Border).BorderBrush,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (Rectangle as Border).CornerRadius,
                    Cursor = Cursors.Hand
                };
                originalStrokeThickness[copy] = 2;
                return copy;
            }
            else if (original == stick)
            {
                var copy = new Border
                {
                    Width = 200,
                    Height = 30,
                    Background = (stick as Border).Background,
                    BorderBrush = (stick as Border).BorderBrush,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (stick as Border).CornerRadius,
                    Cursor = Cursors.Hand
                };
                originalStrokeThickness[copy] = 2;
                return copy;
            }
            else if (original == SuperEllipse)
            {
                var copy = new Border
                {
                    Width = 120,
                    Height = 80,
                    Background = (SuperEllipse as Border).Background,
                    BorderBrush = (SuperEllipse as Border).BorderBrush,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (SuperEllipse as Border).CornerRadius,
                    Cursor = Cursors.Hand
                };
                originalStrokeThickness[copy] = 2;
                return copy;
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
            var element = sender as FrameworkElement;
            if (element != null)
            {
                // Сохраняем текущую толщину ПЕРЕД выделением
                SaveOriginalColors(element);

                // Выделяем элемент при щелчке лкм
                SelectElement(element as FrameworkElement);
                SelectShape(element);
                isDragging = true;
                draggedElement = element;
                element.CaptureMouse();

                // СКРЫВАЕМ РУЧКИ ИЗМЕНЕНИЯ РАЗМЕРА ПРИ НАЧАЛЕ ПЕРЕТАСКИВАНИЯ
                if (resizableElements.ContainsKey(element))
                {
                    resizableElements[element].HideResizeHandles();
                }

                // Рассчитываем смещение так, чтобы центр фигуры был под курсором
                offset = new Point(
                    GetElementWidth(element) / 2,
                    GetElementHeight(element) / 2
                );

                // ПОДНИМАЕМ ЭЛЕМЕНТ НАВЕРХ ПРИ ЗАХВАТЕ (ZIndex = 1000)
                Canvas.SetZIndex(element, 1000);

                // Визуальный фидбэк
                // Выделяем фигуру при клике
                //SelectShape(element);

                isDragging = true;
                draggedElement = element;
                element.CaptureMouse();
                offset = e.GetPosition(element);
                ApplyCanvasDragVisualFeedback(element, true);
                e.Handled = true;
            }
        }

        private void ApplyCanvasDragVisualFeedback(UIElement element, bool isDragging)
        {
            if (isDragging)
            {
                // Только визуальные эффекты без изменения реальных цветов
                element.Opacity = 0.7;
                if (element is Ellipse ellipse)
                {
                    ellipse.StrokeThickness += 1;
                }
                else if (element is Border border)
                {
                    border.BorderThickness = new Thickness(3);
                }
                else if (element is Line line)
                {
                    line.StrokeThickness += 1;
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
                    ellipse.StrokeThickness = ellipse == SimpleRing ? 5 : 3;
                }
                else if (element is Border border)
                {
                    border.BorderThickness = new Thickness(2);
                }
                else if (element is Line line)
                {
                    line.StrokeThickness -= 1;
                }
            }
        }

        private void CanvasShape_MouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            // Завершение выделения
            if (isSelecting)
            {
                isSelecting = false;
                MainCanvas.ReleaseMouseCapture();
                selectionRect.Visibility = Visibility.Collapsed;
            }

            // Завершение перетаскивания
            if (isDragging && draggedElement != null)
            {
                // ВЫДЕЛЯЕМ фигуру при клике (но не начинаем новое перетаскивание)
                //SelectShape(element);

                isDragging = false;
                draggedElement.ReleaseMouseCapture();

                // ФИКСИРУЕМ ТРАНСФОРМАЦИЮ ДЛЯ РОМБА
                if (draggedElement is Border border && border.RenderTransform is RotateTransform rotateTransform)
                {
                    // Убеждаемся что ромб сохраняет поворот на 45 градусов
                    rotateTransform.Angle = 45;
                }

                // ВОЗВРАЩАЕМ НОРМАЛЬНЫЙ ZIndex ПОСЛЕ ПЕРЕТАСКИВАНИЯ
                Canvas.SetZIndex(draggedElement, 1);

                // ПОКАЗЫВАЕМ РУЧКИ ИЗМЕНЕНИЯ РАЗМЕРА ПОСЛЕ ЗАВЕРШЕНИЯ ПЕРЕТАСКИВАНИЯ
                if (resizableElements.ContainsKey(draggedElement))
                {
                    resizableElements[draggedElement].ShowResizeHandles();
                }

                // Возвращаем исходный вид
                ApplyCanvasDragVisualFeedback(draggedElement, false);

                draggedElement = null;
                e.Handled = true;
            }
        }

        // Добавьте метод для снятия выделения при клике на пустую область канваса
        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isArrowModeActive)
            {
                Point clickPoint = e.GetPosition(MainCanvas);

                if (currentArrow == null)
                {
                    StartArrowDrawing(clickPoint);
                }
                else
                {
                    AddArrowSegment(clickPoint);
                }

                e.Handled = true;
                return;
            }

            if (isCommentModeActive)
            {
                Point clickPosition = e.GetPosition(MainCanvas);

                // Проверяем, что клик не по другим элементам
                if (e.OriginalSource is System.Windows.Controls.Canvas)
                {
                    CreateCommentAtPosition(clickPosition);
                    e.Handled = true;
                    return; // Выходим, чтобы не выполнять остальной код
                }
            }

            // Если кликнули на пустую область канваса - снимаем выделение
            if (e.OriginalSource == MainCanvas)
            {
                DeselectAll();
                e.Handled = true;
            }
        }

        // Методы для системы выделения
        private void StartSelection(Point position)
        {
            // Сбрасываем предыдущее выделение, если не зажат Ctrl
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                ClearSelection();
            }

            selectionStart = position;
            isSelecting = true;

            selectionRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(selectionRect, position.X);
            Canvas.SetTop(selectionRect, position.Y);
            selectionRect.Width = 0;
            selectionRect.Height = 0;

            MainCanvas.CaptureMouse();
        }
        private void UpdateSelectionInRealTime(Rect selectionBounds)
        {
            foreach (UIElement element in MainCanvas.Children)
            {
                if (element == selectionRect || element == null) continue;

                var elementBounds = GetElementBounds(element);
                bool shouldBeSelected = selectionBounds.IntersectsWith(elementBounds);

                if (element is FrameworkElement frameworkElement)
                {
                    if (Keyboard.IsKeyDown(Key.LeftCtrl))
                    {
                        // В режиме Ctrl: добавляем к существующему выделению
                        if (shouldBeSelected && !selectedElements.Contains(frameworkElement))
                        {
                            // Добавляем к выделению без вызова DeselectAll
                            if (!selectedElements.Contains(frameworkElement))
                            {
                                selectedElements.Add(frameworkElement);
                                SaveOriginalColors(frameworkElement);
                                ApplySelectionVisualFeedback(frameworkElement, true);
                                SelectShape(frameworkElement);
                            }
                        }
                    }
                    else
                    {
                        // В обычном режиме: выделяем только элементы в области
                        if (shouldBeSelected && !selectedElements.Contains(frameworkElement))
                        {
                            // Добавляем к выделению без вызова DeselectAll
                            if (!selectedElements.Contains(frameworkElement))
                            {
                                selectedElements.Add(frameworkElement);
                                SaveOriginalColors(frameworkElement);
                                ApplySelectionVisualFeedback(frameworkElement, true);
                                SelectShape(frameworkElement);
                            }
                        }
                        else if (!shouldBeSelected && selectedElements.Contains(frameworkElement))
                        {
                            DeselectElement(frameworkElement);
                        }
                    }
                }
            }
        }
        private Rect GetElementBounds(UIElement element)
        {
            if (element == null) return new Rect(0, 0, 0, 0);
            
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            double width = 0;
            double height = 0;

            if (element is Ellipse ellipse)
            {
                width = ellipse.Width;
                height = ellipse.Height;
            }
            else if (element is Border border)
            {
                width = border.Width;
                height = border.Height;
            }
            else if (element is Line line)
            {
                // Для линии используем bounding box
                double minX = Math.Min(line.X1, line.X2);
                double minY = Math.Min(line.Y1, line.Y2);
                double maxX = Math.Max(line.X1, line.X2);
                double maxY = Math.Max(line.Y1, line.Y2);
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            else if (element is TextBlock textBlock)
            {
                // Для текста используем размеры текста
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = textBlock.DesiredSize.Width;
                height = textBlock.DesiredSize.Height;
            }

            return new Rect(left, top, width, height);
        }
        //ПОСМОТРЕТЬ ПРАВИЛЬНОСТЬ
        private UIElement FindElementAtPosition(Point position)
        {
            // Ищем элементы с конца (верхние элементы в z-order)
            for (int i = MainCanvas.Children.Count - 1; i >= 0; i--)
            {
                var element = MainCanvas.Children[i];
                if (element == selectionRect) continue;

                var elementBounds = GetElementBounds(element);
                if (elementBounds.Contains(position))
                {
                    return element;
                }
            }
            return null;
        }

        private void HandleElementClick(UIElement element, MouseButtonEventArgs e)
        {
            if (element == null) return;
            
            if (element is FrameworkElement frameworkElement)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    // Ctrl + клик: переключаем выделение
                    if (selectedElements.Contains(frameworkElement))
                    {
                        DeselectElement(frameworkElement);
                    }
                    else
                    {
                        SelectElement(frameworkElement);
                    }
                }
            }
        }

        // Метод для очистки текстовых полей размеров
        private void ClearDimensionTextboxes()
        {
            if (WidthTextBox != null)
                WidthTextBox.Text = "";

            if (HeightTextBox != null)
                HeightTextBox.Text = "";
        }

        // Метод для сброса комбо-боксов цветов
        private void ResetColorComboBoxes()
        {
            if (FillColorComboBox != null)
                FillColorComboBox.SelectedIndex = -1;

            if (StrokeColorComboBox != null)
                StrokeColorComboBox.SelectedIndex = -1;

            // Сбрасываем превью цветов к значениям по умолчанию
            if (FillColorPreview != null)
                FillColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC44141"));

            if (StrokeColorPreview != null)
                StrokeColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF000000"));
        }

        private void DeselectElement(UIElement element)
        {
            if (element == null)
            {
                return;
            }

            selectedElements.Remove(element);
            ApplySelectionVisualFeedback(element, false);

            if (element is FrameworkElement frameworkElement)
            {
                frameworkElement.Opacity = 1;
                frameworkElement.ContextMenu = null;
            }

            if (resizableElements.TryGetValue(element, out var resizable))
            {
                resizable.HideResizeHandles();
            }
        }

        public void SelectShape(UIElement shape)
        {
            if (shape == null) return;

            // Проверяем, находится ли элемент в панели нотации
            if (IsElementInNotation(shape))
            {
                // Для элементов в нотации не показываем ручки изменения размера
                return;
            }

            DeselectArrow();

            // Сохраняем оригинальные цвета перед выделением
            SaveOriginalColors(shape);

            // Выделяем новую фигуру
            selectedShape = shape;

            if (shape is FrameworkElement frameworkElement)
            {
                selectedElement = frameworkElement;

                // Добавляем в список выделенных элементов если еще нет
                if (!selectedElements.Contains(frameworkElement))
                {
                    selectedElements.Add(frameworkElement);
                }
            }

            // Применяем визуальное выделение
            ApplySelectionVisualFeedback(shape, true);

            // Показываем ручки изменения размера
            ShowResizeHandlesForElement(shape);

            // Обновляем комбо-боксы в соответствии с цветами фигуры
            UpdateColorComboBoxesFromShape(selectedShape);

            // Создаем контекстное меню
            if (shape is FrameworkElement frameworkElementForMenu)
            {
                CreateContextMenu(frameworkElementForMenu);
            }
        }

        private void ShowResizeHandlesForElement(UIElement element)
        {
            // Не показываем ручки во время перетаскивания
            if (isDragging) return;

            // Проверяем, не находится ли элемент в панели нотации
            if (IsElementInNotation(element))
            {
                // Не показываем ручки изменения размера для элементов в нотации
                return;
            }

            // Скрываем предыдущие ручки
            if (currentResizableElement != null)
            {
                currentResizableElement.HideResizeHandles();
                currentResizableElement = null;
            }

            // Создаем или используем существующий ResizableElement для текущего элемента
            try
            {
                if (!resizableElements.ContainsKey(element))
                {
                    // Создаем новый ResizableElement
                    resizableElements[element] = new ResizableElement(element, MainCanvas);
                }

                currentResizableElement = resizableElements[element];
                currentResizableElement.ShowResizeHandles();

                // Обновляем текстовые поля размеров
                if (element is FrameworkElement frameworkElement)
                {
                    UpdateDimensions(frameworkElement.ActualWidth, frameworkElement.ActualHeight);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка создания ResizableElement: {ex.Message}");
            }
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Обработка рисования стрелки
            if (isArrowModeActive)
            {
                if (currentArrow != null)
                {
                    UpdateArrowDrawing(e.GetPosition(MainCanvas));
                }

                e.Handled = true;
                return;
            }

            // Обработка выделения
            if (isSelecting)
            {
                var currentPos = e.GetPosition(MainCanvas);

                var x = Math.Min(currentPos.X, selectionStart.X);
                var y = Math.Min(currentPos.Y, selectionStart.Y);
                var width = Math.Abs(currentPos.X - selectionStart.X);
                var height = Math.Abs(currentPos.Y - selectionStart.Y);

                Canvas.SetLeft(selectionRect, x);
                Canvas.SetTop(selectionRect, y);
                selectionRect.Width = width;
                selectionRect.Height = height;

                UpdateSelectionInRealTime(new Rect(x, y, width, height));
            }

            // Обработка перетаскивания (частичное повторение написанного ранее)
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
        private void MainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isArrowModeActive)
            {
                e.Handled = true;
                return;
            }

            // Завершение выделения
            if (isSelecting)
            {
                isSelecting = false;
                MainCanvas.ReleaseMouseCapture();
                selectionRect.Visibility = Visibility.Collapsed;
            }

            // Завершение перетаскивания
            if (isDragging && draggedElement != null)
            {
                // Сохраняем текущую толщину ПОСЛЕ перетаскивания
                if (originalStrokeThickness.ContainsKey(draggedElement))
                {
                    // Обновляем сохраненную толщину текущим значением
                    if (draggedElement is Ellipse ellipseElement)
                    {
                        originalStrokeThickness[draggedElement] = ellipseElement.StrokeThickness;
                    }
                    else if (draggedElement is Border borderElement)
                    {
                        originalStrokeThickness[draggedElement] = borderElement.BorderThickness.Left;
                    }
                    else if (draggedElement is Line lineElement)
                    {
                        originalStrokeThickness[draggedElement] = lineElement.StrokeThickness;
                    }
                }

                isDragging = false;
                draggedElement.ReleaseMouseCapture();
                // ФИКСИРУЕМ ТРАНСФОРМАЦИЮ ДЛЯ РОМБА
                if (draggedElement is Border border && border.RenderTransform is RotateTransform rotateTransform)
                {
                    rotateTransform.Angle = 45;
                }

                // ВОЗВРАЩАЕМ НОРМАЛЬНЫЙ ZIndex ПОСЛЕ ПЕРЕТАСКИВАНИЯ
                Canvas.SetZIndex(draggedElement, 1000);
                ApplyCanvasDragVisualFeedback(draggedElement, false);
                draggedElement = null;
            }

            e.Handled = true;
        }

        // Метод применения цвета к выделенной фигуре
        private void ApplyColorToSelectedShape(string hexColor)
        {
            if (selectedShape != null)
            {
                Color newColor = (Color)ColorConverter.ConvertFromString("#" + hexColor);
                Brush newBrush = new SolidColorBrush(newColor);

                ApplyFillColorToShape(selectedShape, newBrush);

                // Сохраняем новый цвет как оригинальный
                if (originalFillColors.ContainsKey(selectedShape))
                {
                    originalFillColors[selectedShape] = newBrush;
                }
            }
        }

        // Удаление визуального выделения
        private void RemoveSelectionVisual(UIElement element)
        {
            if (element is Ellipse ellipse)
            {
                ellipse.StrokeThickness -= 2;
                ellipse.Opacity = 1.0;
            }
            else if (element is Border border)
            {
                border.BorderThickness = new Thickness(2);
                border.Opacity = 1.0;
            }
            else if (element is Line line)
            {
                line.StrokeThickness -= 2;
                line.Opacity = 1.0;
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
            if (selectedShape != null)
            {
                Color newColor = (Color)ColorConverter.ConvertFromString("#" + hexColor);
                Brush newBrush = new SolidColorBrush(newColor);

                ApplyStrokeColorToShape(selectedShape, newBrush);

                // Сохраняем новый цвет как оригинальный
                if (originalStrokeColors.ContainsKey(selectedShape))
                {
                    originalStrokeColors[selectedShape] = newBrush;
                }
            }
        }

        // Инициализация системы выделения
        private void InitializeSelectionSystem()
        {
            selectionRect = new Rectangle
            {
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(0x20, 0x1E, 0x90, 0xFF)),
                Visibility = Visibility.Collapsed
            };
            MainCanvas.Children.Add(selectionRect);
            Panel.SetZIndex(selectionRect, 1000);

            // Добавляем обработчики событий для выделения
            MainCanvas.MouseLeftButtonDown += MainCanvas_MouseLeftButtonDown;
            MainCanvas.MouseMove += MainCanvas_MouseMove;
            MainCanvas.MouseLeftButtonUp += MainCanvas_MouseLeftButtonUp;
            MainCanvas.MouseRightButtonDown += MainCanvas_MouseRightButtonDown;
        }

        private void ClearSelection()
        {
            foreach (var element in selectedElements.ToList())
            {
                DeselectElement(element);
            }

            DeselectArrow();
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

        private double GetElementHeight(UIElement element)
        {
            if (element is FrameworkElement fe)
                return fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;
            return 0;
        }

        private double GetElementWidth(UIElement element)
        {
            if (element is FrameworkElement fe)
                return fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
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
        private void CopySelectedElements()
        {
            if (selectedElements.Count > 0)
            {
                // Сохраняем копии выделенных элементов
                copiedElements = selectedElements.Select(CloneUIElement)
                                               .Cast<UIElement>()
                                               .ToList();
                MessageBox.Show($"Скопировано {selectedElements.Count} элементов", "Копирование", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Сначала выделите элементы", "Копирование", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PasteElements()
        {
            if (copiedElements != null && copiedElements.Count > 0)
            {
                var newElements = new List<UIElement>();

                foreach (var copiedElement in copiedElements)
                {
                    // Создаем копию со смещением
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

                    newElements.Add(newElement);

                    // Сохраняем в стек отмены
                    SaveToUndoStack(newElement);
                }

                // Выделяем вставленные элементы
                ClearSelection();
                foreach (var element in newElements)
                {
                    if (element is FrameworkElement frameworkElement)
                    {
                        SelectElement(frameworkElement);
                    }
                }

                MessageBox.Show($"Вставлено {newElements.Count} элементов", "Вставка", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Нет скопированных элементов", "Вставка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Добавляем поле для хранения скопированных элементов
        private List<UIElement> copiedElements = new List<UIElement>();

        /* // Создание сетки (должен существовать, нужно пересмотреть)
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
                CreateGrid();
                gridPath.Visibility = Visibility.Visible;
                GridToggleMenuItem.IsChecked = true;
                GridToggleMenuItem.Header = "Скрыть сетку";
            }
            else
            {
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
        }*/

        // Переключение режима рисования стрелок
        private void ToggleArrowMode()
        {
            isArrowModeActive = !isArrowModeActive;

            if (isArrowModeActive)
            {
                // Активируем режим рисования
                MainCanvas.Cursor = Cursors.Cross;
                pencil.Background = Brushes.LightBlue;
            }
            else
            {
                // Деактивируем режим рисования
                MainCanvas.Cursor = Cursors.Arrow;
                pencil.Background = Brushes.Transparent;

                if (currentArrow != null)
                {
                    CompleteArrow();
                }
            }
        }

        // Методы для рисования стрелок
        private void StartArrowDrawing(Point startPoint)
        {
            arrowStartPoint = startPoint;

            currentArrow = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 4,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Triangle,
                Points = new PointCollection { startPoint, startPoint }
            };

            MainCanvas.Children.Add(currentArrow);
            Canvas.SetZIndex(currentArrow, 500);
        }

        // Метод обновления рисования ломаной стрелки
        private void UpdateArrowDrawing(Point currentPoint)
        {
            if (currentArrow != null && currentArrow.Points.Count >= 1)
            {
                // Для ломаной линии добавляем новую точку при перемещении мыши
                // или обновляем последнюю точку для плавного рисования

                if (currentArrow.Points.Count == 1)
                {
                    // Первое перемещение - добавляем вторую точку
                    currentArrow.Points.Add(currentPoint);
                }
                else
                {
                    // Обновляем последнюю точку для плавного рисования
                    currentArrow.Points[currentArrow.Points.Count - 1] = currentPoint;
                }
            }
        }
        // Метод добавления излома (нового сегмента)
        private void AddArrowSegment(Point point)
        {
            if (currentArrow != null && currentArrow.Points.Count >= 2)
            {
                // Добавляем новую точку для создания излома
                currentArrow.Points.Add(point);
            }
        }

        private void CompleteArrow()
        {
            if (currentArrow != null && currentArrow.Points.Count >= 2)
            {
                // Удаляем последнюю точку если она совпадает с предыдущей
                if (currentArrow.Points.Count > 2)
                {
                    Point lastPoint = currentArrow.Points[currentArrow.Points.Count - 1];
                    Point prevPoint = currentArrow.Points[currentArrow.Points.Count - 2];

                    if (lastPoint == prevPoint)
                    {
                        currentArrow.Points.RemoveAt(currentArrow.Points.Count - 1);
                    }
                }

                // Получаем точки для наконечника
                Point lineStart = currentArrow.Points[currentArrow.Points.Count - 2];
                Point lineEnd = currentArrow.Points[currentArrow.Points.Count - 1];

                // Создаем контейнер стрелки с наконечником
                var arrowContainer = new ArrowContainer
                {
                    Line = currentArrow,
                    Arrowhead = CreateArrowhead(lineStart, lineEnd, currentArrow.Stroke)
                };

                // Добавляем наконечник на холст
                if (arrowContainer.Arrowhead != null)
                {
                    MainCanvas.Children.Add(arrowContainer.Arrowhead);
                }

                // Регистрируем стрелку
                RegisterArrowContainer(arrowContainer);

                // Сохраняем в стек отмены
                SaveToUndoStack(currentArrow);

                currentArrow = null;
                ToggleArrowMode();
            }

        }
        private void RegisterArrowContainer(ArrowContainer arrowContainer)
        {
            if (arrowContainer == null || arrowContainer.Line == null) return;

            CaptureArrowOriginalAppearance(arrowContainer);
            arrows.Add(arrowContainer);
            MakeArrowDraggable(arrowContainer);
            MakeArrowPointsDraggable(arrowContainer);
        }

        private void CaptureArrowOriginalAppearance(ArrowContainer arrowContainer)
        {
            if (arrowContainer == null || arrowContainer.Line == null) return;

            arrowContainer.OriginalStroke = arrowContainer.Line.Stroke?.CloneCurrentValue();
            arrowContainer.OriginalThickness = arrowContainer.Line.StrokeThickness;

            if (arrowContainer.Arrowhead != null)
            {
                arrowContainer.OriginalArrowheadFill = arrowContainer.Arrowhead.Fill?.CloneCurrentValue();
                arrowContainer.OriginalArrowheadStroke = arrowContainer.Arrowhead.Stroke?.CloneCurrentValue();
            }
        }

        private void ApplyArrowSelectionVisual(ArrowContainer arrowContainer)
        {
            if (arrowContainer == null) return;

            arrowContainer.Line.Stroke = Brushes.Blue;
            arrowContainer.Line.StrokeThickness = arrowContainer.OriginalThickness + 1;

            if (arrowContainer.Arrowhead != null)
            {
                arrowContainer.Arrowhead.Fill = Brushes.Blue;
                arrowContainer.Arrowhead.Stroke = Brushes.Blue;
            }
        }

        private void RestoreArrowAppearance(ArrowContainer arrowContainer)
        {
            if (arrowContainer == null) return;

            if (arrowContainer.OriginalStroke != null)
                arrowContainer.Line.Stroke = arrowContainer.OriginalStroke.CloneCurrentValue();

            arrowContainer.Line.StrokeThickness = arrowContainer.OriginalThickness;

            if (arrowContainer.Arrowhead != null)
            {
                if (arrowContainer.OriginalArrowheadFill != null)
                    arrowContainer.Arrowhead.Fill = arrowContainer.OriginalArrowheadFill.CloneCurrentValue();
                if (arrowContainer.OriginalArrowheadStroke != null)
                    arrowContainer.Arrowhead.Stroke = arrowContainer.OriginalArrowheadStroke.CloneCurrentValue();
            }
        }

        private void DeselectArrow()
        {
            if (selectedArrowContainer != null)
            {
                RestoreArrowAppearance(selectedArrowContainer);
                selectedArrowContainer = null;
            }
        }

        private void SetArrowDragVisual(ArrowContainer arrowContainer, bool isDraggingArrow)
        {
            if (arrowContainer == null) return;

            if (isDraggingArrow)
            {
                arrowContainer.Line.Stroke = Brushes.Red;
                arrowContainer.Line.StrokeThickness = arrowContainer.OriginalThickness + 1;

                if (arrowContainer.Arrowhead != null)
                {
                    arrowContainer.Arrowhead.Fill = Brushes.Red;
                    arrowContainer.Arrowhead.Stroke = Brushes.Red;
                }
            }
            else
            {
                if (selectedArrowContainer == arrowContainer)
                {
                    ApplyArrowSelectionVisual(arrowContainer);
                }
                else
                {
                    RestoreArrowAppearance(arrowContainer);
                }
            }
        }

        // Создание стрелки с наконечником
        private ArrowContainer CreateArrowWithArrowhead(Point startPoint, Point endPoint, Brush color, double thickness)
        {
            var arrowContainer = new ArrowContainer();

            // Создаем линию стрелки
            var arrow = new Polyline
            {
                Stroke = color,
                StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Triangle,
                Points = new PointCollection { startPoint, endPoint },
                Cursor = Cursors.Hand
            };

            // Создаем наконечник
            var arrowhead = CreateArrowhead(startPoint, endPoint, color);

            arrowContainer.Line = arrow;
            arrowContainer.Arrowhead = arrowhead;

            // Добавляем на холст
            MainCanvas.Children.Add(arrow);
            if (arrowhead != null)
            {
                MainCanvas.Children.Add(arrowhead);
            }

            RegisterArrowContainer(arrowContainer);

            return arrowContainer;
        }
        // Делаем стрелку перетаскиваемой
        private void MakeArrowDraggable(ArrowContainer arrowContainer)
        {
            arrowContainer.Line.MouseLeftButtonDown += Arrow_MouseLeftButtonDown;
            arrowContainer.Line.MouseMove += Arrow_MouseMove;
            arrowContainer.Line.MouseLeftButtonUp += Arrow_MouseLeftButtonUp;

            if (arrowContainer.Arrowhead != null)
            {
                arrowContainer.Arrowhead.MouseLeftButtonDown += (s, e) =>
                {
                    // При клике на наконечник выделяем всю стрелку
                    SelectArrow(arrowContainer.Line);
                    e.Handled = true;
                };
            }

            // Добавляем контекстное меню - ИСПРАВЛЕННЫЙ ВЫЗОВ
            arrowContainer.Line.MouseRightButtonDown += (s, e) =>
            {
                CreateArrowContextMenu(arrowContainer); // Убраны лишние параметры
                e.Handled = true;
            };
        }

        // Обработчики событий для перетаскивания стрелок
        private void Arrow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isArrowModeActive) return;

            var arrow = sender as Polyline;
            if (arrow != null)
            {
                // Находим контейнер стрелки
                var arrowContainer = arrows.FirstOrDefault(a => a.Line == arrow);
                if (arrowContainer == null) return;

                // Выделяем стрелку
                SelectArrow(arrow);

                isDragging = true;
                draggedElement = arrow;
                arrow.CaptureMouse();

                // Сохраняем начальную позицию мыши
                dragStartPoint = e.GetPosition(MainCanvas);

                // Сохраняем оригинальные точки стрелки
                arrowContainer.OriginalPoints = new PointCollection(arrow.Points);

                // Визуальный фидбэк
                SetArrowDragVisual(arrowContainer, true);

                e.Handled = true;
            }
        }
        private void Arrow_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var arrow = draggedElement as Polyline;
                if (arrow != null)
                {
                    // Находим контейнер стрелки
                    var arrowContainer = arrows.FirstOrDefault(a => a.Line == arrow);
                    if (arrowContainer == null || arrowContainer.OriginalPoints == null) return;

                    Point currentPosition = e.GetPosition(MainCanvas);

                    // Вычисляем смещение от начальной точки
                    double deltaX = currentPosition.X - dragStartPoint.X;
                    double deltaY = currentPosition.Y - dragStartPoint.Y;

                    // Создаем новую коллекцию точек с применением смещения
                    var newPoints = new PointCollection();
                    foreach (Point point in arrowContainer.OriginalPoints)
                    {
                        newPoints.Add(new Point(point.X + deltaX, point.Y + deltaY));
                    }

                    // Обновляем точки стрелки
                    arrow.Points = newPoints;

                    // ОБНОВЛЯЕМ ПОЗИЦИЮ НАКОНЕЧНИКА СИНХРОННО
                    if (arrowContainer.Arrowhead != null)
                    {
                        UpdateArrowheadPosition(arrowContainer);
                    }
                }
            }
        }

        private void MakeArrowPointsDraggable(ArrowContainer arrowContainer)
        {
            // Создаем невидимые маркеры для точек излома
            for (int i = 1; i < arrowContainer.Line.Points.Count - 1; i++)
            {
                var pointMarker = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.Transparent,
                    Cursor = Cursors.SizeAll,
                    Tag = i // Сохраняем индекс точки
                };

                // Позиционируем маркер на точке
                Point point = arrowContainer.Line.Points[i];
                Canvas.SetLeft(pointMarker, point.X - 5);
                Canvas.SetTop(pointMarker, point.Y - 5);

                MainCanvas.Children.Add(pointMarker);

                // Обработчики для перетаскивания точки
                pointMarker.MouseLeftButtonDown += (s, e) =>
                {
                    var marker = s as Ellipse;
                    if (marker != null && marker.Tag is int pointIndex)
                    {
                        isDragging = true;
                        draggedElement = marker;
                        marker.CaptureMouse();
                        dragStartPoint = e.GetPosition(MainCanvas);

                        // Сохраняем оригинальные точки
                        arrowContainer.OriginalPoints = new PointCollection(arrowContainer.Line.Points);
                        e.Handled = true;
                    }
                };

                pointMarker.MouseMove += (s, e) =>
                {
                    var marker = s as Ellipse;
                    if (isDragging && marker != null && marker.Tag is int pointIndex &&
                        arrowContainer.OriginalPoints != null)
                    {
                        Point currentPosition = e.GetPosition(MainCanvas);

                        // Обновляем конкретную точку в стрелке
                        var newPoints = new PointCollection(arrowContainer.Line.Points);
                        newPoints[pointIndex] = currentPosition;

                        arrowContainer.Line.Points = newPoints;

                        // Обновляем позицию маркера
                        Canvas.SetLeft(marker, currentPosition.X - 5);
                        Canvas.SetTop(marker, currentPosition.Y - 5);

                        // Обновляем наконечник если это последняя точка
                        if (pointIndex == arrowContainer.Line.Points.Count - 2)
                        {
                            UpdateArrowheadPosition(arrowContainer);
                        }
                    }
                };

                pointMarker.MouseLeftButtonUp += (s, e) =>
                {
                    var marker = s as Ellipse;
                    if (marker != null)
                    {
                        isDragging = false;
                        marker.ReleaseMouseCapture();
                        arrowContainer.OriginalPoints = null;
                        draggedElement = null;
                        e.Handled = true;
                    }
                };
            }
        }



        private void Arrow_MouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            var arrow = sender as Polyline;
            if (arrow != null)
            {
                // Находим контейнер стрелки
                var arrowContainer = arrows.FirstOrDefault(a => a.Line == arrow);
                if (arrowContainer != null)
                {
                    isDragging = false;
                    arrow.ReleaseMouseCapture();

                    // ФИНАЛЬНОЕ ОБНОВЛЕНИЕ НАКОНЕЧНИКА
                    UpdateArrowheadPosition(arrowContainer);

                    SetArrowDragVisual(arrowContainer, false);

                    // Очищаем временные данные
                    arrowContainer.OriginalPoints = null;
                    draggedElement = null;
                }
                e.Handled = true;
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
            originalStrokeColors[shape] = brush;
        }
        // Метод для очистки комбо-боксов при запуске программы
        private void ClearColorComboBoxesOnStartup()
        {
            FillColorComboBox.SelectedIndex = -1;
            StrokeColorComboBox.SelectedIndex = -1;
            FillColorPreview.Background = Brushes.Transparent;
            StrokeColorPreview.Background = Brushes.Transparent;
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
        private void CommentButton_Click(object sender, RoutedEventArgs e)
        {
            // Переключаем режим комментариев
            isCommentModeActive = !isCommentModeActive;

            if (isCommentModeActive)
            {
                MainCanvas.Cursor = Cursors.Hand;
            }
            else
            {
                MainCanvas.Cursor = Cursors.Arrow;
            }
        }

        // Обработчик кнопки комментариев (старый метод для совместимости)
        private void CommentModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                isCommentModeActive = button.IsChecked == true;
            }
            else
            {
                isCommentModeActive = !isCommentModeActive;
            }

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
                CreateCommentAtPosition(clickPosition);
                e.Handled = true;
            }
        }
        // Создание комментария в указанной позиции
        private void CreateCommentAtPosition(Point position)
        {
            // Создаем визуальный элемент комментария
            var commentIcon = new Border
            {
                Width = 12,
                Height = 12,
                Background = Brushes.Red,
                BorderBrush = Brushes.Red,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand
            };

            // Создаем TextBlock для отображения текста
            var commentTextBlock = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(4),
                Child = new TextBlock
                {
                    Text = "",
                    Foreground = Brushes.Black,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 250
                },
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false // Чтобы не мешал взаимодействию
            };

            // Устанавливаем позицию
            Canvas.SetLeft(commentIcon, position.X - 6);
            Canvas.SetTop(commentIcon, position.Y - 6);

            // Позиционируем TextBlock рядом с иконкой
            Canvas.SetLeft(commentTextBlock, position.X + 15);
            Canvas.SetTop(commentTextBlock, position.Y - 10);

            // Добавляем на холст
            MainCanvas.Children.Add(commentIcon);
            MainCanvas.Children.Add(commentTextBlock);

            // Создаем комментарий
            var comment = new Comment
            {
                VisualElement = commentIcon,
                TextElement = commentTextBlock,
                Position = position,
                Text = ""
            };

            comments.Add(comment);

            // Обработчик двойного клика
            commentIcon.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    EditComment(comment);
                    e.Handled = true;
                }
            };

            // Показ текста при наведении
            commentIcon.MouseEnter += (s, e) =>
            {
                commentIcon.Opacity = 0.7;

                if (!string.IsNullOrEmpty(comment.Text))
                {
                    if (commentTextBlock.Child is TextBlock textBlock)
                    {
                        textBlock.Text = comment.Text;
                    }
                    commentTextBlock.Visibility = Visibility.Visible;
                    Canvas.SetZIndex(commentTextBlock, 1000); // Поверх других элементов
                }
            };

            commentIcon.MouseLeave += (s, e) =>
            {
                commentIcon.Opacity = 1.0;
                commentTextBlock.Visibility = Visibility.Collapsed;
            };

            // Сразу открываем редактор
            EditComment(comment);
        }
        // Редактирование комментария
        private void EditComment(Comment comment)
        {
            var editTextBox = new TextBox
            {
                Width = 200,
                Height = 100,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = false,
                Text = comment.Text,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            // Позиционируем рядом с комментарием
            double left = Canvas.GetLeft(comment.VisualElement) + 20;
            double top = Canvas.GetTop(comment.VisualElement);
            Canvas.SetLeft(editTextBox, left);
            Canvas.SetTop(editTextBox, top);

            MainCanvas.Children.Add(editTextBox);
            editTextBox.Focus();
            editTextBox.SelectAll();

            // Завершение редактирования
            void FinishEditing()
            {
                string newText = editTextBox.Text.Trim();

                if (string.IsNullOrEmpty(newText))
                {
                    DeleteComment(comment);
                }
                else
                {
                    comment.Text = newText;

                    if (comment.VisualElement is Border border)
                    {
                        border.Background = Brushes.Red;
                    }

                    // Обновляем TextBlock
                    if (comment.TextElement is Border textBorder && textBorder.Child is TextBlock textBlock)
                    {
                        textBlock.Text = newText;
                    }
                }

                MainCanvas.Children.Remove(editTextBox);
            }

            editTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    // Если нажат Ctrl+Enter - добавляем новую строку
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        // Разрешаем обычное поведение TextBox (добавление новой строки)
                        return;
                    }

                    // Обычный Enter - завершаем редактирование
                    FinishEditing();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    // При отмене проверяем, был ли текст изначально
                    if (string.IsNullOrEmpty(comment.Text))
                    {
                        DeleteComment(comment);
                    }
                    MainCanvas.Children.Remove(editTextBox);
                    e.Handled = true;
                }
            };

            editTextBox.LostFocus += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (MainCanvas.Children.Contains(editTextBox))
                    {
                        FinishEditing();
                    }
                }));
            };
        }
        // Удаление комментария
        private void DeleteComment(Comment comment)
        {
            if (comment.VisualElement != null)
            {
                MainCanvas.Children.Remove(comment.VisualElement);
            }
            if (comment.TextElement != null)
            {
                MainCanvas.Children.Remove(comment.TextElement);
            }
            comments.Remove(comment);
        }

        // Выделение стрелки
        private void SelectArrow(Polyline arrow)
        {
            // Снимаем выделение с других элементов
            ClearSelection();

            // Находим контейнер стрелки
            var arrowContainer = arrows.FirstOrDefault(a => a.Line == arrow);
            if (arrowContainer == null) return;

            selectedArrowContainer = arrowContainer;
            ApplyArrowSelectionVisual(arrowContainer);

            CreateArrowContextMenu(arrowContainer);
        }

        // Контекстное меню для стрелки
        private void CreateArrowContextMenu(ArrowContainer arrowContainer)
        {
            ContextMenu contextMenu = new ContextMenu
            {
                Background = Brushes.White,
                Foreground = Brushes.Black,
                FontSize = 12,
                Padding = new Thickness(5)
            };

            // Пункт "Изменить цвет"
            MenuItem colorMenuItem = new MenuItem
            {
                Header = "Изменить цвет",
                Icon = new TextBlock { Text = "🎨", FontSize = 14 }
            };
            colorMenuItem.Click += (s, e) => ChangeArrowColor(arrowContainer);
            contextMenu.Items.Add(colorMenuItem);

            // Пункт "Изменить толщину"
            MenuItem thicknessMenuItem = new MenuItem
            {
                Header = "Изменить толщину",
                Icon = new TextBlock { Text = "📏", FontSize = 14 }
            };
            thicknessMenuItem.Click += (s, e) => ChangeArrowThickness(arrowContainer);
            contextMenu.Items.Add(thicknessMenuItem);

            contextMenu.Items.Add(new Separator());

            // Пункт "Удалить"
            MenuItem deleteMenuItem = new MenuItem
            {
                Header = "Удалить",
                Icon = new TextBlock { Text = "🗑", FontSize = 14 }
            };
            deleteMenuItem.Click += (s, e) => DeleteArrow(arrowContainer.Line);
            contextMenu.Items.Add(deleteMenuItem);

            arrowContainer.Line.ContextMenu = contextMenu;
        }

        // Изменение цвета стрелки
        private void ChangeArrowColor(ArrowContainer arrowContainer)
        {
            var dialog = new Window
            {
                Title = "Выбор цвета",
                Width = 300,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stackPanel = new StackPanel();

            // ComboBox с основными цветами
            var colorComboBox = new ComboBox
            {
                Margin = new Thickness(10),
                ItemsSource = new[]
                {
                    new { Name = "Красный", Color = Colors.Red },
                    new { Name = "Синий", Color = Colors.Blue },
                    new { Name = "Зеленый", Color = Colors.Green },
                    new { Name = "Черный", Color = Colors.Black },
                    new { Name = "Белый", Color = Colors.White }
                },
                DisplayMemberPath = "Name"
            };

            // Кнопки
            var okButton = new Button { Content = "OK", Width = 80 };
            var cancelButton = new Button { Content = "Отмена", Width = 80 };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(colorComboBox);
            stackPanel.Children.Add(buttonPanel);
            dialog.Content = stackPanel;

            okButton.Click += (s, e) =>
            {
                if (colorComboBox.SelectedValue != null)
                {
                    var selectedColor = (colorComboBox.SelectedValue as dynamic)?.Color;
                    Brush newBrush = new SolidColorBrush(selectedColor);

                    // Применяем цвет к линии
                    arrowContainer.Line.Stroke = newBrush;

                    // Применяем цвет к наконечнику
                    if (arrowContainer.Arrowhead != null)
                    {
                        arrowContainer.Arrowhead.Fill = newBrush;
                        arrowContainer.Arrowhead.Stroke = newBrush;
                    }

                    CaptureArrowOriginalAppearance(arrowContainer);

                    if (selectedArrowContainer == arrowContainer)
                    {
                        ApplyArrowSelectionVisual(arrowContainer);
                    }
                }
                dialog.DialogResult = true;
            };

            cancelButton.Click += (s, e) => dialog.DialogResult = false;

            dialog.ShowDialog();
        }

        // Изменение толщины стрелки
        private void ChangeArrowThickness(ArrowContainer arrowContainer)
        {
            var inputDialog = new InputDialog("Толщина стрелки", "Введите толщину:", arrowContainer.Line.StrokeThickness.ToString());
            if (inputDialog.ShowDialog() == true)
            {
                if (double.TryParse(inputDialog.Answer, out double thickness) && thickness > 0)
                {
                    arrowContainer.Line.StrokeThickness = thickness;
                    arrowContainer.OriginalThickness = thickness;

                    if (selectedArrowContainer == arrowContainer)
                    {
                        ApplyArrowSelectionVisual(arrowContainer);
                    }
                }
            }
        }

        // Удаление стрелки
        private void DeleteArrow(Polyline arrow)
        {
            // Находим контейнер стрелки
            var arrowContainer = arrows.FirstOrDefault(a => a.Line == arrow);
            if (arrowContainer != null)
            {
                if (selectedArrowContainer == arrowContainer)
                {
                    DeselectArrow();
                }

                // Удаляем линию
                if (MainCanvas.Children.Contains(arrowContainer.Line))
                {
                    SaveToUndoStack(arrowContainer.Line);
                    MainCanvas.Children.Remove(arrowContainer.Line);
                }

                // Удаляем наконечник
                if (arrowContainer.Arrowhead != null && MainCanvas.Children.Contains(arrowContainer.Arrowhead))
                {
                    MainCanvas.Children.Remove(arrowContainer.Arrowhead);
                }

                // Удаляем из списка
                arrows.Remove(arrowContainer);
            }
        }
        private void RemoveArrowhead(Polyline arrow)
        {
            // Находим контейнер стрелки
            var arrowContainer = arrows.FirstOrDefault(a => a.Line == arrow);
            if (arrowContainer != null && arrowContainer.Arrowhead != null)
            {
                if (MainCanvas.Children.Contains(arrowContainer.Arrowhead))
                {
                    MainCanvas.Children.Remove(arrowContainer.Arrowhead);
                }
                arrowContainer.Arrowhead = null;
            }

            /* var arrowheadToRemove = arrows.OfType<Polygon>()
                                           .FirstOrDefault(ah => IsArrowheadForLine(ah, arrow));

             if (arrowheadToRemove != null)
             {
                 MainCanvas.Children.Remove(arrowheadToRemove);
                 arrows.Remove(arrowheadToRemove);
             }*/
        }
        // Создание стрелки из данных файла
        private void CreateArrowFromData(string[] data)
        {
            if (data.Length >= 4)
            {
                var arrow = new Polyline
                {
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data[2])),
                    StrokeThickness = double.Parse(data[3]),
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Triangle,
                    Points = new PointCollection(),
                    Cursor = Cursors.Hand
                };

                // Парсим точки
                string[] pointStrings = data[1].Split(';');
                foreach (string pointStr in pointStrings)
                {
                    string[] coords = pointStr.Split(',');
                    if (coords.Length == 2)
                    {
                        arrow.Points.Add(new Point(double.Parse(coords[0]), double.Parse(coords[1])));
                    }
                }

                // Создаем контейнер стрелки
                var arrowContainer = new ArrowContainer { Line = arrow };

                // Создаем наконечник для загруженной стрелки
                if (arrow.Points.Count >= 2)
                {
                    Point lineStart = arrow.Points[arrow.Points.Count - 2];
                    Point lineEnd = arrow.Points[arrow.Points.Count - 1];
                    arrowContainer.Arrowhead = CreateArrowhead(lineStart, lineEnd, arrow.Stroke);
                }

                // Добавляем на холст
                MainCanvas.Children.Add(arrow);
                if (arrowContainer.Arrowhead != null)
                {
                    MainCanvas.Children.Add(arrowContainer.Arrowhead);
                }

                // Добавляем в список и делаем перетаскиваемой
                RegisterArrowContainer(arrowContainer);
            }
        }

        // Обработчик для кнопки пера на панели инструментов
        private void PenButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleArrowMode(); //изменить название кнопки
        }

        // Метод для создания треугольника-наконечника
        private Polygon CreateArrowhead(Point startPoint, Point endPoint, Brush color)
        {
            // Вычисляем направление стрелки
            double dx = endPoint.X - startPoint.X;
            double dy = endPoint.Y - startPoint.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length == 0) return null;

            // Нормализуем вектор направления
            dx /= length;
            dy /= length;

            // Размеры наконечника
            double arrowLength = 20;
            double arrowWidth = 12;

            // Вычисляем точки треугольника
            Point arrowTip = endPoint;
            Point arrowLeft = new Point(
                endPoint.X - dx * arrowLength + dy * arrowWidth,
                endPoint.Y - dy * arrowLength - dx * arrowWidth
            );
            Point arrowRight = new Point(
                endPoint.X - dx * arrowLength - dy * arrowWidth,
                endPoint.Y - dy * arrowLength + dx * arrowWidth
            );

            // Создаем треугольник
            Polygon arrowhead = new Polygon
            {
                Points = new PointCollection { arrowTip, arrowLeft, arrowRight },
                Fill = color,
                Stroke = color,
                StrokeThickness = 1,
                Cursor = Cursors.Hand
            };

            return arrowhead;
        }

        // Метод для перетаскивания наконечника вместе со стрелкой
        private void MakeArrowheadDraggable(Polygon arrowhead, Polyline arrow)
        {
            arrowhead.MouseLeftButtonDown += (s, e) =>
            {
                // При клике на наконечник выделяем всю стрелку
                SelectArrow(arrow);
                e.Handled = true;
            };
        }
        private void UpdateArrowheadPosition(ArrowContainer arrowContainer)
        {
            if (arrowContainer.Line.Points.Count < 2 || arrowContainer.Arrowhead == null) return;

            // Находим последний сегмент стрелки
            Point lineStart = arrowContainer.Line.Points[arrowContainer.Line.Points.Count - 2];
            Point lineEnd = arrowContainer.Line.Points[arrowContainer.Line.Points.Count - 1];

            // ОБНОВЛЯЕМ ГЕОМЕТРИЮ НАКОНЕЧНИКА НА МЕСТЕ
            UpdateArrowheadGeometry(arrowContainer.Arrowhead, lineStart, lineEnd, arrowContainer.Line.Stroke);
        }

        // Метод для создания и прикрепления наконечника
        private void CreateAndAttachArrowhead(Polyline arrow, Point lineStart, Point lineEnd)
        {
            Polygon arrowhead = CreateArrowhead(lineStart, lineEnd, arrow.Stroke);
            if (arrowhead != null)
            {
                MainCanvas.Children.Add(arrowhead);
                arrowheads.Add(arrowhead); // Используем правильную коллекцию
                MakeArrowheadDraggable(arrowhead, arrow);

                StoreArrowheadRelation(arrow, arrowhead);
            }
        }

        // Метод для сохранения связи между стрелкой и наконечником
        private void StoreArrowheadRelation(Polyline arrow, Polygon arrowhead)
        {
            arrowToArrowheadMap[arrow] = arrowhead;
        }


        // Метод для обновления геометрии наконечника
        private void UpdateArrowheadGeometry(Polygon arrowhead, Point lineStart, Point lineEnd, Brush color)
        {
            // Вычисляем направление стрелки
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length == 0) return;

            // Нормализуем вектор направления
            dx /= length;
            dy /= length;

            // Размеры наконечника
            double arrowLength = 20;
            double arrowWidth = 12;

            // Вычисляем точки треугольника
            // Острие наконечника должно быть точно в конце стрелки
            Point arrowTip = lineEnd;

            // Боковые точки наконечника
            Point arrowLeft = new Point(
                lineEnd.X - dx * arrowLength + dy * arrowWidth,
                lineEnd.Y - dy * arrowLength - dx * arrowWidth
            );
            Point arrowRight = new Point(
                lineEnd.X - dx * arrowLength - dy * arrowWidth,
                lineEnd.Y - dy * arrowLength + dx * arrowWidth
            );

            // Обновляем точки наконечника
            arrowhead.Points = new PointCollection { arrowTip, arrowLeft, arrowRight };
            arrowhead.Fill = color;
            arrowhead.Stroke = color;
        }

        // Метод для обновления точек существующего наконечника
        private void UpdateArrowheadPoints(Polygon arrowhead, Point lineStart, Point lineEnd, Brush color)
        {
            // Вычисляем направление стрелки
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length == 0) return;

            // Нормализуем вектор направления
            dx /= length;
            dy /= length;

            // Размеры наконечника
            double arrowLength = 15; // Увеличим для лучшей видимости
            double arrowWidth = 10;

            // Вычисляем новые точки треугольника
            // Острие наконечника должно быть точно в конце стрелки
            Point arrowTip = lineEnd;

            // Боковые точки наконечника
            Point arrowLeft = new Point(
                lineEnd.X - dx * arrowLength + dy * arrowWidth,
                lineEnd.Y - dy * arrowLength - dx * arrowWidth
            );
            Point arrowRight = new Point(
                lineEnd.X - dx * arrowLength - dy * arrowWidth,
                lineEnd.Y - dy * arrowLength + dx * arrowWidth
            );

            // Обновляем точки наконечника
            arrowhead.Points = new PointCollection { arrowTip, arrowLeft, arrowRight };
            arrowhead.Fill = color;
            arrowhead.Stroke = color;
        }


        // Проверка, принадлежит ли наконечник стрелке
        // Улучшенный метод проверки принадлежности наконечника
        private bool IsArrowheadForLine(Polygon arrowhead, Polyline arrow)
        {
            if (arrow.Points.Count < 2 || arrowhead.Points.Count == 0) return false;

            Point lineEnd = arrow.Points[arrow.Points.Count - 1];
            Point arrowheadTip = arrowhead.Points[0]; // Первая точка - острие наконечника

            // Проверяем близость острия наконечника к концу стрелки
            double distance = Math.Sqrt(Math.Pow(arrowheadTip.X - lineEnd.X, 2) + Math.Pow(arrowheadTip.Y - lineEnd.Y, 2));
            return distance < 15; // Увеличиваем допуск для лучшего определения
        }

        // Получение центра полигона
        private Point GetPolygonCenter(Polygon polygon)
        {
            double centerX = 0, centerY = 0;
            foreach (Point point in polygon.Points)
            {
                centerX += point.X;
                centerY += point.Y;
            }
            return new Point(centerX / polygon.Points.Count, centerY / polygon.Points.Count);
        }

        private void MainCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isArrowModeActive && currentArrow != null)
            {
                // Завершаем рисование стрелки по правой кнопке
                CompleteArrow();
                e.Handled = true;
            }
        }

        // Метод для поиска наконечника по стрелке
        private Polygon FindArrowheadForArrow(Polyline arrow)
        {
            var arrowContainer = arrows.FirstOrDefault(a => a.Line == arrow);
            return arrowContainer?.Arrowhead;
        }

    }
}