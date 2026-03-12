using System;
using System.Collections.Generic;
using System.Windows;

namespace XIAOHAI.AI;

public partial class SecurityConfirmationWindow : Window
{
    private readonly string _script;
    private readonly string _description;
    private readonly List<string> _risks;

    public SecurityConfirmationWindow(string description, List<string> risks, string script)
    {
        InitializeComponent();
        _description = description;
        _risks = risks;
        _script = script;

        // 填充数据
        DescriptionText.Text = description;
        RisksList.ItemsSource = risks;
        
        // 显示代码预览（最多 500 字符）
        var preview = script.Length > 500 ? script.Substring(0, 500) + "..." : script;
        CodePreviewText.Text = preview;
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 如果用户勾选"记住选择"，保存到设置
        if (RememberChoiceCheckBox.IsChecked == true)
        {
            // 保存用户偏好
        }
        
        DialogResult = true;
        Close();
    }
}
