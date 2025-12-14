using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    // Менеджер для управления всеми операциями с заливкой и обводкой
    public class FillStrokeManager
    {
        // Зависимости
        private SelectionManager selectionManager;
        private ToggleButton togglePanelsButton;
        
        // UI элементы
        private ComboBox fillColorComboBox;
        private ComboBox strokeColorComboBox;
        private ComboBox strokeWidthComboBox;
        private Border fillColorPreview;
        private Border strokeColorPreview;
        private Border fillPanel;
        private Border strokePanel;
        private Border strokeWidthPanel;

        public FillStrokeManager(
            SelectionManager selectionManager,
            ToggleButton togglePanelsButton,
            ComboBox fillColorComboBox,
            ComboBox strokeColorComboBox,
            ComboBox strokeWidthComboBox,
            Border fillColorPreview,
            Border strokeColorPreview,
            Border fillPanel,
            Border strokePanel,
            Border strokeWidthPanel)
        {
            this.selectionManager = selectionManager;
            this.togglePanelsButton = togglePanelsButton;
            this.fillColorComboBox = fillColorComboBox;
            this.strokeColorComboBox = strokeColorComboBox;
            this.strokeWidthComboBox = strokeWidthComboBox;
            this.fillColorPreview = fillColorPreview;
            this.strokeColorPreview = strokeColorPreview;
            this.fillPanel = fillPanel;
            this.strokePanel = strokePanel;
            this.strokeWidthPanel = strokeWidthPanel;
        }

        // Метод для показа/скрытия панели толщины обводки
        public void UpdateStrokeWidthPanelVisibility()
        {
            // Если все панели скрыты - не показываем панель ширины обводки
            if (togglePanelsButton.IsChecked == true)
            {
                strokeWidthPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement != null)
            {
                // Показываем панель толщины только для элементов, у которых есть обводка
                bool hasStroke = selectedElement is Ellipse || selectedElement is Line ||
                                selectedElement is Border || selectedElement is TextBlock;
                strokeWidthPanel.Visibility = hasStroke ? Visibility.Visible : Visibility.Collapsed;

                // Обновляем комбо-бокс толщины текущим значением
                UpdateStrokeWidthComboBoxFromShape(selectedElement);
            }
            else
            {
                strokeWidthPanel.Visibility = Visibility.Collapsed;
            }
        }

        // Обработчик изменения толщины обводки
        public void StrokeWidthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (strokeWidthComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string thicknessValue = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(thicknessValue))
                {
                    try
                    {
                        double thickness = double.Parse(thicknessValue);
                        ApplyStrokeThicknessToSelectedShape(thickness);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка применения толщины: {ex.Message}");
                    }
                }
            }
        }

        // Метод применения толщины обводки к выделенной фигуре
        private void ApplyStrokeThicknessToSelectedShape(double thickness)
        {
            var selectedElement = selectionManager?.SelectedElement;
            if (selectedElement != null)
            {
                ApplyStrokeThicknessToShape(selectedElement, thickness);

                // Сохраняем новую толщину как оригинальную
                selectionManager?.UpdateOriginalStrokeThickness(selectedElement, thickness);
                
                var selectedShape = selectionManager?.SelectedShape;
                if (selectedShape != null)
                {
                    selectionManager?.UpdateOriginalStrokeThickness(selectedShape, thickness);
                }
            }
        }

        // Вспомогательный метод для применения толщины обводки к конкретной фигуре
        public void ApplyStrokeThicknessToShape(UIElement shape, double thickness)
        {
            UpdateStrokeThicknessInAllReferences(shape, thickness);
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
            else if (shape is TextBlock textBlock)
            {
                // Для текста толщина не применяется
            }

            // Сохраняем толщину в словарь оригинальных толщин
            selectionManager?.UpdateOriginalStrokeThickness(shape, thickness);
        }

        // Метод обновления комбо-бокса толщины на основе фигуры
        private void UpdateStrokeWidthComboBoxFromShape(UIElement shape)
        {
            if (shape == null) return;

            double currentThickness = 0;

            if (shape is Ellipse ellipse)
            {
                currentThickness = ellipse.StrokeThickness;
            }
            else if (shape is Border border)
            {
                currentThickness = border.BorderThickness.Left;
            }
            else if (shape is Line line)
            {
                currentThickness = line.StrokeThickness;
            }

            // Обновляем комбо-бокс
            UpdateStrokeWidthComboBoxSelection(currentThickness);
        }

        // Вспомогательный метод для обновления комбо-бокса толщины
        public void UpdateStrokeWidthComboBoxSelection(double thickness)
        {
            foreach (ComboBoxItem item in strokeWidthComboBox.Items)
            {
                if (item.Tag != null && double.Parse(item.Tag as string) == thickness)
                {
                    strokeWidthComboBox.SelectedItem = item;
                    return;
                }
            }

            // Если точное значение не найдено, выбираем ближайшее
            strokeWidthComboBox.SelectedIndex = 0;
        }

        // Метод для принудительного обновления толщины во всех связанных элементах
        private void UpdateStrokeThicknessInAllReferences(UIElement element, double thickness)
        {
            // Обновляем толщину в основном элементе
            if (element is Ellipse ellipse)
            {
                ellipse.StrokeThickness = thickness;
            }
            else if (element is Border border)
            {
                border.BorderThickness = new Thickness(thickness);
            }
            else if (element is Line line)
            {
                line.StrokeThickness = thickness;
            }

            // Сохраняем в словарь
            selectionManager?.UpdateOriginalStrokeThickness(element, thickness);

            // Если это selectedElement, обновляем и в selectedShape
            var selectedElement = selectionManager?.SelectedElement;
            var selectedShape = selectionManager?.SelectedShape;
            if (selectedElement == element && selectedShape != null)
            {
                selectionManager?.UpdateOriginalStrokeThickness(selectedShape, thickness);
            }
        }

        // ОБРАБОТЧИК СОБЫТИЯ ДЛЯ COMBOBOX ЗАЛИВКИ
        public void FillColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fillColorComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string hexColor = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(hexColor))
                {
                    try
                    {
                        // Обновляем превью цвета
                        fillColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + hexColor));

                        // Применяем цвет к выделенной фигуре
                        ApplyColorToSelectedShape(hexColor);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка применения цвета: {ex.Message}");
                    }
                }
            }
        }

        // Метод применения цвета к выделенной фигуре
        private void ApplyColorToSelectedShape(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor) || selectionManager == null) return;

            Color newColor = (Color)ColorConverter.ConvertFromString("#" + hexColor);
            Brush newBrush = new SolidColorBrush(newColor);

            // Применяем цвет ко всем выделенным элементам
            foreach (var element in selectionManager.SelectedElements)
            {
                ApplyFillColorToShape(element, newBrush);
            }

            // Также применяем к selectedShape, если он не в selectedElements
            var selectedShape = selectionManager.SelectedShape;
            if (selectedShape != null && !selectionManager.IsElementSelected(selectedShape))
            {
                ApplyFillColorToShape(selectedShape, newBrush);
            }
        }

        // Вспомогательный метод для применения цвета заливки к конкретной фигуре
        public void ApplyFillColorToShape(UIElement shape, Brush brush)
        {
            if (shape is Ellipse ellipse)
            {
                // Для эллипсов меняем Fill (заливку), только если это не кольцо
                if (ellipse.Fill != Brushes.Transparent)
                {
                    ellipse.Fill = brush;
                    // Сохраняем новый цвет как оригинальный
                    selectionManager?.UpdateOriginalFillColor(shape, brush);
                }
            }
            else if (shape is Border border)
            {
                // Для Border меняем Background (заливку)
                border.Background = brush;
                // Сохраняем новый цвет как оригинальный
                selectionManager?.UpdateOriginalFillColor(shape, brush);
            }
            // Для Line и TextBlock заливка не применяется
        }

        // ОБРАБОТЧИК СОБЫТИЯ ДЛЯ COMBOBOX ОБВОДКИ
        public void StrokeColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (strokeColorComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string hexColor = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(hexColor))
                {
                    try
                    {
                        // Обновляем превью цвета обводки
                        strokeColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + hexColor));

                        // Применяем цвет обводки к выделенной фигуре
                        ApplyStrokeColorToSelectedShape(hexColor);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка применения цвета обводки: {ex.Message}");
                    }
                }
            }
        }

        // Метод применения цвета обводки к выделенной фигуре
        private void ApplyStrokeColorToSelectedShape(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor) || selectionManager == null) return;

            Color newColor = (Color)ColorConverter.ConvertFromString("#" + hexColor);
            Brush newBrush = new SolidColorBrush(newColor);

            // Применяем цвет обводки ко всем выделенным элементам
            foreach (var element in selectionManager.SelectedElements)
            {
                ApplyStrokeColorToShape(element, newBrush);
            }

            // Также применяем к selectedShape, если он не в selectedElements
            var selectedShape = selectionManager.SelectedShape;
            if (selectedShape != null && !selectionManager.IsElementSelected(selectedShape))
            {
                ApplyStrokeColorToShape(selectedShape, newBrush);
            }
        }

        // Вспомогательный метод для применения цвета обводки к конкретной фигуре
        public void ApplyStrokeColorToShape(UIElement shape, Brush brush)
        {
            if (shape is Ellipse ellipse)
            {
                // Для эллипсов меняем Stroke (контур)
                ellipse.Stroke = brush;
            }
            else if (shape is Border border)
            {
                // Для Border меняем BorderBrush (рамку)
                border.BorderBrush = brush;
            }
            else if (shape is Line line)
            {
                // Для линии меняем Stroke (цвет линии)
                line.Stroke = brush;
            }
            else if (shape is TextBlock textBlock)
            {
                // Для текста меняем Foreground (цвет текста)
                textBlock.Foreground = brush;
            }
            // Сохраняем новый цвет как оригинальный
            selectionManager?.UpdateOriginalStrokeColor(shape, brush);
        }

        // Вспомогательный метод для обновления комбо-бокса
        public void UpdateColorComboBoxSelection(ComboBox comboBox, string hexColor)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag as string == hexColor)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void UpdateComboBoxSelection(ComboBox comboBox, string hexColor)
        {
            UpdateColorComboBoxSelection(comboBox, hexColor);
        }

        // Метод обновления комбо-боксов на основе цветов фигуры
        public void UpdateColorComboBoxesFromShape(UIElement shape)
        {
            if (shape == null) return;

            Brush fillBrush = null;
            Brush strokeBrush = null;

            // Используем оригинальные цвета если они сохранены
            fillBrush = selectionManager?.GetOriginalFillColor(shape);
            strokeBrush = selectionManager?.GetOriginalStrokeColor(shape);

            // Если оригинальные цвета не сохранены, берем текущие
            if (fillBrush == null)
            {
                if (shape is Ellipse ellipse)
                {
                    fillBrush = ellipse.Fill;
                }
                else if (shape is Border border)
                {
                    fillBrush = border.Background;
                }
            }

            if (strokeBrush == null)
            {
                if (shape is Ellipse ellipse)
                {
                    strokeBrush = ellipse.Stroke;
                }
                else if (shape is Border border)
                {
                    strokeBrush = border.BorderBrush;
                }
                else if (shape is Line line)
                {
                    strokeBrush = line.Stroke;
                }
                else if (shape is TextBlock textBlock)
                {
                    strokeBrush = textBlock.Foreground;
                }
            }

            // Обновляем комбо-бокс заливки
            if (fillBrush is SolidColorBrush fillSolidBrush && fillBrush != Brushes.Transparent)
            {
                string fillHexColor = fillSolidBrush.Color.ToString().Substring(3);
                UpdateComboBoxSelection(fillColorComboBox, fillHexColor);
            }

            // Обновляем комбо-бокс обводки
            if (strokeBrush is SolidColorBrush strokeSolidBrush)
            {
                string strokeHexColor = strokeSolidBrush.Color.ToString().Substring(3);
                UpdateComboBoxSelection(strokeColorComboBox, strokeHexColor);
            }
        }

        // Метод для очистки комбо-боксов цветов
        public void ClearColorComboBoxes()
        {
            fillColorComboBox.SelectedIndex = -1;
            strokeColorComboBox.SelectedIndex = -1;
            fillColorPreview.Background = Brushes.Transparent;
            strokeColorPreview.Background = Brushes.Transparent;
        }
    }
}

