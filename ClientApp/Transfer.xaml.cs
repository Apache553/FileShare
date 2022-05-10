using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FileShareLibrary;

namespace ClientApp
{
    /// <summary>
    /// Interaction logic for Transfer.xaml
    /// </summary>
    public partial class Transfer : Window
    {
        private DispatcherTimer _timer = new DispatcherTimer();

        public Transfer()
        {
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += DoWork;
            _worker.RunWorkerCompleted += WorkCompleted;
            _timer.Interval = TimeSpan.FromMilliseconds(250);
            _timer.Tick += UpdateProgress;
            for (int i = 0; i < SAMPLE_COUNT; ++i)
            {
                _samples[i].Millisecond = 1;
                _samples[i].Bytes = 0;
            }

            InitializeComponent();
        }

        private void DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (Operation == OperationType.Download)
                {
                    DoDownload();
                }
                else
                {
                    DoUpload();
                }
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
            }
        }

        private readonly object _lock = new object();

        private int _transferredCount = 0;
        private int _totalCount = 0;

        private long _transferredBytes = 0;
        private long _totalBytes = 0;

        private string _filename = "";

        private struct TransferSample
        {
            public int Bytes { get; set; }
            public long Millisecond { get; set; }
        }

        private static readonly int SAMPLE_COUNT = 5;
        private readonly TransferSample[] _samples = new TransferSample[SAMPLE_COUNT];
        private int _samplePointer = 0;

        private void DoDownload()
        {
            lock (_lock) _totalCount += ItemList.Count;
            foreach (var item in ItemList)
            {
                FileStat stat = item.Item1 as FileStat;
                if (stat.IsDirectory)
                {
                    DownloadDirectory(stat.FullPath, item.Item2);
                }
                else
                {
                    DownloadFile(stat.FullPath, item.Item2);
                }
            }
        }

        private void DoUpload()
        {
            lock (_lock) _totalCount += ItemList.Count;
            foreach (var item in ItemList)
            {
                string local = item.Item1 as string;
                if (File.GetAttributes(local).HasFlag(FileAttributes.Directory))
                {
                    UploadDirectory(local, item.Item2);
                }
                else
                {
                    UploadFile(local, item.Item2);
                }
            }
        }

        private void UpdateProgress(object sender, EventArgs e)
        {
            lock (_lock)
            {
                TotalProgressBar.Maximum = _totalCount;
                TotalProgressBar.Value = _transferredCount;
                FileProgressBar.Maximum = _totalBytes;
                FileProgressBar.Value = _transferredBytes;
                TotalProgressLabel.Content = $"{_transferredCount}/{_totalCount}";
                long bytes = 0;
                long ms = 0;
                for (int i = 0; i < SAMPLE_COUNT; i++)
                {
                    bytes += _samples[i].Bytes;
                    ms += _samples[i].Millisecond;
                }

                ItemProgressLabel.Content =
                    $"{_transferredBytes}/{_totalBytes}  {(double)bytes / 1024 * 1000 / ms:0.##} KiB/s";
                Filename.Text = _filename;
            }
        }

        private void WorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _timer.Stop();
            if (e.Error != null)
            {
                MessageBox.Show($"Exception: {e.Error.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (e.Error == null && e.Cancelled == false)
            {
                Success = true;
            }

            this.Close();
        }

        public bool Success { get; set; } = false;

        public Client Client { get; set; }

        public string RemotePathSeparator { get; set; }

        public enum OperationType
        {
            Download,
            Upload
        }

        public OperationType Operation { get; set; }

        public ICollection<Tuple<object, string>> ItemList { get; set; }

        private readonly BackgroundWorker _worker = new BackgroundWorker();

        private void Transfer_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Client == null || RemotePathSeparator == null || ItemList == null)
            {
                MessageBox.Show("Invalid transfer parameters!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            _timer.Start();
            _worker.RunWorkerAsync();
        }

        private static readonly int _transferSize = 4096 * 1024;

        private void DownloadFile(string remotePath, string localPath)
        {
            var size = Client.GetFileSize(remotePath);
            if (!size.Success)
                throw new InvalidOperationException("Get file size failed");
            lock (_lock)
            {
                _totalBytes = size.Size;
                _transferredBytes = 0;
                _filename = remotePath;
            }

            using (FileStream fs = File.OpenWrite(localPath))
            {
                fs.SetLength(0);
                for (int offset = 0; offset < size.Size; offset += _transferSize)
                {
                    if (_worker.CancellationPending)
                        throw new OperationCanceledException();

                    var before = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                    var read = Client.GetFile(remotePath, offset, _transferSize);
                    if (!read.Success)
                        throw new InvalidOperationException("Get file segment failed");
                    fs.Write(read.Data.Content, 0, read.Data.ContentLength);

                    var after = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    lock (_lock)
                    {
                        _transferredBytes += read.Data.ContentLength;
                        _samples[_samplePointer].Bytes = read.Data.ContentLength;
                        _samples[_samplePointer].Millisecond = Math.Max(after - before, 1);
                        _samplePointer = (_samplePointer + 1) % SAMPLE_COUNT;
                    }
                }
            }

            lock (_lock) ++_transferredCount;
        }

        private void DownloadDirectory(string remotePath, string localPath)
        {
            Directory.CreateDirectory(localPath);
            lock (_lock)
            {
                ++_transferredCount;
                _filename = remotePath;
            }

            if (_worker.CancellationPending)
                throw new OperationCanceledException();

            var list = Client.List(remotePath);
            if (!list.Success)
                throw new InvalidOperationException("List directory failed");
            lock (_lock) _totalCount += list.Items.Count;

            foreach (var item in list.Items)
            {
                if (item.IsDirectory)
                {
                    DownloadDirectory(item.FullPath, System.IO.Path.Join(localPath, item.Path));
                }
                else
                {
                    DownloadFile(item.FullPath, System.IO.Path.Join(localPath, item.Path));
                }
            }
        }

        private void UploadFile(string localPath, string remotePath)
        {
            byte[] buffer = new byte[_transferSize];
            using (FileStream fs = File.OpenRead(localPath))
            {
                var touch = Client.PutFile(remotePath, 0, 0, buffer, 0, true);
                if (!touch.Success)
                    throw new InvalidOperationException("Create remote file failed");
                int offset = 0;

                lock (_lock)
                {
                    _totalBytes = fs.Length;
                    _transferredBytes = 0;
                    _filename = remotePath;
                }

                while (true)
                {
                    if (_worker.CancellationPending)
                        throw new OperationCanceledException();

                    var before = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                    var readBytes = fs.Read(buffer, 0, _transferSize);
                    if (readBytes == 0)
                        break;
                    var write = Client.PutFile(remotePath, offset, readBytes, buffer, 0, false);
                    if (!write.Success)
                        throw new InvalidOperationException("Write file segment failed");

                    var after = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    lock (_lock)
                    {
                        _transferredBytes += readBytes;
                        _samples[_samplePointer].Bytes = readBytes;
                        _samples[_samplePointer].Millisecond = Math.Max(after - before, 1);
                        _samplePointer = (_samplePointer + 1) % SAMPLE_COUNT;
                    }

                    offset += readBytes;
                }
            }

            lock (_lock) ++_transferredCount;
        }

        private void UploadDirectory(string localPath, string remotePath)
        {
            DirectoryInfo dir = new DirectoryInfo(localPath);

            var mkdir = Client.MakeDirectory(remotePath);
            if (!mkdir.Success)
                throw new InvalidOperationException("cannot create remote directory");
            lock (_lock)
            {
                ++_transferredCount;
                _filename = localPath;
            }

            if (_worker.CancellationPending)
                throw new OperationCanceledException();

            var items = dir.GetFileSystemInfos();
            lock (_lock) _totalCount += items.Length;
            foreach (var item in items)
            {
                if (item.Attributes.HasFlag(FileAttributes.Directory))
                {
                    UploadDirectory(System.IO.Path.Combine(localPath, item.Name),
                        remotePath + RemotePathSeparator + item.Name);
                }
                else
                {
                    UploadFile(System.IO.Path.Combine(localPath, item.Name),
                        remotePath + RemotePathSeparator + item.Name);
                }
            }
        }

        private void CancelCommand(object sender, ExecutedRoutedEventArgs e)
        {
            _worker.CancelAsync();
            CancelButton.IsEnabled = false;
        }

        private void Transfer_OnClosing(object sender, CancelEventArgs e)
        {
            if (_worker.IsBusy)
            {
                _worker.CancelAsync();
                CancelButton.IsEnabled = false;
                e.Cancel = true;
            }
        }
    }

    public static class TransferCommand
    {
        public static readonly RoutedUICommand Cancel = new RoutedUICommand("Cancel", "Cancel", typeof(Transfer));
    }
}