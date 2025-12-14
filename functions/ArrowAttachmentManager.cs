using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    public class ArrowAttachmentManager
    {
        // Структура для хранения информации о прикреплении
        public class ArrowAttachment
        {
            public ArrowContainer ArrowContainer { get; set; }
            public UIElement StartElement { get; set; }  // Объект, к которому прикреплено начало стрелки
            public UIElement EndElement { get; set; }    // Объект, к которому прикреплен конец стрелки
            public Point StartAttachmentPoint { get; set; } // Точка прикрепления на начальном объекте
            public Point EndAttachmentPoint { get; set; }   // Точка прикрепления на конечном объекте
        }

        private Canvas mainCanvas;
        private List<ArrowAttachment> attachments = new List<ArrowAttachment>();
        private const double AttachmentRadius = 50; // Радиус поиска объектов для прикрепления

        public ArrowAttachmentManager(Canvas canvas)
        {
            mainCanvas = canvas;
        }

        private UIElement FindNearestElement(Point point, UIElement excludeElement = null)
        {
            UIElement nearestElement = null;
            double minDistance = AttachmentRadius;

            foreach (UIElement element in mainCanvas.Children)
            {
                // Пропускаем стрелки, наконечники и исключаемый элемент
                if (element is Polyline || element is Polygon || element == excludeElement)
                    continue;

                // Пропускаем элементы в нотации
                if (IsElementInNotation(element))
                    continue;

                Rect bounds = GetElementBounds(element);
                Point nearestPoint = GetNearestPointOnBounds(point, bounds);
                double distance = GetDistance(point, nearestPoint);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestElement = element;
                }
            }

            return nearestElement;
        }

        private Rect GetElementBounds(UIElement element)
        {
            if (element is FrameworkElement fe)
            {
                double left = Canvas.GetLeft(element);
                double top = Canvas.GetTop(element);

                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                double width = fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
                double height = fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;

                if (width <= 0 || height <= 0)
                    return new Rect(left, top, 0, 0);

                return new Rect(left, top, width, height);
            }

            return new Rect();
        }

        private Point GetNearestPointOnBounds(Point point, Rect bounds)
        {
            if (bounds.IsEmpty)
                return point;

            // Ограничиваем точку границами прямоугольника
            double x = Math.Max(bounds.Left, Math.Min(point.X, bounds.Right));
            double y = Math.Max(bounds.Top, Math.Min(point.Y, bounds.Bottom));

            // Если точка внутри прямоугольника, находим ближайшую сторону
            if (point.X >= bounds.Left && point.X <= bounds.Right &&
                point.Y >= bounds.Top && point.Y <= bounds.Bottom)
            {
                double distToLeft = point.X - bounds.Left;
                double distToRight = bounds.Right - point.X;
                double distToTop = point.Y - bounds.Top;
                double distToBottom = bounds.Bottom - point.Y;

                double minDist = Math.Min(Math.Min(distToLeft, distToRight), Math.Min(distToTop, distToBottom));

                if (minDist == distToLeft)
                    x = bounds.Left;
                else if (minDist == distToRight)
                    x = bounds.Right;
                else if (minDist == distToTop)
                    y = bounds.Top;
                else
                    y = bounds.Bottom;
            }
            else
            {
                // Если точка снаружи, находим ближайшую точку на границе
                if (x == point.X && y == point.Y)
                {
                    // Точка находится на границе или вне прямоугольника
                    if (point.X < bounds.Left)
                        x = bounds.Left;
                    else if (point.X > bounds.Right)
                        x = bounds.Right;

                    if (point.Y < bounds.Top)
                        y = bounds.Top;
                    else if (point.Y > bounds.Bottom)
                        y = bounds.Bottom;
                }
            }

            return new Point(x, y);
        }

        private double GetDistance(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public void AttachArrowOnCreation(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null || arrowContainer.Line.Points.Count < 2)
                return;

            var attachment = new ArrowAttachment
            {
                ArrowContainer = arrowContainer
            };

            // Находим объекты для начала и конца стрелки
            Point startPoint = arrowContainer.Line.Points[0];
            Point endPoint = arrowContainer.Line.Points[arrowContainer.Line.Points.Count - 1];

            UIElement startElement = FindNearestElement(startPoint);
            UIElement endElement = FindNearestElement(endPoint, startElement);

            if (startElement != null)
            {
                attachment.StartElement = startElement;
                Rect startBounds = GetElementBounds(startElement);
                Point startCenter = new Point(startBounds.Left + startBounds.Width / 2, startBounds.Top + startBounds.Height / 2);
                
                // Используем направление от центра объекта к конечной точке для определения точки прикрепления
                Point targetForStart = endElement != null ? 
                    new Point(GetElementBounds(endElement).Left + GetElementBounds(endElement).Width / 2, 
                             GetElementBounds(endElement).Top + GetElementBounds(endElement).Height / 2) : 
                    endPoint;
                
                attachment.StartAttachmentPoint = GetIntersectionPointWithBounds(startCenter, targetForStart, startBounds);
                
                // Обновляем первую точку стрелки
                arrowContainer.Line.Points[0] = attachment.StartAttachmentPoint;
            }

            if (endElement != null)
            {
                attachment.EndElement = endElement;
                Rect endBounds = GetElementBounds(endElement);
                Point endCenter = new Point(endBounds.Left + endBounds.Width / 2, endBounds.Top + endBounds.Height / 2);
                
                // Используем направление от центра объекта к начальной точке для определения точки прикрепления
                Point targetForEnd = startElement != null ? 
                    new Point(GetElementBounds(startElement).Left + GetElementBounds(startElement).Width / 2, 
                             GetElementBounds(startElement).Top + GetElementBounds(startElement).Height / 2) : 
                    startPoint;
                
                attachment.EndAttachmentPoint = GetIntersectionPointWithBounds(endCenter, targetForEnd, endBounds);
                
                // Обновляем последнюю точку стрелки
                int lastIndex = arrowContainer.Line.Points.Count - 1;
                arrowContainer.Line.Points[lastIndex] = attachment.EndAttachmentPoint;
            }

            // Если есть прикрепления, сохраняем их
            if (startElement != null || endElement != null)
            {
                attachments.Add(attachment);
                
                // Подписываемся на перемещение объектов
                if (startElement != null)
                {
                    SubscribeToElementMove(startElement);
                }
                if (endElement != null && endElement != startElement)
                {
                    SubscribeToElementMove(endElement);
                }

                // Обновляем наконечник
                UpdateArrowhead(arrowContainer);
            }
        }

        private void SubscribeToElementMove(UIElement element)
        {
            // Прикрепление обрабатывается через прямой вызов UpdateAttachedArrows из MainWindow
            // при перемещении элементов через DragManager
        }

        public void UpdateAttachedArrows(UIElement element)
        {
            foreach (var attachment in attachments)
            {
                bool needsUpdate = false;

                // Обновляем начало стрелки
                if (attachment.StartElement == element)
                {
                    Rect bounds = GetElementBounds(element);
                    
                    // Находим направление от центра объекта к первой промежуточной точке стрелки
                    Point center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                    Point targetPoint = center;
                    
                    // Если есть промежуточные точки, используем направление к первой из них
                    if (attachment.ArrowContainer.Line.Points.Count > 1)
                    {
                        targetPoint = attachment.ArrowContainer.Line.Points[1];
                    }
                    else if (attachment.EndElement != null)
                    {
                        // Если нет промежуточных точек, используем направление к конечному объекту
                        Rect endBounds = GetElementBounds(attachment.EndElement);
                        targetPoint = new Point(endBounds.Left + endBounds.Width / 2, endBounds.Top + endBounds.Height / 2);
                    }
                    
                    // Находим точку пересечения луча от центра к целевой точке с границей объекта
                    Point newStartPoint = GetIntersectionPointWithBounds(center, targetPoint, bounds);
                    
                    // Обновляем только первую точку стрелки
                    Point currentStart = attachment.ArrowContainer.Line.Points[0];
                    if (GetDistance(currentStart, newStartPoint) > 0.1)
                    {
                        attachment.ArrowContainer.Line.Points[0] = newStartPoint;
                        attachment.StartAttachmentPoint = newStartPoint;
                        needsUpdate = true;
                    }
                }

                // Обновляем конец стрелки
                if (attachment.EndElement == element)
                {
                    Rect bounds = GetElementBounds(element);
                    int lastIndex = attachment.ArrowContainer.Line.Points.Count - 1;
                    
                    // Находим направление от центра объекта к последней промежуточной точке стрелки
                    Point center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                    Point targetPoint = center;
                    
                    // Если есть промежуточные точки, используем направление от предпоследней
                    if (attachment.ArrowContainer.Line.Points.Count > 1)
                    {
                        targetPoint = attachment.ArrowContainer.Line.Points[lastIndex - 1];
                    }
                    else if (attachment.StartElement != null)
                    {
                        // Если нет промежуточных точек, используем направление от начального объекта
                        Rect startBounds = GetElementBounds(attachment.StartElement);
                        targetPoint = new Point(startBounds.Left + startBounds.Width / 2, startBounds.Top + startBounds.Height / 2);
                    }
                    
                    // Находим точку пересечения луча от центра к целевой точке с границей объекта
                    Point newEndPoint = GetIntersectionPointWithBounds(center, targetPoint, bounds);
                    
                    // Обновляем только последнюю точку
                    Point currentEnd = attachment.ArrowContainer.Line.Points[lastIndex];
                    if (GetDistance(currentEnd, newEndPoint) > 0.1)
                    {
                        attachment.ArrowContainer.Line.Points[lastIndex] = newEndPoint;
                        attachment.EndAttachmentPoint = newEndPoint;
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    UpdateArrowhead(attachment.ArrowContainer);
                }
            }
        }
        
        private Point GetIntersectionPointWithBounds(Point center, Point target, Rect bounds)
        {
            if (bounds.IsEmpty)
                return target;
            
            // Вычисляем направление от центра к целевой точке
            double dx = target.X - center.X;
            double dy = target.Y - center.Y;
            
            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
            {
                // Если центр и цель совпадают, возвращаем ближайшую точку на границе
                return GetNearestPointOnBounds(center, bounds);
            }
            
            // Нормализуем направление
            double length = Math.Sqrt(dx * dx + dy * dy);
            dx /= length;
            dy /= length;
            
            // Находим пересечение луча с границами прямоугольника
            // Проверяем пересечение с каждой стороной
            double t = double.MaxValue;
            Point intersection = target;
            
            // Левая сторона
            if (dx < 0)
            {
                double tLeft = (bounds.Left - center.X) / dx;
                double y = center.Y + dy * tLeft;
                if (y >= bounds.Top && y <= bounds.Bottom && tLeft > 0 && tLeft < t)
                {
                    t = tLeft;
                    intersection = new Point(bounds.Left, y);
                }
            }
            
            // Правая сторона
            if (dx > 0)
            {
                double tRight = (bounds.Right - center.X) / dx;
                double y = center.Y + dy * tRight;
                if (y >= bounds.Top && y <= bounds.Bottom && tRight > 0 && tRight < t)
                {
                    t = tRight;
                    intersection = new Point(bounds.Right, y);
                }
            }
            
            // Верхняя сторона
            if (dy < 0)
            {
                double tTop = (bounds.Top - center.Y) / dy;
                double x = center.X + dx * tTop;
                if (x >= bounds.Left && x <= bounds.Right && tTop > 0 && tTop < t)
                {
                    t = tTop;
                    intersection = new Point(x, bounds.Top);
                }
            }
            
            // Нижняя сторона
            if (dy > 0)
            {
                double tBottom = (bounds.Bottom - center.Y) / dy;
                double x = center.X + dx * tBottom;
                if (x >= bounds.Left && x <= bounds.Right && tBottom > 0 && tBottom < t)
                {
                    t = tBottom;
                    intersection = new Point(x, bounds.Bottom);
                }
            }
            
            return intersection;
        }

        private void UpdateArrowhead(ArrowContainer arrowContainer)
        {
            if (arrowContainer?.Line == null || arrowContainer.Line.Points.Count < 2 || arrowContainer.Arrowhead == null)
                return;

            Point lineStart = arrowContainer.Line.Points[arrowContainer.Line.Points.Count - 2];
            Point lineEnd = arrowContainer.Line.Points[arrowContainer.Line.Points.Count - 1];

            // Вычисляем направление стрелки
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length == 0) return;

            // Нормализуем вектор направления
            dx /= length;
            dy /= length;

            // Размеры наконечника
            double arrowLength = 20;
            double arrowWidth = 12;

            // Вычисляем точки треугольника
            Point arrowTip = lineEnd;
            Point arrowLeft = new Point(
                lineEnd.X - dx * arrowLength + dy * arrowWidth,
                lineEnd.Y - dy * arrowLength - dx * arrowWidth
            );
            Point arrowRight = new Point(
                lineEnd.X - dx * arrowLength - dy * arrowWidth,
                lineEnd.Y - dy * arrowLength + dx * arrowWidth
            );

            // Обновляем точки наконечника
            arrowContainer.Arrowhead.Points = new PointCollection { arrowTip, arrowLeft, arrowRight };
        }

        public void DetachArrow(ArrowContainer arrowContainer)
        {
            var attachment = attachments.FirstOrDefault(a => a.ArrowContainer == arrowContainer);
            if (attachment != null)
            {
                attachments.Remove(attachment);
            }
        }

        private bool IsElementInNotation(UIElement element)
        {
            // Проверяем, есть ли у элемента родитель Canvas с именем NotationCanvas
            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is Canvas canvas && canvas.Name == "NotationCanvas")
                    return true;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return false;
        }

        public void ClearAllAttachments()
        {
            attachments.Clear();
        }

        public IReadOnlyList<ArrowAttachment> GetAttachments()
        {
            return attachments.AsReadOnly();
        }
    }
}

