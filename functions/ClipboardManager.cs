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
    // Перенесено из MainWindow.xaml.cs
    public enum UndoActionType
    {
        Add,    // Элемент был добавлен
        Remove  // Элемент был удален
    }

    // Перенесено из MainWindow.xaml.cs
    public class UndoAction
    {
        public UndoActionType ActionType { get; set; }
        public UIElement Element { get; set; } // Копия элемента для восстановления
        public UIElement OriginalElement { get; set; } // Ссылка на оригинальный элемент (для Remove)

        public UndoAction(UndoActionType actionType, UIElement element, UIElement originalElement = null)
        {
            ActionType = actionType;
            Element = element;
            OriginalElement = originalElement;
        }
    }

    public class ClipboardManager
    {
        // События для обновления интерфейса
        public event Action SelectionChanged;
        public event Action UndoRedoStackChanged;

        // Приватные поля
        private Canvas mainCanvas;
        private SelectionManager selectionManager;
        private Dictionary<UIElement, ResizableElement> resizableElements;
        private GridManager gridManager;
        private Stack<UndoAction> undoStack = new Stack<UndoAction>();
        private Stack<UndoAction> redoStack = new Stack<UndoAction>();
        private FrameworkElement copiedElement;

        public ClipboardManager(
            Canvas mainCanvas,
            SelectionManager selectionManager,
            Dictionary<UIElement, ResizableElement> resizableElements,
            GridManager gridManager)
        {
            this.mainCanvas = mainCanvas;
            this.selectionManager = selectionManager;
            this.resizableElements = resizableElements;
            this.gridManager = gridManager;
        }

        #region Основные операции с буфером обмена

        // Копирование выделенного элемента
        public void CopySelectedElement()
        {
            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement != null)
            {
                copiedElement = CloneUIElement(selectedElement);
                SelectionChanged?.Invoke();
            }
        }

        // Вырезание выделенного элемента
        public void CutSelectedElement()
        {
            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement != null)
            {
                // Сохраняем копию для буфера обмена
                copiedElement = CloneUIElement(selectedElement);

                // Сохраняем в стек отмены как удаление
                SaveToUndoStack(selectedElement, UndoActionType.Remove);

                // Удаляем ResizableElement если есть
                if (resizableElements.ContainsKey(selectedElement))
                {
                    resizableElements[selectedElement].Dispose();
                    resizableElements.Remove(selectedElement);
                }

                // Удаляем элемент
                mainCanvas.Children.Remove(selectedElement);

                selectionManager.DeselectAll();
                SelectionChanged?.Invoke();
            }
        }

        // Вставка элемента
        public void PasteElement()
        {
            if (copiedElement != null)
            {
                // Создаем копию скопированного элемента со смещением
                UIElement newElement = CloneUIElement(copiedElement);

                // Смещаем новую копию на 20 пикселей
                double currentLeft = Canvas.GetLeft(newElement);
                double currentTop = Canvas.GetTop(newElement);

                double newLeft = currentLeft + 20;
                double newTop = currentTop + 20;

                // Применяем привязку к сетке если включена
                if (gridManager.IsSnapToGridEnabled)
                {
                    Point snappedPoint = gridManager.SnapToGrid(new Point(newLeft, newTop));
                    newLeft = snappedPoint.X;
                    newTop = snappedPoint.Y;
                }

                Canvas.SetLeft(newElement, newLeft);
                Canvas.SetTop(newElement, newTop);

                // Добавляем на холст
                mainCanvas.Children.Add(newElement);

                // Перемещаем на передний план
                if (newElement is FrameworkElement frameworkElement)
                {
                    BringToFront(frameworkElement);
                }

                // Создаем ResizableElement для нового элемента
                resizableElements[newElement] = new ResizableElement(
                    newElement,
                    mainCanvas,
                    gridManager.SnapToGrid,
                    gridManager.IsSnapToGridEnabled);

                // Выделяем вставленный элемент
                selectionManager.SelectElement(newElement as FrameworkElement);

                // Сохраняем в стек отмены
                SaveToUndoStack(newElement);

                SelectionChanged?.Invoke();
                UndoRedoStackChanged?.Invoke();
            }
        }

        // Удаление выделенного элемента
        public void DeleteSelectedElement()
        {
            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement != null)
            {
                // Сохраняем в стек отмены как удаление
                SaveToUndoStack(selectedElement, UndoActionType.Remove);

                // Удаляем ResizableElement если есть
                if (resizableElements.ContainsKey(selectedElement))
                {
                    resizableElements[selectedElement].Dispose();
                    resizableElements.Remove(selectedElement);
                }

                mainCanvas.Children.Remove(selectedElement);

                selectionManager.DeselectAll();
                SelectionChanged?.Invoke();
                UndoRedoStackChanged?.Invoke();
            }
        }

        // Вырезание нескольких элементов
        public void CutSelectedElements()
        {
            var selectedElements = selectionManager?.SelectedElements.ToList() ?? new List<UIElement>();
            if (selectedElements.Count > 0)
            {
                foreach (var element in selectedElements)
                {
                    // Сохраняем в стек отмены как удаление
                    SaveToUndoStack(element, UndoActionType.Remove);

                    // Удаляем ResizableElement если есть
                    if (resizableElements.ContainsKey(element))
                    {
                        resizableElements[element].Dispose();
                        resizableElements.Remove(element);
                    }

                    // Удаляем элемент
                    mainCanvas.Children.Remove(element);
                }

                selectionManager.ClearSelection();
                SelectionChanged?.Invoke();
                UndoRedoStackChanged?.Invoke();
            }
        }

        #endregion

        #region Система отмены/повтора

        // Отмена последнего действия
        public void UndoLastAction()
        {
            if (undoStack.Count > 0)
            {
                selectionManager.DeselectAll();
                UndoAction action = undoStack.Pop();

                if (action.ActionType == UndoActionType.Add)
                {
                    // Отменяем добавление - удаляем элемент
                    UIElement elementToRemove = action.OriginalElement ?? FindElementByClone(action.Element);
                    if (elementToRemove != null && mainCanvas.Children.Contains(elementToRemove))
                    {
                        // Удаляем ResizableElement если есть
                        if (resizableElements.ContainsKey(elementToRemove))
                        {
                            resizableElements[elementToRemove].Dispose();
                            resizableElements.Remove(elementToRemove);
                        }

                        mainCanvas.Children.Remove(elementToRemove);

                        // Сохраняем действие для повтора
                        redoStack.Push(new UndoAction(UndoActionType.Remove, action.Element, elementToRemove));
                    }
                }
                else if (action.ActionType == UndoActionType.Remove)
                {
                    // Отменяем удаление - восстанавливаем элемент
                    UIElement elementToRestore = CloneUIElement(action.Element);

                    // Восстанавливаем позицию
                    double left = Canvas.GetLeft(action.Element);
                    double top = Canvas.GetTop(action.Element);
                    if (!double.IsNaN(left)) Canvas.SetLeft(elementToRestore, left);
                    if (!double.IsNaN(top)) Canvas.SetTop(elementToRestore, top);

                    mainCanvas.Children.Add(elementToRestore);

                    // Создаем ResizableElement для восстановленного элемента
                    resizableElements[elementToRestore] = new ResizableElement(
                        elementToRestore,
                        mainCanvas,
                        gridManager.SnapToGrid,
                        gridManager.IsSnapToGridEnabled);

                    RestoreOriginalAppearance(elementToRestore);

                    // Сохраняем действие для повтора
                    redoStack.Push(new UndoAction(UndoActionType.Add, action.Element, elementToRestore));
                }

                SelectionChanged?.Invoke();
                UndoRedoStackChanged?.Invoke();
            }
        }

        // Повтор последнего действия
        public void RedoLastAction()
        {
            if (redoStack.Count > 0)
            {
                selectionManager.DeselectAll();
                UndoAction action = redoStack.Pop();

                if (action.ActionType == UndoActionType.Add)
                {
                    // Повторяем добавление - добавляем элемент
                    UIElement elementToAdd = CloneUIElement(action.Element);

                    // Восстанавливаем позицию
                    double left = Canvas.GetLeft(action.Element);
                    double top = Canvas.GetTop(action.Element);
                    if (!double.IsNaN(left)) Canvas.SetLeft(elementToAdd, left);
                    if (!double.IsNaN(top)) Canvas.SetTop(elementToAdd, top);

                    mainCanvas.Children.Add(elementToAdd);

                    // Создаем ResizableElement для добавленного элемента
                    resizableElements[elementToAdd] = new ResizableElement(
                        elementToAdd,
                        mainCanvas,
                        gridManager.SnapToGrid,
                        gridManager.IsSnapToGridEnabled);

                    RestoreOriginalAppearance(elementToAdd);

                    // Сохраняем действие для отмены
                    undoStack.Push(new UndoAction(UndoActionType.Add, action.Element, elementToAdd));
                }
                else if (action.ActionType == UndoActionType.Remove)
                {
                    // Повторяем удаление - удаляем элемент
                    UIElement elementToRemove = action.OriginalElement ?? FindElementByClone(action.Element);
                    if (elementToRemove != null && mainCanvas.Children.Contains(elementToRemove))
                    {
                        // Удаляем ResizableElement если есть
                        if (resizableElements.ContainsKey(elementToRemove))
                        {
                            resizableElements[elementToRemove].Dispose();
                            resizableElements.Remove(elementToRemove);
                        }

                        mainCanvas.Children.Remove(elementToRemove);

                        // Сохраняем действие для отмены
                        undoStack.Push(new UndoAction(UndoActionType.Remove, action.Element, elementToRemove));
                    }
                }

                SelectionChanged?.Invoke();
                UndoRedoStackChanged?.Invoke();
            }
        }

        // Сохранение в стек отмены
        public void SaveToUndoStack(UIElement element, UndoActionType actionType = UndoActionType.Add)
        {
            if (element == null) return;

            UIElement elementCopy = CloneUIElement(element);
            SaveOriginalColorsForCopy(element, elementCopy);

            // Сохраняем действие с указанием типа и ссылкой на оригинальный элемент
            UndoAction action = new UndoAction(actionType, elementCopy, element);
            undoStack.Push(action);

            // Очищаем стек повтора при новом действии
            redoStack.Clear();

            UndoRedoStackChanged?.Invoke();
        }

        // Проверка доступности отмены
        public bool CanUndo => undoStack.Count > 0;

        // Проверка доступности повтора
        public bool CanRedo => redoStack.Count > 0;

        // Очистка стеков
        public void ClearStacks()
        {
            undoStack.Clear();
            redoStack.Clear();
            UndoRedoStackChanged?.Invoke();
        }

        #endregion

        #region Вспомогательные методы

        // Клонирование UI элемента
        private FrameworkElement CloneUIElement(UIElement original)
        {
            if (original is Ellipse originalEllipse)
            {
                var newEllipse = new Ellipse
                {
                    Width = originalEllipse.Width,
                    Height = originalEllipse.Height,
                    Fill = originalEllipse.Fill,
                    Stroke = originalEllipse.Stroke,
                    StrokeThickness = originalEllipse.StrokeThickness,
                    Cursor = Cursors.Hand
                };

                Canvas.SetLeft(newEllipse, Canvas.GetLeft(originalEllipse));
                Canvas.SetTop(newEllipse, Canvas.GetTop(originalEllipse));

                return newEllipse;
            }
            else if (original is Border originalBorder)
            {
                var newBorder = new Border
                {
                    Width = originalBorder.Width,
                    Height = originalBorder.Height,
                    Background = originalBorder.Background,
                    BorderBrush = originalBorder.BorderBrush,
                    BorderThickness = originalBorder.BorderThickness,
                    CornerRadius = originalBorder.CornerRadius,
                    Cursor = Cursors.Hand
                };

                // Копируем трансформации
                if (originalBorder.RenderTransform is RotateTransform originalRotate)
                {
                    newBorder.RenderTransform = new RotateTransform(originalRotate.Angle);
                    newBorder.RenderTransformOrigin = new Point(0.5, 0.5);
                }

                // Копируем позицию
                Canvas.SetLeft(newBorder, Canvas.GetLeft(originalBorder));
                Canvas.SetTop(newBorder, Canvas.GetTop(originalBorder));

                return newBorder;
            }
            else if (original is Line originalLine)
            {
                var newLine = new Line
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

                // Копируем трансформации
                if (originalLine.RenderTransform is RotateTransform originalRotate)
                {
                    newLine.RenderTransform = new RotateTransform(originalRotate.Angle);
                }
                return newLine;
            }
            else if (original is TextBlock originalText)
            {
                var newText = new TextBlock
                {
                    Text = originalText.Text,
                    FontFamily = originalText.FontFamily,
                    FontSize = originalText.FontSize,
                    FontStyle = originalText.FontStyle,
                    FontWeight = originalText.FontWeight,
                    Foreground = originalText.Foreground,
                    Background = originalText.Background,
                    Cursor = Cursors.Hand
                };

                Canvas.SetLeft(newText, Canvas.GetLeft(originalText));
                Canvas.SetTop(newText, Canvas.GetTop(originalText));

                return newText;
            }
            else if (original is Polyline originalArrow)
            {
                var newArrow = new Polyline
                {
                    Stroke = originalArrow.Stroke,
                    StrokeThickness = originalArrow.StrokeThickness,
                    StrokeLineJoin = originalArrow.StrokeLineJoin,
                    StrokeStartLineCap = originalArrow.StrokeStartLineCap,
                    StrokeEndLineCap = originalArrow.StrokeEndLineCap,
                    Points = new PointCollection(originalArrow.Points),
                    Cursor = Cursors.Hand
                };

                return newArrow;
            }
            return null;
        }

        // Поиск элемента на холсте по его копии
        private UIElement FindElementByClone(UIElement clone)
        {
            double cloneLeft = Canvas.GetLeft(clone);
            double cloneTop = Canvas.GetTop(clone);

            foreach (UIElement element in mainCanvas.Children)
            {
                if (element.GetType() == clone.GetType())
                {
                    double elemLeft = Canvas.GetLeft(element);
                    double elemTop = Canvas.GetTop(element);

                    if (Math.Abs(elemLeft - cloneLeft) < 0.1 && Math.Abs(elemTop - cloneTop) < 0.1)
                    {
                        return element;
                    }
                }
            }
            return null;
        }

        // Сохранение оригинальных цветов для копии элемента
        private void SaveOriginalColorsForCopy(UIElement original, UIElement copy)
        {
            if (original == null || copy == null) return;

            selectionManager?.CopyOriginalStyles(original, copy);

            if (original is Ellipse originalEllipse && copy is Ellipse copyEllipse)
            {
                if (!selectionManager?.HasOriginalStrokeThickness(copy) ?? true)
                {
                    selectionManager?.SetOriginalStyles(copy, originalEllipse.Stroke, originalEllipse.Fill, originalEllipse.StrokeThickness);
                }
            }
            else if (original is Border originalBorder && copy is Border copyBorder)
            {
                if (!selectionManager?.HasOriginalStrokeThickness(copy) ?? true)
                {
                    selectionManager?.SetOriginalStyles(copy, originalBorder.BorderBrush, originalBorder.Background, originalBorder.BorderThickness.Left);
                }
            }
            else if (original is Line originalLine && copy is Line copyLine)
            {
                if (!selectionManager?.HasOriginalStrokeThickness(copy) ?? true)
                {
                    selectionManager?.SetOriginalStyles(copy, originalLine.Stroke, null, originalLine.StrokeThickness);
                }
            }
            else if (original is TextBlock originalText && copy is TextBlock copyText)
            {
                if (selectionManager?.GetOriginalStrokeColor(copy) == null)
                {
                    selectionManager?.UpdateOriginalStrokeColor(copy, originalText.Foreground);
                }
            }
        }

        // Восстановление оригинального вида элемента
        private void RestoreOriginalAppearance(UIElement element)
        {
            if (element == null) return;

            if (element is Ellipse ellipse)
            {
                // Возвращаем сохраненный цвет обводки
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    ellipse.Stroke = strokeColor;
                }
                else
                {
                    ellipse.Stroke = Brushes.Black;
                }

                // Возвращаем сохраненную толщину
                double thickness = selectionManager?.GetOriginalStrokeThickness(element) ?? 0;
                if (thickness > 0)
                {
                    ellipse.StrokeThickness = thickness;
                }
                else
                {
                    ellipse.StrokeThickness = 3;
                }
                ellipse.Opacity = 1.0;
            }
            else if (element is Border border)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    border.BorderBrush = strokeColor;
                }
                else
                {
                    border.BorderBrush = Brushes.White;
                }

                // Возвращаем сохраненную толщину
                double thickness = selectionManager?.GetOriginalStrokeThickness(element) ?? 0;
                if (thickness > 0)
                {
                    border.BorderThickness = new Thickness(thickness);
                }
                else
                {
                    border.BorderThickness = new Thickness(2);
                }
                border.Opacity = 1.0;
            }
            else if (element is Line line)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    line.Stroke = strokeColor;
                }
                else
                {
                    line.Stroke = Brushes.White;
                }

                // Возвращаем сохраненную толщину
                double thickness = selectionManager?.GetOriginalStrokeThickness(element) ?? 0;
                if (thickness > 0)
                {
                    line.StrokeThickness = thickness;
                }
                else
                {
                    line.StrokeThickness = 3;
                }
                line.Opacity = 1.0;
            }
            else if (element is TextBlock textBlock)
            {
                var strokeColor = selectionManager?.GetOriginalStrokeColor(element);
                if (strokeColor != null)
                {
                    textBlock.Foreground = strokeColor;
                }
                textBlock.Background = Brushes.Transparent;
                textBlock.Opacity = 1.0;
            }

            Canvas.SetZIndex(element, 1);
        }

        // Перенести элемент на передний план
        private void BringToFront(FrameworkElement element)
        {
            if (element == null || !mainCanvas.Children.Contains(element)) return;

            int maxZIndex = 0;
            foreach (UIElement child in mainCanvas.Children)
            {
                int zIndex = Canvas.GetZIndex(child);
                if (zIndex > maxZIndex)
                {
                    maxZIndex = zIndex;
                }
            }

            Canvas.SetZIndex(element, maxZIndex + 1);
            mainCanvas.Children.Remove(element);
            mainCanvas.Children.Add(element);
        }

        #endregion
    }
}