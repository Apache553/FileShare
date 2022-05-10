using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FileShareLibrary;

namespace ClientApp
{
    /// <summary>
    /// Interaction logic for ConnectDialog.xaml
    /// </summary>
    public partial class ConnectDialog : Window
    {
        public ConnectDialog()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public Client ConnectedClient { get; set; }

        private void Connect(object sender, ExecutedRoutedEventArgs e)
        {
            var host = Host.Text;
            var port = int.Parse(Port.Text);

            Client client = new Client();
            try
            {
                client.ConnectRemote(host, port);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ConnectedClient = client;

            this.Close();
        }

        private void Cancel(object sender, ExecutedRoutedEventArgs e)
        {
            ConnectedClient?.Disconnect();
            ConnectedClient = null;
            this.Close();
        }

        private void FocusPortBox(object sender, ExecutedRoutedEventArgs e)
        {
            Port.Focus();
        }

        private void ConnectDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
            Host.Focus();
        }
    }

    public static class ConnectDialogCommand
    {
        public static readonly RoutedUICommand Connect =
            new RoutedUICommand("Do Connect", "Connect", typeof(ConnectDialog));

        public static readonly RoutedUICommand Cancel =
            new RoutedUICommand("Cancel", "Cancel", typeof(ConnectDialog));

        public static readonly RoutedUICommand FocusPortBox =
            new RoutedUICommand("FocusPortBox", "FocusPortBox", typeof(ConnectDialog));
    }
}