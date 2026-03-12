using System.Windows;

namespace XIAOHAI.AI;

public partial class TutorialWindow : Window
{
    public TutorialWindow()
    {
        InitializeComponent();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
