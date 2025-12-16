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
        #region Поля

        private Canvas mainCanvas;
        private TextBox fileNameTextBox;
        private ArrowManager arrowManager;
        private Dictionary<UIElement, ResizableElementManager> resizableElements;
        private List<UIElement> canvasElements;
        private TextManager textManager;
        private CommentManager commentManager;
        private PoolManager poolManager;
        
        // Делегаты для создания элементов (зависят от MainWindow)
        private Action<UIElement> makeCanvasShapeDraggable;
        private Func<Point, Point> snapToGrid;
        private Func<bool> getIsSnapToGridEnabled;

        #endregion

        #region Конструктор и инициализация

        public FileManager(
            Canvas mainCanvas,
            TextBox fileNameTextBox,
            ArrowManager arrowManager,
            Dictionary<UIElement, ResizableElementManager> resizableElements,
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

        public void SetManagers(TextManager textManager, CommentManager commentManager, PoolManager poolManager)
        {
            this.textManager = textManager;
            this.commentManager = commentManager;
            this.poolManager = poolManager;
        }

        #endregion

        #region Сохранение файла

        public void SaveToFile(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("GEDv1.0"); // Заголовок файла
                
                // Сохраняем имя файла из TextBox
                string fileName = fileNameTextBox?.Text ?? System.IO.Path.GetFileNameWithoutExtension(filePath);
                writer.WriteLine($"FILENAME|{fileName}"); // Метаданные: имя файла

                // Собираем данные комментариев и пулов при первом проходе
                var processedComments = new HashSet<UIElement>();
                var processedPools = new HashSet<UIElement>();
                var commentsData = new List<CommentData>();
                var poolsData = new List<PoolData>();

                // Сначала собираем пулы
                foreach (UIElement element in mainCanvas.Children)
                {
                    if (element is Border border && IsPoolContainer(border))
                    {
                        processedPools.Add(border);
                        var poolData = ExtractPoolData(border);
                        if (poolData != null)
                        {
                            poolsData.Add(poolData);
                        }
                    }
                }

                // Затем собираем комментарии
                foreach (UIElement element in mainCanvas.Children)
                {
                    if (element is Border border && IsCommentIcon(border) && !processedComments.Contains(border))
                    {
                        processedComments.Add(border);
                        var commentData = ExtractCommentData(border);
                        if (commentData != null)
                        {
                            commentsData.Add(commentData);
                        }
                    }
                }

                // Теперь сохраняем обычные элементы (исключая пулы и комментарии)
                foreach (UIElement element in mainCanvas.Children)
                {
                    if (processedPools.Contains(element) || processedComments.Contains(element))
                    {
                        continue;
                    }

                    // Проверяем, не является ли это частью пула или комментария
                    if (IsPoolPart(element, processedPools) || IsCommentPart(element, processedComments))
                    {
                        continue;
                    }

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
                    else if (element is TextBlock textBlock)
                    {
                        // Сохраняем текстовые элементы
                        double left = Canvas.GetLeft(textBlock);
                        double top = Canvas.GetTop(textBlock);
                        string text = textBlock.Text ?? "";
                        string foregroundColor = (textBlock.Foreground as SolidColorBrush)?.Color.ToString() ?? "White";
                        string fontFamily = textBlock.FontFamily?.Source ?? "Gotham Pro";
                        double fontSize = textBlock.FontSize;
                        string fontStyle = textBlock.FontStyle == FontStyles.Italic ? "Italic" : "Normal";
                        string fontWeight = textBlock.FontWeight == FontWeights.Bold ? "Bold" : "Normal";

                        // Экранируем специальные символы в тексте
                        text = text.Replace("|", "||").Replace("\n", "\\n").Replace("\r", "\\r");
                        
                        writer.WriteLine($"TEXT|{left}|{top}|{text}|{foregroundColor}|{fontFamily}|{fontSize}|{fontStyle}|{fontWeight}");
                    }
                }

                // Сохраняем комментарии
                foreach (var commentData in commentsData)
                {
                    string text = commentData.Text ?? "";
                    // Экранируем специальные символы
                    text = text.Replace("|", "||").Replace("\n", "\\n").Replace("\r", "\\r");
                    writer.WriteLine($"COMMENT|{commentData.PositionX}|{commentData.PositionY}|{text}");
                }

                // Сохраняем пулы
                foreach (var poolData in poolsData)
                {
                    string name = poolData.Name ?? "Участник";
                    // Экранируем специальные символы
                    name = name.Replace("|", "||").Replace("\n", "\\n").Replace("\r", "\\r");
                    writer.WriteLine($"POOL|{poolData.PositionX}|{poolData.PositionY}|{poolData.Width}|{poolData.Height}|{poolData.IsVertical}|{name}");
                }
            }
        }

        #endregion

        #region Вспомогательные классы для данных

        private class CommentData
        {
            public double PositionX { get; set; }
            public double PositionY { get; set; }
            public string Text { get; set; }
        }

        private class PoolData
        {
            public double PositionX { get; set; }
            public double PositionY { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public bool IsVertical { get; set; }
            public string Name { get; set; }
        }

        #endregion

        #region Вспомогательные методы для сохранения

        // Проверка, является ли Border контейнером пула
        private bool IsPoolContainer(Border border)
        {
            if (border == null || border.Child == null) return false;
            
            // Пул имеет Grid внутри
            if (!(border.Child is Grid grid)) return false;
            
            // Проверяем цвет фона (стиль пула)
            if (border.Background is SolidColorBrush bgBrush)
            {
                Color bgColor = bgBrush.Color;
                // Пул имеет цвет #343434
                if (bgColor.R == 0x34 && bgColor.G == 0x34 && bgColor.B == 0x34)
                {
                    // Проверяем наличие header и body
                    Border header = null;
                    Border body = null;
                    foreach (UIElement child in grid.Children)
                    {
                        if (child is Border childBorder)
                        {
                            if (childBorder.Background is SolidColorBrush childBg)
                            {
                                Color childColor = childBg.Color;
                                // Header имеет цвет #424242
                                if (childColor.R == 0x42 && childColor.G == 0x42 && childColor.B == 0x42)
                                {
                                    header = childBorder;
                                }
                                // Body имеет тот же цвет что и container
                                else if (childColor.R == 0x34 && childColor.G == 0x34 && childColor.B == 0x34)
                                {
                                    body = childBorder;
                                }
                            }
                        }
                    }
                    return header != null && body != null;
                }
            }
            
            return false;
        }

        // Извлечение данных пула
        private PoolData ExtractPoolData(Border poolContainer)
        {
            if (poolContainer?.Child is Grid grid)
            {
                Border header = null;
                TextBlock headerText = null;
                
                // Находим header и текст в нем
                foreach (UIElement child in grid.Children)
                {
                    if (child is Border childBorder)
                    {
                        if (childBorder.Background is SolidColorBrush childBg)
                        {
                            Color childColor = childBg.Color;
                            if (childColor.R == 0x42 && childColor.G == 0x42 && childColor.B == 0x42)
                            {
                                header = childBorder;
                                if (childBorder.Child is TextBlock textBlock)
                                {
                                    headerText = textBlock;
                                }
                            }
                        }
                    }
                }

                if (header == null) return null;

                // Определяем ориентацию (вертикальный или горизонтальный)
                bool isVertical = grid.RowDefinitions.Count > 0;

                // Получаем имя пула из менеджера (если доступен), иначе из headerText
                string name = "Участник";
                if (poolManager != null)
                {
                    var pool = poolManager.Pools?.FirstOrDefault(p => p.Container == poolContainer);
                    if (pool != null && !string.IsNullOrEmpty(pool.Name))
                    {
                        name = pool.Name;
                    }
                    else if (headerText != null && !string.IsNullOrEmpty(headerText.Text))
                    {
                        name = headerText.Text;
                    }
                }
                else if (headerText != null && !string.IsNullOrEmpty(headerText.Text))
                {
                    name = headerText.Text;
                }

                double left = Canvas.GetLeft(poolContainer);
                double top = Canvas.GetTop(poolContainer);
                double width = poolContainer.Width;
                double height = poolContainer.Height;

                return new PoolData
                {
                    PositionX = double.IsNaN(left) ? 0 : left,
                    PositionY = double.IsNaN(top) ? 0 : top,
                    Width = double.IsNaN(width) ? 150 : width,
                    Height = double.IsNaN(height) ? 300 : height,
                    IsVertical = isVertical,
                    Name = name
                };
            }
            return null;
        }

        // Проверка, является ли элемент частью пула
        private bool IsPoolPart(UIElement element, HashSet<UIElement> processedPools)
        {
            if (element == null) return false;
            
            // Проверяем, находится ли элемент внутри обработанного пула
            foreach (UIElement poolContainer in processedPools)
            {
                if (poolContainer is Border border && border.Child is Grid grid)
                {
                    // Проверяем, является ли element частью grid
                    if (IsElementInGrid(element, grid))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Проверка, находится ли элемент в Grid
        private bool IsElementInGrid(UIElement element, Grid grid)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent == grid)
                {
                    return true;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return false;
        }

        // Проверка, является ли Border иконкой комментария
        private bool IsCommentIcon(Border border)
        {
            if (border == null) return false;
            
            // Иконка комментария: маленький красный круглый Border (12x12)
            if (Math.Abs(border.Width - 12) < 0.1 && Math.Abs(border.Height - 12) < 0.1)
            {
                if (border.CornerRadius.TopLeft > 5 && border.CornerRadius.TopLeft < 7)
                {
                    if (border.Background is SolidColorBrush bgBrush)
                    {
                        Color bgColor = bgBrush.Color;
                        // Красный цвет
                        if (bgColor.R == 255 && bgColor.G == 0 && bgColor.B == 0)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // Извлечение данных комментария
        private CommentData ExtractCommentData(Border commentIcon)
        {
            if (commentIcon == null) return null;

            double iconLeft = Canvas.GetLeft(commentIcon);
            double iconTop = Canvas.GetTop(commentIcon);
            
            // Позиция комментария (центр иконки)
            double positionX = (double.IsNaN(iconLeft) ? 0 : iconLeft) + 6;
            double positionY = (double.IsNaN(iconTop) ? 0 : iconTop) + 6;

            // Получаем текст комментария из менеджера (если доступен), иначе ищем в визуальных элементах
            string commentText = "";
            if (commentManager != null)
            {
                // Ищем Comment по VisualElement
                var comment = commentManager.Comments?.FirstOrDefault(c => c.VisualElement == commentIcon);
                if (comment != null)
                {
                    commentText = comment.Text ?? "";
                }
            }
            
            // Если не нашли через менеджер, пытаемся найти в визуальных элементах (как fallback)
            if (string.IsNullOrEmpty(commentText))
            {
                foreach (UIElement element in mainCanvas.Children)
                {
                    if (element is Border textBorder && 
                        textBorder.Child is TextBlock textBlock &&
                        Math.Abs(Canvas.GetLeft(element) - (iconLeft + 15)) < 5 &&
                        Math.Abs(Canvas.GetTop(element) - (iconTop - 10)) < 5)
                    {
                        commentText = textBlock.Text ?? "";
                        break;
                    }
                }
            }

            return new CommentData
            {
                PositionX = positionX,
                PositionY = positionY,
                Text = commentText
            };
        }

        // Проверка, является ли элемент частью комментария
        private bool IsCommentPart(UIElement element, HashSet<UIElement> processedComments)
        {
            if (element == null) return false;
            
            // Проверяем, является ли это TextBlock комментария (Border с TextBlock внутри)
            if (element is Border textBorder && textBorder.Child is TextBlock)
            {
                // Проверяем, связан ли он с обработанным комментарием
                double left = Canvas.GetLeft(element);
                double top = Canvas.GetTop(element);
                
                foreach (UIElement commentIcon in processedComments)
                {
                    if (commentIcon is Border icon)
                    {
                        double iconLeft = Canvas.GetLeft(icon);
                        double iconTop = Canvas.GetTop(icon);
                        
                        // TextBlock комментария находится рядом с иконкой
                        if (Math.Abs(left - (iconLeft + 15)) < 5 && Math.Abs(top - (iconTop - 10)) < 5)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        #endregion

        #region Загрузка файла

        public void LoadFromFile(string filePath)
        {
            mainCanvas.Children.Clear();
            canvasElements.Clear();
            arrowManager?.ClearAllArrows();
            commentManager?.ClearAllComments();
            poolManager?.ClearAllPools();

            var commentsData = new List<CommentManager.CommentData>();
            var poolsData = new List<PoolManager.PoolData>();

            using (var reader = new StreamReader(filePath))
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
                        case "TEXT":
                            CreateTextFromData(parts);
                            break;
                        case "COMMENT":
                            if (parts.Length >= 4)
                            {
                                commentsData.Add(new CommentManager.CommentData
                                {
                                    PositionX = double.Parse(parts[1]),
                                    PositionY = double.Parse(parts[2]),
                                    Text = parts.Length > 3 ? parts[3].Replace("||", "|").Replace("\\n", "\n").Replace("\\r", "\r") : ""
                                });
                            }
                            break;
                        case "POOL":
                            if (parts.Length >= 7)
                            {
                                poolsData.Add(new PoolManager.PoolData
                                {
                                    PositionX = double.Parse(parts[1]),
                                    PositionY = double.Parse(parts[2]),
                                    Width = double.Parse(parts[3]),
                                    Height = double.Parse(parts[4]),
                                    IsVertical = bool.Parse(parts[5]),
                                    Name = parts.Length > 6 ? parts[6].Replace("||", "|").Replace("\\n", "\n").Replace("\\r", "\r") : "Участник"
                                });
                            }
                            break;
                    }
                }
                
                // Загружаем комментарии после загрузки всех элементов
                if (commentManager != null && commentsData.Count > 0)
                {
                    commentManager.LoadComments(commentsData);
                }

                // Загружаем пулы после загрузки всех элементов
                if (poolManager != null && poolsData.Count > 0)
                {
                    poolManager.LoadPools(poolsData);
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

        #endregion

        #region Создание элементов из данных

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
            resizableElements[ellipse] = new ResizableElementManager(ellipse, mainCanvas, snapToGrid, isSnapEnabled);
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
            resizableElements[square] = new ResizableElementManager(square, mainCanvas, snapToGrid, isSnapEnabled);
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
            resizableElements[diamond] = new ResizableElementManager(diamond, mainCanvas, snapToGrid, isSnapEnabled);
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
            resizableElements[line] = new ResizableElementManager(line, mainCanvas, snapToGrid, isSnapEnabled);
        }

        private void CreateTextFromData(string[] data)
        {
            if (data.Length < 9 || textManager == null) return;

            double left = double.Parse(data[1]);
            double top = double.Parse(data[2]);
            string text = data[3].Replace("||", "|").Replace("\\n", "\n").Replace("\\r", "\r");
            string foregroundColor = data[4];
            string fontFamily = data[5];
            double fontSize = double.Parse(data[6]);
            string fontStyle = data[7];
            string fontWeight = data[8];

            try
            {
                Brush foregroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foregroundColor));
                
                // Используем TextManager для создания текста (это правильно настроит все обработчики)
                textManager.CreateTextElementWithColor(text, left, top, foregroundBrush);
                
                // Находим созданный TextBlock и применяем сохраненное форматирование
                TextBlock createdTextBlock = null;
                foreach (UIElement element in mainCanvas.Children)
                {
                    if (element is TextBlock tb && 
                        Math.Abs(Canvas.GetLeft(element) - left) < 1.0 && 
                        Math.Abs(Canvas.GetTop(element) - top) < 1.0 &&
                        tb.Text == text)
                    {
                        createdTextBlock = tb;
                        break;
                    }
                }

                if (createdTextBlock != null)
                {
                    // Применяем форматирование из файла
                    try
                    {
                        createdTextBlock.FontFamily = new FontFamily(fontFamily);
                    }
                    catch
                    {
                        createdTextBlock.FontFamily = new FontFamily("Gotham Pro");
                    }
                    createdTextBlock.FontSize = fontSize;
                    createdTextBlock.FontStyle = fontStyle == "Italic" ? FontStyles.Italic : FontStyles.Normal;
                    createdTextBlock.FontWeight = fontWeight == "Bold" ? FontWeights.Bold : FontWeights.Normal;

                    // Обновляем форматирование в TextManager
                    if (textManager.TextFormatting != null)
                    {
                        textManager.TextFormatting[createdTextBlock] = new TextFormatting
                        {
                            FontFamily = createdTextBlock.FontFamily,
                            FontSize = createdTextBlock.FontSize,
                            FontStyle = createdTextBlock.FontStyle,
                            FontWeight = createdTextBlock.FontWeight
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при создании текста: {ex.Message}");
            }
        }

        #endregion

        #region Экспорт в изображение

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

        #endregion
    }
}

