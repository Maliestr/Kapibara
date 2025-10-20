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
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private UIElement draggedElement;
        private Point offset;
        private string currentShapeType;

        public MainWindow()
        {
            InitializeComponent();
            SetupDragAndDrop();
        }
        private void SetupDragAndDrop()
        {
            // Добавляем обработчики для всех фигур в палитре
            AddPaletteHandlers(CirclePalette);
            AddPaletteHandlers(SquarePalette);
            AddPaletteHandlers(DiamondPalette);
            AddPaletteHandlers(LinePalette);
            AddPaletteHandlers(RingPalette);
            AddPaletteHandlers(TrianglePalette);

            // Обработчики для холста
            MainCanvas.MouseMove += MainCanvas_MouseMove;
            MainCanvas.MouseLeftButtonUp += MainCanvas_MouseLeftButtonUp;
            MainCanvas.Drop += MainCanvas_Drop;
        }
        private void AddPaletteHandlers(UIElement element)
        {
            element.MouseLeftButtonDown += PaletteShape_MouseLeftButtonDown;
            element.MouseMove += PaletteShape_MouseMove;
            element.MouseLeftButtonUp += PaletteShape_MouseLeftButtonUp;
        }

        private void PaletteShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border != null)
            {
                isDragging = true;
                draggedElement = border;
                currentShapeType = border.Tag as string;

                // Визуальный фидбэк - уменьшаем прозрачность
                border.Opacity = 0.7;

                e.Handled = true;
            }
        }

        private void PaletteShape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                // Меняем курсор на указывающий, что идет перетаскивание
                Mouse.SetCursor(Cursors.Hand);
            }
        }

        private void PaletteShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                var border = sender as Border;
                if (border != null)
                {
                    border.Opacity = 1.0; // Возвращаем прозрачность
                }
                isDragging = false;
                draggedElement = null;
                e.Handled = true;
            }
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                // Можно добавить визуальный фидбэк при перемещении над холстом
            }
        }

        private void MainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                CreateShapeOnCanvas(e.GetPosition(MainCanvas));
                isDragging = false;
                draggedElement = null;
                e.Handled = true;
            }
        }

        private void MainCanvas_Drop(object sender, DragEventArgs e)
        {
            if (isDragging)
            {
                CreateShapeOnCanvas(e.GetPosition(MainCanvas));
                isDragging = false;
                draggedElement = null;
            }
        }

        private void CreateShapeOnCanvas(Point position)
        {
            UIElement newShape = null;

            switch (currentShapeType)
            {
                case "Circle":
                    newShape = CreateCircle(position);
                    break;
                case "Square":
                    newShape = CreateSquare(position);
                    break;
                case "Diamond":
                    newShape = CreateDiamond(position);
                    break;
                case "Line":
                    newShape = CreateLine(position);
                    break;
                case "Ring":
                    newShape = CreateRing(position);
                    break;
                case "Triangle":
                    newShape = CreateTriangle(position);
                    break;
            }

            if (newShape != null)
            {
                MainCanvas.Children.Add(newShape);
                MakeShapeDraggable(newShape);

                // Анимация появления
                AnimateShapeAppearance(newShape);
            }
        }

        private Ellipse CreateCircle(Point position)
        {
            var circle = new Ellipse
            {
                Width = 80,
                Height = 80,
                Fill = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Stroke = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
                StrokeThickness = 2,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(circle, position.X - 40);
            Canvas.SetTop(circle, position.Y - 40);

            return circle;
        }

        private Border CreateSquare(Point position)
        {
            var square = new Border
            {
                Width = 80,
                Height = 80,
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 142, 60)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(0),
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(square, position.X - 40);
            Canvas.SetTop(square, position.Y - 40);

            return square;
        }

        private Polygon CreateDiamond(Point position)
        {
            var diamond = new Polygon
            {
                Points = new PointCollection { new Point(0, 40), new Point(40, 0), new Point(80, 40), new Point(40, 80) },
                Fill = new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                Stroke = new SolidColorBrush(Color.FromRgb(123, 31, 162)),
                StrokeThickness = 2,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(diamond, position.X - 40);
            Canvas.SetTop(diamond, position.Y - 40);

            return diamond;
        }

        private Line CreateLine(Point position)
        {
            var line = new Line
            {
                X1 = position.X - 30,
                Y1 = position.Y,
                X2 = position.X + 30,
                Y2 = position.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Cursor = Cursors.Hand
            };

            return line;
        }

        private Ellipse CreateRing(Point position)
        {
            var ring = new Ellipse
            {
                Width = 80,
                Height = 80,
                Stroke = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                StrokeThickness = 8,
                Fill = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(ring, position.X - 40);
            Canvas.SetTop(ring, position.Y - 40);

            return ring;
        }

        private Polygon CreateTriangle(Point position)
        {
            var triangle = new Polygon
            {
                Points = new PointCollection { new Point(40, 10), new Point(70, 70), new Point(10, 70) },
                Fill = new SolidColorBrush(Color.FromRgb(0, 150, 136)),
                Stroke = new SolidColorBrush(Color.FromRgb(0, 121, 107)),
                StrokeThickness = 2,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(triangle, position.X - 40);
            Canvas.SetTop(triangle, position.Y - 40);

            return triangle;
        }

        private void MakeShapeDraggable(UIElement shape)
        {
            shape.MouseLeftButtonDown += Shape_MouseLeftButtonDown;
            shape.MouseMove += Shape_MouseMove;
            shape.MouseLeftButtonUp += Shape_MouseLeftButtonUp;
        }

        private void Shape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var shape = sender as UIElement;
            if (shape != null)
            {
                isDragging = true;
                draggedElement = shape;
                shape.CaptureMouse();
                offset = e.GetPosition(shape);

                // Визуальный фидбэк
                if (shape is Shape visualShape)
                {
                    visualShape.Stroke = Brushes.Red;
                    visualShape.StrokeThickness += 1;
                }

                e.Handled = true;
            }
        }

        private void Shape_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var shape = draggedElement;
                Point currentPosition = e.GetPosition(MainCanvas);

                double newLeft = currentPosition.X - offset.X;
                double newTop = currentPosition.Y - offset.Y;

                Canvas.SetLeft(shape, newLeft);
                Canvas.SetTop(shape, newTop);
            }
        }

        private void Shape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var shape = sender as UIElement;
            if (shape != null)
            {
                isDragging = false;
                shape.ReleaseMouseCapture();
                draggedElement = null;

                // Возвращаем исходный вид
                if (shape is Shape visualShape)
                {
                    // Возвращаем оригинальные цвета в зависимости от типа фигуры
                    if (shape is Ellipse ellipse && ellipse.Fill != Brushes.Transparent) // Круг
                        visualShape.Stroke = new SolidColorBrush(Color.FromRgb(25, 118, 210));
                    else if (shape is Border) // Квадрат
                        visualShape.Stroke = new SolidColorBrush(Color.FromRgb(56, 142, 60));
                    else if (shape is Polygon polygon) // Ромб или треугольник
                        visualShape.Stroke = polygon.Points.Count == 4 ?
                            new SolidColorBrush(Color.FromRgb(123, 31, 162)) :
                            new SolidColorBrush(Color.FromRgb(0, 121, 107));
                    else if (shape is Line) // Линия
                        visualShape.Stroke = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    else if (shape is Ellipse ring && ring.Fill == Brushes.Transparent) // Кольцо
                        visualShape.Stroke = new SolidColorBrush(Color.FromRgb(255, 152, 0));

                    visualShape.StrokeThickness -= 1;
                }

                e.Handled = true;
            }
        }

        private void AnimateShapeAppearance(UIElement shape)
        {
            var scaleAnimation = new DoubleAnimation
            {
                From = 0.3,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 2 }
            };

            var transform = new ScaleTransform();
            shape.RenderTransform = transform;
            shape.RenderTransformOrigin = new Point(0.5, 0.5);

            transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            MainCanvas.Children.Clear();
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // закрытие окна
        }

        private void HeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
