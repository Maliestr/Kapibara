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
    // Тип действия для системы отмены/повтора
    public enum UndoActionType
    {
        Add,    // Элемент был добавлен
        Remove  // Элемент был удален
    }

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
        private SelectionManager selectionManager; //Менеджер системы выделения
        private FrameworkElement copiedElement; //Элемент в буфере обмена для копирования/вставки
        private Point lastMousePosition; //Последняя позиция мыши (для отслеживания движения)
        private Dictionary<UIElement, ResizableElement> resizableElements = 
            new Dictionary<UIElement, ResizableElement>(); //Хранит связь между элементами на холсте и их контроллерами изменения размера
        private ResizableElement currentResizableElement; //Текущий активный изменяемый в размерах элемент

        //Система автосохранения
        private AutoSave autoSave; //Система автоматического сохранения проекта

        //Система отмены/повтора

        private class UndoAction
        {
            public UndoActionType ActionType { get; set; }
            public UIElement Element { get; set; } // Копия элемента для восстановления
            public UIElement OriginalElement { get; set; } // Ссылка на оригинальный элемент (для Remove)

            public UndoAction(UndoActionType actionType, UIElement element, UIElement originalElement = null)
            {
                ActionType = actionType;
                Element = element;
                OriginalElement = originalElement;
            }
        }

        private Stack<UndoAction> undoStack = new Stack<UndoAction>(); //Стек для отмены действий
        private Stack<UndoAction> redoStack = new Stack<UndoAction>(); //Стек для повтора действий

        //Система сетки
        private bool isGridEnabled = false; //Отображение сетки - нет
        private bool isSnapToGridEnabled = false; //Привязка к сетке - нет
        private System.Windows.Shapes.Path gridPath; //Отрисовка линий сетки
        private const double GridSize = 20; //Размер ячейки сетки в пикселях


        //Система сохранения оригинальных стилей (теперь управляется SelectionManager)
        // Все операции с оригинальными стилями выполняются через selectionManager

        //Управление интерфейсом
        private bool arePanelsVisible = true; //Видимость всех панелей инструментов
        private List<Border> panels = new List<Border>(); //Список всех плашек для управления видимостью

        //Система комментариев
        private bool isCommentModeActive = false; //Активность режима добавления комментариев - нет
        private List<Comment> comments = new List<Comment>(); //Список всех комментариев на холсте

        //Система рисования стрелок
        private ArrowManager arrowManager;

        //Система управления текстом
        private TextManager textManager;

        //Не работает
        private const double WorkAreaTop = 40;    // Отступ сверху (под меню)
        private const double WorkAreaBottom = 100; // Отступ снизу (над панелью инструментов)

        private bool isCursor = true;

        public class Comment //Класс для комментирования
        {
            public UIElement VisualElement { get; set; }
            public UIElement TextElement { get; set; }
            public string Text { get; set; }
            public Point Position { get; set; }
        }


        public MainWindow()
        {
            InitializeComponent(); //Инициализация WPF компонентов

            MakeAllShapesDraggable(); //Делает фигуры в панели инструментов перетаскиваемыми
            //ClearColorComboBoxesOnStartup(); // Очищаем блоки свойств заливки и обводки при запуске

            InitializeGrid(); //Создает и инициализирует визуальную сетку
            InitializePanels(); //Находит и регистрирует все панели выделения
            InitializePropertyBlocks(); //Скрывает блоки свойств при загрузке приложения
            InitializeArrowManager(); //Инициализация менеджера стрелок
            InitializeSelectionSystem(); //Настраивает систему выделения через SelectionManager
            InitializeTextManager(); //Инициализация менеджера текста
            isActiveCursor();

            // Обработка горячих клавиш
            this.KeyDown += MainWindow_KeyDown; //Ctrl+Z/Y - отмена/повтор
                                                //Ctrl+X/C/V - вырезание/копирование/вставка
                                                //Delete - удаление выделенного
                                                //Ctrl+G - переключение сетки
                                                //Ctrl+S - привязка к сетке
                                                //F12/Ctrl+H - скрытие панелей

            // Добавляем обработчик клика по Canvas для сброса выделения
            MainCanvas.MouseLeftButtonDown += MainCanvas_MouseLeftButtonDown;

            // Кнопка курсора для обработки взаимодействия с объектами
            cursor.Click += CursorButton_Click;
            // Кнопка пера включает/выключает режим рисования стрелок
            pencil.Click += PenButton_Click;
            // Добавляем обработчики для комментариев (переключает режим комментариев)
            comment.Click += CommentButton_Click;

            // Обработчики для размеров текстовых полей
            WidthTextBox.LostFocus += DimensionTextBox_LostFocus;
            HeightTextBox.LostFocus += DimensionTextBox_LostFocus;
            WidthTextBox.KeyDown += DimensionTextBox_KeyDown;
            HeightTextBox.KeyDown += DimensionTextBox_KeyDown;

            // Инициализация системы автосохранения (интервал 60 секунд)
            autoSave = new AutoSave(MainCanvas, intervalSeconds: 60, enabled: true);

            // Обработчик закрытия окна для очистки ресурсов
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Останавливаем автосохранение и сохраняем перед закрытием
            if (autoSave != null)
            {
                autoSave.SaveNow(); // Принудительное сохранение перед закрытием
                autoSave.Dispose();
            }
        }

        // Обработчики для ручного ввода размеров
        private void DimensionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyDimensionsFromTextboxes();
        }

        // Принадлежит ли элемент панели нотации
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
            var selectedShape = selectionManager?.SelectedShape;
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

        // Инициализация сетки
        private void InitializeGrid()
        {
            gridPath = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)), // Полупрозрачный серый
                StrokeThickness = 0.5,
                IsHitTestVisible = false // Сетка не должна мешать взаимодействию
            };

            // Добавляем сетку на Canvas, но скрываем
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
            // Сохраняем текущее состояние перед проверкой
            bool wasGridEnabled = isGridEnabled;
            
            // Не позволяем отключить сетку, если включена привязка
            if (wasGridEnabled && isSnapToGridEnabled)
            {
                // Отменяем изменение состояния меню
                GridToggleMenuItem.IsChecked = true;
                e.Handled = true;
                return;
            }
            
            ToggleGridVisibility();
        }

        // Обработчик переключения привязки к сетке
        private void SnapToGridMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем текущее состояние перед проверкой
            bool wasSnapEnabled = isSnapToGridEnabled;
            
            // Не позволяем включить привязку, если сетка не включена
            if (!wasSnapEnabled && !isGridEnabled)
            {
                // Отменяем изменение состояния меню
                SnapToGridMenuItem.IsChecked = false;
                e.Handled = true;
                return;
            }
            
            ToggleSnapToGrid();
        }

        // Метод переключения сетки
        private void ToggleGridVisibility()
        {
            // Не позволяем отключить сетку, если включена привязка
            if (isGridEnabled && isSnapToGridEnabled)
            {
                // Не позволяем отключить сетку, если включена привязка
                return;
            }

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
            // Не позволяем включить привязку, если сетка не включена
            if (!isSnapToGridEnabled && !isGridEnabled)
            {
                // Сначала нужно включить сетку
                return;
            }

            isSnapToGridEnabled = !isSnapToGridEnabled;

            if (isSnapToGridEnabled)
            {
                SnapToGridMenuItem.IsChecked = true;
                SnapToGridMenuItem.Header = "Отключить привязку";

                // Автоматически включаем сетку если она выключена (на случай если сетка была отключена другим способом)
                if (!isGridEnabled)
                {
                    isGridEnabled = true;
                    CreateGrid();
                    gridPath.Visibility = Visibility.Visible;
                    GridToggleMenuItem.IsChecked = true;
                    GridToggleMenuItem.Header = "Скрыть сетку";
                }
                
                // Привязываем все существующие элементы к сетке
                SnapAllElementsToGrid();
            }
            else
            {
                SnapToGridMenuItem.IsChecked = false;
                SnapToGridMenuItem.Header = "Привязка к сетке";
            }
            
            // Обновляем привязку во всех существующих ResizableElement
            foreach (var resizableElement in resizableElements.Values)
            {
                resizableElement.UpdateSnapToGrid(SnapToGrid, isSnapToGridEnabled);
            }
        }
        
        // Метод для привязки всех элементов на холсте к сетке
        private void SnapAllElementsToGrid()
        {
            foreach (UIElement element in MainCanvas.Children)
            {
                // Пропускаем сетку и другие служебные элементы
                if (element == gridPath || element is System.Windows.Shapes.Path)
                    continue;
                    
                // Пропускаем элементы из панели нотации
                if (IsElementInNotation(element))
                    continue;
                
                // Обрабатываем Line элементы отдельно (они используют X1, Y1, X2, Y2)
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
                
                // Обрабатываем Polyline элементы (стрелки) - привязываем все точки
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
                
                // Обрабатываем остальные элементы (Ellipse, Border, TextBlock и т.д.)
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

        // Метод для привязки точки к сетке
        private Point SnapToGrid(Point originalPoint)
        {
            if (!isSnapToGridEnabled || GridSize <= 0) return originalPoint;

            double snappedX = Math.Round(originalPoint.X / GridSize) * GridSize;
            double snappedY = Math.Round(originalPoint.Y / GridSize) * GridSize;

            return new Point(snappedX, snappedY);
        }

        // Метод перемещения фигур на Canvas
        private void CanvasShape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = draggedElement;

                // ВАЖНО: Привязываем позицию курсора к сетке перед вычислением
                Point currentPosition = e.GetPosition(MainCanvas);
                if (isSnapToGridEnabled)
                {
                    currentPosition = SnapToGrid(currentPosition);
                }

                // Вычисляем новую позицию с учетом offset
                double newLeft = currentPosition.X - offset.X;
                double newTop = currentPosition.Y - offset.Y;
                // ОГРАНИЧИВАЕМ ПЕРЕМЕЩЕНИЕ РАБОЧЕЙ ОБЛАСТЬЮ
                double elementWidth = GetElementWidth(element);
                double elementHeight = GetElementHeight(element);

                double maxTop = MainCanvas.ActualHeight - elementHeight - WorkAreaBottom;

                newLeft = Math.Max(0, Math.Min(newLeft, MainCanvas.ActualWidth - elementWidth));
                newTop = Math.Max(WorkAreaTop, Math.Min(newTop, maxTop));

                // ДОПОЛНИТЕЛЬНО ПРИВЯЗЫВАЕМ ФИНАЛЬНУЮ ПОЗИЦИЮ
                if (isSnapToGridEnabled)
                {
                    Point snappedPosition = SnapToGrid(new Point(newLeft, newTop));
                    newLeft = snappedPosition.X;
                    newTop = snappedPosition.Y;
                }

                // Обрабатываем Line элементы отдельно
                if (element is Line line)
                {
                    double currentX1 = line.X1;
                    double currentY1 = line.Y1;
                    double currentX2 = line.X2;
                    double currentY2 = line.Y2;

                    // Вычисляем смещение линии
                    double lineLeft = Math.Min(currentX1, currentX2);
                    double lineTop = Math.Min(currentY1, currentY2);
                    double deltaX = newLeft - lineLeft;
                    double deltaY = newTop - lineTop;

                    // Применяем смещение к обеим точкам линии
                    double newX1 = currentX1 + deltaX;
                    double newY1 = currentY1 + deltaY;
                    double newX2 = currentX2 + deltaX;
                    double newY2 = currentY2 + deltaY;

                    // Применяем привязку к сетке к обеим точкам линии
                    if (isSnapToGridEnabled)
                    {
                        Point snappedStart = SnapToGrid(new Point(newX1, newY1));
                        Point snappedEnd = SnapToGrid(new Point(newX2, newY2));
                        newX1 = snappedStart.X;
                        newY1 = snappedStart.Y;
                        newX2 = snappedEnd.X;
                        newY2 = snappedEnd.Y;
                    }

                    line.X1 = newX1;
                    line.Y1 = newY1;
                    line.X2 = newX2;
                    line.Y2 = newY2;
                }
                else
                {
                    // Для остальных элементов
                    Canvas.SetLeft(element, newLeft);
                    Canvas.SetTop(element, newTop);
                }
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

        // Обновляем сетку когда Canvas полностью загружен и проинициализирован
        private void MainCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (isGridEnabled)
            {
                CreateGrid();
            }
        }

        // Горячие клавиши для удобства
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Обработка Escape для завершения стрелки
            if (e.Key == Key.Escape && arrowManager != null && arrowManager.IsArrowModeActive && arrowManager.CurrentArrow != null)
            {
                arrowManager.CompleteArrow();
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
            
            // Сбрасываем имя файла на "Untitled"
            if (FileNameTextBox != null)
            {
                FileNameTextBox.Text = "Untitled";
            }
            
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
                    // LoadFromFile уже обновляет TextBox, но обновим заголовок окна
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
            
            // Используем имя из TextBox для предложения имени файла
            if (FileNameTextBox != null && !string.IsNullOrEmpty(FileNameTextBox.Text) && FileNameTextBox.Text != "Untitled")
            {
                string fileNameFromTextBox = FileNameTextBox.Text;
                // Убираем недопустимые символы для имени файла
                string invalidChars = new string(System.IO.Path.GetInvalidFileNameChars());
                foreach (char c in invalidChars)
                {
                    fileNameFromTextBox = fileNameFromTextBox.Replace(c, '_');
                }
                saveFileDialog.FileName = fileNameFromTextBox;
            }

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    SaveToFile(saveFileDialog.FileName);
                    currentFilePath = saveFileDialog.FileName;
                    
                    // Обновляем TextBox с именем файла без расширения
                    if (FileNameTextBox != null)
                    {
                        string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                        FileNameTextBox.Text = fileNameWithoutExtension;
                    }
                    
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

        private void InitializePropertyBlocks()
        {
            var dimensionsPanel = FindName("DimensionsPanel") as Border;
            var fillPanel = FindName("FillPanel") as Border;
            var strokePanel = FindName("StrokePanel") as Border;
            var textPanel = FindName("TextPanel") as Border;

            DimensionsPanel.Visibility = Visibility.Collapsed;
            FillPanel.Visibility = Visibility.Collapsed;
            StrokePanel.Visibility = Visibility.Collapsed;
            TextPanel.Visibility = Visibility.Collapsed;
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
            // Скрываем панель ширины обводки
            if (StrokeWidthPanel != null)
            {
                StrokeWidthPanel.Visibility = Visibility.Collapsed;
            }
        }

        // Показать все панели
        private void ShowAllPanels()
        {
            NotationCard.Visibility = Visibility.Visible;

            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement is Ellipse ellipse || selectedElement is Border border || selectedElement is Line line)
            {
                DimensionsPanel.Visibility = Visibility.Visible;
                FillPanel.Visibility = Visibility.Visible;
                StrokePanel.Visibility = Visibility.Visible;
                // Показываем панель ширины обводки только если элемент НЕ в нотации
                if (!IsElementInNotation(selectedElement))
                {
                    StrokeWidthPanel.Visibility = Visibility.Visible;
                }
            }
            else if (selectedElement is TextBlock textBlock)
            {
                DimensionsPanel.Visibility = Visibility.Visible;
                // Не показываем панель обводки для текста
                StrokePanel.Visibility = Visibility.Collapsed;
                TextPanel.Visibility = Visibility.Visible;
                // Не показываем панель ширины обводки для текста
                StrokeWidthPanel.Visibility = Visibility.Collapsed;
            }

            //foreach (var panel in panels)
            //{
            //    if (panel != null)
            //    {
            //        panel.Visibility = Visibility.Visible;
            //    }
            //}
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
                
                // Сохраняем имя файла из TextBox
                string fileName = FileNameTextBox?.Text ?? System.IO.Path.GetFileNameWithoutExtension(filePath);
                writer.WriteLine($"FILENAME|{fileName}"); // Метаданные: имя файла

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
            arrowManager?.ClearAllArrows();

            using (StreamReader reader = new StreamReader(filePath))
            {
                string header = reader.ReadLine();
                if (header != "GEDv1.0")
                {
                    throw new Exception("Неверный формат файла");
                }

                string loadedFileName = null;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 2) continue;

                    // Читаем имя файла из метаданных
                    if (parts[0] == "FILENAME" && parts.Length >= 2)
                    {
                        loadedFileName = parts[1];
                        continue;
                    }

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
                            arrowManager?.CreateArrowFromData(parts);
                            break;
                    }
                }
                
                // Обновляем TextBox с именем файла (из метаданных или из файловой системы)
                if (loadedFileName != null)
                {
                    if (FileNameTextBox != null)
                    {
                        FileNameTextBox.Text = loadedFileName;
                    }
                }
                else
                {
                    // Если метаданных нет, используем имя файла из файловой системы без расширения
                    string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    if (FileNameTextBox != null)
                    {
                        FileNameTextBox.Text = fileNameWithoutExtension;
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
            resizableElements[ellipse] = new ResizableElement(ellipse, MainCanvas, SnapToGrid, isSnapToGridEnabled);
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
            resizableElements[square] = new ResizableElement(square, MainCanvas, SnapToGrid, isSnapToGridEnabled);
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

            diamond.RenderTransform = new RotateTransform(45);
            Canvas.SetLeft(diamond, double.Parse(data[1]));
            Canvas.SetTop(diamond, double.Parse(data[2]));
            MainCanvas.Children.Add(diamond);
            MakeCanvasShapeDraggable(diamond);

            // Создаем ResizableElement
            resizableElements[diamond] = new ResizableElement(diamond, MainCanvas, SnapToGrid, isSnapToGridEnabled);
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
            resizableElements[line] = new ResizableElement(line, MainCanvas, SnapToGrid, isSnapToGridEnabled);
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
            // Используем текст из TextBox, если он доступен, иначе имя файла из файловой системы
            string fileName;
            if (FileNameTextBox != null && !string.IsNullOrEmpty(FileNameTextBox.Text) && FileNameTextBox.Text != "Untitled")
            {
                fileName = FileNameTextBox.Text;
            }
            else if (!string.IsNullOrEmpty(currentFilePath))
            {
                fileName = System.IO.Path.GetFileNameWithoutExtension(currentFilePath);
            }
            else
            {
                fileName = "Новый файл";
            }
            this.Title = $"Kapibara - {fileName}";
        }
        
        // Обработчик изменения текста в TextBox имени файла
        private void FileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateWindowTitle();
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
            HelpSystem.ShowHelp();
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
            var selectedElement = selectionManager?.SelectedElement;
            if (e.Key == Key.Delete && selectedElement != null)
            {
                DeleteSelectedElement();
                e.Handled = true;
            }

            // Удаление выделенной стрелки по клавише Delete
            if (e.Key == Key.Delete && arrowManager != null && arrowManager.SelectedArrowContainer != null)
            {
                arrowManager.DeleteArrow(arrowManager.SelectedArrowContainer.Line);
                e.Handled = true;
            }
        }

        // Новые методы для работы с множественным выделением
        private void CutSelectedElements()
        {
            var selectedElements = selectionManager?.SelectedElements.ToList() ?? new List<UIElement>();
            if (selectedElements.Count > 0)
            {
                // Сохраняем копии для буфера обмена
                var elementsToCut = selectedElements;

                foreach (var element in elementsToCut)
                {
                    // Сохраняем в стек отмены как удаление
                    SaveToUndoStack(element, UndoActionType.Remove);
                    
                    // Удаляем ResizableElement если есть
                    if (resizableElements.ContainsKey(element))
                    {
                        resizableElements[element].Dispose();
                        resizableElements.Remove(element);
                    }

                    // Удаляем элемент
                    MainCanvas.Children.Remove(element);
                    canvasElements.Remove(element);
                }

                ClearSelection();
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

            contextMenu.Items.Add(new Separator());

            // Пункт "Перенести на передний план"
            MenuItem bringToFrontMenuItem = new MenuItem
            {
                Header = "Перенести на передний план",
                Icon = new TextBlock { Text = "⬆", FontSize = 14 }
            };
            bringToFrontMenuItem.Click += (s, e) => BringToFront(element);
            contextMenu.Items.Add(bringToFrontMenuItem);

            // Пункт "Перенести на задний план"
            MenuItem sendToBackMenuItem = new MenuItem
            {
                Header = "Перенести на задний план",
                Icon = new TextBlock { Text = "⬇", FontSize = 14 }
            };
            sendToBackMenuItem.Click += (s, e) => SendToBack(element);
            contextMenu.Items.Add(sendToBackMenuItem);

            // Устанавливаем контекстное меню для элемента
            element.ContextMenu = contextMenu;
        }

        // Метод для получения максимального ZIndex на холсте
        private int GetMaxZIndex()
        {
            int maxZIndex = 0;
            foreach (UIElement child in MainCanvas.Children)
            {
                int zIndex = Canvas.GetZIndex(child);
                if (zIndex > maxZIndex)
                {
                    maxZIndex = zIndex;
                }
            }
            return maxZIndex;
        }

        // Метод для получения минимального ZIndex на холсте
        private int GetMinZIndex()
        {
            int minZIndex = 0;
            bool first = true;
            foreach (UIElement child in MainCanvas.Children)
            {
                int zIndex = Canvas.GetZIndex(child);
                if (first || zIndex < minZIndex)
                {
                    minZIndex = zIndex;
                    first = false;
                }
            }
            return minZIndex;
        }

        // Перенести элемент на передний план
        private void BringToFront(FrameworkElement element)
        {
            if (element == null || !MainCanvas.Children.Contains(element)) return;

            int maxZIndex = GetMaxZIndex();
            Canvas.SetZIndex(element, maxZIndex + 1);

            // Также перемещаем элемент в конец коллекции для гарантии
            MainCanvas.Children.Remove(element);
            MainCanvas.Children.Add(element);
        }

        // Перенести элемент на задний план
        private void SendToBack(FrameworkElement element)
        {
            if (element == null || !MainCanvas.Children.Contains(element)) return;

            int minZIndex = GetMinZIndex();
            Canvas.SetZIndex(element, minZIndex - 1);

            // Также перемещаем элемент в начало коллекции для гарантии
            MainCanvas.Children.Remove(element);
            MainCanvas.Children.Insert(0, element);
        }


        // Метод для показа/скрытия панели толщины обводки
        private void UpdateStrokeWidthPanelVisibility() //Интересно
        {
            // Если все панели скрыты - не показываем панель ширины обводки
            if (TogglePanelsButton.IsChecked == true)
            {
                StrokeWidthPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var selectedElement = selectionManager?.SelectedElement;
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
            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement != null)
            {
                ApplyStrokeThicknessToShape(selectedElement, thickness);

                // Сохраняем новую толщину как оригинальную
                selectionManager?.UpdateOriginalStrokeThickness(selectedElement, thickness);
                
                var selectedShape = selectionManager?.SelectedShape;
                if (selectedShape != null)
                {
                    selectionManager?.UpdateOriginalStrokeThickness(selectedShape, thickness);
                }
            }
        }

        // Вспомогательный метод для применения толщины обводки к конкретной фигуре
        internal void ApplyStrokeThicknessToShape(UIElement shape, double thickness)
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
            selectionManager?.UpdateOriginalStrokeThickness(shape, thickness);
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

        // Сохранение оригинальных цветов и толщины (делегируется в SelectionManager)
        private void SaveOriginalColors(UIElement shape)
        {
            selectionManager?.SaveOriginalColors(shape);
        }

        // Обновите метод RestoreOriginalAppearance для восстановления толщины
        private void RestoreOriginalAppearance(UIElement element)
        {
            if (element is Ellipse ellipse)
            {
                // Возвращаем сохраненный цвет обводки
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    ellipse.Stroke = strokeColor;
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
                double thickness = selectionManager?.GetOriginalStrokeThickness(element) ?? 0;
                if (thickness > 0)
                {
                    ellipse.StrokeThickness = thickness;
                }
                else
                {
                    ellipse.StrokeThickness = ellipse == SimpleRing ? 5 : 3;
                }
                ellipse.Opacity = 1.0;
            }
            else if (element is Border border)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    border.BorderBrush = strokeColor;
                }
                else
                {
                    border.BorderBrush = Brushes.White;
                }
                // Возвращаем сохраненную толщину
                double thickness = selectionManager?.GetOriginalStrokeThickness(element) ?? 0;
                if (thickness > 0)
                {
                    border.BorderThickness = new Thickness(thickness);
                }
                else
                {
                    border.BorderThickness = new Thickness(2);
                }
                border.Opacity = 1.0;
            }
            else if (element is Line line)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    line.Stroke = strokeColor;
                }
                else
                {
                    line.Stroke = Brushes.White;
                }
                // Возвращаем сохраненную толщину
                double thickness = selectionManager?.GetOriginalStrokeThickness(element) ?? 0;
                if (thickness > 0)
                {
                    line.StrokeThickness = thickness;
                }
                else
                {
                    line.StrokeThickness = 3;
                }
                line.Opacity = 1.0;
            }
            else if (element is TextBlock textBlock)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    textBlock.Foreground = strokeColor;
                }
                textBlock.Background = Brushes.Transparent;
                textBlock.Opacity = 1.0;
            }

            Canvas.SetZIndex(element, 1);
        }

        private void SelectElement(FrameworkElement element)
        {
            if (element == null) return;
            selectionManager?.SelectElement(element);

            if (TogglePanelsButton.IsChecked == false && element is TextBlock textBlock)
            {
                DimensionsPanel.Visibility = Visibility.Visible;
                StrokePanel.Visibility = Visibility.Visible;
                TextPanel.Visibility = Visibility.Visible;
                StrokeWidthPanel.Visibility = Visibility.Visible;
            }
        }

        private void DeselectAll()
        {
            selectionManager?.DeselectAll();
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
            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement != null)
            {
                copiedElement = CloneUIElement(selectedElement);
            }
        }

        // Вырезание выделенного элемента
        private void CutSelectedElement()
        {
            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement != null)
            {
                // Сохраняем копию для буфера обмена
                copiedElement = CloneUIElement(selectedElement);

                // Сохраняем в стек отмены как удаление
                SaveToUndoStack(selectedElement, UndoActionType.Remove);

                // Удаляем ResizableElement если есть
                if (resizableElements.ContainsKey(selectedElement))
                {
                    resizableElements[selectedElement].Dispose();
                    resizableElements.Remove(selectedElement);
                }

                // Удаляем элемент
                MainCanvas.Children.Remove(selectedElement);
                canvasElements.Remove(selectedElement);

                DeselectAll();
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
                
                double newLeft = currentLeft + 20;
                double newTop = currentTop + 20;
                
                // Применяем привязку к сетке если включена
                if (isSnapToGridEnabled)
                {
                    Point snappedPoint = SnapToGrid(new Point(newLeft, newTop));
                    newLeft = snappedPoint.X;
                    newTop = snappedPoint.Y;
                }
                
                Canvas.SetLeft(newElement, newLeft);
                Canvas.SetTop(newElement, newTop);
                
                // Добавляем на холст
                MainCanvas.Children.Add(newElement);
                canvasElements.Add(newElement);
                MakeCanvasShapeDraggable(newElement);

                // Перемещаем на передний план
                if (newElement is FrameworkElement frameworkElement)
                {
                    BringToFront(frameworkElement);
                }

                // Создаем ResizableElement для нового элемента
                resizableElements[newElement] = new ResizableElement(newElement, MainCanvas, SnapToGrid, isSnapToGridEnabled);

                // Выделяем вставленный элемент
                SelectElement(newElement as FrameworkElement);
                
                // Сохраняем в стек отмены
                SaveToUndoStack(newElement);
            }
        }

        // Удаление выделенного элемента
        private void DeleteSelectedElement()
        {
            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement != null)
            {
                // Сохраняем в стек отмены как удаление
                SaveToUndoStack(selectedElement, UndoActionType.Remove);

                // Удаляем ResizableElement если есть
                if (resizableElements.ContainsKey(selectedElement))
                {
                    resizableElements[selectedElement].Dispose();
                    resizableElements.Remove(selectedElement);
                }

                MainCanvas.Children.Remove(selectedElement);
                canvasElements.Remove(selectedElement);
                
                DeselectAll();
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
            else if (original is Ellipse originalEllipse)
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
                UndoAction action = undoStack.Pop();

                if (action.ActionType == UndoActionType.Add)
                {
                    // Отменяем добавление - удаляем элемент
                    UIElement elementToRemove = action.OriginalElement ?? FindElementByClone(action.Element);
                    if (elementToRemove != null && MainCanvas.Children.Contains(elementToRemove))
                    {
                        // Удаляем ResizableElement если есть
                        if (resizableElements.ContainsKey(elementToRemove))
                        {
                            resizableElements[elementToRemove].Dispose();
                            resizableElements.Remove(elementToRemove);
                        }

                        MainCanvas.Children.Remove(elementToRemove);
                        canvasElements.Remove(elementToRemove);
                        
                        // Сохраняем действие для повтора
                        redoStack.Push(new UndoAction(UndoActionType.Remove, action.Element, elementToRemove));
                    }
                }
                else if (action.ActionType == UndoActionType.Remove)
                {
                    // Отменяем удаление - восстанавливаем элемент
                    UIElement elementToRestore = CloneUIElement(action.Element);
                    
                    // Восстанавливаем позицию
                    double left = Canvas.GetLeft(action.Element);
                    double top = Canvas.GetTop(action.Element);
                    if (!double.IsNaN(left)) Canvas.SetLeft(elementToRestore, left);
                    if (!double.IsNaN(top)) Canvas.SetTop(elementToRestore, top);
                    
                    MainCanvas.Children.Add(elementToRestore);
                    canvasElements.Add(elementToRestore);
                    MakeCanvasShapeDraggable(elementToRestore);
                    
                    // Создаем ResizableElement для восстановленного элемента
                    resizableElements[elementToRestore] = new ResizableElement(elementToRestore, MainCanvas, SnapToGrid, isSnapToGridEnabled);
                    
                    RestoreOriginalAppearance(elementToRestore);
                    
                    // Сохраняем действие для повтора
                    redoStack.Push(new UndoAction(UndoActionType.Add, action.Element, elementToRestore));
                }
            }
        }
        
        // Поиск элемента на холсте по его копии
        private UIElement FindElementByClone(UIElement clone)
        {
            // Пытаемся найти элемент по позиции и типу
            double cloneLeft = Canvas.GetLeft(clone);
            double cloneTop = Canvas.GetTop(clone);
            
            foreach (UIElement element in MainCanvas.Children)
            {
                if (element.GetType() == clone.GetType())
                {
                    double elemLeft = Canvas.GetLeft(element);
                    double elemTop = Canvas.GetTop(element);
                    
                    if (Math.Abs(elemLeft - cloneLeft) < 0.1 && Math.Abs(elemTop - cloneTop) < 0.1)
                    {
                        return element;
                    }
                }
            }
            return null;
        }

        // Повтор последнего действия
        private void RedoLastAction()
        {
            if (redoStack.Count > 0)
            {
                DeselectAll();
                UndoAction action = redoStack.Pop();

                if (action.ActionType == UndoActionType.Add)
                {
                    // Повторяем добавление - добавляем элемент
                    UIElement elementToAdd = CloneUIElement(action.Element);
                    
                    // Восстанавливаем позицию
                    double left = Canvas.GetLeft(action.Element);
                    double top = Canvas.GetTop(action.Element);
                    if (!double.IsNaN(left)) Canvas.SetLeft(elementToAdd, left);
                    if (!double.IsNaN(top)) Canvas.SetTop(elementToAdd, top);
                    
                    MainCanvas.Children.Add(elementToAdd);
                    canvasElements.Add(elementToAdd);
                    MakeCanvasShapeDraggable(elementToAdd);
                    
                    // Создаем ResizableElement для добавленного элемента
                    resizableElements[elementToAdd] = new ResizableElement(elementToAdd, MainCanvas, SnapToGrid, isSnapToGridEnabled);
                    
                    RestoreOriginalAppearance(elementToAdd);
                    
                    // Сохраняем действие для отмены
                    undoStack.Push(new UndoAction(UndoActionType.Add, action.Element, elementToAdd));
                }
                else if (action.ActionType == UndoActionType.Remove)
                {
                    // Повторяем удаление - удаляем элемент
                    UIElement elementToRemove = action.OriginalElement ?? FindElementByClone(action.Element);
                    if (elementToRemove != null && MainCanvas.Children.Contains(elementToRemove))
                    {
                        // Удаляем ResizableElement если есть
                        if (resizableElements.ContainsKey(elementToRemove))
                        {
                            resizableElements[elementToRemove].Dispose();
                            resizableElements.Remove(elementToRemove);
                        }

                        MainCanvas.Children.Remove(elementToRemove);
                        canvasElements.Remove(elementToRemove);
                        
                        // Сохраняем действие для отмены
                        undoStack.Push(new UndoAction(UndoActionType.Remove, action.Element, elementToRemove));
                    }
                }
            }
        }
        
        // Сохранение в стек отмены для добавления элемента
        private void SaveToUndoStack(UIElement element, UndoActionType actionType = UndoActionType.Add)
        {
            if (element == null) return;
            
            UIElement elementCopy = CloneUIElement(element);
            SaveOriginalColorsForCopy(element, elementCopy);
            
            // Сохраняем действие с указанием типа и ссылкой на оригинальный элемент
            UndoAction action = new UndoAction(actionType, elementCopy, element);
            undoStack.Push(action);
            
            // Очищаем стек повтора при новом действии
            redoStack.Clear();
        }
        
        // Сохранение в стек отмены (обратная совместимость)
        private void SaveToUndoStack(UIElement element)
        {
            SaveToUndoStack(element, UndoActionType.Add);
        }

        // Сохранение оригинальных цветов для копии элемента
        private void SaveOriginalColorsForCopy(UIElement original, UIElement copy)
        {
            if (original == null || copy == null) return;
            
            // Используем метод копирования из SelectionManager
            selectionManager?.CopyOriginalStyles(original, copy);
            
            // Если оригинальные стили не были сохранены, сохраняем текущие
            if (original is Ellipse originalEllipse && copy is Ellipse copyEllipse)
            {
                if (!selectionManager?.HasOriginalStrokeThickness(copy) ?? true)
                {
                    selectionManager?.SetOriginalStyles(copy, originalEllipse.Stroke, originalEllipse.Fill, originalEllipse.StrokeThickness);
                }
            }
            else if (original is Border originalBorder && copy is Border copyBorder)
            {
                if (!selectionManager?.HasOriginalStrokeThickness(copy) ?? true)
                {
                    selectionManager?.SetOriginalStyles(copy, originalBorder.BorderBrush, originalBorder.Background, originalBorder.BorderThickness.Left);
                }
            }
            else if (original is Line originalLine && copy is Line copyLine)
            {
                if (!selectionManager?.HasOriginalStrokeThickness(copy) ?? true)
                {
                    selectionManager?.SetOriginalStyles(copy, originalLine.Stroke, null, originalLine.StrokeThickness);
                }
            }
            else if (original is TextBlock originalText && copy is TextBlock copyText)
            {
                if (selectionManager?.GetOriginalStrokeColor(copy) == null)
                {
                    selectionManager?.UpdateOriginalStrokeColor(copy, originalText.Foreground);
                }
            }
        }

        // Восстановление оригинальных цветов элемента
        private void RestoreOriginalColors(UIElement element)
        {
            if (element == null) return;
            
            if (element is Ellipse ellipse)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    ellipse.Stroke = strokeColor;
                }
                var fillColor = selectionManager?.GetOriginalFillColor(element);
                if (fillColor != null)
                {
                    ellipse.Fill = fillColor;
                }
            }
            else if (element is Border border)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    border.BorderBrush = strokeColor;
                }
                var fillColor = selectionManager?.GetOriginalFillColor(element);
                if (fillColor != null)
                {
                    border.Background = fillColor;
                }
            }
            else if (element is Line line)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    line.Stroke = strokeColor;
                }
            }
            else if (element is TextBlock textBlock)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    textBlock.Foreground = strokeColor;
                }
            }
        }

        private void SelectAllElements()
        {
            selectionManager?.SelectAllElements();
        }

        private bool IsSelectableElement(UIElement element)
        {
            // Проверяем, что элемент не является прямоугольником выделения из SelectionManager
            // SelectionManager управляет selectionRect, поэтому просто проверяем тип
            return element != null &&
                   !(element is Rectangle) && // Исключаем прямоугольники (selectionRect)
                   !IsElementInNotation(element) &&
                   element is FrameworkElement;
        }

        // Обработчик для кнопки текста на панели инструментов
        private void TextButton_Click(object sender, RoutedEventArgs e)
        {
            if (textManager?.IsTextModeActive == true) return;
            textManager?.ActivateTextMode();
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

                    // ПРИВЯЗЫВАЕМ НАЧАЛЬНУЮ ПОЗИЦИЮ К СЕТКЕ
                    if (isSnapToGridEnabled)
                    {
                        startPosition = SnapToGrid(startPosition);
                    }

                    double startLeft = startPosition.X - (GetElementWidth(shapeCopy) / 2);
                    double startTop = startPosition.Y - (GetElementHeight(shapeCopy) / 2);

                    // Дополнительно привязываем финальную позицию к сетке
                    if (isSnapToGridEnabled)
                    {
                        Point snappedStart = SnapToGrid(new Point(startLeft, startTop));
                        startLeft = snappedStart.X;
                        startTop = snappedStart.Y;
                    }

                    Canvas.SetLeft(shapeCopy, startLeft);
                    Canvas.SetTop(shapeCopy, startTop);

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

                // ПРИМЕНЯЕМ ПРИВЯЗКУ К СЕТКЕ К КАЖДОЙ ПОЗИЦИИ КУРСОРА
                if (isSnapToGridEnabled)
                {
                    currentPosition = SnapToGrid(currentPosition);
                }

                if (currentPosition.Y > WorkAreaTop && currentPosition.Y < MainCanvas.ActualHeight - WorkAreaBottom)
                {
                    double newLeft = currentPosition.X - (GetElementWidth(shapeCopy) / 2);
                    double newTop = currentPosition.Y - (GetElementHeight(shapeCopy) / 2);
                    // Дополнительные ограничения для копии
                    double elementWidth = GetElementWidth(shapeCopy);
                    double elementHeight = GetElementHeight(shapeCopy);

                    double maxTop = MainCanvas.ActualHeight - elementHeight - WorkAreaBottom;

                    newLeft = Math.Max(0, Math.Min(newLeft, MainCanvas.ActualWidth - elementWidth));
                    newTop = Math.Max(WorkAreaTop, Math.Min(newTop, maxTop));

                    // ДОПОЛНИТЕЛЬНО ПРИВЯЗЫВАЕМ ФИНАЛЬНУЮ ПОЗИЦИЮ К СЕТКЕ
                    if (isSnapToGridEnabled)
                    {
                        Point snappedPosition = SnapToGrid(new Point(newLeft, newTop));
                        newLeft = snappedPosition.X;
                        newTop = snappedPosition.Y;
                    }

                    Canvas.SetLeft(shapeCopy, newLeft);
                    Canvas.SetTop(shapeCopy, newTop);
                }
            }
        }

        // Применение визуального выделения (делегируется в SelectionManager)
        private void ApplySelectionVisualFeedback(UIElement element, bool isSelected)
        {
            selectionManager?.ApplySelectionVisualFeedback(element, isSelected);
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
                    
                    // Применяем привязку к сетке если включена
                    double elementWidth = GetElementWidth(shapeCopy);
                    double elementHeight = GetElementHeight(shapeCopy);
                    double snappedLeft = dropPosition.X - (elementWidth / 2);
                    double snappedTop = dropPosition.Y - (elementHeight / 2);
                    
                    // Ограничиваем координаты
                    double maxTop = MainCanvas.ActualHeight - elementHeight - WorkAreaBottom;
                    snappedLeft = Math.Max(0, Math.Min(snappedLeft, MainCanvas.ActualWidth - elementWidth));
                    snappedTop = Math.Max(WorkAreaTop, Math.Min(snappedTop, maxTop));
                    
                    // Применяем привязку к сетке к финальной позиции фигуры
                    if (isSnapToGridEnabled)
                    {
                        Point snappedPosition = SnapToGrid(new Point(snappedLeft, snappedTop));
                        snappedLeft = snappedPosition.X;
                        snappedTop = snappedPosition.Y;
                    }
                    
                    Canvas.SetLeft(shapeCopy, snappedLeft);
                    Canvas.SetTop(shapeCopy, snappedTop);

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

                        // ПЕРЕМЕЩАЕМ ФИГУРУ НА ПЕРЕДНИЙ ПЛАН
                        BringToFront(shapeCopy as FrameworkElement);

                        MakeCanvasShapeDraggable(shapeCopy);

                        // Добавляем эффект появления
                        AnimateShapeAppearance(shapeCopy);

                        // ВЫДЕЛЯЕМ НОВУЮ ФИГУРУ ПОСЛЕ ПЕРЕТАСКИВАНИЯ
                        SelectElement(shapeCopy as FrameworkElement);
                        SelectShape(shapeCopy);
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
            selectionManager?.UpdateOriginalStrokeThickness(element, thickness);

            // Если это selectedElement, обновляем и в selectedShape
            var selectedElement = selectionManager?.SelectedElement;
            var selectedShape = selectionManager?.SelectedShape;
            if (selectedElement == element && selectedShape != null)
            {
                selectionManager?.UpdateOriginalStrokeThickness(selectedShape, thickness);
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
            // Все новые фигуры создаются с белым цветом обводки и заливки
            if (original == Circle)
            {
                var copy = new Ellipse
                {
                    Width = 110,
                    Height = 110,
                    Fill = Brushes.White,
                    Stroke = Brushes.White,
                    StrokeThickness = 3, // ФИКСИРОВАННАЯ толщина
                    Cursor = Cursors.Hand
                };
                selectionManager?.SetOriginalStyles(copy, Brushes.White, Brushes.White, 3);
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
                    Stroke = Brushes.White,
                    StrokeThickness = 1, // ФИКСИРОВАННАЯ толщина
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                border.Child = circle;
                selectionManager?.SetOriginalStyles(border, Brushes.White, null, 3);
                selectionManager?.SetOriginalStyles(circle, Brushes.White, Brushes.White, 1);
                return border;
            }
            else if (original == SimpleRing)
            {
                var copy = new Ellipse
                {
                    Width = 110,
                    Height = 110,
                    Stroke = Brushes.White,
                    StrokeThickness = 5, // ФИКСИРОВАННАЯ толщина
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                selectionManager?.SetOriginalStyles(copy, Brushes.White, null, 5);
                return copy;
            }
            else if (original == Ellipse)
            {
                var copy = new Ellipse
                {
                    Width = 130,
                    Height = 80,
                    Stroke = Brushes.White,
                    StrokeThickness = 3, // ФИКСИРОВАННАЯ толщина
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                selectionManager?.SetOriginalStyles(copy, Brushes.White, null, 3);
                return copy;
            }
            else if (original == Square)
            {
                var copy = new Border
                {
                    Width = 100,
                    Height = 100,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (Square as Border).CornerRadius,
                    Cursor = Cursors.Hand
                };
                selectionManager?.SetOriginalStyles(copy, Brushes.White, Brushes.White, 2);
                return copy;
            }
            else if (original == ResizableSquare)
            {
                var copy = new Border
                {
                    Width = 70,
                    Height = 71,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (ResizableSquare as Border).CornerRadius,
                    Cursor = Cursors.Hand,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                copy.RenderTransform = new RotateTransform(45);
                selectionManager?.SetOriginalStyles(copy, Brushes.White, Brushes.White, 2);
                return copy;
            }
            else if (original == Rectangle)
            {
                var copy = new Border
                {
                    Width = 150,
                    Height = 80,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (Rectangle as Border).CornerRadius,
                    Cursor = Cursors.Hand
                };
                selectionManager?.SetOriginalStyles(copy, Brushes.White, Brushes.White, 2);
                return copy;
            }
            else if (original == stick)
            {
                var copy = new Border
                {
                    Width = 200,
                    Height = 30,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (stick as Border).CornerRadius,
                    Cursor = Cursors.Hand
                };
                selectionManager?.SetOriginalStyles(copy, Brushes.White, Brushes.White, 2);
                return copy;
            }
            else if (original == SuperEllipse)
            {
                var copy = new Border
                {
                    Width = 120,
                    Height = 80,
                    Background = Brushes.White,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2), // ФИКСИРОВАННАЯ толщина
                    CornerRadius = (SuperEllipse as Border).CornerRadius,
                    Cursor = Cursors.Hand
                };
                selectionManager?.SetOriginalStyles(copy, Brushes.White, Brushes.White, 2);
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

                // Получаем текущую позицию элемента
                double currentLeft = Canvas.GetLeft(element);
                double currentTop = Canvas.GetTop(element);
                if (double.IsNaN(currentLeft)) currentLeft = 0;
                if (double.IsNaN(currentTop)) currentTop = 0;

                // Получаем позицию курсора на холсте и ПРИВЯЗЫВАЕМ ЕЕ К СЕТКЕ
                Point canvasPos = e.GetPosition(MainCanvas);
                if (isSnapToGridEnabled)
                {
                    canvasPos = SnapToGrid(canvasPos);
                }

                // Вычисляем offset как разницу между ПРИВЯЗАННОЙ позицией курсора и позицией элемента
                offset = new Point(
                    canvasPos.X - currentLeft,
                    canvasPos.Y - currentTop
                );

                // ПОДНИМАЕМ ЭЛЕМЕНТ НАВЕРХ ПРИ ЗАХВАТЕ (ZIndex = 1000)
                Canvas.SetZIndex(element, 1000);

                // Выделяем фигуру при клике
                //SelectShape(element);

                isDragging = true;
                draggedElement = element;
                element.CaptureMouse();
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
            if (selectionManager?.IsSelecting == true)
            {
                selectionManager.EndSelection();
            }

            // Завершение перетаскивания
            if (isDragging && draggedElement != null)
            {
                // ВЫДЕЛЯЕМ фигуру при клике (но не начинаем новое перетаскивание)
                //SelectShape(element);

                // Применяем привязку к сетке при завершении перетаскивания
                if (isSnapToGridEnabled)
                {
                    // Обрабатываем Line элементы отдельно
                    if (draggedElement is Line line)
                    {
                        Point snappedStart = SnapToGrid(new Point(line.X1, line.Y1));
                        Point snappedEnd = SnapToGrid(new Point(line.X2, line.Y2));
                        line.X1 = snappedStart.X;
                        line.Y1 = snappedStart.Y;
                        line.X2 = snappedEnd.X;
                        line.Y2 = snappedEnd.Y;
                    }
                    else
                    {
                        double currentLeft = Canvas.GetLeft(draggedElement);
                        double currentTop = Canvas.GetTop(draggedElement);
                        
                        if (!double.IsNaN(currentLeft) && !double.IsNaN(currentTop))
                        {
                            Point snappedPosition = SnapToGrid(new Point(currentLeft, currentTop));
                            Canvas.SetLeft(draggedElement, snappedPosition.X);
                            Canvas.SetTop(draggedElement, snappedPosition.Y);
                        }
                    }
                }

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

        // Добавьте метод для снятия выделения при клике на пустую область Canvas
        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (arrowManager != null && arrowManager.IsArrowModeActive)
            {
                Point clickPoint = e.GetPosition(MainCanvas);

                if (arrowManager.CurrentArrow == null)
                {
                    arrowManager.StartArrowDrawing(clickPoint);
                }
                else
                {
                    arrowManager.AddArrowSegment(clickPoint);
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

        // Методы для системы выделения (теперь делегируются в SelectionManager)
        // Старые методы удалены, используются методы SelectionManager

        //Выделение через Ctrl
        private void HandleElementClick(UIElement element, MouseButtonEventArgs e)
        {
            if (element == null || selectionManager == null) return;
            
            if (element is FrameworkElement frameworkElement)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    // Ctrl + клик: переключаем выделение
                    if (selectionManager.IsElementSelected(frameworkElement))
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
            if (element == null) return;
            
            selectionManager?.DeselectElement(element);

            if (resizableElements.TryGetValue(element, out var resizable))
            {
                resizable.HideResizeHandles();
            }
        }

        public void SelectShape(UIElement shape)
        {
            if (shape == null) return;
            selectionManager?.SelectShape(shape);
        }

        // Углы для изменения размера, а также появление блоков
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

            if (TogglePanelsButton.IsChecked == false && (element is Ellipse ellipse || element is Border border || element is Line line))
            {
                DimensionsPanel.Visibility = Visibility.Visible;
                FillPanel.Visibility = Visibility.Visible;
                StrokePanel.Visibility = Visibility.Visible;
            }
            //else if (element is TextBlock textBlock)
            //{
            //    DimensionsPanel.Visibility = Visibility.Visible;
            //    StrokePanel.Visibility = Visibility.Visible;
            //    TextPanel.Visibility = Visibility.Visible;
            //}

            // Создаем или используем существующий ResizableElement для текущего элемента
            if (!resizableElements.ContainsKey(element))
            {
                // Создаем новый ResizableElement
                resizableElements[element] = new ResizableElement(element, MainCanvas, SnapToGrid, isSnapToGridEnabled);
            }

            currentResizableElement = resizableElements[element];
            currentResizableElement.ShowResizeHandles();

            // Обновляем текстовые поля размеров
            if (element is FrameworkElement frameworkElement)
            {
                UpdateDimensions(frameworkElement.ActualWidth, frameworkElement.ActualHeight);
            }
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Обработка рисования стрелки
            if (arrowManager != null && arrowManager.IsArrowModeActive)
            {
                if (arrowManager.CurrentArrow != null)
                {
                    arrowManager.UpdateArrowDrawing(e.GetPosition(MainCanvas));
                }

                e.Handled = true;
                return;
            }

            // Обработка выделения
            if (selectionManager?.IsSelecting == true)
            {
                var currentPos = e.GetPosition(MainCanvas);
                selectionManager.UpdateSelectionRect(currentPos);
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
            if (arrowManager != null && arrowManager.IsArrowModeActive)
            {
                e.Handled = true;
                return;
            }

            // Завершение выделения
            if (selectionManager?.IsSelecting == true)
            {
                selectionManager.EndSelection();
            }

            // Завершение перетаскивания
            if (isDragging && draggedElement != null)
            {
                // Сохраняем текущую толщину ПОСЛЕ перетаскивания
                if (selectionManager?.HasOriginalStrokeThickness(draggedElement) == true)
                {
                    // Обновляем сохраненную толщину текущим значением
                    double thickness = 0;
                    if (draggedElement is Ellipse ellipseElement)
                    {
                        thickness = ellipseElement.StrokeThickness;
                    }
                    else if (draggedElement is Border borderElement)
                    {
                        thickness = borderElement.BorderThickness.Left;
                    }
                    else if (draggedElement is Line lineElement)
                    {
                        thickness = lineElement.StrokeThickness;
                    }
                    selectionManager.UpdateOriginalStrokeThickness(draggedElement, thickness);
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
            if (string.IsNullOrEmpty(hexColor) || selectionManager == null) return;

            Color newColor = (Color)ColorConverter.ConvertFromString("#" + hexColor);
            Brush newBrush = new SolidColorBrush(newColor);

            // Применяем цвет ко всем выделенным элементам
            foreach (var element in selectionManager.SelectedElements)
            {
                ApplyFillColorToShape(element, newBrush);
            }

            // Также применяем к selectedShape, если он не в selectedElements
            var selectedShape = selectionManager.SelectedShape;
            if (selectedShape != null && !selectionManager.IsElementSelected(selectedShape))
            {
                ApplyFillColorToShape(selectedShape, newBrush);
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
            if (string.IsNullOrEmpty(hexColor) || selectionManager == null) return;

            Color newColor = (Color)ColorConverter.ConvertFromString("#" + hexColor);
            Brush newBrush = new SolidColorBrush(newColor);

            // Применяем цвет обводки ко всем выделенным элементам
            foreach (var element in selectionManager.SelectedElements)
            {
                ApplyStrokeColorToShape(element, newBrush);
            }

            // Также применяем к selectedShape, если он не в selectedElements
            var selectedShape = selectionManager.SelectedShape;
            if (selectedShape != null && !selectionManager.IsElementSelected(selectedShape))
            {
                ApplyStrokeColorToShape(selectedShape, newBrush);
            }
        }

        // Инициализация менеджера стрелок
        private void InitializeArrowManager()
        {
            arrowManager = new ArrowManager(
                MainCanvas,
                pencil,
                isSnapToGridEnabled,
                WorkAreaTop,
                WorkAreaBottom,
                (element) => { if (element is FrameworkElement fe) BringToFront(fe); },
                (element, actionType) => SaveToUndoStack(element, actionType),
                DeselectAll,
                (element) => { if (element is FrameworkElement fe) { SelectElement(fe); return true; } return false; },
                SnapToGrid,
                GetMaxZIndex,
                GetMinZIndex,
                UpdateArrowPropertiesInPanels
            );
        }

        // Инициализация системы выделения
        private void InitializeSelectionSystem()
        {
            // Создаем менеджер выделения
            selectionManager = new SelectionManager(MainCanvas, this);
            
            // Настраиваем делегат для снятия выделения стрелки
            selectionManager.OnDeselectArrow = () => arrowManager.DeselectArrow();

            // Подписываемся на события выделения для обновления панелей свойств
            selectionManager.OnElementSelected += (element) =>
            {
                if (element != null)
                {
                    // Обновляем комбо-боксы в соответствии с цветами фигуры
                    UpdateColorComboBoxesFromShape(element);

                    // Показываем панель толщины обводки только если элемент НЕ в нотации
                    if (!IsElementInNotation(element))
                    {
                        UpdateStrokeWidthPanelVisibility();
                    }

                    // Обновляем панель текста, если элемент является текстовым
                    textManager?.UpdateTextPanelForSelectedElement(element);

                    // Показываем ручки изменения размера
                    ShowResizeHandlesForElement(element);

                    // Создаем контекстное меню для выделенного элемента
                    CreateContextMenu(element);
                }
            };

            selectionManager.OnSelectionCleared += () =>
            {
                // Скрываем панели свойств
                StrokeWidthPanel.Visibility = Visibility.Collapsed;
                DimensionsPanel.Visibility = Visibility.Collapsed;
                FillPanel.Visibility = Visibility.Collapsed;
                StrokePanel.Visibility = Visibility.Collapsed;
                TextPanel.Visibility = Visibility.Collapsed;
            };

            // Добавляем обработчики событий для выделения
            MainCanvas.MouseLeftButtonDown += MainCanvas_MouseLeftButtonDown;
            MainCanvas.MouseMove += MainCanvas_MouseMove;
            MainCanvas.MouseLeftButtonUp += MainCanvas_MouseLeftButtonUp;
            MainCanvas.MouseRightButtonDown += MainCanvas_MouseRightButtonDown;
        }

        // Инициализация менеджера текста
        private void InitializeTextManager()
        {
            var fontComboBox = FindName("FontComboBox") as ComboBox;
            var fontStyleComboBox = FindName("FontStyleComboBox") as ComboBox;
            var fontSizeComboBox = FindName("FontSizeComboBox") as ComboBox;
            var textPanel = FindName("TextPanel") as Border;

            textManager = new TextManager(
                MainCanvas,
                selectionManager,
                fontComboBox,
                fontStyleComboBox,
                fontSizeComboBox,
                textPanel,
                resizableElements,
                isSnapToGridEnabled,
                (element) => { if (element is FrameworkElement fe) BringToFront(fe); },
                (element) => { if (element is FrameworkElement fe) SelectElement(fe); },
                (element) => { if (element is UIElement ue) SelectShape(ue); },
                DeselectAll,
                MakeCanvasShapeDraggable,
                SnapToGrid,
                (element) => SaveToUndoStack(element, UndoActionType.Add)
            );
        }

        private void ClearSelection()
        {
            selectionManager?.ClearSelection();
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
        internal void ApplyFillColorToShape(UIElement shape, Brush brush)
        {
            if (shape is Ellipse ellipse)
            {
                // Для эллипсов меняем Fill (заливку), только если это не кольцо
                if (ellipse.Fill != Brushes.Transparent)
                {
                    ellipse.Fill = brush;
                    // Сохраняем новый цвет как оригинальный
                    selectionManager?.UpdateOriginalFillColor(shape, brush);
                }
            }
            else if (shape is Border border)
            {
                // Для Border меняем Background (заливку)
                border.Background = brush;
                // Сохраняем новый цвет как оригинальный
                selectionManager?.UpdateOriginalFillColor(shape, brush);
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
            fillBrush = selectionManager?.GetOriginalFillColor(shape);
            strokeBrush = selectionManager?.GetOriginalStrokeColor(shape);

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

        private void CopySelectedElements()
        {
            var selectedElements = selectionManager?.SelectedElements?.ToList() ?? new List<UIElement>();
            if (selectedElements.Count > 0)
            {
                // Сохраняем копии выделенных элементов
                copiedElements = selectedElements.Select(CloneUIElement)
                                               .Cast<UIElement>()
                                               .ToList();
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

                    double newLeft = currentLeft + 20;
                    double newTop = currentTop + 20;
                    
                    // Применяем привязку к сетке если включена
                    if (isSnapToGridEnabled)
                    {
                        Point snappedPoint = SnapToGrid(new Point(newLeft, newTop));
                        newLeft = snappedPoint.X;
                        newTop = snappedPoint.Y;
                    }

                    Canvas.SetLeft(newElement, newLeft);
                    Canvas.SetTop(newElement, newTop);

                    // Добавляем на холст
                    MainCanvas.Children.Add(newElement);
                    canvasElements.Add(newElement);
                    MakeCanvasShapeDraggable(newElement);

                    // Перемещаем на передний план
                    if (newElement is FrameworkElement frameworkElement)
                    {
                        BringToFront(frameworkElement);
                    }

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

        // Вспомогательный метод для применения цвета обводки к конкретной фигуре
        internal void ApplyStrokeColorToShape(UIElement shape, Brush brush)
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
            // Сохраняем новый цвет как оригинальный
            selectionManager?.UpdateOriginalStrokeColor(shape, brush);
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
                comment.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3939"));
            }
            else
            {
                MainCanvas.Cursor = Cursors.Arrow;
                comment.Background = Brushes.Transparent;
            }
        }

        //Создание комментария в указанной позиции
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


        private void UpdateArrowPropertiesInPanels(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null) return;

            try
            {
                // Показываем панель обводки для стрелки
                if (StrokeWidthPanel != null)
                {
                    StrokeWidthPanel.Visibility = Visibility.Visible;
                }

                // Обновляем комбо-бокс толщины
                if (StrokeWidthComboBox != null)
                {
                    UpdateStrokeWidthComboBoxSelection(arrowContainer.Line.StrokeThickness);
                }

                // Обновляем комбо-бокс цвета обводки
                if (StrokeColorComboBox != null && arrowContainer.Line.Stroke is SolidColorBrush strokeBrush)
                {
                    string strokeHexColor = strokeBrush.Color.ToString().Substring(3);
                    UpdateComboBoxSelection(StrokeColorComboBox, strokeHexColor);
                }

                // Скрываем панели, которые не применимы к стрелкам
                if (FillPanel != null)
                {
                    FillPanel.Visibility = Visibility.Collapsed;
                }
                if (TextPanel != null)
                {
                    TextPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в UpdateArrowPropertiesInPanels: {ex.Message}");
            }
        }

        private void PenButton_Click(object sender, RoutedEventArgs e)
        {
            if (arrowManager != null)
            {
                arrowManager.ToggleArrowMode();
            }
        }

        private void CursorButton_Click(object sender, RoutedEventArgs e)
        {
            isCursor = !isCursor;
            isActiveCursor();
        }

        private void isActiveCursor()
        {
            if (isCursor)
            {
                cursor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3939"));
            }
            else
            {
                cursor.Background = Brushes.Transparent;
            }
        }

        private void MainCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (arrowManager != null && arrowManager.IsArrowModeActive && arrowManager.CurrentArrow != null)
            {
                // Завершаем рисование стрелки по правой кнопке
                arrowManager.CompleteArrow();
                e.Handled = true;
            }
        }
    }
}