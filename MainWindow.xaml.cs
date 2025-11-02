using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private UIElement draggedElement;
        private Point offset;
        private Transform originalTransform;
        private Point dragStartPoint;

        private bool isTextModeActive = false; 
        private TextBox textBox;

        private Dictionary<UIElement, Brush> originalStrokeColors = new Dictionary<UIElement, Brush>();

        public MainWindow()
        {
            InitializeComponent();
            MakeAllShapesDraggable();
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
            // Если цвет не указан, используем белый по умолчанию
            Brush finalColor = color ?? Brushes.White;

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = finalColor,
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
            originalStrokeColors[textBlock] = finalColor;

            // Добавляем текстовый элемент на холст
            MainCanvas.Children.Add(textBlock);

            textBlock.MouseLeftButtonDown += TextElement_MouseLeftButtonDown;
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
                    Background = Brushes.White, // Белый фон для видимости при редактировании
                    Foreground = Brushes.Black, // Черный текст для контраста
                    BorderBrush = Brushes.Gray,
                    FontSize = 14,
                    Text = textBlock.Text,
                    CaretBrush = Brushes.Black, // Черный курсор для видимости
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