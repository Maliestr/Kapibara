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
using System.Windows.Controls.Primitives;
namespace Graphic_Editor
{
    public partial class MainWindow : Window
    {
        #region Поля и свойства

        // Управление элементами холста
        private string currentFilePath = null; // Путь к текущему файлу (null - новый файл)
        private List<UIElement> canvasElements = new List<UIElement>(); // Список всех элементов на холсте

        // Добавляем поле для хранения скопированных элементов
        private Dictionary<UIElement, ResizableElement> resizableElements =
            new Dictionary<UIElement, ResizableElement>(); // Хранит связь между элементами на холсте и их контроллерами изменения размера
        private ResizableElement currentResizableElement; // Текущий активный изменяемый в размерах элемент

        // Управление интерфейсом
        private bool arePanelsVisible = true; // Видимость всех панелей инструментов
        private List<Border> panels = new List<Border>(); // Список всех плашек для управления видимостью

        // Системы управления
        private SelectionManager selectionManager; // Менеджер системы выделения
        private CommentManager commentManager; // Система комментариев
        private ArrowManager arrowManager; // Система рисования стрелок
        private TextManager textManager; // Система управления текстом
        private FillStrokeManager fillStrokeManager; // Система управления заливкой и обводкой
        private FileManager fileManager; // Система управления файлами
        private DragManager dragManager; // Система перемещения объектов
        private GridManager gridManager; // Система сетки
        private ClipboardManager clipboardManager; // Система буфера обмена и отмены/повтора
        private PoolManager poolManager; // Система управления пулами (swimlanes)
        private CanvasPanningManager canvasPanningManager;
        private AutoSave autoSave; // Система автоматического сохранения проекта

        // Константы
        private const double WorkAreaTop = 40;    // Отступ сверху (под меню)
        private const double WorkAreaBottom = 100; // Отступ снизу (над панелью инструментов)

        private bool isCursor = true;
        private object canvas;

        public object StrokeWidthPanel { get; private set; }
        public ToggleButton TogglePanelsButton { get; private set; }
        public Ellipse SimpleRing { get; private set; }

        #endregion

        #region Конструктор и инициализация

        public MainWindow(string filePath)
        {
            InitializeComponent(); // Инициализация WPF компонентов

            InitializeManagers(); // Инициализация всех менеджеров
            InitializePanels(); // Находит и регистрирует все панели выделения
            InitializePropertyBlocks(); // Скрывает блоки свойств при загрузке приложения

            isActiveCursor();

            // Обработка горячих клавиш
            this.KeyDown += MainWindow_KeyDown; // Ctrl+Z/Y - отмена/повтор
                                                // Ctrl+X/C/V - вырезание/копирование/вставка
                                                // Delete - удаление выделенного
                                                // Ctrl+G - переключение сетки
                                                // Ctrl+S - привязка к сетке
                                                // F12/Ctrl+H - скрытие панелей

            // Добавляем обработчик клика по Canvas для сброса выделения
            var mainCanvas = FindName("MainCanvas") as Canvas;
            if (mainCanvas != null)
            {
                mainCanvas.MouseLeftButtonDown += MainCanvas_MouseLeftButtonDown;
                mainCanvas.Loaded += MainCanvas_Loaded;
            }

            // Кнопка курсора для обработки взаимодействия с объектами
            var cursorBtn = FindName("cursor") as Button;
            if (cursorBtn != null) cursorBtn.Click += CursorButton_Click;
            // Кнопка пера включает/выключает режим рисования стрелок
            var pencilBtn = FindName("pencil") as Button;
            if (pencilBtn != null) pencilBtn.Click += PenButton_Click;

            // Обработчики для размеров текстовых полей
            var widthTextBox = FindName("WidthTextBox") as TextBox;
            var heightTextBox = FindName("HeightTextBox") as TextBox;
            if (widthTextBox != null)
            {
                widthTextBox.LostFocus += DimensionTextBox_LostFocus;
                widthTextBox.KeyDown += DimensionTextBox_KeyDown;
            }
            if (heightTextBox != null)
            {
                heightTextBox.LostFocus += DimensionTextBox_LostFocus;
                heightTextBox.KeyDown += DimensionTextBox_KeyDown;
            }

            // Инициализация системы автосохранения (интервал 60 секунд)
            if (mainCanvas != null)
            {
                autoSave = new AutoSave(mainCanvas, intervalSeconds: 60, enabled: true);
            }

            // Обработчик закрытия окна для очистки ресурсов
            this.Closing += MainWindow_Closing;
        }

        private void InitializeComponent()
        {
            throw new NotImplementedException();
        }

