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
        private RectangleSelectionManager rectangleSelectionManager; // Менеджер прямоугольного выделения
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

        #endregion

        #region Конструктор и инициализация

        public MainWindow()
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
            MainCanvas.MouseLeftButtonDown += MainCanvas_MouseLeftButtonDown;
            
            // Перехватываем события правой кнопки мыши на уровне Preview для предотвращения стандартного меню
            MainCanvas.PreviewMouseRightButtonDown += MainCanvas_PreviewMouseRightButtonDown;
            this.PreviewMouseRightButtonDown += MainWindow_PreviewMouseRightButtonDown;

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

            // Устанавливаем пустое контекстное меню на Canvas и Window, чтобы предотвратить показ стандартного меню
            MainCanvas.ContextMenu = new ContextMenu { IsEnabled = false };
            this.ContextMenu = new ContextMenu { IsEnabled = false };

            // Обработчики для предотвращения показа стандартного контекстного меню
            this.ContextMenuOpening += MainWindow_ContextMenuOpening;
            MainCanvas.ContextMenuOpening += MainCanvas_ContextMenuOpening;
        }

        private void InitializeManagers()
        {
            InitializeGridManager(); // Создает и инициализирует визуальную сетку
            InitializeDragManager(); // Делает фигуры в панели инструментов перетаскиваемыми
            InitializeArrowManager(); // Инициализация менеджера стрелок
            InitializeSelectionSystem(); // Настраивает систему выделения через SelectionManager
            InitializeRectangleSelectionManager(); // Инициализация менеджера прямоугольного выделения
            InitializeFillStrokeManager(); // Инициализация менеджера заливки и обводки
            InitializeTextManager(); // Инициализация менеджера текста
            InitializeFileManager(); // Инициализация менеджера файлов
            InitializeCommentManager(); // Инициализация менеджера комментариев
            InitializeClipboardManager(); // Инициализация менеджера буфера обмена и отмены/повтора
            InitializePoolManager(); // Инициализация менеджера пулов
            canvasPanningManager = new CanvasPanningManager(MainCanvas);
            canvasPanningManager.Mode = CanvasPanningManager.PanningMode.MiddleButton;
            
            // Устанавливаем ссылку на ArrowManager в FillStrokeManager после инициализации всех менеджеров
            if (arrowManager != null && fillStrokeManager != null)
            {
                fillStrokeManager.SetArrowManager(arrowManager);
            }
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
                GetSelectedElements = () => selectionManager?.SelectedElements.ToList() ?? new List<UIElement>(),
                HideResizeHandlesForElement = (element) =>
                {
                    if (element != null && resizableElements.ContainsKey(element))
                    {
                        resizableElements[element].HideResizeHandles();
                    }
                },
                ShowResizeHandlesForElement = (element) =>
                {
                    if (element == null) return;
                    
                    // Проверяем, не находится ли элемент в панели нотации
                    if (IsElementInNotation(element))
                    {
                        return;
                    }

                    // Создаем или используем существующий ResizableElement для элемента
                    if (!resizableElements.ContainsKey(element))
                    {
                        resizableElements[element] = new ResizableElement(
                            element,
                            MainCanvas,
                            gridManager.SnapToGrid,
                            gridManager.IsSnapToGridEnabled);
                    }

                    resizableElements[element].ShowResizeHandles();
                },
                // SetOriginalStyles = (copy, original) =>
                // {
                //     if (original is Ellipse originalEllipse && copy is Ellipse copyEllipse)
                //     {
                //         selectionManager?.SetOriginalStyles(copyEllipse, originalEllipse.Stroke, originalEllipse.Fill, originalEllipse.StrokeThickness);
                //     }
                //     else if (original is Border originalBorder && copy is Border copyBorder)
                //     {
                //         selectionManager?.SetOriginalStyles(copyBorder, originalBorder.BorderBrush, originalBorder.Background, originalBorder.BorderThickness.Left);
                //     }
                // },
                // SetOriginalStyles теперь обрабатывается в FillStrokeManager через SetFillStrokeManager
                SetOriginalStyles = null,
                AnimateShapeAppearance = AnimateShapeAppearance,
                IsShapeFromNotation = IsShapeFromNotation,
                GetShapeType = GetShapeType,
                OnElementMoved = (element, position) => poolManager?.HandleElementMove(element, position) // Обработка прикрепления к пулам
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
                poolManager?.UpdateSnapToGrid(isEnabled);
                rectangleSelectionManager?.UpdateSnapToGrid();
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

        private void InitializeClipboardManager()
        {
            clipboardManager = new ClipboardManager(
                MainCanvas,
                selectionManager,
                resizableElements,
                gridManager,
                MakeCanvasShapeDraggable);

            // Подписываемся на события для обновления интерфейса
            clipboardManager.SelectionChanged += UpdateSelectionInfo;
            
            // Подписываемся на события выделения
            if (selectionManager != null)
            {
                selectionManager.OnSelectionCleared += UpdateSelectionInfo;
            }
            
            UpdateSelectionInfo();
            
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

            if (poolManager != null && poolManager.HandleMouseClick(e.GetPosition(MainCanvas), e.OriginalSource))
            {
                e.Handled = true;
                return;
            }

            if (textManager != null && textManager.HandleMouseClick(e.GetPosition(MainCanvas), e.OriginalSource))
            {
                e.Handled = true;
                return;
            }

            // Обработка прямоугольного выделения (только в режиме курсора)
            if (isCursor && rectangleSelectionManager != null)
            {
                UIElement clickedElement = e.OriginalSource as UIElement;
                
                // Начинаем прямоугольное выделение только если клик на пустую область
                if (clickedElement == MainCanvas || clickedElement == null)
                {
                    if (rectangleSelectionManager.HandleMouseLeftButtonDown(e, clickedElement))
                    {
                        e.Handled = true;
                        return;
                    }
                }
                // Если клик на элемент, не начинаем прямоугольное выделение
                // (элемент будет обработан через другие обработчики)
            }

            // Если кликнули на пустую область канваса и не начали прямоугольное выделение
            // - снимаем выделение и скрываем ручки
            if (e.OriginalSource == MainCanvas && (rectangleSelectionManager == null || !rectangleSelectionManager.IsSelecting))
            {
                DeselectAll();
                HideAllResizeHandles();
                e.Handled = true;
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

            // Обработка прямоугольного выделения (только в режиме курсора)
            if (isCursor && rectangleSelectionManager != null && rectangleSelectionManager.IsSelecting)
            {
                rectangleSelectionManager.HandleMouseMove(e);
                e.Handled = true;
                return;
            }

            // Обработка выделения (старая система)
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

            // Завершение прямоугольного выделения (только в режиме курсора)
            if (isCursor && rectangleSelectionManager != null && rectangleSelectionManager.IsSelecting)
            {
                rectangleSelectionManager.HandleMouseLeftButtonUp(e);
                e.Handled = true;
                return;
            }

            // Завершение выделения (старая система)
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
                return;
            }

            // Если клик на пустой области Canvas, предотвращаем показ стандартного контекстного меню
            if (e.OriginalSource == MainCanvas)
            {
                e.Handled = true;
            }
        }

        private void MainCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Если клик на пустой области Canvas или элемент без контекстного меню, предотвращаем стандартное меню
            if (e.OriginalSource == MainCanvas)
            {
                e.Handled = true;
                return;
            }

            // Если элемент имеет контекстное меню, позволяем ему обработать событие
            FrameworkElement source = e.OriginalSource as FrameworkElement;
            if (source != null && source.ContextMenu == null)
            {
                // Проверяем родительские элементы на наличие контекстного меню
                DependencyObject parent = VisualTreeHelper.GetParent(source);
                bool hasContextMenu = false;
                while (parent != null)
                {
                    if (parent is FrameworkElement fe && fe.ContextMenu != null)
                    {
                        hasContextMenu = true;
                        break;
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }

                // Если ни у одного родителя нет контекстного меню, предотвращаем стандартное
                if (!hasContextMenu)
                {
                    e.Handled = true;
                }
            }
        }

        private void MainWindow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Предотвращаем стандартное контекстное меню на уровне Window
            FrameworkElement source = e.OriginalSource as FrameworkElement;
            
            // Если источник - Window или элемент без контекстного меню, предотвращаем
            if (source == this || (source != null && source.ContextMenu == null && 
                !(source is MenuItem) && !(source is ContextMenu)))
            {
                // Проверяем, не является ли источник частью контекстного меню
                DependencyObject parent = VisualTreeHelper.GetParent(source);
                bool isPartOfContextMenu = false;
                while (parent != null)
                {
                    if (parent is ContextMenu || parent is MenuItem)
                    {
                        isPartOfContextMenu = true;
                        break;
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }

                if (!isPartOfContextMenu)
                {
                    e.Handled = true;
                }
            }
        }

        private void MainWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Предотвращаем показ стандартного контекстного меню на уровне Window
            FrameworkElement source = e.Source as FrameworkElement;
            
            // Если источник - Window, отменяем
            if (source == this)
            {
                e.Handled = true;
                return;
            }
        }

        private void MainCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Предотвращаем показ стандартного контекстного меню на Canvas
            FrameworkElement source = e.Source as FrameworkElement;
            
            // Если источник - Canvas или элемент без кастомного контекстного меню, отменяем
            if (source == MainCanvas || (source != null && source.ContextMenu == null))
            {
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
            if (e.Key == Key.Delete)
            {
                // Сначала проверяем, выделена ли стрелка
                if (arrowManager?.SelectedArrowContainer != null)
                {
                    arrowManager.DeleteArrow(arrowManager.SelectedArrowContainer.Line);
                    e.Handled = true;
                }
                // Затем проверяем обычные элементы
                else if (selectionManager?.SelectedElement != null)
                {
                    clipboardManager?.DeleteSelectedElement();
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Меню "Файл"

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            // Очищаем холст
            MainCanvas.Children.Clear();
            canvasElements.Clear();
            currentFilePath = null;
            commentManager?.ClearAllComments();
            poolManager?.ClearAllPools();

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

            DimensionsPanel.Visibility = Visibility.Collapsed;
            FillPanel.Visibility = Visibility.Collapsed;
            StrokePanel.Visibility = Visibility.Collapsed;
            TextPanel.Visibility = Visibility.Collapsed;
            
            // Инициализируем панель информации о выделении
            var selectionInfoPanel = FindName("SelectionInfoPanel") as Border;
            if (selectionInfoPanel != null)
            {
                selectionInfoPanel.Visibility = Visibility.Collapsed;
            }
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
                StrokeWidthPanel.Visibility = Visibility.Collapsed;
            }
        }

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

        #endregion

        #region Управление элементами

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

        private void UpdateSelectionInfo()
        {
            if (selectionManager == null) return;
            
            var selectedCount = selectionManager.SelectedElements.Count;
            var selectionInfoPanel = FindName("SelectionInfoPanel") as Border;
            var selectedCountTextBlock = FindName("SelectedCountTextBlock") as TextBlock;
            
            if (selectionInfoPanel != null && selectedCountTextBlock != null)
            {
                if (selectedCount > 1)
                {
                    selectionInfoPanel.Visibility = Visibility.Visible;
                    selectedCountTextBlock.Text = $"Выделено элементов: {selectedCount}";
                }
                else
                {
                    selectionInfoPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

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

        #endregion

        #region Управление Z-порядком

        private void BringToFront(FrameworkElement element)
        {
            if (element == null || !MainCanvas.Children.Contains(element)) return;

            int maxZIndex = GetMaxZIndex();
            Canvas.SetZIndex(element, maxZIndex + 1);

            // Также перемещаем элемент в конец коллекции для гарантии
            MainCanvas.Children.Remove(element);
            MainCanvas.Children.Add(element);
        }

        private void SendToBack(FrameworkElement element)
        {
            if (element == null || !MainCanvas.Children.Contains(element)) return;

            int minZIndex = GetMinZIndex();
            Canvas.SetZIndex(element, minZIndex - 1);

            // Также перемещаем элемент в начало коллекции для гарантии
            MainCanvas.Children.Remove(element);
            MainCanvas.Children.Insert(0, element);
        }

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

        #endregion

        #region Контекстное меню

        private void CreateContextMenu(FrameworkElement element)
        {
            ContextMenu contextMenu = new ContextMenu();
            
            // Флаги для отслеживания действий, которые нужно выполнить после закрытия меню
            bool shouldCut = false;
            bool shouldDelete = false;

            // Обработчик открытия меню
            contextMenu.Opened += (s, e) =>
            {
                // Убеждаемся, что меню использует наш стиль
                e.Handled = false;
            };

            // Обработчик закрытия меню - выполняем действия сразу после закрытия
            contextMenu.Closed += (s, e) =>
            {
                // Убеждаемся, что меню полностью закрыто
                contextMenu.IsOpen = false;
                
                // Выполняем отложенные действия сразу после закрытия (минимальная задержка)
                if (shouldCut)
                {
                    shouldCut = false;
                    // Используем минимальную задержку для гарантии закрытия меню
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        clipboardManager?.CutSelectedElement();
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
                else if (shouldDelete)
                {
                    shouldDelete = false;
                    // Используем минимальную задержку для гарантии закрытия меню
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        clipboardManager?.DeleteSelectedElement();
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            };

            // Пункт "Вырезать"
            MenuItem cutMenuItem = new MenuItem
            {
                Header = "Вырезать",
                InputGestureText = "Ctrl+X"
            };
            cutMenuItem.Click += (s, e) =>
            {
                e.Handled = true;
                // Устанавливаем флаг для выполнения после закрытия меню
                shouldCut = true;
                // Закрываем меню - действие выполнится в обработчике Closed
                contextMenu.IsOpen = false;
            };
            contextMenu.Items.Add(cutMenuItem);

            // Пункт "Копировать"
            MenuItem copyMenuItem = new MenuItem
            {
                Header = "Копировать",
                InputGestureText = "Ctrl+C"
            };
            copyMenuItem.Click += (s, e) =>
            {
                e.Handled = true;
                clipboardManager?.CopySelectedElement();
            };
            contextMenu.Items.Add(copyMenuItem);

            // Пункт "Вставить"
            MenuItem pasteMenuItem = new MenuItem
            {
                Header = "Вставить",
                InputGestureText = "Ctrl+V"
            };
            pasteMenuItem.Click += (s, e) =>
            {
                e.Handled = true;
                clipboardManager?.PasteElement();
            };
            contextMenu.Items.Add(pasteMenuItem);

            contextMenu.Items.Add(new Separator());

            // Пункт "Удалить"
            MenuItem deleteMenuItem = new MenuItem
            {
                Header = "Удалить",
                InputGestureText = "Delete"
            };
            deleteMenuItem.Click += (s, e) =>
            {
                e.Handled = true;
                // Устанавливаем флаг для выполнения после закрытия меню
                shouldDelete = true;
                // Закрываем меню - действие выполнится в обработчике Closed
                contextMenu.IsOpen = false;
            };
            contextMenu.Items.Add(deleteMenuItem);

            contextMenu.Items.Add(new Separator());

            // Пункт "Перенести на передний план"
            MenuItem bringToFrontMenuItem = new MenuItem
            {
                Header = "Перенести на передний план"
            };
            bringToFrontMenuItem.Click += (s, e) =>
            {
                e.Handled = true;
                BringToFront(element);
            };
            contextMenu.Items.Add(bringToFrontMenuItem);

            // Пункт "Перенести на задний план"
            MenuItem sendToBackMenuItem = new MenuItem
            {
                Header = "Перенести на задний план"
            };
            sendToBackMenuItem.Click += (s, e) =>
            {
                e.Handled = true;
                SendToBack(element);
            };
            contextMenu.Items.Add(sendToBackMenuItem);

            // Устанавливаем контекстное меню для элемента
            element.ContextMenu = contextMenu;
        }

        #endregion

        #region Вспомогательные методы

        private bool IsElementInNotation(UIElement element)
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
            arrowManager = new ArrowManager(
                MainCanvas,
                pencil,
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
                UpdateArrowPropertiesInPanels,
                () => fillStrokeManager?.GetCurrentStrokeThickness() ?? 3 // Функция для получения текущей толщины
            );
            
            // Подписываемся на события стрелки для скрытия панели толщины
            arrowManager.OnArrowDeleted += () =>
            {
                if (StrokeWidthPanel != null)
                {
                    StrokeWidthPanel.Visibility = Visibility.Collapsed;
                }
            };
            
            arrowManager.OnArrowDeselected += () =>
            {
                // Скрываем панель толщины при снятии выделения со стрелки
                if (StrokeWidthPanel != null)
                {
                    StrokeWidthPanel.Visibility = Visibility.Collapsed;
                }
            };
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
                if (StrokeWidthPanel != null)
                {
                    StrokeWidthPanel.Visibility = Visibility.Visible;
                }

                // Обновляем комбо-бокс толщины, используя оригинальную толщину
                // (текущая может быть увеличена при выделении)
                if (StrokeWidthComboBox != null)
                {
                    double thickness = arrowContainer.OriginalThickness > 0 
                        ? arrowContainer.OriginalThickness 
                        : arrowContainer.Line.StrokeThickness;
                    fillStrokeManager?.UpdateStrokeWidthComboBoxSelection(thickness);
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

        #endregion

        #region Система выделения

        private void InitializeSelectionSystem()
        {
            // Создаем менеджер выделения
            selectionManager = new SelectionManager(MainCanvas, this);

            // Настраиваем делегат для снятия выделения стрелки
            selectionManager.OnDeselectArrow = () => arrowManager.DeselectArrow();

            // Подписываемся на события выделения для обновления панелей свойств
            selectionManager.OnElementSelected += (element) =>
            {
                UpdateSelectionInfo();
                
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
                UpdateSelectionInfo();
                
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

        private void InitializeRectangleSelectionManager()
        {
            rectangleSelectionManager = new RectangleSelectionManager(
                MainCanvas,
                selectionManager,
                resizableElements,
                gridManager);
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
            
            // Устанавливаем FillStrokeManager в DragManager для сохранения оригинальных стилей
            if (dragManager != null)
            {
                dragManager.SetFillStrokeManager(fillStrokeManager);
            }
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
            FillColorComboBox.SelectedIndex = -1;
            StrokeColorComboBox.SelectedIndex = -1;
            FillColorPreview.Background = Brushes.Transparent;
            StrokeColorPreview.Background = Brushes.Transparent;
        }

        #endregion

        #region Текст

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
                (element) => clipboardManager?.SaveToUndoStack(element, UndoActionType.Add)
            );

            // Подписываемся на события для выделения кнопки
            textManager.OnTextModeActivated += () =>
            {
                if (text != null)
                {
                    text.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3939"));
                }
            };

            textManager.OnTextModeDeactivated += () =>
            {
                if (text != null)
                {
                    text.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF343434"));
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

        private void FileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateWindowTitle();
        }

        #endregion

        #region Пулы (Swimlanes)

        private void InitializePoolManager()
        {
            poolManager = new PoolManager(
                MainCanvas,
                rectangle, // Кнопка rectangle (3-я кнопка) на нижней панели
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
            if (isCursor)
            {
                cursor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3939"));
            }
            else
            {
                cursor.Background = Brushes.Transparent;
            }
        }

        #endregion
    }
}