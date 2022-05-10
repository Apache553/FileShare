using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ClientApp
{
    /// <summary>
    /// Interaction logic for NewName.xaml
    /// </summary>
    public partial class NewName : Window
    {
        public NewName()
        {
            InitializeComponent();
        }

        public string PromptText { get; set; } = "New Name:";
        public string NewNameString{ get; set; }
        public bool Canceled { get; set; } = true;

        private void OKCommand(object sender, ExecutedRoutedEventArgs e)
        {
            NewNameString = NewNameText.Text;
            Canceled = false;
            this.Close();
        }

        private void CancelCommand(object sender, ExecutedRoutedEventArgs e)
        {
            this.Close();
        }

        private void NewName_OnLoaded(object sender, RoutedEventArgs e)
        {
            NewNameText.Focus();
            Prompt.Content = PromptText;
            NewNameText.Text = NewNameString;
            NewNameText.SelectAll();
        }
    }

    public static class NewNameCommand
    {
        public static readonly RoutedUICommand OK = new RoutedUICommand("OK", "OK", typeof(NewName));
        public static readonly RoutedUICommand Cancel = new RoutedUICommand("Cancel", "Cancel", typeof(NewName));
    }
}