        private void InitializeManagers()
        {
            InitializeGridManager(); // Создает и инициализирует визуальную сетку
            InitializeDragManager(); // Делает фигуры в панели инструментов перетаскиваемыми
            InitializeArrowManager(); // Инициализация менеджера стрелок
            InitializeSelectionSystem(canvas); // Настраивает систему выделения через SelectionManager
            InitializeFillStrokeManager(); // Инициализация менеджера заливки и обводки
            InitializeTextManager(); // Инициализация менеджера текста
            InitializeFileManager(); // Инициализация менеджера файлов
            InitializeCommentManager(); // Инициализация менеджера комментариев
            InitializeClipboardManager(); // Инициализация менеджера буфера обмена и отмены/повтора
            InitializePoolManager(); // Инициализация менеджера пулов
            var mainCanvasForPanning = FindName("MainCanvas") as Canvas;
            if (mainCanvasForPanning != null)
            {
                canvasPanningManager = new CanvasPanningManager(mainCanvasForPanning);
                canvasPanningManager.Mode = CanvasPanningManager.PanningMode.MiddleButton;
            }
        }

        private void InitializeSelectionSystem(object canvas)
        {
            throw new NotImplementedException();
        }

        private void InitializeDragManager()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            var notationCanvas = FindName("NotationCanvas") as Canvas;
            if (mainCanvas == null || notationCanvas == null) return;

            dragManager = new DragManager(mainCanvas, notationCanvas)
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
                GetShapeType = GetShapeType,
                OnElementMoved = (element, position) => 
                {
                    poolManager?.HandleElementMove(element, position); // Обработка прикрепления к пулам
                    arrowManager?.UpdateAttachedArrows(element); // Обновление прикрепленных стрелок
                }
            };

            // Передаем состояние привязки к сетке
            dragManager.SetSnapToGridEnabled(gridManager.IsSnapToGridEnabled);

            // Передаем ссылку на GridManager в DragManager
            gridManager.SetDragManager(dragManager);

            // Делаем фигуры перетаскиваемыми
            var circle = FindName("Circle") as Ellipse;
            var simpleRing = FindName("SimpleRing") as Ellipse;
            var square = FindName("Square") as Border;
            var resizableSquare = FindName("ResizableSquare") as Border;
            var rectangle = FindName("Rectangle") as Border;
            var superEllipse = FindName("SuperEllipse") as Border;
            var ellipse = FindName("Ellipse") as Ellipse;
            var umlInitialNode = FindName("UmlInitialNode") as Border;
            var stickShape = FindName("stick") as Border;

            List<UIElement> shapes = new List<UIElement>();
            if (circle != null) shapes.Add(circle);
            if (simpleRing != null) shapes.Add(simpleRing);
            if (square != null) shapes.Add(square);
            if (resizableSquare != null) shapes.Add(resizableSquare);
            if (rectangle != null) shapes.Add(rectangle);
            if (superEllipse != null) shapes.Add(superEllipse);
            if (ellipse != null) shapes.Add(ellipse);
            if (umlInitialNode != null) shapes.Add(umlInitialNode);
            if (stickShape != null) shapes.Add(stickShape);

