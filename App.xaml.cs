using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Graphic_Editor
{
    // Логика взаимодействия для App.xaml - точка входа приложения
    public partial class App : Application
    {
        // Обработка запуска приложения - создание главного окна и загрузка файла из аргументов командной строки
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Создаем главное окно
            MainWindow mainWindow = new MainWindow();
            
            // Если передан путь к файлу в аргументах командной строки, загружаем его
            if (e.Args != null && e.Args.Length > 0)
            {
                string filePath = e.Args[0];
                if (System.IO.File.Exists(filePath))
                {
                    // Загружаем файл после инициализации окна
                    mainWindow.Loaded += (s, args) =>
                    {
                        mainWindow.LoadFileFromPath(filePath);
                    };
                }
            }
            
            mainWindow.Show();
        }
    }
}
