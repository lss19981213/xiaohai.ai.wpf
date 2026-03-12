using System.Windows;

namespace XIAOHAI.AI
{
    /// <summary>
    /// PasswordDialog.xaml 的交互逻辑
    /// </summary>
    public partial class PasswordDialog : Window
    {
        public string Password { get; private set; }

        public PasswordDialog()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordBox.Password;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}