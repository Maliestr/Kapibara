using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
//тестыамамамамамам
namespace GraphicEditor_Tests
{
    [TestClass]
    public class ShapeDragTests
    {
        private Canvas _mainCanvas;

        [TestInitialize]
        public void Initialize()
        {
            _mainCanvas = new Canvas
            {
                Width = 800,
                Height = 600
            };
            // Принудительно измеряем Canvas для корректной работы ActualWidth/ActualHeight
            _mainCanvas.Measure(new Size(800, 600));
            _mainCanvas.Arrange(new Rect(0, 0, 800, 600));
        }

        [TestMethod]
        public void Test_Move_Ellipse_Success()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White
            };

            Canvas.SetLeft(ellipse, 100);
            Canvas.SetTop(ellipse, 100);
            _mainCanvas.Children.Add(ellipse);

            // Act
            bool result = SimpleMoveShape(ellipse, 200, 200);

            // Assert
            Assert.IsTrue(result, "Фигура должна перемещаться");
            Assert.AreEqual(200, Canvas.GetLeft(ellipse), 0.01);
            Assert.AreEqual(200, Canvas.GetTop(ellipse), 0.01);
        }

        [TestMethod]
        public void Test_Move_Ellipse_Outside_Left()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White
            };

            Canvas.SetLeft(ellipse, 100);
            Canvas.SetTop(ellipse, 100);
            _mainCanvas.Children.Add(ellipse);

            // Act
            bool result = SimpleMoveShape(ellipse, -10, 100);

            // Assert
            Assert.IsTrue(result, "Фигура должна быть ограничена");
            Assert.AreEqual(0, Canvas.GetLeft(ellipse), 0.01, "Должна быть прижата к левой границе");
        }

        [TestMethod]
        public void Test_Move_Element_Not_On_Canvas()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White
            };

            // Не добавляем на Canvas!

            // Act
            bool result = SimpleMoveShape(ellipse, 100, 100);

            // Assert
            Assert.IsFalse(result, "Элемент не на Canvas не должен перемещаться");
        }

        [TestMethod]
        public void Test_Move_Multiple_Shapes()
        {
            // Arrange
            var results = new bool[3];

            for (int i = 0; i < 3; i++)
            {
                var ellipse = new Ellipse
                {
                    Width = 40,
                    Height = 40,
                    Fill = Brushes.White
                };

                Canvas.SetLeft(ellipse, 50 + i * 100);
                Canvas.SetTop(ellipse, 50 + i * 100);
                _mainCanvas.Children.Add(ellipse);

                // Act для каждой фигуры
                results[i] = SimpleMoveShape(ellipse, 200 + i * 50, 200 + i * 50);
            }

            // Assert
            for (int i = 0; i < results.Length; i++)
            {
                Assert.IsTrue(results[i], $"Фигура {i} должна перемещаться");
            }
        }

        // Простой метод перемещения без зависимости от MainWindow
        private bool SimpleMoveShape(UIElement element, double x, double y)
        {
            try
            {
                // Проверяем, что элемент на Canvas
                if (element == null || !_mainCanvas.Children.Contains(element))
                    return false;

                double elementWidth = GetElementWidth(element);
                double elementHeight = GetElementHeight(element);

                // Границы как в реальном коде
                const double WorkAreaTop = 40;
                const double WorkAreaBottom = 100;

                double maxLeft = _mainCanvas.ActualWidth - elementWidth;
                double maxTop = _mainCanvas.ActualHeight - elementHeight - WorkAreaBottom;

                // Ограничиваем координаты
                double newLeft = Math.Max(0, Math.Min(x, maxLeft));
                double newTop = Math.Max(WorkAreaTop, Math.Min(y, maxTop));

                Canvas.SetLeft(element, newLeft);
                Canvas.SetTop(element, newTop);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private double GetElementWidth(UIElement element)
        {
            if (element is FrameworkElement fe)
            {
                // Принудительно измеряем элемент, если он еще не был измерен
                if (fe.Parent != null)
                {
                    fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    fe.Arrange(new Rect(0, 0, fe.DesiredSize.Width, fe.DesiredSize.Height));
                }
                return fe.ActualWidth > 0 ? fe.ActualWidth : (fe.Width > 0 ? fe.Width : fe.DesiredSize.Width);
            }
            return 0;
        }

        private double GetElementHeight(UIElement element)
        {
            if (element is FrameworkElement fe)
            {
                // Принудительно измеряем элемент, если он еще не был измерен
                if (fe.Parent != null)
                {
                    fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    fe.Arrange(new Rect(0, 0, fe.DesiredSize.Width, fe.DesiredSize.Height));
                }
                return fe.ActualHeight > 0 ? fe.ActualHeight : (fe.Height > 0 ? fe.Height : fe.DesiredSize.Height);
            }
            return 0;
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mainCanvas.Children.Clear();
        }
    }
}