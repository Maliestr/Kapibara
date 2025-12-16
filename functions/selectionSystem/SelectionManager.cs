using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    // Управляет системой выделения элементов на холсте
    public class SelectionManager
    {
        #region Поля

        private Canvas canvas;
        private MainWindow mainWindow;

        // Состояние выделения
        private FrameworkElement selectedElement; // Текущий выделенный элемент (для операций)
        private UIElement selectedShape; // Выделенная фигура (альтернативная система выделения)
        private List<UIElement> selectedElements = new List<UIElement>(); // Список выделенных элементов

        // Система прямоугольного выделения
        private bool isSelecting = false;
        private Point selectionStart;
        private Rectangle selectionRect;

        // Словари для сохранения оригинальных стилей
        private Dictionary<UIElement, Brush> originalStrokeColors = new Dictionary<UIElement, Brush>();
        private Dictionary<UIElement, Brush> originalFillColors = new Dictionary<UIElement, Brush>();
        private Dictionary<UIElement, double> originalStrokeThickness = new Dictionary<UIElement, double>();

        #endregion

        #region События

        // События для уведомления MainWindow
        public event Action<FrameworkElement> OnElementSelected;
        public event Action<FrameworkElement> OnElementDeselected;
        public event Action OnSelectionCleared;
        public event Action<UIElement> OnSelectionVisualFeedbackChanged;

        #endregion

        #region Конструктор и инициализация

        public SelectionManager(Canvas canvas, MainWindow mainWindow)
        {
            this.canvas = canvas;
            this.mainWindow = mainWindow;
            InitializeSelectionSystem();
        }

        private void InitializeSelectionSystem()
        {
            selectionRect = new Rectangle
            {
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(0x20, 0x1E, 0x90, 0xFF)),
                Visibility = Visibility.Collapsed
            };
            canvas.Children.Add(selectionRect);
            Panel.SetZIndex(selectionRect, 1000);
        }

        #endregion

        #region Основные методы выделения

        public void SelectElement(FrameworkElement element)
        {
            if (element == null) return;

            // Если не зажат Ctrl, снимаем все выделения
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                ClearSelection();
            }

            // Если элемент уже выделен и нажат Ctrl - снимаем выделение
            if (selectedElements.Contains(element) && Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                DeselectElement(element);
                return;
            }

            // Если элемент не выделен - выделяем его
            if (!selectedElements.Contains(element))
            {
                SaveOriginalColors(element);
                selectedElement = element;
                ApplySelectionVisualFeedback(element, true);
                selectedElements.Add(element);

                // Уведомляем MainWindow о выделении
                OnElementSelected?.Invoke(element);
            }
        }

        public void SelectShape(UIElement shape)
        {
            if (shape == null) return;

            DeselectArrow();

            SaveOriginalColors(shape);
            selectedShape = shape;

            if (shape is FrameworkElement frameworkElement)
            {
                selectedElement = frameworkElement;

                if (!selectedElements.Contains(frameworkElement))
                {
                    selectedElements.Add(frameworkElement);
                }
            }

            ApplySelectionVisualFeedback(shape, true);
            OnElementSelected?.Invoke(shape as FrameworkElement);
        }

        public void DeselectElement(UIElement element)
        {
            if (element == null) return;

            selectedElements.Remove(element);
            ApplySelectionVisualFeedback(element, false);

            if (element is FrameworkElement frameworkElement)
            {
                frameworkElement.Opacity = 1;
                
                // Закрываем контекстное меню перед удалением, чтобы избежать появления стандартного меню
                if (frameworkElement.ContextMenu != null)
                {
                    frameworkElement.ContextMenu.IsOpen = false;
                    frameworkElement.ContextMenu = null;
                }
            }

            if (element == selectedElement)
            {
                selectedElement = null;
            }

            if (element == selectedShape)
            {
                selectedShape = null;
            }

            OnElementDeselected?.Invoke(element as FrameworkElement);
        }

        public void DeselectAll()
        {
            foreach (var element in selectedElements.ToList())
            {
                DeselectElement(element);
            }

            DeselectArrow();
            OnSelectionCleared?.Invoke();
        }

        public void ClearSelection()
        {
            DeselectAll();
        }

        public void SelectAllElements()
        {
            ClearSelection();

            foreach (UIElement element in canvas.Children)
            {
                if (IsSelectableElement(element) && element is FrameworkElement frameworkElement)
                {
                    SelectElement(frameworkElement);
                }
            }
        }

        #endregion

        #region Прямоугольное выделение

        public void StartSelection(Point position)
        {
            // Сбрасываем предыдущее выделение, если не зажат Ctrl
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                ClearSelection();
            }

            selectionStart = position;
            isSelecting = true;

            selectionRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(selectionRect, position.X);
            Canvas.SetTop(selectionRect, position.Y);
            selectionRect.Width = 0;
            selectionRect.Height = 0;

            canvas.CaptureMouse();
        }

        public void UpdateSelectionInRealTime(Rect selectionBounds)
        {
            foreach (UIElement element in canvas.Children)
            {
                if (element == selectionRect || element == null) continue;

                var elementBounds = GetElementBounds(element);
                bool shouldBeSelected = selectionBounds.IntersectsWith(elementBounds);

                if (element is FrameworkElement frameworkElement)
                {
                    bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl);

                    if (shouldBeSelected && !selectedElements.Contains(frameworkElement))
                    {
                        // ИСПРАВЛЕНО: Удалена избыточная проверка
                        selectedElements.Add(frameworkElement);
                        SaveOriginalColors(frameworkElement);
                        ApplySelectionVisualFeedback(frameworkElement, true);
                        SelectShape(frameworkElement);
                    }
                    else if (!shouldBeSelected && selectedElements.Contains(frameworkElement) && !isCtrlPressed)
                    {
                        DeselectElement(frameworkElement);
                    }
                }
            }
        }

        public void EndSelection()
        {
            isSelecting = false;
            canvas.ReleaseMouseCapture();
            selectionRect.Visibility = Visibility.Collapsed;
        }

        public void UpdateSelectionRect(Point currentPosition)
        {
            if (!isSelecting) return;

            var x = Math.Min(currentPosition.X, selectionStart.X);
            var y = Math.Min(currentPosition.Y, selectionStart.Y);
            var width = Math.Abs(currentPosition.X - selectionStart.X);
            var height = Math.Abs(currentPosition.Y - selectionStart.Y);

            Canvas.SetLeft(selectionRect, x);
            Canvas.SetTop(selectionRect, y);
            selectionRect.Width = width;
            selectionRect.Height = height;

            UpdateSelectionInRealTime(new Rect(x, y, width, height));
        }

        public bool IsSelecting => isSelecting;

        #endregion

        #region Визуальное выделение

        public void ApplySelectionVisualFeedback(UIElement element, bool isSelected)
        {
            if (element == null) return;

            if (isSelected)
            {
                // Выделение только увеличением толщины, без изменения цвета
                if (selectedElements.Contains(element))
                {
                    if (element is Ellipse ellipseElement)
                    {
                        // Восстанавливаем сохраненный цвет обводки перед выделением
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            ellipseElement.Stroke = originalStrokeColors[element];
                        }
                        // Сохраняем оригинальную толщину перед изменением (если еще не сохранена)
                        if (!originalStrokeThickness.ContainsKey(element))
                        {
                            originalStrokeThickness[element] = ellipseElement.StrokeThickness;
                        }
                        // Увеличиваем толщину на 2, используя сохраненную базовую толщину
                        double baseThickness = originalStrokeThickness[element];
                        ellipseElement.StrokeThickness = baseThickness + 2;
                    }
                    else if (element is Border borderElement)
                    {
                        // Восстанавливаем сохраненный цвет обводки перед выделением
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            borderElement.BorderBrush = originalStrokeColors[element];
                        }
                        // Сохраняем оригинальную толщину перед изменением (если еще не сохранена)
                        if (!originalStrokeThickness.ContainsKey(element))
                        {
                            originalStrokeThickness[element] = borderElement.BorderThickness.Left;
                        }
                        // Увеличиваем толщину на 2, используя сохраненную базовую толщину
                        double baseThickness = originalStrokeThickness[element];
                        borderElement.BorderThickness = new Thickness(baseThickness + 2);
                    }
                    else if (element is Line lineElement)
                    {
                        // Восстанавливаем сохраненный цвет обводки перед выделением
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            lineElement.Stroke = originalStrokeColors[element];
                        }
                        // Сохраняем оригинальную толщину перед изменением (если еще не сохранена)
                        if (!originalStrokeThickness.ContainsKey(element))
                        {
                            originalStrokeThickness[element] = lineElement.StrokeThickness;
                        }
                        // Увеличиваем толщину на 2, используя сохраненную базовую толщину
                        double baseThickness = originalStrokeThickness[element];
                        lineElement.StrokeThickness = baseThickness + 2;
                    }
                    else if (element is TextBlock textBlockElement)
                    {
                        textBlockElement.Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 255));
                    }
                }
            }
            else
            {
                // Возвращаем оригинальные значения только если элемент не выделен
                if (!selectedElements.Contains(element))
                {
                    if (element is Ellipse ellipseElement)
                    {
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            ellipseElement.Stroke = originalStrokeColors[element];
                        }
                        else
                        {
                            // Если цвет не сохранен, используем логику по умолчанию
                            if (ellipseElement.Fill == Brushes.Transparent) // Кольцо
                            {
                                ellipseElement.Stroke = Brushes.White;
                            }
                            else // Круг
                            {
                                ellipseElement.Stroke = Brushes.Black;
                            }
                        }

                        // Восстанавливаем толщину
                        if (originalStrokeThickness.ContainsKey(element))
                        {
                            ellipseElement.StrokeThickness = originalStrokeThickness[element];
                        }
                    }
                    else if (element is Border borderElement)
                    {
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            borderElement.BorderBrush = originalStrokeColors[element];
                        }
                        else
                        {
                            borderElement.BorderBrush = Brushes.White;
                        }

                        // Восстанавливаем толщину
                        if (originalStrokeThickness.ContainsKey(element))
                        {
                            borderElement.BorderThickness = new Thickness(originalStrokeThickness[element]);
                        }
                    }
                    else if (element is Line lineElement)
                    {
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            lineElement.Stroke = originalStrokeColors[element];
                        }
                        else
                        {
                            lineElement.Stroke = Brushes.White;
                        }

                        // Восстанавливаем толщину
                        if (originalStrokeThickness.ContainsKey(element))
                        {
                            lineElement.StrokeThickness = originalStrokeThickness[element];
                        }
                    }
                    else if (element is TextBlock textBlockElement)
                    {
                        if (originalStrokeColors.ContainsKey(element))
                        {
                            textBlockElement.Foreground = originalStrokeColors[element];
                        }
                        textBlockElement.Background = Brushes.Transparent;
                    }
                }
            }

            OnSelectionVisualFeedbackChanged?.Invoke(element);
        }

        public void SaveOriginalColors(UIElement shape)
        {
            if (shape == null) return;

            // Если элемент уже выделен (толщина увеличена), восстанавливаем базовую толщину перед сохранением
            bool isCurrentlySelected = selectedElements.Contains(shape);
            double savedThickness = originalStrokeThickness.ContainsKey(shape) ? originalStrokeThickness[shape] : 0;

            if (shape is Ellipse ellipse)
            {
                // Если элемент выделен, восстанавливаем базовую толщину перед сохранением
                if (isCurrentlySelected && savedThickness > 0)
                {
                    ellipse.StrokeThickness = savedThickness;
                }
                // Сохраняем текущие цвета
                originalStrokeColors[shape] = ellipse.Stroke;
                originalFillColors[shape] = ellipse.Fill;
                // Сохраняем толщину только если она еще не была сохранена
                if (!originalStrokeThickness.ContainsKey(shape))
                {
                    originalStrokeThickness[shape] = ellipse.StrokeThickness;
                }
            }
            else if (shape is Border border)
            {
                // Если элемент выделен, восстанавливаем базовую толщину перед сохранением
                if (isCurrentlySelected && savedThickness > 0)
                {
                    border.BorderThickness = new Thickness(savedThickness);
                }
                // Сохраняем текущие цвета
                originalStrokeColors[shape] = border.BorderBrush;
                originalFillColors[shape] = border.Background;
                // Сохраняем толщину только если она еще не была сохранена
                if (!originalStrokeThickness.ContainsKey(shape))
                {
                    originalStrokeThickness[shape] = border.BorderThickness.Left;
                }
            }
            else if (shape is Line line)
            {
                // Если элемент выделен, восстанавливаем базовую толщину перед сохранением
                if (isCurrentlySelected && savedThickness > 0)
                {
                    line.StrokeThickness = savedThickness;
                }
                // Сохраняем текущие цвета
                originalStrokeColors[shape] = line.Stroke;
                // Сохраняем толщину только если она еще не была сохранена
                if (!originalStrokeThickness.ContainsKey(shape))
                {
                    originalStrokeThickness[shape] = line.StrokeThickness;
                }
            }
            else if (shape is TextBlock textBlock)
            {
                // Сохраняем текущие цвета
                originalStrokeColors[shape] = textBlock.Foreground;
            }
        }

        #endregion

        #region Вспомогательные методы

        private Rect GetElementBounds(UIElement element)
        {
            if (element == null) return new Rect(0, 0, 0, 0);

            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            double width = 0;
            double height = 0;

            if (element is Ellipse ellipse)
            {
                width = ellipse.Width;
                height = ellipse.Height;
            }
            else if (element is Border border)
            {
                width = border.Width;
                height = border.Height;
            }
            else if (element is Line line)
            {
                // Для линии используем bounding box
                double minX = Math.Min(line.X1, line.X2);
                double minY = Math.Min(line.Y1, line.Y2);
                double maxX = Math.Max(line.X1, line.X2);
                double maxY = Math.Max(line.Y1, line.Y2);
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            else if (element is TextBlock textBlock)
            {
                // Для текста используем размеры текста
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = textBlock.DesiredSize.Width;
                height = textBlock.DesiredSize.Height;
            }

            return new Rect(left, top, width, height);
        }

        private bool IsSelectableElement(UIElement element)
        {
            return element != null &&
                   element != selectionRect &&
                   (element is Ellipse || element is Border || element is Line || element is TextBlock);
        }

        // Делегат для снятия выделения стрелки (устанавливается из MainWindow)
        public Action OnDeselectArrow { get; set; }

        private void DeselectArrow()
        {
            OnDeselectArrow?.Invoke();
        }

        #endregion

        #region Публичные свойства и методы

        public FrameworkElement SelectedElement => selectedElement;
        public UIElement SelectedShape => selectedShape;
        public IReadOnlyList<UIElement> SelectedElements => selectedElements.AsReadOnly();
        public int SelectedCount => selectedElements.Count;

        public bool IsElementSelected(UIElement element)
        {
            return selectedElements.Contains(element);
        }

        public Brush GetOriginalStrokeColor(UIElement element)
        {
            return originalStrokeColors.ContainsKey(element) ? originalStrokeColors[element] : null;
        }

        public Brush GetOriginalFillColor(UIElement element)
        {
            return originalFillColors.ContainsKey(element) ? originalFillColors[element] : null;
        }

        public double GetOriginalStrokeThickness(UIElement element)
        {
            return originalStrokeThickness.ContainsKey(element) ? originalStrokeThickness[element] : 0;
        }

        public void ClearOriginalStyles(UIElement element)
        {
            originalStrokeColors.Remove(element);
            originalFillColors.Remove(element);
            originalStrokeThickness.Remove(element);
        }

        public void ClearAllOriginalStyles()
        {
            originalStrokeColors.Clear();
            originalFillColors.Clear();
            originalStrokeThickness.Clear();
        }

        public void UpdateOriginalStrokeThickness(UIElement element, double thickness)
        {
            if (element != null)
            {
                originalStrokeThickness[element] = thickness;
            }
        }

        public void UpdateOriginalStrokeColor(UIElement element, Brush color)
        {
            if (element != null && color != null)
            {
                originalStrokeColors[element] = color;
            }
        }

        public void UpdateOriginalFillColor(UIElement element, Brush color)
        {
            if (element != null && color != null)
            {
                originalFillColors[element] = color;
            }
        }

        public void SetOriginalStyles(UIElement element, Brush strokeColor, Brush fillColor, double strokeThickness)
        {
            if (element != null)
            {
                if (strokeColor != null)
                    originalStrokeColors[element] = strokeColor;
                if (fillColor != null)
                    originalFillColors[element] = fillColor;
                originalStrokeThickness[element] = strokeThickness;
            }
        }

        public void CopyOriginalStyles(UIElement from, UIElement to)
        {
            if (from == null || to == null) return;

            if (originalStrokeColors.ContainsKey(from))
                originalStrokeColors[to] = originalStrokeColors[from];
            if (originalFillColors.ContainsKey(from))
                originalFillColors[to] = originalFillColors[from];
            if (originalStrokeThickness.ContainsKey(from))
                originalStrokeThickness[to] = originalStrokeThickness[from];
        }

        public bool HasOriginalStrokeThickness(UIElement element)
        {
            return element != null && originalStrokeThickness.ContainsKey(element);
        }

        #endregion
    }
}

