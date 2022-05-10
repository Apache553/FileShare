using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ClientApp.Annotations;
using FileShareLibrary;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DragAction = System.Windows.DragAction;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using GiveFeedbackEventArgs = System.Windows.GiveFeedbackEventArgs;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using QueryContinueDragEventArgs = System.Windows.QueryContinueDragEventArgs;

namespace ClientApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            this.DataContext = this;
            InitializeComponent();
        }

        private Point _start;
        private DragDropEffects _effect;

        private void UpdateRemoteView(ListOperationResult result = null)
        {
            ListOperationResult list = result;
            if (list == null)
            {
                if (_client == null)
                    return;

                list = _client.List();
                if (!list.Success)
                {
                    MessageBox.Show("Unable to get file list!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            RemoteView.ItemsSource = list.Items;
        }

        private void UpdateRemotePath(GetCurrentDirectoryOperationResult result = null)
        {
            GetCurrentDirectoryOperationResult pwd = result;
            if (pwd == null)
            {
                if (_client == null)
                    return;

                pwd = _client.GetCurrentDirectory();
                if (!pwd.Success)
                {
                    MessageBox.Show("Unable to get remote working directory!", "Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }

            RemotePathText = pwd.Path;
        }

        private void RemoteView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this._start = e.GetPosition(null);
            _selectedItems = RemoteView.SelectedItems.Cast<object>().ToArray();
        }

        private string _dragTempDir;
        private FileStat[] _dragItems;
        private object[] _selectedItems;

        private void RemoteView_MouseMove(object sender, MouseEventArgs e)
        {
            Point mpos = e.GetPosition(null);
            Vector diff = this._start - mpos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))

            {
                if (_selectedItems != null)
                    foreach (var item in _selectedItems)
                    {
                        if (!RemoteView.SelectedItems.Contains(item))
                            RemoteView.SelectedItems.Add(item);
                    }

                if (this.RemoteView.SelectedItems.Count == 0)
                {
                    return;
                }

                RemoteView.AllowDrop = false;

                _dragTempDir = System.IO.Path.Join(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
                Directory.CreateDirectory(_dragTempDir);
                _dragItems = new FileStat[this.RemoteView.SelectedItems.Count];
                string[] files = new string[this.RemoteView.SelectedItems.Count];

                for (int i = 0; i < this.RemoteView.SelectedItems.Count; i++)
                {
                    var stat = this.RemoteView.SelectedItems[i] as FileStat;
                    _dragItems[i] = stat;
                    files[i] = System.IO.Path.Join(_dragTempDir, stat.Path);
                }

                try
                {
                    DataObject dataObject = new DataObject(DataFormats.FileDrop, files);
                    DragDrop.DoDragDrop(this.RemoteView, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
                    Directory.Delete(_dragTempDir, true);
                }
                catch
                {
                }

                _dragTempDir = null;
                _dragItems = null;

                RemoteView.AllowDrop = true;
            }
        }


        private bool UploadItems(ICollection<Tuple<object, string>> items)
        {
            var transfer = new Transfer();
            transfer.Owner = this;
            transfer.Client = _client;
            transfer.RemotePathSeparator = _remotePathSep;
            transfer.Operation = Transfer.OperationType.Upload;
            transfer.ItemList = items;
            transfer.ShowDialog();
            return transfer.Success;
        }

        private bool DownloadItems(ICollection<Tuple<object, string>> items)
        {
            var transfer = new Transfer();
            transfer.Owner = this;
            transfer.Client = _client;
            transfer.RemotePathSeparator = _remotePathSep;
            transfer.Operation = Transfer.OperationType.Download;
            transfer.ItemList = items;
            transfer.ShowDialog();
            return transfer.Success;
        }


        private bool PrepareFiles()
        {
            List<Tuple<object, string>> items = new List<Tuple<object, string>>();
            foreach (var item in _dragItems)
            {
                items.Add(new Tuple<object, string>(item, System.IO.Path.Join(_dragTempDir, item.Path)));
            }

            return DownloadItems(items);
        }

        private void RemoteView_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed)
            {
                e.Action = DragAction.Cancel;
                return;
            }

            if (!e.KeyStates.HasFlag(DragDropKeyStates.LeftMouseButton))
            {
                if (_effect == DragDropEffects.None)
                {
                    e.Action = DragAction.Cancel;
                    return;
                }

                // prepare files here
                if (!PrepareFiles())
                {
                    e.Action = DragAction.Cancel;
                    return;
                }

                e.Action = DragAction.Drop;
                return;
            }

            e.Action = DragAction.Continue;
        }

        private void RemoteView_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            _effect = e.Effects;
        }

        private Client _client;

        private string _remotePathSep;

        private string _remotePathText;

        public string RemotePathText
        {
            get => _remotePathText;
            set
            {
                _remotePathText = value;
                OnPropertyChanged(nameof(RemotePathText));
            }
        }

        private bool _connectedStatus = false;

        public bool ConnectedStatus
        {
            get => _connectedStatus;
            set
            {
                _connectedStatus = value;
                OnPropertyChanged(nameof(ConnectedStatus));
            }
        }

        private Tuple<bool, ChangeDirectoryOperationResult, ListOperationResult> ChangeDirectory(string path)
        {
            if (_client == null)
                return new Tuple<bool, ChangeDirectoryOperationResult, ListOperationResult>(false, null, null);

            var cd = _client.ChangeDirectory(path);
            if (!cd.Success)
                return new Tuple<bool, ChangeDirectoryOperationResult, ListOperationResult>(false, cd, null);
            var list = _client.List();
            if (!list.Success)
                return new Tuple<bool, ChangeDirectoryOperationResult, ListOperationResult>(false, cd, list);
            return new Tuple<bool, ChangeDirectoryOperationResult, ListOperationResult>(true, cd, list);
        }

        private readonly List<string> _tempDirs = new List<string>();

        private void RemoteView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FileStat stat;
            if (RemoteView.SelectedItem != null)
                stat = RemoteView.SelectedItem as FileStat;
            else
                return;

            if (stat.IsDirectory)
            {
                var cd = ChangeDirectory(stat.FullPath);
                if (!cd.Item1)
                {
                    MessageBox.Show("Unable to go directory!", "Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                UpdateRemotePath();
                UpdateRemoteView(cd.Item3);
            }
            else
            {
                try
                {
                    var tempDir = System.IO.Path.Join(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
                    Directory.CreateDirectory(tempDir);

                    _tempDirs.Add(tempDir);

                    var filePath = System.IO.Path.Join(tempDir, stat.Path);
                    List<Tuple<object, string>> items = new List<Tuple<object, string>>();

                    items.Add(new Tuple<object, string>(stat, filePath));

                    if (!DownloadItems(items))
                    {
                        return;
                    }

                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Exception: {ex.Message}", "Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private void ConnectCommand(object sender, ExecutedRoutedEventArgs e)
        {
            DisconnectCommand(sender, e);

            var connectDialog = new ConnectDialog();
            connectDialog.Owner = this;
            connectDialog.ShowDialog();

            if (connectDialog.ConnectedClient == null)
            {
                return;
            }

            _client = connectDialog.ConnectedClient;
            var config = _client.GetConfig();
            if (!config.Success)
            {
                _client = null;
                return;
            }

            _remotePathSep = config.PathSeparator;

            ConnectedStatus = true;
            UpdateRemoteView();
            UpdateRemotePath();
        }

        private void DisconnectCommand(object sender, ExecutedRoutedEventArgs e)
        {
            ConnectedStatus = false;
            _client?.Disconnect();
            _client?.Dispose();
            _client = null;
            RemotePathText = null;
            RemoteView.ItemsSource = null;
        }

        private void GoParentDirectoryCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var cd = ChangeDirectory("..");

            if (!cd.Item1)
            {
                MessageBox.Show("Unable to go parent directory!", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            UpdateRemotePath();
            UpdateRemoteView(cd.Item3);
        }

        private void ChangeDirectoryCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var path = RemotePath.Text;

            var cd = ChangeDirectory(path);
            if (!cd.Item1)
            {
                MessageBox.Show("Unable to go directory!", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            UpdateRemotePath();
            UpdateRemoteView(cd.Item3);
        }

        private void RemoteView_OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                try
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                    List<Tuple<object, string>> items = new List<Tuple<object, string>>();

                    foreach (string file in files)
                    {
                        if (File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                        {
                            DirectoryInfo info = new DirectoryInfo(file);

                            items.Add(new Tuple<object, string>(file, _remotePathText + _remotePathSep + info.Name));
                        }
                        else
                        {
                            FileInfo info = new FileInfo(file);
                            items.Add(new Tuple<object, string>(file, _remotePathText + _remotePathSep + info.Name));
                        }
                    }

                    UploadItems(items);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Exception: \n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                UpdateRemotePath();
                UpdateRemoteView();
            }
        }

        private void RefreshCommand(object sender, ExecutedRoutedEventArgs e)
        {
            UpdateRemotePath();
            UpdateRemoteView();
        }

        private void DeleteCommand(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                foreach (var item in RemoteView.SelectedItems)
                {
                    FileStat stat = item as FileStat;
                    var remove = _client.Remove(stat.FullPath, stat.IsDirectory);
                    if (!remove.Success)
                        throw new InvalidOperationException("cannot delete file/directory");
                }

                UpdateRemotePath();
                UpdateRemoteView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: \n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DownloadCommand(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                using var folder = new FolderBrowserDialog();
                DialogResult result = folder.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folder.SelectedPath))
                {
                    if (File.GetAttributes(folder.SelectedPath).HasFlag(FileAttributes.Directory))
                    {
                        List<Tuple<object, string>> items = new List<Tuple<object, string>>();
                        FileStat[] files = RemoteView.SelectedItems.Cast<FileStat>().ToArray();
                        foreach (var item in files)
                        {
                            items.Add(new Tuple<object, string>(item,
                                System.IO.Path.Join(folder.SelectedPath, item.Path)));
                        }

                        DownloadItems(items);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void UploadCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var open = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All files|*"
            };
            if (open.ShowDialog() == true)
            {
                try
                {
                    string[] files = open.FileNames;
                    List<Tuple<object, string>> items = new List<Tuple<object, string>>();

                    foreach (string file in files)
                    {
                        if (File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                        {
                            DirectoryInfo info = new DirectoryInfo(file);

                            items.Add(new Tuple<object, string>(file, _remotePathText + _remotePathSep + info.Name));
                        }
                        else
                        {
                            FileInfo info = new FileInfo(file);
                            items.Add(new Tuple<object, string>(file, _remotePathText + _remotePathSep + info.Name));
                        }
                    }

                    UploadItems(items);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Exception: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                UpdateRemotePath();
                UpdateRemoteView();
            }
        }

        private void RemoteView_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            DownloadMenuItem.IsEnabled = RemoteView.SelectedItems.Count != 0;
            RenameMenuItem.IsEnabled = RemoteView.SelectedItems.Count == 1;
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            foreach (var item in _tempDirs)
            {
                try
                {
                    Directory.Delete(item, true);
                }
                catch
                {
                }
            }
        }

        private void NewDirectoryCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var name = new NewName();
            name.PromptText = "New Directory Name:";
            name.Owner = this;
            name.ShowDialog();
            if (name.Canceled)
                return;

            var mkdir = _client.MakeDirectory(_remotePathText + _remotePathSep + name.NewNameString);
            if (!mkdir.Success)
            {
                MessageBox.Show("cannot create directory", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            UpdateRemotePath();
            UpdateRemoteView();
        }

        private void RenameCommand(object sender, ExecutedRoutedEventArgs e)
        {
            FileStat stat = RemoteView.SelectedItem as FileStat;
            var name = new NewName();
            name.PromptText = stat.IsDirectory ? "New Directory Name:" : "New File Name:";
            name.NewNameString = stat.Path;
            name.Owner = this;
            name.ShowDialog();
            if (name.Canceled)
                return;

            var rename = _client.Rename(stat.FullPath, _remotePathText + _remotePathSep + name.NewNameString);
            if (!rename.Success)
            {
                MessageBox.Show("cannot rename item", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            UpdateRemotePath();
            UpdateRemoteView();
        }
    }

    [ValueConversion(typeof(Boolean), typeof(String))]
    public class IsDirectoryConverter : IValueConverter
    {
        public string FalseValue { get; } = "File";
        public string TrueValue { get; } = "Directory";

        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return FalseValue;
            else
                return (bool)value ? TrueValue : FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public static class MainCommand
    {
        public static readonly RoutedUICommand Connect =
            new RoutedUICommand("Connect", "Connect", typeof(MainWindow));

        public static readonly RoutedUICommand Disconnect =
            new RoutedUICommand("Disconnect", "Disconnect", typeof(MainWindow));

        public static readonly RoutedUICommand GoParentDirectory =
            new RoutedUICommand("GoParentDirectory", "GoParentDirectory", typeof(MainWindow));

        public static readonly RoutedUICommand ChangeDirectory =
            new RoutedUICommand("ChangeDirectory", "ChangeDirectory", typeof(MainWindow));

        public static readonly RoutedUICommand Refresh =
            new RoutedUICommand("Refresh", "Refresh", typeof(MainWindow));

        public static readonly RoutedUICommand Delete = new RoutedUICommand("Delete", "Delete", typeof(MainWindow));

        public static readonly RoutedUICommand Download =
            new RoutedUICommand("Download", "Download", typeof(MainWindow));

        public static readonly RoutedUICommand Upload = new RoutedUICommand("Upload", "Upload", typeof(MainWindow));

        public static readonly RoutedUICommand NewDirectory =
            new RoutedUICommand("NewDirectory", "NewDirectory", typeof(MainWindow));

        public static readonly RoutedUICommand Rename = new RoutedUICommand("Rename", "Rename", typeof(MainWindow));
    }
}