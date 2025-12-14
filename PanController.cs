using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Graphic_Editor
{
    /// Контроллер для панорамирования (перемещения) по рабочей области
    public class PanController
    {
        private Canvas canvas;
        private ZoomController zoomController;
        private bool isPanning = false;
        private Point panStartPoint;
        private Point panStartOffset;

        // Делегаты для проверки состояния других режимов
        public Func<bool> CanStartPanning { get; set; }

        // Событие для обновления сетки после панорамирования
        public event Action OnPanEnded;

        public bool IsPanning
        {
            get { return isPanning; }
        }

        public PanController(Canvas canvas, ZoomController zoomController)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));
            if (zoomController == null)
                throw new ArgumentNullException(nameof(zoomController));

            this.canvas = canvas;
            this.zoomController = zoomController;
        }

        /// Инициализирует обработчики событий для панорамирования
        public void Initialize()
        {
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseRightButtonUp += Canvas_MouseRightButtonUp;
            canvas.MouseLeave += Canvas_MouseLeave;
        }

        /// Начинает панорамирование правой кнопкой мыши
        public bool StartPanningRightButton(Point mousePosition)
        {
            if (CanStartPanning != null && !CanStartPanning())
                return false;

            isPanning = true;
            panStartPoint = mousePosition;
            panStartOffset = zoomController.GetOffset();
            canvas.Cursor = Cursors.Hand;
            canvas.CaptureMouse();
            return true;
        }

        /// Начинает панорамирование пробелом + левая кнопка мыши
        public bool StartPanningSpace(Point mousePosition)
        {
            if (CanStartPanning != null && !CanStartPanning())
                return false;

            if (!isPanning)
            {
                isPanning = true;
                panStartPoint = mousePosition;
                panStartOffset = zoomController.GetOffset();
                canvas.Cursor = Cursors.Hand;
            }
            return true;
        }

        /// Продолжает панорамирование
        public void ContinuePanning(Point currentPoint, bool updateGrid = false)
        {
            if (!isPanning) return;

            double deltaX = currentPoint.X - panStartPoint.X;
            double deltaY = currentPoint.Y - panStartPoint.Y;

            double newOffsetX = panStartOffset.X + deltaX;
            double newOffsetY = panStartOffset.Y + deltaY;
            zoomController.SetOffset(newOffsetX, newOffsetY);

            if (updateGrid)
            {
                OnPanEnded?.Invoke();
            }
        }

        /// Завершает панорамирование
        public void StopPanning(bool updateGrid = false)
        {
            if (!isPanning) return;

            isPanning = false;
            canvas.Cursor = Cursors.Arrow;
            canvas.ReleaseMouseCapture();

            if (updateGrid)
            {
                OnPanEnded?.Invoke();
            }
        }

        /// Обработчик движения мыши
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Панорамирование правой кнопкой мыши
            if (isPanning && e.RightButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(canvas);
                ContinuePanning(currentPoint, updateGrid: true);
            }
            // Панорамирование пробелом + левая кнопка мыши
            else if (Keyboard.IsKeyDown(Key.Space) && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(canvas);
                if (StartPanningSpace(currentPoint))
                {
                    ContinuePanning(currentPoint, updateGrid: true);
                }
            }
            else if (isPanning && !Keyboard.IsKeyDown(Key.Space) && e.LeftButton != MouseButtonState.Pressed)
            {
                // Завершаем панорамирование пробелом
                StopPanning(updateGrid: true);
            }
        }

        /// Обработчик отпускания правой кнопки мыши
        private void Canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isPanning)
            {
                StopPanning(updateGrid: true);
                e.Handled = true;
            }
        }

        /// Обработчик выхода мыши за пределы Canvas
        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                StopPanning(updateGrid: true);
            }
        }

        /// Обработка нажатия клавиши пробел (изменение курсора)
        public void HandleSpaceKeyDown()
        {
            if (CanStartPanning != null && CanStartPanning())
            {
                canvas.Cursor = Cursors.Hand;
            }
        }

        /// Обработка отпускания клавиши пробел
        public void HandleSpaceKeyUp()
        {
            if (isPanning)
            {
                StopPanning(updateGrid: true);
            }
            else
            {
                canvas.Cursor = Cursors.Arrow;
            }
        }
    }
}



