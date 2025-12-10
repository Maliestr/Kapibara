using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace GraphicEditor_Tests
{
    [TestClass]
    public class ComprehensiveTests
    {
        private Canvas _mainCanvas;
        private Dictionary<UIElement, Brush> _originalFillColors;
        private Dictionary<UIElement, Brush> _originalStrokeColors;
        private Dictionary<UIElement, double> _originalStrokeThickness;

        [TestInitialize]
        public void Initialize()
        {
            _mainCanvas = new Canvas
            {
                Width = 800,
                Height = 600
            };
            _mainCanvas.Measure(new Size(800, 600));
            _mainCanvas.Arrange(new Rect(0, 0, 800, 600));

            _originalFillColors = new Dictionary<UIElement, Brush>();
            _originalStrokeColors = new Dictionary<UIElement, Brush>();
            _originalStrokeThickness = new Dictionary<UIElement, double>();
        }

        #region Тест 1: Применение цвета заливки (ApplyFillColorToShape)

        [TestMethod]
        public void Test_ApplyFillColor_Black()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyFillColorToShape(ellipse, Brushes.Black);

            // Assert
            Assert.AreEqual(Brushes.Black, ellipse.Fill, "Заливка должна быть черной");
            Console.WriteLine("✓ Тест 1.1: Применение черной заливки - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyFillColor_Red()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyFillColorToShape(ellipse, Brushes.Red);

            // Assert
            Assert.AreEqual(Brushes.Red, ellipse.Fill, "Заливка должна быть красной");
            Console.WriteLine("✓ Тест 1.2: Применение красной заливки - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyFillColor_Blue()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyFillColorToShape(ellipse, Brushes.Blue);

            // Assert
            Assert.AreEqual(Brushes.Blue, ellipse.Fill, "Заливка должна быть синей");
            Console.WriteLine("✓ Тест 1.3: Применение синей заливки - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyFillColor_Green()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyFillColorToShape(ellipse, Brushes.Green);

            // Assert
            Assert.AreEqual(Brushes.Green, ellipse.Fill, "Заливка должна быть зеленой");
            Console.WriteLine("✓ Тест 1.4: Применение зеленой заливки - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyFillColor_White()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.Black,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyFillColorToShape(ellipse, Brushes.White);

            // Assert
            Assert.AreEqual(Brushes.White, ellipse.Fill, "Заливка должна быть белой");
            Console.WriteLine("✓ Тест 1.5: Применение белой заливки - ПРОЙДЕН");
        }

        #endregion

        #region Тест 2: Применение цвета обводки (ApplyStrokeColorToShape)

        [TestMethod]
        public void Test_ApplyStrokeColor_Black()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.White
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeColorToShape(ellipse, Brushes.Black);

            // Assert
            Assert.AreEqual(Brushes.Black, ellipse.Stroke, "Обводка должна быть черной");
            Console.WriteLine("✓ Тест 2.1: Применение черной обводки - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyStrokeColor_Red()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeColorToShape(ellipse, Brushes.Red);

            // Assert
            Assert.AreEqual(Brushes.Red, ellipse.Stroke, "Обводка должна быть красной");
            Console.WriteLine("✓ Тест 2.2: Применение красной обводки - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyStrokeColor_Blue()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeColorToShape(ellipse, Brushes.Blue);

            // Assert
            Assert.AreEqual(Brushes.Blue, ellipse.Stroke, "Обводка должна быть синей");
            Console.WriteLine("✓ Тест 2.3: Применение синей обводки - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyStrokeColor_Green()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeColorToShape(ellipse, Brushes.Green);

            // Assert
            Assert.AreEqual(Brushes.Green, ellipse.Stroke, "Обводка должна быть зеленой");
            Console.WriteLine("✓ Тест 2.4: Применение зеленой обводки - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyStrokeColor_White()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeColorToShape(ellipse, Brushes.White);

            // Assert
            Assert.AreEqual(Brushes.White, ellipse.Stroke, "Обводка должна быть белой");
            Console.WriteLine("✓ Тест 2.5: Применение белой обводки - ПРОЙДЕН");
        }

        #endregion

        #region Тест 3: Перемещение элементов (MoveElement)

        [TestMethod]
        public void Test_MoveElement_PositiveCoordinates()
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
            bool result = MoveElement(ellipse, 200, 250);

            // Assert
            Assert.IsTrue(result, "Перемещение должно быть успешным");
            Assert.AreEqual(200, Canvas.GetLeft(ellipse), 0.01, "X координата должна быть 200");
            Assert.AreEqual(250, Canvas.GetTop(ellipse), 0.01, "Y координата должна быть 250");
            Console.WriteLine("✓ Тест 3.1: Перемещение с положительными координатами - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_MoveElement_NegativeCoordinates()
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
            bool result = MoveElement(ellipse, -50, -30);

            // Assert
            Assert.IsTrue(result, "Перемещение должно быть успешным");
            // Координаты должны быть ограничены границами Canvas
            Assert.IsTrue(Canvas.GetLeft(ellipse) >= 0, "X координата не должна быть отрицательной");
            Assert.IsTrue(Canvas.GetTop(ellipse) >= 40, "Y координата должна быть не меньше 40 (WorkAreaTop)");
            Console.WriteLine("✓ Тест 3.2: Перемещение с отрицательными координатами - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_MoveElement_BoundaryValues()
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

            // Act - перемещение к границам
            bool result = MoveElement(ellipse, 0, 40);

            // Assert
            Assert.IsTrue(result, "Перемещение должно быть успешным");
            Assert.AreEqual(0, Canvas.GetLeft(ellipse), 0.01, "X координата должна быть 0 (левая граница)");
            Assert.AreEqual(40, Canvas.GetTop(ellipse), 0.01, "Y координата должна быть 40 (верхняя граница)");
            Console.WriteLine("✓ Тест 3.3: Перемещение к граничным значениям - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_MoveElement_LargeValues()
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

            // Act - перемещение за границы Canvas
            bool result = MoveElement(ellipse, 1000, 1000);

            // Assert
            Assert.IsTrue(result, "Перемещение должно быть успешным");
            // Координаты должны быть ограничены максимальными значениями
            double maxLeft = _mainCanvas.ActualWidth - 50;
            double maxTop = _mainCanvas.ActualHeight - 50 - 100; // WorkAreaBottom = 100
            Assert.IsTrue(Canvas.GetLeft(ellipse) <= maxLeft, "X координата не должна превышать максимум");
            Assert.IsTrue(Canvas.GetTop(ellipse) <= maxTop, "Y координата не должна превышать максимум");
            Console.WriteLine("✓ Тест 3.4: Перемещение с большими значениями - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_MoveElement_ZeroCoordinates()
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
            bool result = MoveElement(ellipse, 0, 40);

            // Assert
            Assert.IsTrue(result, "Перемещение должно быть успешным");
            Assert.AreEqual(0, Canvas.GetLeft(ellipse), 0.01, "X координата должна быть 0");
            Assert.AreEqual(40, Canvas.GetTop(ellipse), 0.01, "Y координата должна быть 40");
            Console.WriteLine("✓ Тест 3.5: Перемещение к нулевым координатам - ПРОЙДЕН");
        }

        #endregion

        #region Тест 4: Изменение толщины обводки (ApplyStrokeThicknessToShape)

        [TestMethod]
        public void Test_ApplyStrokeThickness_1px()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeThicknessToShape(ellipse, 1);

            // Assert
            Assert.AreEqual(1, ellipse.StrokeThickness, 0.01, "Толщина обводки должна быть 1px");
            Console.WriteLine("✓ Тест 4.1: Применение толщины обводки 1px - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyStrokeThickness_2px()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeThicknessToShape(ellipse, 2);

            // Assert
            Assert.AreEqual(2, ellipse.StrokeThickness, 0.01, "Толщина обводки должна быть 2px");
            Console.WriteLine("✓ Тест 4.2: Применение толщины обводки 2px - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyStrokeThickness_3px()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeThicknessToShape(ellipse, 3);

            // Assert
            Assert.AreEqual(3, ellipse.StrokeThickness, 0.01, "Толщина обводки должна быть 3px");
            Console.WriteLine("✓ Тест 4.3: Применение толщины обводки 3px - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyStrokeThickness_5px()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeThicknessToShape(ellipse, 5);

            // Assert
            Assert.AreEqual(5, ellipse.StrokeThickness, 0.01, "Толщина обводки должна быть 5px");
            Console.WriteLine("✓ Тест 4.4: Применение толщины обводки 5px - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ApplyStrokeThickness_10px()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeThicknessToShape(ellipse, 10);

            // Assert
            Assert.AreEqual(10, ellipse.StrokeThickness, 0.01, "Толщина обводки должна быть 10px");
            Console.WriteLine("✓ Тест 4.5: Применение толщины обводки 10px - ПРОЙДЕН");
        }

        #endregion

        #region Тест 5: Изменение размера элементов (ResizeElement)

        [TestMethod]
        public void Test_ResizeElement_Increase()
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

            // Act - увеличение размера
            bool result = ResizeElement(ellipse, 100, 100, 150, 150);

            // Assert
            Assert.IsTrue(result, "Изменение размера должно быть успешным");
            Assert.AreEqual(150, ellipse.Width, 0.01, "Ширина должна быть 150");
            Assert.AreEqual(150, ellipse.Height, 0.01, "Высота должна быть 150");
            Console.WriteLine("✓ Тест 5.1: Увеличение размера элемента - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ResizeElement_Decrease()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 100,
                Height = 100,
                Fill = Brushes.White
            };
            Canvas.SetLeft(ellipse, 100);
            Canvas.SetTop(ellipse, 100);
            _mainCanvas.Children.Add(ellipse);

            // Act - уменьшение размера
            bool result = ResizeElement(ellipse, 100, 100, 60, 60);

            // Assert
            Assert.IsTrue(result, "Изменение размера должно быть успешным");
            Assert.AreEqual(60, ellipse.Width, 0.01, "Ширина должна быть 60");
            Assert.AreEqual(60, ellipse.Height, 0.01, "Высота должна быть 60");
            Console.WriteLine("✓ Тест 5.2: Уменьшение размера элемента - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ResizeElement_MinimumSize()
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

            // Act - попытка уменьшить до минимального размера
            bool result = ResizeElement(ellipse, 100, 100, 20, 20);

            // Assert
            Assert.IsTrue(result, "Изменение размера должно быть успешным");
            Assert.AreEqual(20, ellipse.Width, 0.01, "Ширина должна быть 20 (минимум)");
            Assert.AreEqual(20, ellipse.Height, 0.01, "Высота должна быть 20 (минимум)");
            Console.WriteLine("✓ Тест 5.3: Изменение размера до минимального - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ResizeElement_MaximumSize()
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

            // Act - увеличение до большого размера
            bool result = ResizeElement(ellipse, 100, 100, 500, 400);

            // Assert
            Assert.IsTrue(result, "Изменение размера должно быть успешным");
            Assert.AreEqual(500, ellipse.Width, 0.01, "Ширина должна быть 500");
            Assert.AreEqual(400, ellipse.Height, 0.01, "Высота должна быть 400");
            Console.WriteLine("✓ Тест 5.4: Изменение размера до максимального - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_ResizeElement_Proportional()
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

            // Act - пропорциональное изменение (сохраняем соотношение сторон)
            bool result = ResizeElement(ellipse, 100, 100, 100, 100);

            // Assert
            Assert.IsTrue(result, "Изменение размера должно быть успешным");
            Assert.AreEqual(100, ellipse.Width, 0.01, "Ширина должна быть 100");
            Assert.AreEqual(100, ellipse.Height, 0.01, "Высота должна быть 100");
            Assert.AreEqual(ellipse.Width, ellipse.Height, 0.01, "Элемент должен оставаться квадратным");
            Console.WriteLine("✓ Тест 5.5: Пропорциональное изменение размера - ПРОЙДЕН");
        }

        #endregion

        #region Тест 6: Создание фигур (CreateShape)

        [TestMethod]
        public void Test_CreateShape_Ellipse()
        {
            // Arrange & Act
            var ellipse = CreateEllipse(100, 100, 50, 50);
            _mainCanvas.Children.Add(ellipse);

            // Assert
            Assert.IsNotNull(ellipse, "Эллипс должен быть создан");
            Assert.AreEqual(50, ellipse.Width, 0.01, "Ширина должна быть 50");
            Assert.AreEqual(50, ellipse.Height, 0.01, "Высота должна быть 50");
            Assert.AreEqual(100, Canvas.GetLeft(ellipse), 0.01, "X координата должна быть 100");
            Assert.AreEqual(100, Canvas.GetTop(ellipse), 0.01, "Y координата должна быть 100");
            Console.WriteLine("✓ Тест 6.1: Создание эллипса - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_CreateShape_Border()
        {
            // Arrange & Act
            var border = CreateBorder(150, 150, 60, 60);
            _mainCanvas.Children.Add(border);

            // Assert
            Assert.IsNotNull(border, "Border должен быть создан");
            Assert.AreEqual(60, border.Width, 0.01, "Ширина должна быть 60");
            Assert.AreEqual(60, border.Height, 0.01, "Высота должна быть 60");
            Assert.AreEqual(150, Canvas.GetLeft(border), 0.01, "X координата должна быть 150");
            Assert.AreEqual(150, Canvas.GetTop(border), 0.01, "Y координата должна быть 150");
            Console.WriteLine("✓ Тест 6.2: Создание Border - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_CreateShape_Line()
        {
            // Arrange & Act
            var line = CreateLine(200, 200, 300, 250);
            _mainCanvas.Children.Add(line);

            // Assert
            Assert.IsNotNull(line, "Линия должна быть создана");
            Assert.AreEqual(200, line.X1, 0.01, "X1 должна быть 200");
            Assert.AreEqual(200, line.Y1, 0.01, "Y1 должна быть 200");
            Assert.AreEqual(300, line.X2, 0.01, "X2 должна быть 300");
            Assert.AreEqual(250, line.Y2, 0.01, "Y2 должна быть 250");
            Console.WriteLine("✓ Тест 6.3: Создание линии - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_CreateShape_TextBlock()
        {
            // Arrange & Act
            var textBlock = CreateTextBlock(250, 250, "Test Text");
            _mainCanvas.Children.Add(textBlock);

            // Assert
            Assert.IsNotNull(textBlock, "TextBlock должен быть создан");
            Assert.AreEqual("Test Text", textBlock.Text, "Текст должен быть 'Test Text'");
            Assert.AreEqual(250, Canvas.GetLeft(textBlock), 0.01, "X координата должна быть 250");
            Assert.AreEqual(250, Canvas.GetTop(textBlock), 0.01, "Y координата должна быть 250");
            Console.WriteLine("✓ Тест 6.4: Создание TextBlock - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_CreateShape_DifferentSizes()
        {
            // Arrange & Act - создаем фигуры разных размеров
            var smallEllipse = CreateEllipse(100, 100, 20, 20);
            var mediumEllipse = CreateEllipse(200, 200, 50, 50);
            var largeEllipse = CreateEllipse(300, 300, 100, 100);

            _mainCanvas.Children.Add(smallEllipse);
            _mainCanvas.Children.Add(mediumEllipse);
            _mainCanvas.Children.Add(largeEllipse);

            // Assert
            Assert.AreEqual(20, smallEllipse.Width, 0.01, "Маленький эллипс должен иметь ширину 20");
            Assert.AreEqual(50, mediumEllipse.Width, 0.01, "Средний эллипс должен иметь ширину 50");
            Assert.AreEqual(100, largeEllipse.Width, 0.01, "Большой эллипс должен иметь ширину 100");
            Console.WriteLine("✓ Тест 6.5: Создание фигур разных размеров - ПРОЙДЕН");
        }

        #endregion

        #region Тест 7: Удаление фигур (DeleteElement)

        [TestMethod]
        public void Test_DeleteElement_Single()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White
            };
            _mainCanvas.Children.Add(ellipse);
            int initialCount = _mainCanvas.Children.Count;

            // Act
            bool result = DeleteElement(ellipse);

            // Assert
            Assert.IsTrue(result, "Удаление должно быть успешным");
            Assert.AreEqual(initialCount - 1, _mainCanvas.Children.Count, "Количество элементов должно уменьшиться на 1");
            Assert.IsFalse(_mainCanvas.Children.Contains(ellipse), "Элемент не должен быть в Canvas");
            Console.WriteLine("✓ Тест 7.1: Удаление одной фигуры - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_DeleteElement_Multiple()
        {
            // Arrange
            var ellipse1 = new Ellipse { Width = 50, Height = 50, Fill = Brushes.White };
            var ellipse2 = new Ellipse { Width = 50, Height = 50, Fill = Brushes.White };
            var ellipse3 = new Ellipse { Width = 50, Height = 50, Fill = Brushes.White };

            _mainCanvas.Children.Add(ellipse1);
            _mainCanvas.Children.Add(ellipse2);
            _mainCanvas.Children.Add(ellipse3);
            int initialCount = _mainCanvas.Children.Count;

            // Act
            bool result1 = DeleteElement(ellipse1);
            bool result2 = DeleteElement(ellipse2);
            bool result3 = DeleteElement(ellipse3);

            // Assert
            Assert.IsTrue(result1 && result2 && result3, "Все удаления должны быть успешными");
            Assert.AreEqual(initialCount - 3, _mainCanvas.Children.Count, "Количество элементов должно уменьшиться на 3");
            Console.WriteLine("✓ Тест 7.2: Удаление нескольких фигур - ПРОЙДЕН");
        }

        [TestMethod]
        public void Test_DeleteElement_NotOnCanvas()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White
            };
            // НЕ добавляем на Canvas
            int initialCount = _mainCanvas.Children.Count;

            // Act
            bool result = DeleteElement(ellipse);

            // Assert
            Assert.IsFalse(result, "Удаление несуществующего элемента должно вернуть false");
            Assert.AreEqual(initialCount, _mainCanvas.Children.Count, "Количество элементов не должно измениться");
            Console.WriteLine("✓ Тест 7.3: Удаление несуществующей фигуры - ПРОЙДЕН");
        }

        #endregion

        #region Нерабочие тесты (для демонстрации ошибок)

        [TestMethod]
        public void Test_ApplyFillColor_WrongColor_ShouldFail()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyFillColorToShape(ellipse, Brushes.Red);

            // Assert - НАМЕРЕННО НЕПРАВИЛЬНАЯ ПРОВЕРКА
            // Ожидаем черный цвет, но применили красный - тест должен упасть
            Assert.AreEqual(Brushes.Black, ellipse.Fill, "ОШИБКА: Ожидали черный цвет, но получили красный");
            Console.WriteLine("✗ Тест FAIL_1: Применение цвета заливки - НЕ ПРОЙДЕН (ожидалось)");
        }

        [TestMethod]
        public void Test_MoveElement_WrongCoordinates_ShouldFail()
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
            bool result = MoveElement(ellipse, 200, 250);

            // Assert - НАМЕРЕННО НЕПРАВИЛЬНЫЕ ОЖИДАНИЯ
            // Переместили на (200, 250), но проверяем другие координаты
            Assert.IsTrue(result, "Перемещение должно быть успешным");
            Assert.AreEqual(150, Canvas.GetLeft(ellipse), 0.01, "ОШИБКА: Ожидали X=150, но получили X=200");
            Assert.AreEqual(200, Canvas.GetTop(ellipse), 0.01, "ОШИБКА: Ожидали Y=200, но получили Y=250");
            Console.WriteLine("✗ Тест FAIL_2: Перемещение элемента - НЕ ПРОЙДЕН (ожидалось)");
        }

        [TestMethod]
        public void Test_ApplyStrokeThickness_WrongValue_ShouldFail()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            ApplyStrokeThicknessToShape(ellipse, 5);

            // Assert - НАМЕРЕННО НЕПРАВИЛЬНОЕ ОЖИДАНИЕ
            // Применили толщину 5, но проверяем на 3
            Assert.AreEqual(3, ellipse.StrokeThickness, 0.01, "ОШИБКА: Ожидали толщину 3px, но получили 5px");
            Console.WriteLine("✗ Тест FAIL_3: Применение толщины обводки - НЕ ПРОЙДЕН (ожидалось)");
        }

        [TestMethod]
        public void Test_ResizeElement_WrongSize_ShouldFail()
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

            // Act - увеличиваем размер
            bool result = ResizeElement(ellipse, 100, 100, 100, 100);

            // Assert - НАМЕРЕННО НЕПРАВИЛЬНЫЕ ОЖИДАНИЯ
            // Изменили размер на 100x100, но проверяем на 80x80
            Assert.IsTrue(result, "Изменение размера должно быть успешным");
            Assert.AreEqual(80, ellipse.Width, 0.01, "ОШИБКА: Ожидали ширину 80, но получили 100");
            Assert.AreEqual(80, ellipse.Height, 0.01, "ОШИБКА: Ожидали высоту 80, но получили 100");
            Console.WriteLine("✗ Тест FAIL_4: Изменение размера элемента - НЕ ПРОЙДЕН (ожидалось)");
        }

        [TestMethod]
        public void Test_DeleteElement_ElementStillExists_ShouldFail()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White
            };
            _mainCanvas.Children.Add(ellipse);

            // Act
            bool result = DeleteElement(ellipse);

            // Assert - НАМЕРЕННО НЕПРАВИЛЬНАЯ ПРОВЕРКА
            // Удалили элемент, но проверяем, что он все еще существует
            Assert.IsTrue(result, "Удаление должно быть успешным");
            Assert.IsTrue(_mainCanvas.Children.Contains(ellipse), "ОШИБКА: Элемент был удален, но тест ожидает его наличие");
            Console.WriteLine("✗ Тест FAIL_5: Удаление элемента - НЕ ПРОЙДЕН (ожидалось)");
        }

        [TestMethod]
        public void Test_CreateShape_WrongProperties_ShouldFail()
        {
            // Arrange & Act
            var ellipse = CreateEllipse(100, 100, 50, 50);
            _mainCanvas.Children.Add(ellipse);

            // Assert - НАМЕРЕННО НЕПРАВИЛЬНЫЕ ОЖИДАНИЯ
            // Создали эллипс 50x50, но проверяем на 100x100
            Assert.IsNotNull(ellipse, "Эллипс должен быть создан");
            Assert.AreEqual(100, ellipse.Width, 0.01, "ОШИБКА: Ожидали ширину 100, но получили 50");
            Assert.AreEqual(100, ellipse.Height, 0.01, "ОШИБКА: Ожидали высоту 100, но получили 50");
            Console.WriteLine("✗ Тест FAIL_6: Создание фигуры - НЕ ПРОЙДЕН (ожидалось)");
        }

        [TestMethod]
        public void Test_ApplyStrokeColor_ColorNotApplied_ShouldFail()
        {
            // Arrange
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.White,
                Stroke = Brushes.Black
            };
            _mainCanvas.Children.Add(ellipse);

            // Act - НЕ применяем цвет (намеренно пропускаем вызов функции)
            // ApplyStrokeColorToShape(ellipse, Brushes.Red); // Закомментировано намеренно

            // Assert - проверяем, что цвет изменился (но мы его не меняли)
            Assert.AreEqual(Brushes.Red, ellipse.Stroke, "ОШИБКА: Цвет не был применен, но тест ожидает красный цвет");
            Console.WriteLine("✗ Тест FAIL_7: Применение цвета обводки - НЕ ПРОЙДЕН (ожидалось)");
        }

        #endregion

        #region Вспомогательные методы

        private void ApplyFillColorToShape(UIElement shape, Brush brush)
        {
            if (shape is Ellipse ellipse)
            {
                if (ellipse.Fill != Brushes.Transparent)
                {
                    ellipse.Fill = brush;
                }
            }
            else if (shape is Border border)
            {
                border.Background = brush;
            }
        }

        private void ApplyStrokeColorToShape(UIElement shape, Brush brush)
        {
            if (shape is Ellipse ellipse)
            {
                ellipse.Stroke = brush;
            }
            else if (shape is Border border)
            {
                border.BorderBrush = brush;
            }
            else if (shape is Line line)
            {
                line.Stroke = brush;
            }
            else if (shape is TextBlock textBlock)
            {
                textBlock.Foreground = brush;
            }
        }

        private bool MoveElement(UIElement element, double x, double y)
        {
            try
            {
                if (element == null || !_mainCanvas.Children.Contains(element))
                    return false;

                double elementWidth = GetElementWidth(element);
                double elementHeight = GetElementHeight(element);

                const double WorkAreaTop = 40;
                const double WorkAreaBottom = 100;

                double maxLeft = _mainCanvas.ActualWidth - elementWidth;
                double maxTop = _mainCanvas.ActualHeight - elementHeight - WorkAreaBottom;

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

        private void ApplyStrokeThicknessToShape(UIElement shape, double thickness)
        {
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
        }

        private bool ResizeElement(UIElement element, double currentLeft, double currentTop, double newWidth, double newHeight)
        {
            try
            {
                if (element == null || !_mainCanvas.Children.Contains(element))
                    return false;

                const double minSize = 20;

                if (element is FrameworkElement fe)
                {
                    fe.Width = Math.Max(minSize, newWidth);
                    fe.Height = Math.Max(minSize, newHeight);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private Ellipse CreateEllipse(double left, double top, double width, double height)
        {
            var ellipse = new Ellipse
            {
                Width = width,
                Height = height,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            Canvas.SetLeft(ellipse, left);
            Canvas.SetTop(ellipse, top);
            return ellipse;
        }

        private Border CreateBorder(double left, double top, double width, double height)
        {
            var border = new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1)
            };
            Canvas.SetLeft(border, left);
            Canvas.SetTop(border, top);
            return border;
        }

        private Line CreateLine(double x1, double y1, double x2, double y2)
        {
            return new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
        }

        private TextBlock CreateTextBlock(double left, double top, string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = Brushes.Black,
                FontSize = 12
            };
            Canvas.SetLeft(textBlock, left);
            Canvas.SetTop(textBlock, top);
            return textBlock;
        }

        private bool DeleteElement(UIElement element)
        {
            try
            {
                if (element == null || !_mainCanvas.Children.Contains(element))
                    return false;

                _mainCanvas.Children.Remove(element);
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
                if (fe.ActualWidth > 0)
                    return fe.ActualWidth;
                if (fe.Width > 0)
                    return fe.Width;
                return fe.DesiredSize.Width;
            }
            return 0;
        }

        private double GetElementHeight(UIElement element)
        {
            if (element is FrameworkElement fe)
            {
                if (fe.ActualHeight > 0)
                    return fe.ActualHeight;
                if (fe.Height > 0)
                    return fe.Height;
                return fe.DesiredSize.Height;
            }
            return 0;
        }

        #endregion

        [TestCleanup]
        public void Cleanup()
        {
            _mainCanvas.Children.Clear();
            _originalFillColors.Clear();
            _originalStrokeColors.Clear();
            _originalStrokeThickness.Clear();
        }
    }
}

