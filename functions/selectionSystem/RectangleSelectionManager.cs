using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Graphic_Editor
{
    // Управляет прямоугольным выделением элементов по нажатию и удержанию ЛКМ
    public class RectangleSelectionManager
    {
        #region Поля

        private Canvas canvas;
        private SelectionManager selectionManager;
        private Dictionary<UIElement, ResizableElementManager> resizableElements;
        private GridManager gridManager;

        // Состояние выделения
        private bool isSelecting = false;
        private Point selectionStart;
        private Rectangle selectionRectangle;

        // Флаг для предотвращения конфликтов с другими операциями
        private bool isMouseDownOnElement = false;

        // Флаг для предотвращения рекурсивных вызовов
        private bool isUpdatingSelection = false;

        // Throttling для обновления выделения
        private bool isUpdatePending = false;
        private Rect pendingSelectionBounds;

        #endregion

        #region Конструктор и инициализация

        public RectangleSelectionManager(
            Canvas canvas,
            SelectionManager selectionManager,
            Dictionary<UIElement, ResizableElementManager> resizableElements,
            GridManager gridManager)
        {
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            this.selectionManager = selectionManager ?? throw new ArgumentNullException(nameof(selectionManager));
            this.resizableElements = resizableElements ?? throw new ArgumentNullException(nameof(resizableElements));
            this.gridManager = gridManager;

            InitializeSelectionRectangle();
        }

        private void InitializeSelectionRectangle()
        {
            selectionRectangle = new Rectangle
            {
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(0x20, 0x1E, 0x90, 0xFF)),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false // Прямоугольник не должен перехватывать события мыши
            };
            canvas.Children.Add(selectionRectangle);
            Panel.SetZIndex(selectionRectangle, 1000);
        }

        #endregion

        #region Обработка выделения

        public bool HandleMouseLeftButtonDown(MouseButtonEventArgs e, UIElement clickedElement)
        {
            // Если клик был на элементе, не начинаем прямоугольное выделение
            if (clickedElement != null && clickedElement != canvas && !IsSelectionRectangle(clickedElement))
            {
                isMouseDownOnElement = true;
                return false; // Не обрабатываем как прямоугольное выделение
            }

            isMouseDownOnElement = false;
            Point position = e.GetPosition(canvas);

            // Если не зажат Ctrl, снимаем все выделения
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                ClearAllResizableElements();
                selectionManager.ClearSelection();
            }

            // Начинаем прямоугольное выделение
            selectionStart = position;
            isSelecting = true;

            selectionRectangle.Visibility = Visibility.Visible;
            Canvas.SetLeft(selectionRectangle, position.X);
            Canvas.SetTop(selectionRectangle, position.Y);
            selectionRectangle.Width = 0;
            selectionRectangle.Height = 0;

            canvas.CaptureMouse();
            e.Handled = true;
            return true;
        }

        public void HandleMouseMove(MouseEventArgs e)
        {
            if (!isSelecting || isMouseDownOnElement || isUpdatingSelection) return;

            Point currentPosition = e.GetPosition(canvas);

            // Вычисляем размеры и позицию прямоугольника
            double x = Math.Min(currentPosition.X, selectionStart.X);
            double y = Math.Min(currentPosition.Y, selectionStart.Y);
            double width = Math.Abs(currentPosition.X - selectionStart.X);
            double height = Math.Abs(currentPosition.Y - selectionStart.Y);

            // Обновляем визуальный прямоугольник
            Canvas.SetLeft(selectionRectangle, x);
            Canvas.SetTop(selectionRectangle, y);
            selectionRectangle.Width = width;
            selectionRectangle.Height = height;

            // Обновляем выделение элементов в реальном времени только если прямоугольник достаточно большой
            if (width > 5 || height > 5)
            {
                pendingSelectionBounds = new Rect(x, y, width, height);
                
                // Используем асинхронное обновление через Dispatcher для предотвращения блокировки UI
                if (!isUpdatePending && !isUpdatingSelection)
                {
                    isUpdatePending = true;
                    canvas.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            isUpdatePending = false;
                            if (isSelecting && !isUpdatingSelection)
                            {
                                UpdateSelectionInRealTime(pendingSelectionBounds);
                            }
                        }));
                }
            }
        }

        public void HandleMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!isSelecting) return;

            isSelecting = false;
            isMouseDownOnElement = false;
            isUpdatePending = false;
            canvas.ReleaseMouseCapture();
            selectionRectangle.Visibility = Visibility.Collapsed;

            // Финальное обновление выделения перед завершением
            if (!isUpdatingSelection)
            {
                UpdateSelectionInRealTime(pendingSelectionBounds);
            }

            // Создаем ResizableElement для всех выделенных элементов
            CreateResizableElementsForSelected();
        }

        #endregion

        #region Обновление выделения

        private void UpdateSelectionInRealTime(Rect selectionBounds)
        {
            // Защита от рекурсивных вызовов
            if (isUpdatingSelection || !isSelecting) return;
            isUpdatingSelection = true;

            try
            {
                bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl);

                // Собираем элементы для обработки более эффективно
                var elementsToSelect = new List<FrameworkElement>();
                var elementsToDeselect = new List<FrameworkElement>();

                // Проходим по коллекции один раз - создаем копию для безопасности
                var childrenCopy = new List<UIElement>();
                foreach (UIElement child in canvas.Children)
                {
                    if (child != null && child != selectionRectangle)
                    {
                        childrenCopy.Add(child);
                    }
                }

                foreach (var child in childrenCopy)
                {
                    // Быстрая проверка типа перед более дорогими проверками
                    if (!(child is FrameworkElement frameworkElement)) continue;

                    // Пропускаем элементы из панели нотации и сетки (оптимизированные проверки)
                    if (IsElementInNotation(child) || IsGridElement(child)) continue;

                    var elementBounds = GetElementBounds(child);
                    if (elementBounds.Width == 0 && elementBounds.Height == 0) continue;

                    bool shouldBeSelected = selectionBounds.IntersectsWith(elementBounds);
                    bool isCurrentlySelected = selectionManager.IsElementSelected(frameworkElement);

                    if (shouldBeSelected && !isCurrentlySelected)
                    {
                        elementsToSelect.Add(frameworkElement);
                    }
                    else if (!shouldBeSelected && isCurrentlySelected && !isCtrlPressed)
                    {
                        elementsToDeselect.Add(frameworkElement);
                    }
                }

                // Применяем изменения пакетно
                // Для множественного выделения используем прямой вызов методов выделения
                // чтобы избежать очистки выделения при каждом вызове SelectElement
                foreach (var element in elementsToSelect)
                {
                    SelectElementDirectly(element);
                    
                    // Создаем ResizableElement и показываем ручки сразу для только что выделенных элементов
                    if (!resizableElements.ContainsKey(element))
                    {
                        Func<Point, Point> snapFunction = null;
                        if (gridManager != null)
                        {
                            snapFunction = gridManager.SnapToGrid;
                        }

                        resizableElements[element] = new ResizableElementManager(
                            element,
                            canvas,
                            snapFunction,
                            gridManager?.IsSnapToGridEnabled ?? false);
                    }

                    // Показываем ручки изменения размера сразу
                    resizableElements[element].ShowResizeHandles();
                }

                foreach (var element in elementsToDeselect)
                {
                    selectionManager.DeselectElement(element);
                    RemoveResizableElement(element);
                }

                // Убеждаемся, что все выделенные элементы имеют видимые ручки
                // (включая те, которые были выделены ранее и остались выделенными)
                var allSelectedElements = selectionManager.SelectedElements.ToList();
                foreach (var element in allSelectedElements)
                {
                    if (element is FrameworkElement frameworkElement && resizableElements.ContainsKey(element))
                    {
                        resizableElements[element].ShowResizeHandles();
                    }
                }
            }
            finally
            {
                isUpdatingSelection = false;
            }
        }

        #endregion

        #region Управление ResizableElement

        private void CreateResizableElementsForSelected()
        {
            var selectedElements = selectionManager.SelectedElements.ToList();
            
            // Создаем ResizableElement для всех выделенных элементов и показываем ручки
            foreach (var element in selectedElements)
            {
                if (element is FrameworkElement frameworkElement)
                {
                    // Создаем ResizableElement, если его еще нет
                    // (они могут быть созданы через OnElementSelected в MainWindow)
                    if (!resizableElements.ContainsKey(element))
                    {
                        Func<Point, Point> snapFunction = null;
                        if (gridManager != null)
                        {
                            snapFunction = gridManager.SnapToGrid;
                        }

                        resizableElements[element] = new ResizableElementManager(
                            element,
                            canvas,
                            snapFunction,
                            gridManager?.IsSnapToGridEnabled ?? false);
                    }

                    // Показываем ручки изменения размера для всех выделенных элементов
                    resizableElements[element].ShowResizeHandles();
                }
            }

            // Скрываем ручки для элементов, которые больше не выделены
            var elementsToHide = resizableElements.Keys
                .Where(k => !selectedElements.Contains(k))
                .ToList();

            foreach (var element in elementsToHide)
            {
                if (resizableElements.ContainsKey(element))
                {
                    resizableElements[element].HideResizeHandles();
                }
            }
        }

        private void ClearAllResizableElements()
        {
            foreach (var resizableElement in resizableElements.Values)
            {
                resizableElement.HideResizeHandles();
            }
        }

        private void SelectElementDirectly(FrameworkElement element)
        {
            if (element == null) return;

            // Проверяем, не выделен ли уже элемент
            if (selectionManager.IsElementSelected(element)) return;

            // Используем SelectShape, который добавляет элемент в selectedElements
            // без очистки предыдущих выделений
            selectionManager.SelectShape(element);
        }

        private void RemoveResizableElement(UIElement element)
        {
            if (resizableElements.ContainsKey(element))
            {
                resizableElements[element].HideResizeHandles();
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
                // Для текста используем размеры текста, но избегаем Measure если возможно
                if (textBlock.ActualWidth > 0 && textBlock.ActualHeight > 0)
                {
                    width = textBlock.ActualWidth;
                    height = textBlock.ActualHeight;
                }
                else
                {
                    // Measure только если размеры еще не вычислены
                    textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    width = textBlock.DesiredSize.Width;
                    height = textBlock.DesiredSize.Height;
                }
            }
            else if (element is Polyline polyline && polyline.Points != null && polyline.Points.Count > 0)
            {
                // Для полилинии используем bounding box всех точек
                double minX = polyline.Points.Min(p => p.X);
                double minY = polyline.Points.Min(p => p.Y);
                double maxX = polyline.Points.Max(p => p.X);
                double maxY = polyline.Points.Max(p => p.Y);
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            return new Rect(left, top, width, height);
        }

        private bool IsSelectionRectangle(UIElement element)
        {
            return element == selectionRectangle;
        }

        private bool IsElementInNotation(UIElement element)
        {
            if (element == null) return false;

            // Быстрая проверка - если элемент не имеет родителя Canvas с именем NotationCanvas, пропускаем
            try
            {
                DependencyObject parent = VisualTreeHelper.GetParent(element);
                int depth = 0;
                while (parent != null && depth < 10) // Ограничиваем глубину поиска
                {
                    if (parent is Canvas canvas && canvas.Name == "NotationCanvas")
                        return true;
                    parent = VisualTreeHelper.GetParent(parent);
                    depth++;
                }
            }
            catch
            {
                // Игнорируем ошибки при обходе дерева
            }

            return false;
        }

        private bool IsGridElement(UIElement element)
        {
            // Проверяем, является ли элемент частью сетки
            // GridManager использует Rectangle для сетки
            if (element is Rectangle rect)
            {
                // Можно добавить дополнительную проверку, если нужно
                // Например, проверить Tag или другие свойства
            }
            return false;
        }

        #endregion

        #region Публичные свойства и методы

        public bool IsSelecting => isSelecting;

        public void CancelSelection()
        {
            if (isSelecting)
            {
                isSelecting = false;
                isMouseDownOnElement = false;
                canvas.ReleaseMouseCapture();
                selectionRectangle.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateSnapToGrid()
        {
            if (gridManager != null)
            {
                foreach (var resizableElement in resizableElements.Values)
                {
                    resizableElement.UpdateSnapToGrid(
                        gridManager.SnapToGrid,
                        gridManager.IsSnapToGridEnabled);
                }
            }
        }

        #endregion
    }
}
