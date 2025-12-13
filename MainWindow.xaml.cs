using System;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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
        //Управление элементами холста
        private string currentFilePath = null; //Путь к текущему файлу (null - новый файл)
        private List<UIElement> canvasElements = new List<UIElement>(); //Список всех элементов на холсте

        //Система выделения
        private SelectionManager selectionManager; //Менеджер системы выделения
        private FrameworkElement copiedElement; //Элемент в буфере обмена для копирования/вставки
        // Добавляем поле для хранения скопированных элементов
        private Dictionary<UIElement, ResizableElement> resizableElements = 
            new Dictionary<UIElement, ResizableElement>(); //Хранит связь между элементами на холсте и их контроллерами изменения размера
        private ResizableElement currentResizableElement; //Текущий активный изменяемый в размерах элемент

        //Система автосохранения
        private AutoSave autoSave; //Система автоматического сохранения проекта

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

        //Управление интерфейсом
        private bool arePanelsVisible = true; //Видимость всех панелей инструментов
        private List<Border> panels = new List<Border>(); //Список всех плашек для управления видимостью

        //Система комментариев
        private CommentManager commentManager;

        //Система рисования стрелок
        private ArrowManager arrowManager;

        //Система управления текстом
        private TextManager textManager;

        //Система управления заливкой и обводкой
        private FillStrokeManager fillStrokeManager;

        //Система управления файлами
        private FileManager fileManager;

        //Система перемещения объектов
        private DragManager dragManager;

        //Система сетки
        private GridManager gridManager;

        //Не работает
        private const double WorkAreaTop = 40;    // Отступ сверху (под меню)
        private const double WorkAreaBottom = 100; // Отступ снизу (над панелью инструментов)

        private bool isCursor = true;

        public MainWindow()
        {
            InitializeComponent(); //Инициализация WPF компонентов

            InitializeGridManager(); //Создает и инициализирует визуальную сетку
            InitializeDragManager(); //Делает фигуры в панели инструментов перетаскиваемыми
            InitializeArrowManager(); //Инициализация менеджера стрелок
            InitializeSelectionSystem(); //Настраивает систему выделения через SelectionManager
            InitializeFillStrokeManager(); //Инициализация менеджера заливки и обводки
            InitializeTextManager(); //Инициализация менеджера текста
            InitializeFileManager(); //Инициализация менеджера файлов
            InitializeCommentManager();

            InitializePanels(); //Находит и регистрирует все панели выделения
            InitializePropertyBlocks(); //Скрывает блоки свойств при загрузке приложения

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

            // Обработчики для размеров текстовых полей
            WidthTextBox.LostFocus += DimensionTextBox_LostFocus;
            HeightTextBox.LostFocus += DimensionTextBox_LostFocus;
            WidthTextBox.KeyDown += DimensionTextBox_KeyDown;
            HeightTextBox.KeyDown += DimensionTextBox_KeyDown;

            // Инициализация системы автосохранения (интервал 60 секунд)
            autoSave = new AutoSave(MainCanvas, intervalSeconds: 60, enabled: true);

            // Обработчик закрытия окна для очистки ресурсов
            this.Closing += MainWindow_Closing;

            MainCanvas.Loaded += MainCanvas_Loaded;
        }

        private void InitializeDragManager()
        {
            dragManager = new DragManager(MainCanvas, NotationCanvas)
            {
                SnapToGrid = gridManager.SnapToGrid, // Используем метод из GridManager
                SelectElement = SelectElement,
                SelectShape = SelectShape,
                ApplySelectionVisualFeedback = (element, selected) => selectionManager?.ApplySelectionVisualFeedback(element, selected),
                IsElementInNotation = IsElementInNotation,
                GetElementWidth = GetElementWidth,
                GetElementHeight = GetElementHeight,
                BringToFront = BringToFront,
                MakeCanvasShapeDraggable = MakeCanvasShapeDraggable,
                SetOriginalStyles = (copy, original) =>
                {
                    if (original is Ellipse originalEllipse && copy is Ellipse copyEllipse)
                    {
                        selectionManager?.SetOriginalStyles(copyEllipse, originalEllipse.Stroke, originalEllipse.Fill, originalEllipse.StrokeThickness);
                    }
                    else if (original is Border originalBorder && copy is Border copyBorder)
                    {
                        selectionManager?.SetOriginalStyles(copyBorder, originalBorder.BorderBrush, originalBorder.Background, originalBorder.BorderThickness.Left);
                    }
                },
                AnimateShapeAppearance = AnimateShapeAppearance,
                IsShapeFromNotation = IsShapeFromNotation,
                GetShapeType = GetShapeType
            };

            // Передаем состояние привязки к сетке
            dragManager.SetSnapToGridEnabled(gridManager.IsSnapToGridEnabled);

            // Передаем ссылку на GridManager в DragManager
            gridManager.SetDragManager(dragManager);

            // Делаем фигуры перетаскиваемыми
            dragManager.MakeAllShapesDraggable(
                Circle, SimpleRing, Square, ResizableSquare,
                Rectangle, SuperEllipse, Ellipse, UmlInitialNode, stick
            );
        }

        private void InitializeGridManager()
        {
            gridManager = new GridManager(
                MainCanvas,
                GridToggleMenuItem,
                SnapToGridMenuItem,
                dragManager);

            // Подписываемся на события изменения сетки
            gridManager.OnGridToggled += (isEnabled) =>
            {
                // Обновляем состояние в других менеджерах, если нужно
            };

            gridManager.OnSnapToggled += (isEnabled) =>
            {
                // Обновляем состояние в других менеджерах, если нужно
            };
        }

        private void InitializeCommentManager()
        {
            commentManager = new CommentManager(MainCanvas, comment);

            // Подписка на события, если нужно
            commentManager.CommentModeChanged += (isActive) =>
            {
                // Дополнительная логика при изменении режима комментариев
            };

            commentManager.CommentCreated += (newComment) =>
            {
                // Логика при создании комментария
                // Например, сохранение в историю
                SaveToUndoStack(newComment.VisualElement, UndoActionType.Add);
            };

            commentManager.CommentDeleted += (deletedComment) =>
            {
                // Логика при удалении комментария
            };
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

        // Обработчик переключения сетки из меню
        private void GridToggleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            gridManager.HandleGridToggleClick(sender, e);
        }

        // Обработчик переключения привязки к сетке
        private void SnapToGridMenuItem_Click(object sender, RoutedEventArgs e)
        {
            gridManager.HandleSnapToggleClick(sender, e);
        }

        // Обновляем сетку когда Canvas полностью загружен и проинициализирован
        private void MainCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            gridManager.UpdateGrid();
        }

        // Горячие клавиши для удобства
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Обработка Escape для завершения стрелки
            if (e.Key == Key.Escape && arrowManager != null && arrowManager.IsArrowModeActive && arrowManager.CurrentArrow != null)
            {
                arrowManager.CompleteArrow();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.G && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                gridManager.ToggleGridVisibility();
                e.Handled = true;
            }
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                gridManager.ToggleSnapToGrid();
                e.Handled = true;
            }
            else if (e.Key == Key.F12)
            {
                TogglePanelsVisibility();
                e.Handled = true;
            }
            else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
            {
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
            commentManager?.ClearAllComments();

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
                    fileManager.LoadFromFile(openFileDialog.FileName);
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
            fileManager.SaveComments(commentManager, currentFilePath);
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveAsFile_Click(sender, e);
            }
            else
            {
                try
                {
                    fileManager.SaveToFile(currentFilePath);
                    MessageBox.Show("Файл успешно сохранен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            // ... существующий код ...
            fileManager.LoadComments(commentManager, currentFilePath);
            // ... существующий код ...
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
                    fileManager.SaveToFile(saveFileDialog.FileName);
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
                    fileManager.ExportCanvasToImage(saveFileDialog.FileName);
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
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
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



        // Обработчик изменения толщины обводки
        private void StrokeWidthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            fillStrokeManager?.StrokeWidthComboBox_SelectionChanged(sender, e);
        }

        // Вспомогательный метод для применения толщины обводки к конкретной фигуре
        internal void ApplyStrokeThicknessToShape(UIElement shape, double thickness)
        {
            fillStrokeManager?.ApplyStrokeThicknessToShape(shape, thickness);
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
                if (gridManager.IsSnapToGridEnabled) // Используем свойство GridManager
                {
                    Point snappedPoint = gridManager.SnapToGrid(new Point(newLeft, newTop)); // Используем метод GridManager
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
                resizableElements[newElement] = new ResizableElement(newElement, MainCanvas, gridManager.SnapToGrid, gridManager.IsSnapToGridEnabled);

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
            if (commentManager != null)
            {
                var comment = commentManager.GetCommentByElement(original);
                if (comment != null)
                {
                    // Клонирование комментария (если нужно)
                    // Возможно, потребуется добавить логику клонирования комментариев
                }
            }
            else if (original is Polyline originalArrow)
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
                    resizableElements[elementToRestore] = new ResizableElement(elementToRestore, MainCanvas, gridManager.SnapToGrid, gridManager.IsSnapToGridEnabled);
                    
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
                    resizableElements[elementToAdd] = new ResizableElement(elementToAdd, MainCanvas, gridManager.SnapToGrid, gridManager.IsSnapToGridEnabled);
                    
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

        private bool IsShapeFromNotation(UIElement element)
        {
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

        private void MakeCanvasShapeDraggable(UIElement shape)
        {
            // Подписываемся на события перетаскивания через DragManager
            shape.MouseLeftButtonDown += dragManager.CanvasShape_MouseLeftButtonDown;
            shape.MouseMove += dragManager.CanvasShape_MouseMove;
            shape.MouseLeftButtonUp += dragManager.CanvasShape_MouseLeftButtonUp;

            // Обработчик пкм для контекстного меню
            shape.MouseRightButtonDown += (s, e) =>
            {
                if (shape is FrameworkElement frameworkElement)
                {
                    SelectElement(frameworkElement);
                }
                e.Handled = true;
            };
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

            if (commentManager != null && commentManager.HandleMouseClick(e.GetPosition(MainCanvas), e.OriginalSource))
            {
                e.Handled = true;
                return;
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
            // Используем DragManager для проверки состояния перетаскивания
            if (dragManager?.GetIsDraggingState() == true) return;

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

            // Создаем или используем существующий ResizableElement для текущего элемента
            if (!resizableElements.ContainsKey(element))
            {
                // Создаем новый ResizableElement, передавая состояние сетки
                resizableElements[element] = new ResizableElement(
                    element,
                    MainCanvas,
                    gridManager.SnapToGrid, // Используем метод GridManager
                    gridManager.IsSnapToGridEnabled); // Используем свойство GridManager
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

            // Перетаскивание теперь обрабатывается в DragManager через подписки на события
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

            // Используем DragManager для завершения перетаскивания
            if (dragManager?.IsDragging == true)
            {
                // Сохраняем текущую толщину ПОСЛЕ перетаскивания
                var draggedElement = dragManager.DraggedElement;
                if (draggedElement != null && selectionManager?.HasOriginalStrokeThickness(draggedElement) == true)
                {
                    // Обновляем сохраненную толщину
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

                // Завершаем перетаскивание через DragManager
                // (метод CanvasShape_MouseLeftButtonUp уже обрабатывает это)

                e.Handled = true;
            }

            e.Handled = true;
        }


        // ОБРАБОТЧИК СОБЫТИЯ ДЛЯ COMBOBOX
        private void FillColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            fillStrokeManager?.FillColorComboBox_SelectionChanged(sender, e);
        }


        // ОБРАБОТЧИК СОБЫТИЯ ДЛЯ COMBOBOX ОБВОДКИ
        private void StrokeColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            fillStrokeManager?.StrokeColorComboBox_SelectionChanged(sender, e);
        }


        // Инициализация менеджера стрелок
        private void InitializeArrowManager()
        {
            arrowManager = new ArrowManager(
                MainCanvas,
                pencil,
                gridManager.IsSnapToGridEnabled, // Используем свойство GridManager
                WorkAreaTop,
                WorkAreaBottom,
                (element) => { if (element is FrameworkElement fe) BringToFront(fe); },
                (element, actionType) => SaveToUndoStack(element, actionType),
                DeselectAll,
                (element) => { if (element is FrameworkElement fe) { SelectElement(fe); return true; } return false; },
                gridManager.SnapToGrid, // Используем метод из GridManager
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
                    fillStrokeManager?.UpdateColorComboBoxesFromShape(element);

                    // Показываем панель толщины обводки только если элемент НЕ в нотации
                    if (!IsElementInNotation(element))
                    {
                        fillStrokeManager?.UpdateStrokeWidthPanelVisibility();
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

        // Инициализация менеджера заливки и обводки
        private void InitializeFillStrokeManager()
        {
            var fillColorComboBox = FindName("FillColorComboBox") as ComboBox;
            var strokeColorComboBox = FindName("StrokeColorComboBox") as ComboBox;
            var strokeWidthComboBox = FindName("StrokeWidthComboBox") as ComboBox;
            var fillColorPreview = FindName("FillColorPreview") as Border;
            var strokeColorPreview = FindName("StrokeColorPreview") as Border;
            var fillPanel = FindName("FillPanel") as Border;
            var strokePanel = FindName("StrokePanel") as Border;
            var strokeWidthPanel = FindName("StrokeWidthPanel") as Border;

            fillStrokeManager = new FillStrokeManager(
                selectionManager,
                TogglePanelsButton,
                fillColorComboBox,
                strokeColorComboBox,
                strokeWidthComboBox,
                fillColorPreview,
                strokeColorPreview,
                fillPanel,
                strokePanel,
                strokeWidthPanel
            );
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
                gridManager.IsSnapToGridEnabled, // Используем свойство GridManager
                (element) => { if (element is FrameworkElement fe) BringToFront(fe); },
                (element) => { if (element is FrameworkElement fe) SelectElement(fe); },
                (element) => { if (element is UIElement ue) SelectShape(ue); },
                DeselectAll,
                MakeCanvasShapeDraggable,
                gridManager.SnapToGrid, // Используем метод GridManager
                (element) => SaveToUndoStack(element, UndoActionType.Add)
            );
        }

        // Инициализация менеджера файлов
        private void InitializeFileManager()
        {
            fileManager = new FileManager(
                MainCanvas,
                FileNameTextBox,
                arrowManager,
                resizableElements,
                canvasElements,
                MakeCanvasShapeDraggable,
                gridManager.SnapToGrid, // Используем метод GridManager
                () => gridManager.IsSnapToGridEnabled // Используем свойство GridManager
            );
        }

        private void ClearSelection()
        {
            selectionManager?.ClearSelection();
        }


        // Вспомогательный метод для применения цвета заливки
        internal void ApplyFillColorToShape(UIElement shape, Brush brush)
        {
            fillStrokeManager?.ApplyFillColorToShape(shape, brush);
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

        // Вспомогательный метод для применения цвета обводки к конкретной фигуре
        internal void ApplyStrokeColorToShape(UIElement shape, Brush brush)
        {
            fillStrokeManager?.ApplyStrokeColorToShape(shape, brush);
        }

        private void AnimateShapeAppearance(UIElement shape)
        {
            // Сохраняем оригинальную трансформацию
            Transform originalTransform = shape.RenderTransform;

            var scaleAnimation = new DoubleAnimation
            {
                From = 0.5,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(originalTransform);
            transformGroup.Children.Add(new ScaleTransform());

            shape.RenderTransform = transformGroup;
            shape.RenderTransformOrigin = new Point(0.5, 0.5);

            transformGroup.Children[1].BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            transformGroup.Children[1].BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
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
                    fillStrokeManager?.UpdateStrokeWidthComboBoxSelection(arrowContainer.Line.StrokeThickness);
                }

                // Обновляем комбо-бокс цвета обводки
                if (StrokeColorComboBox != null && arrowContainer.Line.Stroke is SolidColorBrush strokeBrush)
                {
                    string strokeHexColor = strokeBrush.Color.ToString().Substring(3);
                    fillStrokeManager?.UpdateColorComboBoxSelection(StrokeColorComboBox, strokeHexColor);
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