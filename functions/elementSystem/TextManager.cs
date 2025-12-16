using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Graphic_Editor
{
    #region Вспомогательные классы

    public class TextFormatting
    {
        public FontFamily FontFamily { get; set; }
        public double FontSize { get; set; }
        public FontStyle FontStyle { get; set; }
        public FontWeight FontWeight { get; set; }
    }

    #endregion

    public class TextManager
    {
        #region Поля

        private bool isTextModeActive = false;
        private TextBox textBox;
        private Dictionary<UIElement, TextFormatting> textFormatting = new Dictionary<UIElement, TextFormatting>();
        private Dictionary<UIElement, Point> textDragOffsets = new Dictionary<UIElement, Point>();

        // Текущие настройки форматирования
        private string currentFontFamily = "Gotham Pro";
        private double currentFontSize = 14;
        private FontStyle currentFontStyle = FontStyles.Normal;
        private FontWeight currentFontWeight = FontWeights.Normal;

        // Зависимости (передаются из MainWindow)
        private Canvas mainCanvas;
        private SelectionManager selectionManager;
        private ComboBox fontComboBox;
        private ComboBox fontStyleComboBox;
        private ComboBox fontSizeComboBox;
        private Border textPanel;
        private bool isSnapToGridEnabled;
        private Dictionary<UIElement, ResizableElementManager> resizableElements;

        // Делегаты для взаимодействия с MainWindow
        private Action<UIElement> bringToFrontAction;
        private Action<UIElement> selectElementAction;
        private Action<UIElement> selectShapeAction;
        private Action deselectAllAction;
        private Action<UIElement> makeCanvasShapeDraggableAction;
        private Func<Point, Point> snapToGridFunc;
        private Action<UIElement> saveToUndoStackAction;

        #endregion

        #region Свойства

        public bool IsTextModeActive => isTextModeActive;
        public Dictionary<UIElement, TextFormatting> TextFormatting => textFormatting;

        #endregion

        #region События

        public event Action OnTextModeActivated;
        public event Action OnTextModeDeactivated;

        #endregion

        #region Конструктор и инициализация

        public TextManager(
            Canvas canvas,
            SelectionManager selectionManager,
            ComboBox fontComboBox,
            ComboBox fontStyleComboBox,
            ComboBox fontSizeComboBox,
            Border textPanel,
            Dictionary<UIElement, ResizableElementManager> resizableElements,
            bool isSnapToGridEnabled,
            Action<UIElement> bringToFront,
            Action<UIElement> selectElement,
            Action<UIElement> selectShape,
            Action deselectAll,
            Action<UIElement> makeCanvasShapeDraggable,
            Func<Point, Point> snapToGrid,
            Action<UIElement> saveToUndoStack)
        {
            this.mainCanvas = canvas;
            this.selectionManager = selectionManager;
            this.fontComboBox = fontComboBox;
            this.fontStyleComboBox = fontStyleComboBox;
            this.fontSizeComboBox = fontSizeComboBox;
            this.textPanel = textPanel;
            this.resizableElements = resizableElements;
            this.isSnapToGridEnabled = isSnapToGridEnabled;
            this.bringToFrontAction = bringToFront;
            this.selectElementAction = selectElement;
            this.selectShapeAction = selectShape;
            this.deselectAllAction = deselectAll;
            this.makeCanvasShapeDraggableAction = makeCanvasShapeDraggable;
            this.snapToGridFunc = snapToGrid;
            this.saveToUndoStackAction = saveToUndoStack;

            // Подписываемся на события комбо-боксов
            if (fontComboBox != null)
                fontComboBox.SelectionChanged += FontComboBox_SelectionChanged;
            if (fontStyleComboBox != null)
                fontStyleComboBox.SelectionChanged += FontStyleComboBox_SelectionChanged;
            if (fontSizeComboBox != null)
                fontSizeComboBox.SelectionChanged += FontSizeComboBox_SelectionChanged;
        }

        public void UpdateDependencies(bool snapToGridEnabled)
        {
            this.isSnapToGridEnabled = snapToGridEnabled;
        }

        #endregion

        #region Управление режимом текста

        public void ActivateTextMode()
        {
            if (isTextModeActive) return;
            isTextModeActive = true;
            OnTextModeActivated?.Invoke();
            // Текстбокс будет создан при клике на Canvas через HandleMouseClick
        }

        #endregion

        #region Создание и редактирование текста

        // Обработка клика мыши для создания текста в месте клика
        public bool HandleMouseClick(Point position, object originalSource)
        {
            if (!isTextModeActive) return false;

            // Проверяем, что клик не по существующему элементу
            if (originalSource is TextBlock || originalSource is TextBox)
            {
                return false; // Не обрабатываем клик по существующему тексту
            }

            // Если уже есть активный текстбокс, не создаем новый
            if (textBox != null) return false;

            // Создаем текстбокс в месте клика
            CreateTextBoxAtPosition(position);
            return true;
        }

        private void CreateTextBoxAtPosition(Point position)
        {
            // Применяем привязку к сетке, если включена
            if (isSnapToGridEnabled && snapToGridFunc != null)
            {
                position = snapToGridFunc(position);
            }

            textBox = new TextBox
            {
                Width = 200,
                Height = 30,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderBrush = Brushes.Gray,
                FontSize = 14,
                Text = "",
                CaretBrush = Brushes.White,
                FocusVisualStyle = null
            };

            Canvas.SetLeft(textBox, position.X);
            Canvas.SetTop(textBox, position.Y);

            mainCanvas.Children.Add(textBox);

            textBox.Focus();
            textBox.SelectAll();

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
                    mainCanvas.Children.Remove(textBox);
                    textBox = null;
                    OnTextModeDeactivated?.Invoke();
                    e.Handled = true;
                }
            };
            textBox.LostFocus += (s, e) => DeactivateTextMode();
        }

        public void DeactivateTextMode()
        {
            // Если есть активный текстбокс, обрабатываем его
            if (textBox != null)
            {
                //    
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    CreateTextElement(textBox.Text, Canvas.GetLeft(textBox), Canvas.GetTop(textBox));
                }

                //  
                mainCanvas.Children.Remove(textBox);
                textBox = null;
            }

            // Деактивируем режим независимо от наличия текстбокса
            isTextModeActive = false;

            //  -     
            ResetTextComboBoxesToEmpty();

            OnTextModeDeactivated?.Invoke();
        }

        public void CreateTextElement(string text, double left, double top)
        {
            CreateTextElementWithColor(text, left, top, Brushes.White);
        }

        public void CreateTextElementWithColor(string text, double left, double top, Brush color)
        {
            //    
            string fontFamily = string.IsNullOrEmpty(currentFontFamily) ? "Gotham Pro" : currentFontFamily;
            double fontSize = currentFontSize == 0 ? 14 : currentFontSize;
            FontStyle fontStyle = currentFontStyle;
            FontWeight fontWeight = currentFontWeight;

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontFamily = new FontFamily(fontFamily),
                FontSize = fontSize,
                FontStyle = fontStyle,
                FontWeight = fontWeight,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null,
                Background = Brushes.Transparent,
                //     
                Width = double.NaN, //  
                Height = double.NaN //  
            };

            //    
            if (isSnapToGridEnabled && snapToGridFunc != null)
            {
                Point snappedPoint = snapToGridFunc(new Point(left, top));
                left = snappedPoint.X;
                top = snappedPoint.Y;
            }

            //  
            Canvas.SetLeft(textBlock, left);
            Canvas.SetTop(textBlock, top);
            Canvas.SetZIndex(textBlock, 1000);

            //   
            selectionManager?.UpdateOriginalStrokeColor(textBlock, color);
            SaveTextFormatting(textBlock);

            //    
            mainCanvas.Children.Add(textBlock);
            bringToFrontAction?.Invoke(textBlock);

            textBlock.MouseLeftButtonDown += TextElement_MouseLeftButtonDown;
            makeCanvasShapeDraggableAction?.Invoke(textBlock);
            selectElementAction?.Invoke(textBlock);
            selectShapeAction?.Invoke(textBlock);
        }

        //    
        private void SaveTextFormatting(TextBlock textBlock)
        {
            if (!textFormatting.ContainsKey(textBlock))
            {
                textFormatting[textBlock] = new TextFormatting
                {
                    FontFamily = textBlock.FontFamily,
                    FontSize = textBlock.FontSize,
                    FontStyle = textBlock.FontStyle,
                    FontWeight = textBlock.FontWeight
                };
            }
        }

        #endregion

        #region Перетаскивание текста

        private void TextElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock == null) return;

            deselectAllAction?.Invoke();

            //    
            if (selectionManager?.GetOriginalStrokeColor(textBlock) == null)
            {
                selectionManager?.UpdateOriginalStrokeColor(textBlock, textBlock.Foreground);
            }

            selectElementAction?.Invoke(textBlock);
            selectShapeAction?.Invoke(textBlock);

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

        //    
        public void StartTextDrag(UIElement element, MouseButtonEventArgs e)
        {
            // ,     
            if (element is TextBlock textBlock && selectionManager?.GetOriginalStrokeColor(element) == null)
            {
                selectionManager?.UpdateOriginalStrokeColor(element, textBlock.Foreground);
            }

            //    
            Point offset = e.GetPosition(element);
            textDragOffsets[element] = offset;

            //     
            if (resizableElements != null && resizableElements.ContainsKey(element))
            {
                resizableElements[element].HideResizeHandles();
            }

            if (element is TextBlock tb)
            {
                tb.Opacity = 0.7;
            }

            element.CaptureMouse();
            element.MouseMove += TextElement_MouseMove;
            element.MouseLeftButtonUp += TextElement_MouseLeftButtonUp;
        }

        //      
        private void TextElement_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is UIElement draggedElement)
            {
                if (!textDragOffsets.ContainsKey(draggedElement)) return;

                Point currentPosition = e.GetPosition(mainCanvas);
                Point offset = textDragOffsets[draggedElement];

                // :    
            if (isSnapToGridEnabled && snapToGridFunc != null)
                {
                    currentPosition = snapToGridFunc(currentPosition);
                }

                //     
                double newLeft = currentPosition.X - offset.X;
                double newTop = currentPosition.Y - offset.Y;

                //      
                double elementWidth = draggedElement.RenderSize.Width;
                double elementHeight = draggedElement.RenderSize.Height;

                if (elementWidth == 0 || double.IsNaN(elementWidth)) elementWidth = 100;
                if (elementHeight == 0 || double.IsNaN(elementHeight)) elementHeight = 30;

                //  
                double workAreaLeft = 300;
                double workAreaTop = 26;
                double workAreaRight = mainCanvas.ActualWidth - 264;
                double workAreaBottom = mainCanvas.ActualHeight - 100;

                //   
                newLeft = Math.Max(workAreaLeft, Math.Min(newLeft, workAreaRight - elementWidth));
                newTop = Math.Max(workAreaTop, Math.Min(newTop, workAreaBottom - elementHeight));

                //   
                if (isSnapToGridEnabled && snapToGridFunc != null)
                {
                    Point snappedPosition = snapToGridFunc(new Point(newLeft, newTop));
                    newLeft = snappedPosition.X;
                    newTop = snappedPosition.Y;
                }

                Canvas.SetLeft(draggedElement, newLeft);
                Canvas.SetTop(draggedElement, newTop);
            }
        }

        //      
        private void TextElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                if (element is TextBlock textBlock)
                {
                    textBlock.Opacity = 1.0;

                    //     
                    if (resizableElements != null && resizableElements.ContainsKey(textBlock))
                    {
                        resizableElements[textBlock].ShowResizeHandles();
                    }
                }

                element.MouseMove -= TextElement_MouseMove;
                element.MouseLeftButtonUp -= TextElement_MouseLeftButtonUp;
                element.ReleaseMouseCapture();
                textDragOffsets.Remove(element);
            }
        }

        #endregion

        #region Редактирование текста

        public void EditTextElement(TextBlock textBlock)
        {
            // ,   
            if (mainCanvas.Children.Contains(textBlock))
            {
                //   
                Brush originalStrokeColor = selectionManager?.GetOriginalStrokeColor(textBlock) ?? textBlock.Foreground;

                // TextBox    
                var editTextBox = new TextBox
                {
                    Width = Math.Max(textBlock.ActualWidth, 200),
                    Height = Math.Max(textBlock.ActualHeight, 30),
                    Background = Brushes.White,
                    Foreground = Brushes.Black,
                    BorderBrush = Brushes.Gray,
                    FontFamily = textBlock.FontFamily,
                    FontSize = textBlock.FontSize,
                    FontStyle = textBlock.FontStyle,
                    FontWeight = textBlock.FontWeight,
                    Text = textBlock.Text,
                    CaretBrush = Brushes.Black,
                    FocusVisualStyle = null,
                    TextWrapping = TextWrapping.Wrap
                };

                double left = Canvas.GetLeft(textBlock);
                double top = Canvas.GetTop(textBlock);

                mainCanvas.Children.Remove(textBlock);
                Canvas.SetLeft(editTextBox, left);
                Canvas.SetTop(editTextBox, top);
                mainCanvas.Children.Add(editTextBox);
                editTextBox.Focus();
                editTextBox.SelectAll();

                //    
                editTextBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        FinishEditWithFormatting(editTextBox, left, top, originalStrokeColor);
                        FinishEdit(editTextBox, left, top, originalStrokeColor);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        CancelTextEditing(editTextBox, textBlock.Text, left, top, originalStrokeColor);
                        mainCanvas.Children.Remove(editTextBox);
                        CreateTextElementWithColor(textBlock.Text, left, top, originalStrokeColor);
                        e.Handled = true;
                    }
                };
                editTextBox.LostFocus += (s, e) => FinishEdit(editTextBox, left, top, originalStrokeColor);
                editTextBox.LostFocus += (s, e) => FinishEditWithFormatting(editTextBox, left, top, originalStrokeColor);
            }
        }

        //    
        private void FinishEditWithFormatting(TextBox editTextBox, double left, double top, Brush originalColor)
        {
            if (mainCanvas.Children.Contains(editTextBox))
            {
                string newText = string.IsNullOrWhiteSpace(editTextBox.Text) ? "" : editTextBox.Text;
                mainCanvas.Children.Remove(editTextBox);

                //    TextBox
                var textBlock = new TextBlock
                {
                    Text = newText,
                    Foreground = originalColor,
                    FontFamily = editTextBox.FontFamily,
                    FontSize = editTextBox.FontSize,
                    FontStyle = editTextBox.FontStyle,
                    FontWeight = editTextBox.FontWeight,
                    Cursor = Cursors.Hand,
                    FocusVisualStyle = null,
                    Background = Brushes.Transparent,
                    //     
                    Width = double.NaN, //  
                    Height = double.NaN //  
                };

                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, top);
                Canvas.SetZIndex(textBlock, 1000);

                selectionManager?.UpdateOriginalStrokeColor(textBlock, originalColor);
                SaveTextFormatting(textBlock);

                mainCanvas.Children.Add(textBlock);
                bringToFrontAction?.Invoke(textBlock);
                textBlock.MouseLeftButtonDown += TextElement_MouseLeftButtonDown;
                makeCanvasShapeDraggableAction?.Invoke(textBlock);
                selectElementAction?.Invoke(textBlock);
                selectShapeAction?.Invoke(textBlock);
            }
        }

        //  
        private void CancelTextEditing(TextBox editTextBox, string originalText, double left, double top, Brush originalColor)
        {
            if (mainCanvas.Children.Contains(editTextBox))
            {
                mainCanvas.Children.Remove(editTextBox);
                CreateTextElementWithColor(originalText, left, top, originalColor);
            }
        }

        //    
        private void FinishEdit(TextBox editTextBox, double left, double top, Brush originalColor)
        {
            if (mainCanvas.Children.Contains(editTextBox))
            {
                string newText = string.IsNullOrWhiteSpace(editTextBox.Text) ? "" : editTextBox.Text;
                mainCanvas.Children.Remove(editTextBox);
                CreateTextElementWithColor(newText, left, top, originalColor);
            }
        }

        #endregion

        #region Обработчики событий комбо-боксов

        private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fontComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
            {
                currentFontFamily = selectedItem.Content.ToString();
                ApplyTextFormattingToSelected();
            }
        }

        //    
        private void FontStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fontStyleComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
            {
                switch (selectedItem.Content.ToString())
                {
                    case "Regular":
                        currentFontStyle = FontStyles.Normal;
                        currentFontWeight = FontWeights.Normal;
                        break;
                    case "Bold":
                        currentFontStyle = FontStyles.Normal;
                        currentFontWeight = FontWeights.Bold;
                        break;
                    case "Italic":
                        currentFontStyle = FontStyles.Italic;
                        currentFontWeight = FontWeights.Normal;
                        break;
                    case "Bold Italic":
                        currentFontStyle = FontStyles.Italic;
                        currentFontWeight = FontWeights.Bold;
                        break;
                }
                ApplyTextFormattingToSelected();
            }
        }

        //   
        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fontSizeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
            {
                if (double.TryParse(selectedItem.Content.ToString(), out double size))
                {
                    currentFontSize = size;
                    ApplyTextFormattingToSelected();
                }
            }
        }

        #endregion

        #region Применение форматирования

        private void ApplyTextFormattingToSelected()
        {
            //     
            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement is TextBlock textBlock)
            {
                //  , 
                if (!string.IsNullOrEmpty(currentFontFamily))
                {
                    try
                    {
                        textBlock.FontFamily = new FontFamily(currentFontFamily);
                    }
                    catch
                    {
                        //     FontFamily
                    }
                }

                //   , 
                if (currentFontSize > 0)
                {
                    textBlock.FontSize = currentFontSize;
                }

                //    
                textBlock.FontStyle = currentFontStyle;
                textBlock.FontWeight = currentFontWeight;

                //   
                SaveCurrentTextFormatting(textBlock);
            }
        }

        //      
        public void UpdateTextPanelForSelectedElement(FrameworkElement element)
        {
            if (element == null) return;

            if (element is TextBlock textBlock)
            {
                //  -   
                UpdateTextComboBoxes(textBlock);

                //     
                SaveCurrentTextFormatting(textBlock);
            }
            else
            {
                //  -  
                ResetTextComboBoxesToEmpty();
            }
        }

        //  -   TextBlock
        private void UpdateTextComboBoxes(TextBlock textBlock)
        {
            if (textBlock == null) return;

            //  
            string fontFamilySource = textBlock.FontFamily?.Source ?? "";
            foreach (ComboBoxItem item in fontComboBox.Items)
            {
                if (item.Content != null && item.Content.ToString() == fontFamilySource)
                {
                    //   ,   
                    fontComboBox.SelectionChanged -= FontComboBox_SelectionChanged;
                    fontComboBox.SelectedItem = item;
                    fontComboBox.SelectionChanged += FontComboBox_SelectionChanged;
                    currentFontFamily = fontFamilySource;
                    break;
                }
            }

            //   ,   
            if (string.IsNullOrEmpty(currentFontFamily) && !string.IsNullOrEmpty(fontFamilySource))
            {
                currentFontFamily = fontFamilySource;
            }

            //   
            bool fontSizeFound = false;
            foreach (ComboBoxItem item in fontSizeComboBox.Items)
            {
                if (item.Content != null && double.TryParse(item.Content.ToString(), out double size) &&
                    Math.Abs(size - textBlock.FontSize) < 0.1)
                {
                    //   ,   
                    fontSizeComboBox.SelectionChanged -= FontSizeComboBox_SelectionChanged;
                    fontSizeComboBox.SelectedItem = item;
                    fontSizeComboBox.SelectionChanged += FontSizeComboBox_SelectionChanged;
                    currentFontSize = textBlock.FontSize;
                    fontSizeFound = true;
                    break;
                }
            }

            //   ,  
            if (!fontSizeFound && textBlock.FontSize > 0)
            {
                var newItem = new ComboBoxItem { Content = textBlock.FontSize.ToString("0") };
                fontSizeComboBox.Items.Add(newItem);
                //   
                fontSizeComboBox.SelectionChanged -= FontSizeComboBox_SelectionChanged;
                fontSizeComboBox.SelectedItem = newItem;
                fontSizeComboBox.SelectionChanged += FontSizeComboBox_SelectionChanged;
                currentFontSize = textBlock.FontSize;
            }
            else if (!fontSizeFound && textBlock.FontSize > 0)
            {
                currentFontSize = textBlock.FontSize;
            }

            //   
            string styleName = GetFontStyleName(textBlock.FontStyle, textBlock.FontWeight);
            foreach (ComboBoxItem item in fontStyleComboBox.Items)
            {
                if (item.Content != null && item.Content.ToString() == styleName)
                {
                    //   ,   
                    fontStyleComboBox.SelectionChanged -= FontStyleComboBox_SelectionChanged;
                    fontStyleComboBox.SelectedItem = item;
                    fontStyleComboBox.SelectionChanged += FontStyleComboBox_SelectionChanged;
                    currentFontStyle = textBlock.FontStyle;
                    currentFontWeight = textBlock.FontWeight;
                    break;
                }
            }
        }

        //     -
        private string GetFontStyleName(FontStyle style, FontWeight weight)
        {
            if (style == FontStyles.Italic && weight == FontWeights.Bold)
                return "Bold Italic";
            else if (weight == FontWeights.Bold)
                return "Bold";
            else if (style == FontStyles.Italic)
                return "Italic";
            else
                return "Regular";
        }

        //     
        private void SaveCurrentTextFormatting(TextBlock textBlock)
        {
            if (!textFormatting.ContainsKey(textBlock))
            {
                textFormatting[textBlock] = new TextFormatting();
            }

            textFormatting[textBlock].FontFamily = textBlock.FontFamily;
            textFormatting[textBlock].FontSize = textBlock.FontSize;
            textFormatting[textBlock].FontStyle = textBlock.FontStyle;
            textFormatting[textBlock].FontWeight = textBlock.FontWeight;
        }

        #endregion

        #region Вспомогательные методы

        private void ResetTextComboBoxesToEmpty()
        {
            //   
            if (fontComboBox != null)
                fontComboBox.SelectedItem = null;
            if (fontStyleComboBox != null)
                fontStyleComboBox.SelectedItem = null;
            if (fontSizeComboBox != null)
                fontSizeComboBox.SelectedItem = null;

            // 
            currentFontFamily = null;
            currentFontSize = 0;
            currentFontStyle = FontStyles.Normal;
            currentFontWeight = FontWeights.Normal;
        }

        //       
        public MouseButtonEventHandler GetTextElementMouseLeftButtonDownHandler()
        {
            return TextElement_MouseLeftButtonDown;
        }

        #endregion
    }
}
