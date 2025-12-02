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
            return @"Graphic Editor — это минималистичное и сфокусированное приложение для создания профессиональных диаграмм в нотации UML Activity. Следуя принципу «только диаграммы, ничего лишнего», оно предоставляет все необходимые инструменты для эффективного моделирования процессов без перегруженного интерфейса.
Основные Возможности
•	Создание Диаграмм: Быстрое построение UML Activity-диаграмм с использованием интуитивно понятного графического редактора.
•	Автосохранение: Ваша работа автоматически сохраняется, чтобы предотвратить потерю данных.
•	Масштабирование: Используйте пункт меню «Вид» и пункт «Увеличить», для детального просмотра крупных диаграмм, или «Уменьшить».
•	Упорядочивание: Объединяйте объекты в группы, с помощью возможности выделения нескольких объектов, и управляйте их порядком отображения.
•	Отмена и Повтор Действий: Легко исправляйте ошибки с помощью пункта «Отменить» во вкладке «Вид».
________________________________________
Панель Инструментов и Элементы
Интерфейс разделен на панель инструментов и рабочую область.
1. Фигуры 
•	Старт/Конец: Овал для обозначения начального и конечного узлов.
•	Действие: Прямоугольник со скругленными углами.
•	Решение: Ромб для условных ветвлений.
•	Бар Синхронизации: Толстая горизонтальная линия для обозначения параллельных потоков.
•	Другие фигуры: Прямоугольник, круг, квадрат
Толщину обводки фигуры можно менять в меню, которое появляется после взаимодействия с фигурой.
2. Стрелки
•	Сплошная стрелка
Режим начертания стрелок можно активировать с помощью кнопки «Перо», которая находится на нижней панели.
3. Инструменты Редактирования
•	Текст: Добавляйте подписи к фигурам и стрелкам. 
Возможность добавить текст, появляется после выбора на нижней панели режима «Текст». После создания текстового блока его можно отредактировать с помощью меню, которое появляется справа. Редактор поддерживает возможность менять шрифт, написание и размер текста. Для повторного редактирования необходимо дважды щелкнуть на текстовый блок.
•	Заливка: Изменяйте цвет фона выбранных фигур.
Блок «Заливка» позволяет менять цвет выбранного объекта.
•	Контур: Изменяйте цвет границ фигур. 
В блоке «Обводка» можно задать цвет контура фигуры и цвет текста.
•	Комментарии: Добавляйте комментарии в свою диаграмму.
Включение режима «Комментарий» (находится на нижней панели), позволяет создать текстовый блок, помеченный красной точкой. После завершения редактирования текст скрывается. Наведя курсор на точку, текст можно увидеть. Комментарии не сохраняются при экспорте диаграммы.
•	Упорядочивание:
o	На Передний План: Перемещает выбранный объект поверх всех остальных.
o	На Задний План: Перемещает выбранный объект под все остальные.
•	Группировка: Выделите несколько объектов. Это позволяет перемещать и форматировать их как единое целое.
•	Сетка: 
Активировать режим сетки можно в соответствующем меню вкладки «Вид». «Привязка к сетке» дает возможность располагать фигуры четко по сетке.
________________________________________
Работа с Приложением
Создание новой диаграммы:
1.	Выберите нужный элемент на панели инструментов.
2.	Перенесите или кликните на рабочей области, чтобы разместить его.
3.	Для соединения фигур выберите инструмент «Стрелка», кликните на начальной фигуре и, не отпуская кнопку мыши, протяните до конечной фигуры.
Редактирование элементов:
1.	Выделение: Кликните на объект. 
2.	Изменение текста: Двойной клик тексту активирует режим редактирования текста.
3.	Изменение размера: Перетаскивайте углы или границы выбранной фигуры.
4.	Форматирование: Выделите объект и используйте панель свойств для смены цвета заливки, цвета контура и других параметров.
Управление диаграммой:
•	Масштабирование: Используйте кнопки «Увеличить» или «Уменьшить» для работы с масштабом.
•	Отмена: Во вкладке «Правка» выберите пункт «Отменить».
________________________________________
Файловые Операции
•	Сохранение: В меню «Файл» выберите «Сохранить» или «Сохранить как...». Вы можете экспортировать диаграмму в формат PNG с помощью пункта «Экспорт в изображение..».
•	Открытие: «Файл» -> «Открыть...» для загрузки ранее сохраненного проекта.
•	Автосохранение: Приложение автоматически сохраняет черновик вашей работы. 
________________________________________
Горячие Клавиши (Сочетания Клавиш)
Для ускорения работы используйте сочетания клавиш:
•	Ctrl + Z: Отменить последнее действие.
•	Ctrl + Y: Повторить отмененное действие.
•	Ctrl + X: Вырезать фигуру.
•	Ctrl + С: Копировать фигуру.
•	Ctrl + V:Вставить фигуру.
•	Delete: Удалить выбранные объекты.
________________________________________
Советы по Эффективной Работе
1.	Используйте Группировку: При работе со сложными фрагментами диаграммы группируйте связанные элементы для удобства перемещения и копирования.
2.	""Умные"" Соединения: Не бойтесь перемещать фигуры после соединения — стрелки останутся прикрепленными, что сохранит целостность диаграммы.
3.	Выделение Цветом: Используйте красный контур или другой цвет заливки, чтобы визуально выделить критически важные шаги или проблемные зоны на диаграмме.
4.	Ломаные Стрелки: Для создания сложных маршрутов используйте ломаные линии. 
Graphic Editor — ваш надежный инструмент для чистого и быстрого моделирования бизнес-процессов и рабочих потоков. Приятного использования!

";
        }
    }
}
