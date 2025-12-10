using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Graphic_Editor
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Получаем аргументы командной строки
            string filePath = null;
            if (e.Args != null && e.Args.Length > 0)
            {
                filePath = e.Args[0];
            }

            // Создаем главное окно и передаем путь к файлу, если он был передан
            MainWindow mainWindow = new MainWindow(filePath);
            mainWindow.Show();
        }
    }
}
