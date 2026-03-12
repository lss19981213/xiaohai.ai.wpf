using System;
using System.Windows;

namespace XIAOHAI.AI
{
    public partial class ManualInputDialog : Window
    {
        public string Title { get; private set; } = "";
        public string Content { get; private set; } = "";

        public ManualInputDialog()
        {
            InitializeComponent();
            TitleTextBox.Focus();
        }

        public ManualInputDialog(string title, string content) : this()
        {
            TitleTextBox.Text = title;
            ContentTextBox.Text = content;
            Title = title;
            Content = content;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Title = TitleTextBox.Text.Trim();
            Content = ContentTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(Title))
            {
                System.Windows.MessageBox.Show("请输入标题", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return;
            }
            
            if (string.IsNullOrEmpty(Content))
            {
                System.Windows.MessageBox.Show("请输入内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                ContentTextBox.Focus();
                return;
            }
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}