            if (shapes.Count > 0)
            {
                dragManager.MakeAllShapesDraggable(shapes.ToArray());
            }
        }

        private void InitializeGridManager()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            var gridToggleMenuItem = FindName("GridToggleMenuItem") as MenuItem;
            var snapToGridMenuItem = FindName("SnapToGridMenuItem") as MenuItem;
            if (mainCanvas == null || gridToggleMenuItem == null || snapToGridMenuItem == null) return;

            gridManager = new GridManager(
                mainCanvas,
                gridToggleMenuItem,
                snapToGridMenuItem,
                dragManager);

            // Подписываемся на события изменения сетки
            gridManager.OnGridToggled += (isEnabled) =>
            {
                // Обновляем состояние в других менеджерах, если нужно
            };

            gridManager.OnSnapToggled += (isEnabled) =>
            {
                // Обновляем состояние в других менеджерах, если нужно
                poolManager?.UpdateSnapToGrid(isEnabled);
            };
        }

        private void InitializeCommentManager()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            var commentBtn = FindName("comment") as Button;
            if (mainCanvas == null || commentBtn == null) return;

            commentManager = new CommentManager(mainCanvas, commentBtn);

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

        private void InitializeClipboardManager()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            if (mainCanvas == null) return;

            clipboardManager = new ClipboardManager(
                mainCanvas,
                selectionManager,
                resizableElements,
                gridManager);

            // Подписываемся на события для обновления интерфейса
            clipboardManager.SelectionChanged += () =>
            {
                // Обновляем состояние меню/кнопок если нужно
            };

            clipboardManager.UndoRedoStackChanged += () =>
            {
                // Обновляем состояние кнопок Undo/Redo если нужно
                // Например: UndoMenuItem.IsEnabled = clipboardManager.CanUndo;
            };
        }

        #endregion

        #region Обработчики событий окна

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Останавливаем автосохранение и сохраняем перед закрытием
            if (autoSave != null)
            {
                autoSave.SaveNow(); // Принудительное сохранение перед закрытием
                autoSave.Dispose();
            }
        }

        private void MainCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            gridManager.UpdateGrid();
        }

        #endregion

        #region Обработчики событий мыши

        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var mainCanvas = sender as Canvas ?? FindName("MainCanvas") as Canvas;
            if (mainCanvas == null) return;

            if (arrowManager != null && arrowManager.IsArrowModeActive)
            {
                Point clickPoint = e.GetPosition(mainCanvas);

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

            if (commentManager != null && commentManager.HandleMouseClick(e.GetPosition(mainCanvas), e.OriginalSource))
            {
                e.Handled = true;
                return;
            }

            if (poolManager != null && poolManager.HandleMouseClick(e.GetPosition(mainCanvas), e.OriginalSource))
            {
                e.Handled = true;
                return;
            }

            if (textManager != null && textManager.HandleMouseClick(e.GetPosition(mainCanvas), e.OriginalSource))
            {
                e.Handled = true;
                return;
            }

            // Если кликнули на пустую область канваса - снимаем выделение и скрываем ручки
            if (e.OriginalSource == mainCanvas)
            {
                DeselectAll();
                HideAllResizeHandles();
                e.Handled = true;
            }
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var mainCanvas = sender as Canvas ?? FindName("MainCanvas") as Canvas;
            if (mainCanvas == null) return;

            // Обработка рисования стрелки
            if (arrowManager != null && arrowManager.IsArrowModeActive)
            {
                if (arrowManager.CurrentArrow != null)
                {
                    arrowManager.UpdateArrowDrawing(e.GetPosition(mainCanvas));
                }
                e.Handled = true;
                return;
            }

            // Обработка выделения
            if (selectionManager?.IsSelecting == true)
            {
                var currentPos = e.GetPosition(mainCanvas);
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
                
                // Обрабатываем прикрепление элемента к пулу
                if (draggedElement != null && poolManager != null)
                {
                    double elementLeft = Canvas.GetLeft(draggedElement);
                    double elementTop = Canvas.GetTop(draggedElement);
                    
                    if (!double.IsNaN(elementLeft) && !double.IsNaN(elementTop))
                    {
                        Point elementPosition = new Point(elementLeft, elementTop);
                        poolManager.HandleElementMove(draggedElement, elementPosition);
                    }
                }
                
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

        private void MainCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (arrowManager != null && arrowManager.IsArrowModeActive && arrowManager.CurrentArrow != null)
            {
                // Завершаем рисование стрелки по правой кнопке
                arrowManager.CompleteArrow();
                e.Handled = true;
            }
        }

        #endregion

        #region Обработчики клавиатуры

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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        clipboardManager?.UndoLastAction();
                        e.Handled = true;
                        break;
                    case Key.Y:
                        clipboardManager?.RedoLastAction();
                        e.Handled = true;
                        break;
                    case Key.X:
                        clipboardManager?.CutSelectedElement();
                        e.Handled = true;
                        break;
                    case Key.C:
                        clipboardManager?.CopySelectedElement();
                        e.Handled = true;
                        break;
                    case Key.V:
                        clipboardManager?.PasteElement();
                        e.Handled = true;
                        break;
                        // ... остальные клавиши ...
                }
            }

            // Удаление выделенного элемента
            if (e.Key == Key.Delete && selectionManager?.SelectedElement != null)
            {
                clipboardManager?.DeleteSelectedElement();
                e.Handled = true;
            }
        }

        #endregion

        #region Меню "Файл"

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            // Очищаем холст
            var mainCanvas = FindName("MainCanvas") as Canvas;
            if (mainCanvas != null) mainCanvas.Children.Clear();
            canvasElements.Clear();
            currentFilePath = null;
            commentManager?.ClearAllComments();
            poolManager?.ClearAllPools();

            // Сбрасываем имя файла на "Untitled"
            var fileNameTextBox = FindName("FileNameTextBox") as TextBox;
            if (fileNameTextBox != null)
            {
                fileNameTextBox.Text = "Untitled";
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
            var fileNameTextBox = FindName("FileNameTextBox") as TextBox;
            if (fileNameTextBox != null && !string.IsNullOrEmpty(fileNameTextBox.Text) && fileNameTextBox.Text != "Untitled")
            {
                string fileNameFromTextBox = fileNameTextBox.Text;
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
                    if (FindName("FileNameTextBox") as TextBox != null)
                    {
                        string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                        (FindName("FileNameTextBox") as TextBox).Text = fileNameWithoutExtension;
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

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion

        #region Меню "Вид"

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

        #endregion

        #region Меню "Правка"

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            clipboardManager?.UndoLastAction();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            clipboardManager?.RedoLastAction();
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            clipboardManager?.CutSelectedElement();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            clipboardManager?.CopySelectedElement();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            clipboardManager?.PasteElement();
        }

        #endregion

        #region Меню "Справка"

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            HelpSystem.ShowHelp();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("О программе");
        }

        #endregion

        #region Управление окном

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

        private void UpdateWindowTitle()
        {
            // Используем текст из TextBox, если он доступен, иначе имя файла из файловой системы
            string fileName;
            var fileNameTextBox = FindName("FileNameTextBox") as TextBox;
            if (fileNameTextBox != null && !string.IsNullOrEmpty(fileNameTextBox.Text) && fileNameTextBox.Text != "Untitled")
            {
                fileName = fileNameTextBox.Text;
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

        #endregion

        #region Управление панелями

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

            if (dimensionsPanel != null) dimensionsPanel.Visibility = Visibility.Collapsed;
            if (fillPanel != null) fillPanel.Visibility = Visibility.Collapsed;
            if (strokePanel != null) strokePanel.Visibility = Visibility.Collapsed;
            if (textPanel != null) textPanel.Visibility = Visibility.Collapsed;
        }

        private void TogglePanelsButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePanelsVisibility();
        }

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
                //StrokeWidthPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowAllPanels()
        {
            var notationCard = FindName("NotationCard") as Border;
            if (notationCard != null) notationCard.Visibility = Visibility.Visible;

            var selectedElement = selectionManager?.SelectedElement;
            var dimensionsPanel = FindName("DimensionsPanel") as Border;
            var fillPanel = FindName("FillPanel") as Border;
            var strokePanel = FindName("StrokePanel") as Border;
            var textPanel = FindName("TextPanel") as Border;
            var strokeWidthPanel = FindName("StrokeWidthPanel") as Border;

            if (selectedElement is Ellipse ellipse || selectedElement is Border border || selectedElement is Line line)
            {
                if (dimensionsPanel != null) dimensionsPanel.Visibility = Visibility.Visible;
                if (fillPanel != null) fillPanel.Visibility = Visibility.Visible;
                if (strokePanel != null) strokePanel.Visibility = Visibility.Visible;
                // Показываем панель ширины обводки только если элемент НЕ в нотации
                if (!IsElementInNotation(selectedElement))
                {
                    if (strokeWidthPanel != null) strokeWidthPanel.Visibility = Visibility.Visible;
                }
            }
            else if (selectedElement is TextBlock textBlock)
            {
                if (dimensionsPanel != null) dimensionsPanel.Visibility = Visibility.Visible;
                // Не показываем панель обводки для текста
                if (strokePanel != null) strokePanel.Visibility = Visibility.Collapsed;
                if (textPanel != null) textPanel.Visibility = Visibility.Visible;
                // Не показываем панель ширины обводки для текста
                if (strokeWidthPanel != null) strokeWidthPanel.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Управление элементами

        private void SelectElement(FrameworkElement element)
        {
            if (element == null) return;
            selectionManager?.SelectElement(element);

            var togglePanelsButton = FindName("TogglePanelsButton") as ToggleButton;
            var dimensionsPanel = FindName("DimensionsPanel") as Border;
            var strokePanel = FindName("StrokePanel") as Border;
            var textPanel = FindName("TextPanel") as Border;
            var strokeWidthPanel = FindName("StrokeWidthPanel") as Border;

            if (togglePanelsButton != null && togglePanelsButton.IsChecked == false && element is TextBlock textBlock)
            {
                if (dimensionsPanel != null) dimensionsPanel.Visibility = Visibility.Visible;
                if (strokePanel != null) strokePanel.Visibility = Visibility.Visible;
                if (textPanel != null) textPanel.Visibility = Visibility.Visible;
                if (strokeWidthPanel != null) strokeWidthPanel.Visibility = Visibility.Visible;
            }
        }

        private void DeselectAll()
        {
            selectionManager?.DeselectAll();
        }

        public void SelectShape(UIElement shape)
        {
            if (shape == null) return;
            selectionManager?.SelectShape(shape);
        }

        /// <summary>
        /// Скрывает все ручки изменения размера ResizableElement
        /// </summary>
        private void HideAllResizeHandles()
        {
            // Скрываем ручки для обычных элементов
            if (currentResizableElement != null)
            {
                currentResizableElement.HideResizeHandles();
                currentResizableElement = null;
            }

            // Скрываем ручки для всех элементов в resizableElements
            foreach (var resizable in resizableElements.Values)
            {
                resizable?.HideResizeHandles();
            }

            // Скрываем ручки для всех пулов
            if (poolManager != null)
            {
                foreach (var pool in poolManager.Pools)
                {
                    poolManager.HideResizeHandlesForPool(pool);
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

        private void SelectAllElements()
        {
            selectionManager?.SelectAllElements();
        }

        private void ClearSelection()
        {
            selectionManager?.ClearSelection();
        }

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

        #endregion

        #region Управление размерами элементов

        private void DimensionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyDimensionsFromTextboxes();
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

            var widthTextBox = FindName("WidthTextBox") as TextBox;
            var heightTextBox = FindName("HeightTextBox") as TextBox;
            if (widthTextBox == null || heightTextBox == null) return;

            if (double.TryParse(widthTextBox.Text, out double width) &&
                double.TryParse(heightTextBox.Text, out double height))
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

        public void UpdateDimensions(double width, double height)
        {
            // Обновляем текстовые поля размеров если они существуют
            var widthTextBox = FindName("WidthTextBox") as TextBox;
            var heightTextBox = FindName("HeightTextBox") as TextBox;
            if (widthTextBox != null && !double.IsNaN(width))
            {
                widthTextBox.Text = Math.Round(width).ToString();
            }

            if (heightTextBox != null && !double.IsNaN(height))
            {
                heightTextBox.Text = Math.Round(height).ToString();
            }
        }

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

            var togglePanelsButton = FindName("TogglePanelsButton") as ToggleButton;
            var dimensionsPanel = FindName("DimensionsPanel") as Border;
            var fillPanel = FindName("FillPanel") as Border;
            var strokePanel = FindName("StrokePanel") as Border;

            if (togglePanelsButton != null && togglePanelsButton.IsChecked == false && (element is Ellipse ellipse || element is Border border || element is Line line))
            {
                if (dimensionsPanel != null) dimensionsPanel.Visibility = Visibility.Visible;
                if (fillPanel != null) fillPanel.Visibility = Visibility.Visible;
                if (strokePanel != null) strokePanel.Visibility = Visibility.Visible;
            }

            // Создаем или используем существующий ResizableElement для текущего элемента
            if (!resizableElements.ContainsKey(element))
            {
                // Создаем новый ResizableElement, передавая состояние сетки
                var mainCanvas = FindName("MainCanvas") as Canvas;
                if (mainCanvas != null)
                {
                    resizableElements[element] = new ResizableElement(
                        element,
                        mainCanvas,
                        gridManager.SnapToGrid, // Используем метод GridManager
                        gridManager.IsSnapToGridEnabled); // Используем свойство GridManager
                }
            }

            currentResizableElement = resizableElements[element];
            currentResizableElement.ShowResizeHandles();

            // Обновляем текстовые поля размеров
            if (element is FrameworkElement frameworkElement)
            {
                UpdateDimensions(frameworkElement.ActualWidth, frameworkElement.ActualHeight);
            }
        }

        #endregion

        #region Управление Z-порядком

        private void BringToFront(FrameworkElement element)
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            if (element == null || mainCanvas == null || !mainCanvas.Children.Contains(element)) return;

            int maxZIndex = GetMaxZIndex();
            Canvas.SetZIndex(element, maxZIndex + 1);

            // Также перемещаем элемент в конец коллекции для гарантии
            mainCanvas.Children.Remove(element);
            mainCanvas.Children.Add(element);
        }

        private void SendToBack(FrameworkElement element)
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            if (element == null || mainCanvas == null || !mainCanvas.Children.Contains(element)) return;

            int minZIndex = GetMinZIndex();
            Canvas.SetZIndex(element, minZIndex - 1);

            // Также перемещаем элемент в начало коллекции для гарантии
            mainCanvas.Children.Remove(element);
            mainCanvas.Children.Insert(0, element);
        }

        private int GetMaxZIndex()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            if (mainCanvas == null) return 0;

            int maxZIndex = 0;
            foreach (UIElement child in mainCanvas.Children)
            {
                int zIndex = Canvas.GetZIndex(child);
                if (zIndex > maxZIndex)
                {
                    maxZIndex = zIndex;
                }
            }
            return maxZIndex;
        }

        private int GetMinZIndex()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            if (mainCanvas == null) return 0;

            int minZIndex = 0;
            bool first = true;
            foreach (UIElement child in mainCanvas.Children)
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

        #endregion

        #region Контекстное меню

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
            cutMenuItem.Click += (s, e) => clipboardManager?.CutSelectedElement(); // Используем clipboardManager
            contextMenu.Items.Add(cutMenuItem);

            // Пункт "Копировать"
            MenuItem copyMenuItem = new MenuItem
            {
                Header = "Копировать (Ctrl+C)",
                Icon = new TextBlock { Text = "📄", FontSize = 14 }
            };
            copyMenuItem.Click += (s, e) => clipboardManager?.CopySelectedElement(); // Используем clipboardManager
            contextMenu.Items.Add(copyMenuItem);

            // Пункт "Вставить"
            MenuItem pasteMenuItem = new MenuItem
            {
                Header = "Вставить (Ctrl+V)",
                Icon = new TextBlock { Text = "📋", FontSize = 14 }
            };
            pasteMenuItem.Click += (s, e) => clipboardManager?.PasteElement(); // Используем clipboardManager
            contextMenu.Items.Add(pasteMenuItem);

            contextMenu.Items.Add(new Separator());

            // Пункт "Удалить"
            MenuItem deleteMenuItem = new MenuItem
            {
                Header = "Удалить (Delete)",
                Icon = new TextBlock { Text = "🗑", FontSize = 14 }
            };
            deleteMenuItem.Click += (s, e) => clipboardManager?.DeleteSelectedElement(); // Используем clipboardManager
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

        #endregion

        #region Вспомогательные методы

        private bool IsElementInNotation(UIElement element)
        {
            var notationCanvas = FindName("NotationCanvas") as Canvas;
            if (element == null || notationCanvas == null) return false;

            // Проверяем, содержится ли элемент непосредственно в NotationCanvas
            if (notationCanvas.Children.Contains(element))
                return true;

            // Проверяем родителей элемента
            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent == notationCanvas)
                    return true;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return false;
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

        private bool IsShapeFromNotation(UIElement element)
        {
            var notationCanvas = FindName("NotationCanvas") as Canvas;
            return notationCanvas?.Children.Contains(element) == true;
        }

        private string GetShapeType(UIElement element)
        {
            var circle = FindName("Circle") as Ellipse;
            var simpleRing = FindName("SimpleRing") as Ellipse;
            var square = FindName("Square") as Border;
            var resizableSquare = FindName("ResizableSquare") as Border;
            var rectangle = FindName("Rectangle") as Border;
            var superEllipse = FindName("SuperEllipse") as Border;
            var ellipse = FindName("Ellipse") as Ellipse;
            var umlInitialNode = FindName("UmlInitialNode") as Border;
            var stickShape = FindName("stick") as Border;

            if (element == circle) return "Circle";
            if (element == simpleRing) return "SimpleRing";
            if (element == square) return "Square";
            if (element == resizableSquare) return "ResizableSquare";
            if (element == rectangle) return "Rectangle";
            if (element == superEllipse) return "SuperEllipse";
            if (element == ellipse) return "Ellipse";
            if (element == umlInitialNode) return "UmlInitialNode";
            if (element == stickShape) return "stick";
            return "Unknown";
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

        #endregion

        #region Методы управления внешним видом

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

        private void ApplySelectionVisualFeedback(UIElement element, bool isSelected)
        {
            selectionManager?.ApplySelectionVisualFeedback(element, isSelected);
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

        #endregion

        #region Система отмены/повтора

        private void SaveToUndoStack(UIElement element, UndoActionType actionType = UndoActionType.Add)
        {
            clipboardManager?.SaveToUndoStack(element, actionType);
        }

        private void SaveToUndoStack(UIElement element)
        {
            SaveToUndoStack(element, UndoActionType.Add);
        }

        private void CutSelectedElements()
        {
            clipboardManager?.CutSelectedElements();
        }

        #endregion

        #region Сетка

        private void GridToggleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            gridManager.HandleGridToggleClick(sender, e);
        }

        private void SnapToGridMenuItem_Click(object sender, RoutedEventArgs e)
        {
            gridManager.HandleSnapToggleClick(sender, e);
        }

        #endregion

        #region Стрелки

        private void InitializeArrowManager()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            var pencilBtn = FindName("pencil") as Button;
            if (mainCanvas == null || pencilBtn == null) return;

            arrowManager = new ArrowManager(
                mainCanvas,
                pencilBtn,
                gridManager.IsSnapToGridEnabled, // Используем свойство GridManager
                WorkAreaTop,
                WorkAreaBottom,
                (element) => { if (element is FrameworkElement fe) BringToFront(fe); },
                (element, actionType) => clipboardManager?.SaveToUndoStack(element, actionType),
                DeselectAll,
                (element) => { if (element is FrameworkElement fe) { SelectElement(fe); return true; } return false; },
                gridManager.SnapToGrid, // Используем метод из GridManager
                GetMaxZIndex,
                GetMinZIndex,
                UpdateArrowPropertiesInPanels
            );
        }

        private void PenButton_Click(object sender, RoutedEventArgs e)
        {
            if (arrowManager != null)
            {
                arrowManager.ToggleArrowMode();
            }
        }

        private void UpdateArrowPropertiesInPanels(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null) return;

            try
            {
                // Показываем панель обводки для стрелки
                var strokeWidthPanel = FindName("StrokeWidthPanel") as Border;
                if (strokeWidthPanel != null)
                {
                    strokeWidthPanel.Visibility = Visibility.Visible;
                }

                // Обновляем комбо-бокс толщины
                var strokeWidthComboBox = FindName("StrokeWidthComboBox") as ComboBox;
                if (strokeWidthComboBox != null)
                {
                    fillStrokeManager?.UpdateStrokeWidthComboBoxSelection(arrowContainer.Line.StrokeThickness);
                }

                // Обновляем комбо-бокс цвета обводки
                var strokeColorComboBox = FindName("StrokeColorComboBox") as ComboBox;
                if (strokeColorComboBox != null && arrowContainer.Line.Stroke is SolidColorBrush strokeBrush)
                {
                    string strokeHexColor = strokeBrush.Color.ToString().Substring(3);
                    fillStrokeManager?.UpdateColorComboBoxSelection(strokeColorComboBox, strokeHexColor);
                }

                // Скрываем панели, которые не применимы к стрелкам
                var fillPanel = FindName("FillPanel") as Border;
                var textPanel = FindName("TextPanel") as Border;
                if (fillPanel != null)
                {
                    fillPanel.Visibility = Visibility.Collapsed;
                }
                if (textPanel != null)
                {
                    textPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в UpdateArrowPropertiesInPanels: {ex.Message}");
            }
        }

        #endregion

        #region Система выделения

        private void InitializeSelectionSystem(Canvas mainCanvas)
        {
            // Создаем менеджер выделения
            Canvas canvas = FindName("MainCanvas") as Canvas;
            if (canvas == null) return;

            selectionManager = new SelectionManager(canvas, this);

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
                var strokeWidthPanel = FindName("StrokeWidthPanel") as Border;
                var dimensionsPanel = FindName("DimensionsPanel") as Border;
                var fillPanel = FindName("FillPanel") as Border;
                var strokePanel = FindName("StrokePanel") as Border;
                var textPanel = FindName("TextPanel") as Border;

                if (strokeWidthPanel != null) strokeWidthPanel.Visibility = Visibility.Collapsed;
                if (dimensionsPanel != null) dimensionsPanel.Visibility = Visibility.Collapsed;
                if (fillPanel != null) fillPanel.Visibility = Visibility.Collapsed;
                if (strokePanel != null) strokePanel.Visibility = Visibility.Collapsed;
                if (textPanel != null) textPanel.Visibility = Visibility.Collapsed;
            };
            if (canvas != null)
            {
                canvas.MouseLeftButtonDown += MainCanvas_MouseLeftButtonDown;
                canvas.MouseMove += MainCanvas_MouseMove;
                canvas.MouseLeftButtonUp += MainCanvas_MouseLeftButtonUp;
                canvas.MouseRightButtonDown += MainCanvas_MouseRightButtonDown;
            }
        }

        #endregion

        #region Заливка и обводка

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

        private void FillColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            fillStrokeManager?.FillColorComboBox_SelectionChanged(sender, e);
        }

        private void StrokeColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            fillStrokeManager?.StrokeColorComboBox_SelectionChanged(sender, e);
        }

        private void StrokeWidthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            fillStrokeManager?.StrokeWidthComboBox_SelectionChanged(sender, e);
        }

        internal void ApplyFillColorToShape(UIElement shape, Brush brush)
        {
            fillStrokeManager?.ApplyFillColorToShape(shape, brush);
        }

        internal void ApplyStrokeColorToShape(UIElement shape, Brush brush)
        {
            fillStrokeManager?.ApplyStrokeColorToShape(shape, brush);
        }

        internal void ApplyStrokeThicknessToShape(UIElement shape, double thickness)
        {
            fillStrokeManager?.ApplyStrokeThicknessToShape(shape, thickness);
        }

        private void ClearColorComboBoxes()
        {
            var fillColorComboBox = FindName("FillColorComboBox") as ComboBox;
            var strokeColorComboBox = FindName("StrokeColorComboBox") as ComboBox;
            var fillColorPreview = FindName("FillColorPreview") as Border;
            var strokeColorPreview = FindName("StrokeColorPreview") as Border;

            if (fillColorComboBox != null) fillColorComboBox.SelectedIndex = -1;
            if (strokeColorComboBox != null) strokeColorComboBox.SelectedIndex = -1;
            if (fillColorPreview != null) fillColorPreview.Background = Brushes.Transparent;
            if (strokeColorPreview != null) strokeColorPreview.Background = Brushes.Transparent;
        }

        #endregion

        #region Текст

        private void InitializeTextManager()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            var fontComboBox = FindName("FontComboBox") as ComboBox;
            var fontStyleComboBox = FindName("FontStyleComboBox") as ComboBox;
            var fontSizeComboBox = FindName("FontSizeComboBox") as ComboBox;
            var textPanel = FindName("TextPanel") as Border;
            if (mainCanvas == null) return;

            textManager = new TextManager(
                mainCanvas,
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
                (element) => clipboardManager?.SaveToUndoStack(element, UndoActionType.Add)
            );

            // Подписываемся на события для выделения кнопки
            var textBtn = FindName("text") as Button;
            textManager.OnTextModeActivated += () =>
            {
                if (textBtn != null)
                {
                    textBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3939"));
                }
            };

            textManager.OnTextModeDeactivated += () =>
            {
                if (textBtn != null)
                {
                    textBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF343434"));
                }
            };
        }

        private void TextButton_Click(object sender, RoutedEventArgs e)
        {
            if (textManager?.IsTextModeActive == true)
            {
                // Если режим уже активен, деактивируем его
                textManager?.DeactivateTextMode();
                return;
            }
            textManager?.ActivateTextMode();
        }

        #endregion

        #region Файлы

        private void InitializeFileManager()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            var fileNameTextBox = FindName("FileNameTextBox") as TextBox;
            if (mainCanvas == null || fileNameTextBox == null) return;

            fileManager = new FileManager(
                mainCanvas,
                fileNameTextBox,
                arrowManager,
                resizableElements,
                canvasElements,
                MakeCanvasShapeDraggable,
                gridManager.SnapToGrid, // Используем метод GridManager
                () => gridManager.IsSnapToGridEnabled // Используем свойство GridManager
            );
        }

        private void FileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateWindowTitle();
        }

        #endregion

        #region Пулы (Swimlanes)

        private void InitializePoolManager()
        {
            var mainCanvas = FindName("MainCanvas") as Canvas;
            var rectangleBtn = FindName("rectangle") as Button;
            if (mainCanvas == null || rectangleBtn == null) return;

            poolManager = new PoolManager(
                mainCanvas,
                rectangleBtn, // Кнопка rectangle (3-я кнопка) на нижней панели
                gridManager.IsSnapToGridEnabled, // Используем свойство GridManager
                gridManager.SnapToGrid, // Используем метод GridManager
                (element, actionType) => clipboardManager?.SaveToUndoStack(element, actionType)
            );

            // Подписываемся на события
            poolManager.PoolModeChanged += (isActive) =>
            {
                // Дополнительная логика при изменении режима пулов
            };

            poolManager.PoolCreated += (pool) =>
            {
                // Логика при создании пула
                SaveToUndoStack(pool.Container, UndoActionType.Add);
            };

            poolManager.PoolDeleted += (pool) =>
            {
                // Логика при удалении пула
            };
        }

        #endregion

        #region Восстановление внешнего вида

        private void SaveOriginalColors(UIElement shape)
        {
            selectionManager?.SaveOriginalColors(shape);
        }

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

        #endregion

        #region Курсор

        private void CursorButton_Click(object sender, RoutedEventArgs e)
        {
            isCursor = !isCursor;
            isActiveCursor();
        }

        private void isActiveCursor()
        {
            var cursorBtn = FindName("cursor") as Button;
            if (cursorBtn == null) return;

            if (isCursor)
            {
                cursorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3939"));
            }
            else
            {
                cursorBtn.Background = Brushes.Transparent;
            }
        }

        #endregion
    }
}