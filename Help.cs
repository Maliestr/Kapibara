using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Graphic_Editor
{
    public static class HelpSystem
    {
        public static void ShowHelp()
        {
            var helpWindow = new Window
            {
                Title = "",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,

                // Полностью убираем стандартные элементы
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
            };

            // Основной контейнер с закругленными углами
            var mainBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 37)), // #252525
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Кастомный заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Контент
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Кнопка закрыть

            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(66, 66, 66)), 
                Height = 40,
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(8, 8, 0, 0)
            };

            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Логотип
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Заголовок
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Кнопка закрытия

            // ЗАГОЛОВОК
            var titleText = new TextBlock
            {
                Text = " Справка по приложению",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(titleText, 1);
            titleGrid.Children.Add(titleText);

            // КНОПКА ЗАКРЫТИЯ В ЗАГОЛОВКЕ
            var closeButton = new Button
            {
                Content = "✕",
                Width = 40,
                Height = 40,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            closeButton.MouseEnter += (s, e) =>
            {
                closeButton.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35));
            };

            closeButton.MouseLeave += (s, e) =>
            {
                closeButton.Background = Brushes.Transparent;
            };

            closeButton.Click += (s, e) => helpWindow.Close();

            Grid.SetColumn(closeButton, 2);
            titleGrid.Children.Add(closeButton);

            titleBar.Child = titleGrid;
            Grid.SetRow(titleBar, 0);

            var contentBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 37)), 
                Padding = new Thickness(0)
            };

            var contentScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.Transparent,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0)
            };

            var contentText = new TextBlock
            {
                Text = GetHelpText(),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)), 
                Background = Brushes.Transparent,
                FontSize = 14,
                FontFamily = new FontFamily("Segoe UI"),
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(25, 20, 25, 20),
                LineHeight = 22
            };

            contentScroll.Content = contentText;
            contentBorder.Child = contentScroll;
            Grid.SetRow(contentBorder, 1);

            var bottomPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(66, 66, 66)), 
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Height = 70,
                CornerRadius = new CornerRadius(0, 0, 8, 8)
            };

            var bottomCloseButton = new Button
            {
                Content = "Закрыть",
                Width = 120,
                Height = 35,
                Margin = new Thickness(0, 0, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,

                // СЕРАЯ КНОПКА
                Background = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontSize = 14,
                FontWeight = FontWeights.Medium
            };

            bottomCloseButton.MouseEnter += (s, e) =>
            {
                bottomCloseButton.Background = new SolidColorBrush(Color.FromRgb(140, 140, 140));
            };

            bottomCloseButton.MouseLeave += (s, e) =>
            {
                bottomCloseButton.Background = new SolidColorBrush(Color.FromRgb(120, 120, 120));
            };

            bottomCloseButton.Click += (s, e) => helpWindow.Close();

            bottomPanel.Child = bottomCloseButton;
            Grid.SetRow(bottomPanel, 2);

            // Собираем все вместе
            mainGrid.Children.Add(titleBar);
            mainGrid.Children.Add(contentBorder);
            mainGrid.Children.Add(bottomPanel);

            mainBorder.Child = mainGrid;
            helpWindow.Content = mainBorder;

            // Добавляем возможность перетаскивания за заголовок
            titleBar.MouseLeftButtonDown += (s, e) =>
            {
                helpWindow.DragMove();
            };

            helpWindow.ShowDialog();
        }

        private static string GetHelpText()
        {
            return @"";
        }
    }
}
