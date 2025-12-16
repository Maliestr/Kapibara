using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    // Менеджер для управления созданием, редактированием и удалением комментариев на холсте
    public class CommentManager
    {
        #region Поля

        private Canvas _mainCanvas;
        private bool _isCommentModeActive = false;
        private List<Comment> _comments = new List<Comment>();
        private Button _commentButton;

        #endregion

        #region Свойства

        public bool IsCommentModeActive => _isCommentModeActive;
        public List<Comment> Comments => _comments;

        #endregion

        #region События

        public event Action<bool> CommentModeChanged;
        public event Action<Comment> CommentCreated;
        public event Action<Comment> CommentDeleted;

        #endregion

        #region Вспомогательные классы

        // Класс для представления комментария на холсте
        public class Comment
        {
            public UIElement VisualElement { get; set; }
            public UIElement TextElement { get; set; }
            public string Text { get; set; }
            public Point Position { get; set; }
        }

        // Класс для сериализации данных комментария при сохранении в файл
        public class CommentData
        {
            public double PositionX { get; set; }
            public double PositionY { get; set; }
            public string Text { get; set; }
        }

        #endregion

        #region Конструктор и инициализация

        public CommentManager(Canvas mainCanvas, Button commentButton)
        {
            _mainCanvas = mainCanvas ?? throw new ArgumentNullException(nameof(mainCanvas));
            _commentButton = commentButton ?? throw new ArgumentNullException(nameof(commentButton));

            InitializeCommentButton();
        }

        private void InitializeCommentButton()
        {
            _commentButton.Click += CommentButton_Click;
        }

        #endregion

        #region Управление режимом комментариев

        private void CommentButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleCommentMode();
        }

        // Переключение режима создания комментариев
        public void ToggleCommentMode()
        {
            _isCommentModeActive = !_isCommentModeActive;

            if (_isCommentModeActive)
            {
                _mainCanvas.Cursor = Cursors.Hand;
                _commentButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3939"));
            }
            else
            {
                _mainCanvas.Cursor = Cursors.Arrow;
                _commentButton.Background = Brushes.Transparent;
            }

            CommentModeChanged?.Invoke(_isCommentModeActive);
        }

        #endregion

        #region Создание комментариев

        // Создание нового комментария в указанной позиции
        public Comment CreateCommentAtPosition(Point position)
        {
            // Создаем визуальный элемент комментария
            var commentIcon = new Border
            {
                Width = 12,
                Height = 12,
                Background = Brushes.Red,
                BorderBrush = Brushes.Red,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand
            };

            // Создаем TextBlock для отображения текста
            var commentTextBlock = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(4),
                Child = new TextBlock
                {
                    Text = "",
                    Foreground = Brushes.Black,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 250
                },
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false // Чтобы не мешал взаимодействию
            };

            // Устанавливаем позицию
            Canvas.SetLeft(commentIcon, position.X - 6);
            Canvas.SetTop(commentIcon, position.Y - 6);

            // Позиционируем TextBlock рядом с иконкой
            Canvas.SetLeft(commentTextBlock, position.X + 15);
            Canvas.SetTop(commentTextBlock, position.Y - 10);

            // Добавляем на холст
            _mainCanvas.Children.Add(commentIcon);
            _mainCanvas.Children.Add(commentTextBlock);

            // Создаем комментарий
            var comment = new Comment
            {
                VisualElement = commentIcon,
                TextElement = commentTextBlock,
                Position = position,
                Text = ""
            };

            _comments.Add(comment);

            SetupCommentEventHandlers(comment);
            CommentCreated?.Invoke(comment);

            return comment;
        }

        #endregion

        #region Обработчики событий

        private void SetupCommentEventHandlers(Comment comment)
        {
            var commentIcon = comment.VisualElement as Border;
            var commentTextBlock = comment.TextElement as Border;

            if (commentIcon == null || commentTextBlock == null) return;

            // Обработчик двойного клика
            commentIcon.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    EditComment(comment);
                    e.Handled = true;
                }
            };

            // Показ текста при наведении
            commentIcon.MouseEnter += (s, e) =>
            {
                commentIcon.Opacity = 0.7;

                if (!string.IsNullOrEmpty(comment.Text))
                {
                    if (commentTextBlock.Child is TextBlock textBlock)
                    {
                        textBlock.Text = comment.Text;
                    }
                    commentTextBlock.Visibility = Visibility.Visible;
                    Canvas.SetZIndex(commentTextBlock, 1000); // Поверх других элементов
                }
            };

            commentIcon.MouseLeave += (s, e) =>
            {
                commentIcon.Opacity = 1.0;
                commentTextBlock.Visibility = Visibility.Collapsed;
            };
        }

        #endregion

        #region Редактирование комментариев

        // Редактирование текста комментария через текстовое поле
        public void EditComment(Comment comment)
        {
            var editTextBox = new TextBox
            {
                Width = 200,
                Height = 100,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Text = comment.Text,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            // Позиционируем рядом с комментарием
            double left = Canvas.GetLeft(comment.VisualElement) + 20;
            double top = Canvas.GetTop(comment.VisualElement);
            Canvas.SetLeft(editTextBox, left);
            Canvas.SetTop(editTextBox, top);

            _mainCanvas.Children.Add(editTextBox);
            editTextBox.Focus();
            editTextBox.SelectAll();

            // Завершение редактирования
            void FinishEditing(bool removeEmpty = true)
            {
                string newText = editTextBox.Text.Trim();

                if (removeEmpty && string.IsNullOrEmpty(newText))
                {
                    DeleteComment(comment);
                }
                else
                {
                    comment.Text = newText;

                    if (comment.VisualElement is Border border)
                    {
                        border.Background = Brushes.Red;
                    }

                    // Обновляем TextBlock
                    if (comment.TextElement is Border textBorder && textBorder.Child is TextBlock textBlock)
                    {
                        textBlock.Text = newText;
                    }
                }

                // Удаляем TextBox
                _mainCanvas.Children.Remove(editTextBox);
            }

            editTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Control)
                {
                    // Обычный Enter - завершаем редактирование
                    FinishEditing();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    // При отмене проверяем, был ли текст изначально
                    FinishEditing(string.IsNullOrEmpty(comment.Text));
                    e.Handled = true;
                }
            };

            editTextBox.LostFocus += (s, e) =>
            {
                // Немедленно завершаем редактирование при потере фокуса
                FinishEditing();
            };
        }

        #endregion

        #region Удаление комментариев

        // Удаление комментария с холста
        public void DeleteComment(Comment comment)
        {
            if (comment.VisualElement != null)
            {
                _mainCanvas.Children.Remove(comment.VisualElement);
            }
            if (comment.TextElement != null)
            {
                _mainCanvas.Children.Remove(comment.TextElement);
            }
            _comments.Remove(comment);
            CommentDeleted?.Invoke(comment);
        }

        #endregion

        #region Обработка событий мыши

        // Обработка клика мыши для создания комментария в режиме комментариев
        public bool HandleMouseClick(Point clickPosition, object originalSource)
        {
            if (!_isCommentModeActive) return false;

            // Проверяем, что клик не по другим элементам
            if (originalSource is Canvas)
            {
                var comment = CreateCommentAtPosition(clickPosition);
                EditComment(comment);
                return true; // Обработано
            }

            return false; // Не обработано
        }

        #endregion

        #region Очистка

        // Очистка всех комментариев с холста
        public void ClearAllComments()
        {
            foreach (var comment in _comments.ToArray())
            {
                DeleteComment(comment);
            }
        }

        #endregion

        #region Поиск комментариев

        public Comment GetCommentByElement(UIElement element)
        {
            foreach (var comment in _comments)
            {
                if (comment.VisualElement == element || comment.TextElement == element)
                {
                    return comment;
                }
            }
            return null;
        }

        #endregion

        #region Сериализация

        public List<CommentData> GetCommentsData()
        {
            var data = new List<CommentData>();
            foreach (var comment in _comments)
            {
                data.Add(new CommentData
                {
                    PositionX = comment.Position.X,
                    PositionY = comment.Position.Y,
                    Text = comment.Text
                });
            }
            return data;
        }

        public void LoadComments(List<CommentData> commentsData)
        {
            ClearAllComments();

            foreach (var data in commentsData)
            {
                var position = new Point(data.PositionX, data.PositionY);
                var comment = CreateCommentAtPosition(position);
                comment.Text = data.Text;

                // Обновляем визуальное отображение
                if (!string.IsNullOrEmpty(data.Text) && comment.VisualElement is Border border)
                {
                    border.Background = Brushes.Red;
                }
            }
        }

        #endregion
    }
}