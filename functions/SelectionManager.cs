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
    /// <summary>
    /// Управляет системой выделения элементов на холсте
    /// </summary>
    public class SelectionManager
    {
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

        // События для уведомления MainWindow
        public event Action<FrameworkElement> OnElementSelected;
        public event Action<FrameworkElement> OnElementDeselected;
        public event Action OnSelectionCleared;
        public event Action<UIElement> OnSelectionVisualFeedbackChanged;

        public SelectionManager(Canvas canvas, MainWindow mainWindow)
        {
            this.canvas = canvas;
            this.mainWindow = mainWindow;
            InitializeSelectionSystem();
        }

        /// <summary>
        /// Инициализация системы выделения
        /// </summary>
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

        #region Основные методы выделения

        /// <summary>
        /// Выделяет элемент
        /// </summary>
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

        /// <summary>
        /// Выделяет фигуру (альтернативный метод)
        /// </summary>
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

        /// <summary>
        /// Снимает выделение с элемента
        /// </summary>
        public void DeselectElement(UIElement element)
        {
            if (element == null) return;

            selectedElements.Remove(element);
            ApplySelectionVisualFeedback(element, false);

            if (element is FrameworkElement frameworkElement)
            {
                frameworkElement.Opacity = 1;
                frameworkElement.ContextMenu = null;
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

        /// <summary>
        /// Снимает выделение со всех элементов
        /// </summary>
        public void DeselectAll()
        {
            foreach (var element in selectedElements.ToList())
            {
                DeselectElement(element);
            }

            DeselectArrow();
            OnSelectionCleared?.Invoke();
        }

        /// <summary>
        /// Очищает выделение (аналог DeselectAll)
        /// </summary>
        public void ClearSelection()
        {
            DeselectAll();
        }

        /// <summary>
        /// Выделяет все элементы на холсте
        /// </summary>
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

        /// <summary>
        /// Начинает процесс прямоугольного выделения
        /// </summary>
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

        /// <summary>
        /// Обновляет прямоугольное выделение в реальном времени
        /// </summary>
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

        /// <summary>
        /// Завершает процесс прямоугольного выделения
        /// </summary>
        public void EndSelection()
        {
            isSelecting = false;
            canvas.ReleaseMouseCapture();
            selectionRect.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Обновляет размер прямоугольника выделения
        /// </summary>
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

        /// <summary>
        /// Проверяет, идет ли процесс выделения
        /// </summary>
        public bool IsSelecting => isSelecting;

        #endregion

        #region Визуальное выделение

        /// <summary>
        /// Применяет визуальное выделение к элементу
        /// </summary>
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

        /// <summary>
        /// Сохраняет оригинальные цвета и толщину элемента
        /// </summary>
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
                // Сохраняем текущие цвета и толщину
                originalStrokeColors[shape] = ellipse.Stroke;
                originalFillColors[shape] = ellipse.Fill;
                originalStrokeThickness[shape] = ellipse.StrokeThickness;
            }
            else if (shape is Border border)
            {
                // Если элемент выделен, восстанавливаем базовую толщину перед сохранением
                if (isCurrentlySelected && savedThickness > 0)
                {
                    border.BorderThickness = new Thickness(savedThickness);
                }
                // Сохраняем текущие цвета и толщину
                originalStrokeColors[shape] = border.BorderBrush;
                originalFillColors[shape] = border.Background;
                originalStrokeThickness[shape] = border.BorderThickness.Left;
            }
            else if (shape is Line line)
            {
                // Если элемент выделен, восстанавливаем базовую толщину перед сохранением
                if (isCurrentlySelected && savedThickness > 0)
                {
                    line.StrokeThickness = savedThickness;
                }
                // Сохраняем текущие цвета и толщину
                originalStrokeColors[shape] = line.Stroke;
                originalStrokeThickness[shape] = line.StrokeThickness;
            }
            else if (shape is TextBlock textBlock)
            {
                // Сохраняем текущие цвета
                originalStrokeColors[shape] = textBlock.Foreground;
            }
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Получает границы элемента
        /// </summary>
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

        /// <summary>
        /// Проверяет, можно ли выделить элемент
        /// </summary>
        private bool IsSelectableElement(UIElement element)
        {
            return element != null &&
                   element != selectionRect &&
                   (element is Ellipse || element is Border || element is Line || element is TextBlock);
        }

        // Делегат для снятия выделения стрелки (устанавливается из MainWindow)
        public Action OnDeselectArrow { get; set; }

        /// <summary>
        /// Снимает выделение со стрелки (интеграция с системой стрелок)
        /// </summary>
        private void DeselectArrow()
        {
            OnDeselectArrow?.Invoke();
        }

        #endregion

        #region Публичные свойства и методы для доступа к состоянию

        /// <summary>
        /// Текущий выделенный элемент
        /// </summary>
        public FrameworkElement SelectedElement => selectedElement;

        /// <summary>
        /// Выделенная фигура
        /// </summary>
        public UIElement SelectedShape => selectedShape;

        /// <summary>
        /// Список всех выделенных элементов
        /// </summary>
        public IReadOnlyList<UIElement> SelectedElements => selectedElements.AsReadOnly();

        /// <summary>
        /// Количество выделенных элементов
        /// </summary>
        public int SelectedCount => selectedElements.Count;

        /// <summary>
        /// Проверяет, выделен ли элемент
        /// </summary>
        public bool IsElementSelected(UIElement element)
        {
            return selectedElements.Contains(element);
        }

        /// <summary>
        /// Получает оригинальный цвет обводки элемента
        /// </summary>
        public Brush GetOriginalStrokeColor(UIElement element)
        {
            return originalStrokeColors.ContainsKey(element) ? originalStrokeColors[element] : null;
        }

        /// <summary>
        /// Получает оригинальный цвет заливки элемента
        /// </summary>
        public Brush GetOriginalFillColor(UIElement element)
        {
            return originalFillColors.ContainsKey(element) ? originalFillColors[element] : null;
        }

        /// <summary>
        /// Получает оригинальную толщину обводки элемента
        /// </summary>
        public double GetOriginalStrokeThickness(UIElement element)
        {
            return originalStrokeThickness.ContainsKey(element) ? originalStrokeThickness[element] : 0;
        }

        /// <summary>
        /// Очищает сохраненные оригинальные стили для элемента
        /// </summary>
        public void ClearOriginalStyles(UIElement element)
        {
            originalStrokeColors.Remove(element);
            originalFillColors.Remove(element);
            originalStrokeThickness.Remove(element);
        }

        /// <summary>
        /// Очищает все сохраненные оригинальные стили
        /// </summary>
        public void ClearAllOriginalStyles()
        {
            originalStrokeColors.Clear();
            originalFillColors.Clear();
            originalStrokeThickness.Clear();
        }

        /// <summary>
        /// Обновляет оригинальную толщину обводки элемента
        /// </summary>
        public void UpdateOriginalStrokeThickness(UIElement element, double thickness)
        {
            if (element != null)
            {
                originalStrokeThickness[element] = thickness;
            }
        }

        /// <summary>
        /// Обновляет оригинальный цвет обводки элемента
        /// </summary>
        public void UpdateOriginalStrokeColor(UIElement element, Brush color)
        {
            if (element != null && color != null)
            {
                originalStrokeColors[element] = color;
            }
        }

        /// <summary>
        /// Обновляет оригинальный цвет заливки элемента
        /// </summary>
        public void UpdateOriginalFillColor(UIElement element, Brush color)
        {
            if (element != null && color != null)
            {
                originalFillColors[element] = color;
            }
        }

        /// <summary>
        /// Устанавливает оригинальные стили для элемента (для копирования)
        /// </summary>
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

        /// <summary>
        /// Копирует оригинальные стили с одного элемента на другой
        /// </summary>
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

        /// <summary>
        /// Проверяет, есть ли сохраненная оригинальная толщина для элемента
        /// </summary>
        public bool HasOriginalStrokeThickness(UIElement element)
        {
            return element != null && originalStrokeThickness.ContainsKey(element);
        }

        #endregion
    }
}

