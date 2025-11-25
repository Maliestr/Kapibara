using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Graphic_Editor
{
    /// <summary>
    /// Логика взаимодействия для InputDialog.xaml
    /// </summary>
    public partial class InputDialog : Window
    {
        public string Title { get; set; }
        public string Prompt { get; set; }
        public string Answer { get; set; }

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            Title = title;
            Prompt = prompt;
            Answer = defaultValue;
            DataContext = this;
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
