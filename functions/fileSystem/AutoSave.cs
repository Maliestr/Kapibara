using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    public class AutoSave
    {
        #region Поля

        private Canvas canvas;
        private Timer autoSaveTimer;
        private bool isEnabled;
        private int saveIntervalSeconds;
        private string autoSaveDirectory;
        private string currentAutoSavePath;
        private bool isSaving = false;
        private readonly object saveLock = new object();

        #endregion

        #region Конструктор и инициализация

        public AutoSave(Canvas canvas, int intervalSeconds = 60, bool enabled = true)
        {
            this.canvas = canvas;
            this.saveIntervalSeconds = intervalSeconds;
            this.isEnabled = enabled;

            // Создаем директорию для автосохранения в AppData
            autoSaveDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Kapibara",
                "AutoSave"
            );

            if (!Directory.Exists(autoSaveDirectory))
            {
                Directory.CreateDirectory(autoSaveDirectory);
            }

            if (isEnabled)
            {
                Start();
            }
        }

        #endregion

        #region Управление автосохранением

        public void Start()
        {
            if (autoSaveTimer != null)
            {
                autoSaveTimer.Dispose();
            }

            isEnabled = true;
            autoSaveTimer = new Timer(SaveCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(saveIntervalSeconds));
        }

        public void Stop()
        {
            isEnabled = false;
            if (autoSaveTimer != null)
            {
                autoSaveTimer.Dispose();
                autoSaveTimer = null;
            }
        }

        public void SetInterval(int seconds)
        {
            if (seconds <= 0)
                throw new ArgumentException("Интервал должен быть больше 0");

            saveIntervalSeconds = seconds;

            if (isEnabled)
            {
                Stop();
                Start();
            }
        }

        public void SaveNow()
        {
            if (isSaving)
                return;

            lock (saveLock)
            {
                if (isSaving)
                    return;

                isSaving = true;
            }

            try
            {
                PerformSave();
            }
            finally
            {
                lock (saveLock)
                {
                    isSaving = false;
                }
            }
        }

        #endregion

        #region Вспомогательные методы

        public void CleanupOldSaves(int keepCount = 10)
        {
            try
            {
                if (!Directory.Exists(autoSaveDirectory))
                    return;

                var files = Directory.GetFiles(autoSaveDirectory, "autosave_*.ged");

                if (files.Length <= keepCount)
                    return;

                // Сортируем по времени создания (новые первыми)
                Array.Sort(files, (x, y) => File.GetCreationTime(y).CompareTo(File.GetCreationTime(x)));

                // Удаляем старые файлы
                for (int i = keepCount; i < files.Length; i++)
                {
                    try
                    {
                        File.Delete(files[i]);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Не удалось удалить старый файл автосохранения {files[i]}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при очистке старых файлов автосохранения: {ex.Message}");
            }
        }

        public string GetLastAutoSavePath()
        {
            return currentAutoSavePath;
        }

        #endregion

        #region Сохранение

        private void SaveCallback(object state)
        {
            if (!isEnabled || isSaving)
                return;

            // Выполняем сохранение в фоновом потоке
            Task.Run(() =>
            {
                if (isSaving)
                    return;

                lock (saveLock)
                {
                    if (isSaving)
                        return;

                    isSaving = true;
                }

                try
                {
                    PerformSave();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка автосохранения: {ex.Message}");
                }
                finally
                {
                    lock (saveLock)
                    {
                        isSaving = false;
                    }
                }
            });
        }

        private void PerformSave()
        {
            if (canvas == null || canvas.Dispatcher == null)
                return;

            // Проверяем, есть ли что сохранять
            bool hasElements = false;
            canvas.Dispatcher.Invoke(() =>
            {
                hasElements = canvas.Children.Count > 0;
            });

            if (!hasElements)
                return;

            // Создаем имя файла с временной меткой
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            currentAutoSavePath = System.IO.Path.Combine(autoSaveDirectory, $"autosave_{timestamp}.ged");

            canvas.Dispatcher.Invoke(() =>
            {
                try
                {
                    SaveCanvasToFile(currentAutoSavePath);

                    // Очищаем старые файлы после успешного сохранения
                    CleanupOldSaves(10);

                    System.Diagnostics.Debug.WriteLine($"Автосохранение выполнено: {currentAutoSavePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при сохранении в файл: {ex.Message}");
                }
            });
        }

        private void SaveCanvasToFile(string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("GEDv1.0"); // Заголовок файла

                foreach (UIElement element in canvas.Children)
                {
                    int zIndex = Canvas.GetZIndex(element);
                    if (zIndex >= 1000)
                        continue;

                    if (element is Ellipse ellipse)
                    {
                        double left = Canvas.GetLeft(ellipse);
                        double top = Canvas.GetTop(ellipse);

                        if (double.IsNaN(left)) left = 0;
                        if (double.IsNaN(top)) top = 0;

                        string fillColor = (ellipse.Fill as SolidColorBrush)?.Color.ToString() ?? "Transparent";
                        string strokeColor = (ellipse.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
                        double strokeThickness = ellipse.StrokeThickness;

                        writer.WriteLine($"ELLIPSE|{left}|{top}|{ellipse.Width}|{ellipse.Height}|{fillColor}|{strokeColor}|{strokeThickness}");
                    }
                    else if (element is Polyline arrow)
                    {
                        string strokeColor = (arrow.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
                        double strokeThickness = arrow.StrokeThickness;
                        string points = string.Join(";", arrow.Points.Select(p => $"{p.X},{p.Y}"));
                        writer.WriteLine($"ARROW|{points}|{strokeColor}|{strokeThickness}");
                    }
                    else if (element is Border border)
                    {
                        double left = Canvas.GetLeft(border);
                        double top = Canvas.GetTop(border);

                        if (double.IsNaN(left)) left = 0;
                        if (double.IsNaN(top)) top = 0;

                        string bgColor = (border.Background as SolidColorBrush)?.Color.ToString() ?? "Transparent";
                        string borderColor = (border.BorderBrush as SolidColorBrush)?.Color.ToString() ?? "Black";
                        double borderThickness = border.BorderThickness.Left;

                        string type = "SQUARE";
                        if (border.RenderTransform is RotateTransform rotateTransform && Math.Abs(rotateTransform.Angle - 45.354) < 0.1)
                        {
                            type = "DIAMOND";
                        }

                        writer.WriteLine($"{type}|{left}|{top}|{border.Width}|{border.Height}|{bgColor}|{borderColor}|{borderThickness}");
                    }
                    else if (element is Line line)
                    {
                        string strokeColor = (line.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
                        double strokeThickness = line.StrokeThickness;
                        double rotateAngle = 0;

                        if (line.RenderTransform is RotateTransform rotateTransform)
                        {
                            rotateAngle = rotateTransform.Angle;
                        }

                        writer.WriteLine($"LINE|{line.X1}|{line.Y1}|{line.X2}|{line.Y2}|{strokeColor}|{strokeThickness}|{rotateAngle}");
                    }
                }
            }
        }

        #endregion

        #region Освобождение ресурсов

        public void Dispose()
        {
            Stop();
        }

        #endregion
    }
}

