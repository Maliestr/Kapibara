using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    public class FileManager
    {
        private Canvas mainCanvas;
        private TextBox fileNameTextBox;
        private ArrowManager arrowManager;
        private Dictionary<UIElement, ResizableElement> resizableElements;
        private List<UIElement> canvasElements;
        
        // Делегаты для создания элементов (зависят от MainWindow)
        private Action<UIElement> makeCanvasShapeDraggable;
        private Func<Point, Point> snapToGrid;
        private Func<bool> getIsSnapToGridEnabled;

        public FileManager(
            Canvas mainCanvas,
            TextBox fileNameTextBox,
            ArrowManager arrowManager,
            Dictionary<UIElement, ResizableElement> resizableElements,
            List<UIElement> canvasElements,
            Action<UIElement> makeCanvasShapeDraggable,
            Func<Point, Point> snapToGrid,
            Func<bool> getIsSnapToGridEnabled)
        {
            this.mainCanvas = mainCanvas;
            this.fileNameTextBox = fileNameTextBox;
            this.arrowManager = arrowManager;
            this.resizableElements = resizableElements;
            this.canvasElements = canvasElements;
            this.makeCanvasShapeDraggable = makeCanvasShapeDraggable;
            this.snapToGrid = snapToGrid;
            this.getIsSnapToGridEnabled = getIsSnapToGridEnabled;
        }

        public void SaveToFile(string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("GEDv1.0"); // Заголовок файла
                
                // Сохраняем имя файла из TextBox
                string fileName = fileNameTextBox?.Text ?? System.IO.Path.GetFileNameWithoutExtension(filePath);
                writer.WriteLine($"FILENAME|{fileName}"); // Метаданные: имя файла

                foreach (UIElement element in mainCanvas.Children)
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

        public void LoadFromFile(string filePath)
        {
            mainCanvas.Children.Clear();
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
                    if (fileNameTextBox != null)
                    {
                        fileNameTextBox.Text = loadedFileName;
                    }
                }
                else
                {
                    // Если метаданных нет, используем имя файла из файловой системы без расширения
                    string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    if (fileNameTextBox != null)
                    {
                        fileNameTextBox.Text = fileNameWithoutExtension;
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
            mainCanvas.Children.Add(ellipse);
            makeCanvasShapeDraggable?.Invoke(ellipse);

            // Создаем ResizableElement
            bool isSnapEnabled = getIsSnapToGridEnabled?.Invoke() ?? false;
            resizableElements[ellipse] = new ResizableElement(ellipse, mainCanvas, snapToGrid, isSnapEnabled);
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
            mainCanvas.Children.Add(square);
            makeCanvasShapeDraggable?.Invoke(square);

            // Создаем ResizableElement
            bool isSnapEnabled = getIsSnapToGridEnabled?.Invoke() ?? false;
            resizableElements[square] = new ResizableElement(square, mainCanvas, snapToGrid, isSnapEnabled);
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
            mainCanvas.Children.Add(diamond);
            makeCanvasShapeDraggable?.Invoke(diamond);

            // Создаем ResizableElement
            bool isSnapEnabled = getIsSnapToGridEnabled?.Invoke() ?? false;
            resizableElements[diamond] = new ResizableElement(diamond, mainCanvas, snapToGrid, isSnapEnabled);
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
            mainCanvas.Children.Add(line);
            makeCanvasShapeDraggable?.Invoke(line);

            // Создаем ResizableElement
            bool isSnapEnabled = getIsSnapToGridEnabled?.Invoke() ?? false;
            resizableElements[line] = new ResizableElement(line, mainCanvas, snapToGrid, isSnapEnabled);
        }

        public void ExportCanvasToImage(string filePath)
        {
            // Создаем RenderTargetBitmap с размерами холста
            var renderTargetBitmap = new RenderTargetBitmap(
                (int)mainCanvas.ActualWidth,
                (int)mainCanvas.ActualHeight,
                96, 96, PixelFormats.Pbgra32);

            renderTargetBitmap.Render(mainCanvas);

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

        public void SaveComments(CommentManager commentManager, string filePath)
        {
            // Логика сохранения комментариев
        }

        public void LoadComments(CommentManager commentManager, string filePath)
        {
            // Логика загрузки комментариев
        }
    }
}

