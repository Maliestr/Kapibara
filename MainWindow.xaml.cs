using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private UIElement draggedElement;
        private Point offset;
        private UIElement shapeCopy;

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

                e.Handled = true;
            }
        }

        private void Shape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && shapeCopy != null && e.LeftButton == MouseButtonState.Pressed)
            {
                // Перемещаем копию фигуры за курсором
                Point currentPosition = e.GetPosition(MainCanvas);

                double newLeft = currentPosition.X - (GetElementWidth(shapeCopy) / 2);
                double newTop = currentPosition.Y - (GetElementHeight(shapeCopy) / 2);

                Canvas.SetLeft(shapeCopy, newLeft);
                Canvas.SetTop(shapeCopy, newTop);
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
                    // Если копия была создана, делаем ее полностью видимой и добавляем функционал перетаскивания
                    shapeCopy.Opacity = 1.0;

                    // ВОЗВРАЩАЕМ НОРМАЛЬНЫЙ ZIndex ДЛЯ ФИГУРЫ НА ХОЛСТЕ
                    Canvas.SetZIndex(shapeCopy, 1);

                    MakeCanvasShapeDraggable(shapeCopy);

                    // Добавляем эффект появления
                    AnimateShapeAppearance(shapeCopy);
                }

                draggedElement = null;
                shapeCopy = null;
                e.Handled = true;
            }
        }

        private UIElement CreateShapeCopy(UIElement original)
        {
            // Создаем копию фигуры в зависимости от типа оригинала
            if (original == Circle)
            {
                return new Ellipse
                {
                    Width = Circle.Width,
                    Height = Circle.Height,
                    Fill = (Circle as Ellipse).Fill,
                    Stroke = (Circle as Ellipse).Stroke,
                    StrokeThickness = (Circle as Ellipse).StrokeThickness,
                    Cursor = Cursors.Hand
                };
            }
            else if (original == SimpleRing)
            {
                return new Ellipse
                {
                    Width = SimpleRing.Width,
                    Height = SimpleRing.Height,
                    Stroke = (SimpleRing as Ellipse).Stroke,
                    StrokeThickness = (SimpleRing as Ellipse).StrokeThickness,
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
            }
            else if (original == Square)
            {
                var originalBorder = Square as Border;
                return new Border
                {
                    Width = originalBorder.Width,
                    Height = originalBorder.Height,
                    Background = originalBorder.Background,
                    BorderBrush = originalBorder.BorderBrush,
                    BorderThickness = originalBorder.BorderThickness,
                    CornerRadius = originalBorder.CornerRadius,
                    Cursor = Cursors.Hand
                };
            }
            else if (original == ResizableSquare)
            {
                var originalDiamond = ResizableSquare as Border;
                var diamond = new Border
                {
                    Width = originalDiamond.Width,
                    Height = originalDiamond.Height,
                    Background = originalDiamond.Background,
                    BorderBrush = originalDiamond.BorderBrush,
                    BorderThickness = originalDiamond.BorderThickness,
                    CornerRadius = originalDiamond.CornerRadius,
                    Cursor = Cursors.Hand,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                // Применяем ТОЛЬКО поворот на 45 градусов как у оригинала
                diamond.RenderTransform = new RotateTransform(45);
                return diamond;
            }
            else if (original == Line)
            {
                var originalLine = Line as Line;
                var line = new Line
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
                // Копируем точную трансформацию
                if (originalLine.RenderTransform is RotateTransform originalRotate)
                {
                    line.RenderTransform = new RotateTransform(originalRotate.Angle);
                }
                return line;
            }

            return null;
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

        private void CanvasShape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = draggedElement;
                Point currentPosition = e.GetPosition(MainCanvas);

                // Перемещаем элемент так, чтобы центр был под курсором
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

                // ФИКСИРУЕМ ТРАНСФОРМАЦИЮ ДЛЯ РОМБА
                if (element is Border border && border.RenderTransform is RotateTransform rotateTransform)
                {
                    // Убеждаемся что ромб сохраняет поворот на 45 градусов
                    rotateTransform.Angle = 45;
                }

                // ВОЗВРАЩАЕМ НОРМАЛЬНЫЙ ZIndex ПОСЛЕ ПЕРЕТАСКИВАНИЯ
                Canvas.SetZIndex(element, 1);

                // Возвращаем исходный вид
                ApplyCanvasDragVisualFeedback(element, false);
                e.Handled = true;
            }
        }

        private void ApplyCanvasDragVisualFeedback(UIElement element, bool isDragging)
        {
            if (isDragging)
            {
                // Подсвечиваем при перетаскивании
                if (element is Ellipse ellipse)
                {
                    ellipse.Stroke = Brushes.Red;
                    ellipse.StrokeThickness += 2;
                }
                else if (element is Border border)
                {
                    border.BorderBrush = Brushes.Red;
                    border.BorderThickness = new Thickness(border.BorderThickness.Left + 2);
                }
                else if (element is Line line)
                {
                    line.Stroke = Brushes.Red;
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
                    ellipse.StrokeThickness = ellipse == SimpleRing ? 5 : 3;
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
    }
}