using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace FileShareLibrary
{
    public class Server : IDisposable
    {
        private int _running = 0;

        private ManualResetEvent _stopped = new ManualResetEvent(false);

        private int _connectionCount = 0;

        public string StartDirectory { get; set; }

        public void StartServer(string address, int port)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 1) == 1)
                throw new Exception("Server already started.");
            using var _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAddress = IPAddress.Parse(address);
            EndPoint endPoint = new IPEndPoint(ipAddress, port);
            _listenSocket.Bind(endPoint);
            _listenSocket.Listen(8);

            _stopped.Reset();
            Interlocked.Exchange(ref _running, 1);

            while (Interlocked.CompareExchange(ref _running, 1, 1) == 1)
            {
                var result = _listenSocket.BeginAccept(null, null);
                if (!result.CompletedSynchronously)
                {
                    while (!result.AsyncWaitHandle.WaitOne(200))
                    {
                        if (Interlocked.CompareExchange(ref _running, 0, 0) == 0)
                        {
                            // cancelling
                            _listenSocket.Close();
                            return;
                        }
                    }
                }

                Socket clientSocket = _listenSocket.EndAccept(result);
                Task task = new Task(() => HandleConnection(clientSocket));
                task.Start();
                Interlocked.Increment(ref _connectionCount);
            }
        }

        public void StopServer()
        {
            if (Interlocked.CompareExchange(ref _running, 0, 0) == 0)
                return;
            Interlocked.Exchange(ref _running, 0);
            if (Interlocked.CompareExchange(ref _connectionCount, 0, 0) != 0)
                _stopped.WaitOne();
        }

        private void HandleConnection(Socket socket)
        {
            try
            {
                using (socket)
                {
                    var session = new ServerSession(socket,
                        StartDirectory ?? Directory.GetCurrentDirectory());
                    session.RunSession();
                }
            }
            catch
            {
            }
            finally
            {
                if (Interlocked.Decrement(ref _connectionCount) == 0 &&
                    Interlocked.CompareExchange(ref _running, 0, 0) == 0)
                {
                    _stopped.Set();
                }
            }
        }

        public void Dispose()
        {
            _stopped?.Dispose();
        }
    }

    internal class ServerSession
    {
        private Socket _socket;

        private NetworkStream _stream;

        internal ServerSession(Socket socket, string startDir)
        {
            _socket = socket;
            _stream = new NetworkStream(socket);
            _currentDir = Path.GetFullPath(startDir);
        }

        string _currentDir;

        internal void RunSession()
        {
            try
            {
                using var reader = new BinaryReader(_stream, Encoding.UTF8, true);
                while (true)
                {
                    Header header = new Header();
                    header.FromStream(reader);
                    switch (header.OpCode)
                    {
                        case OpCode.List:
                            HandleOperation<ListOperationParam, ListOperationResult>(reader, HandleList);
                            break;
                        case OpCode.ChangeDirectory:
                            HandleOperation<ChangeDirectoryOperationParam, ChangeDirectoryOperationResult>(reader,
                                HandleChangeDirectory);
                            break;
                        case OpCode.MakeDirectory:
                            HandleOperation<MakeDirectoryOperationParam, MakeDirectoryOperationResult>(reader,
                                HandleMakeDirectory);
                            break;
                        case OpCode.Rename:
                            HandleOperation<RenameOperationParam, RenameOperationResult>(reader, HandleRename);
                            break;
                        case OpCode.Remove:
                            HandleOperation<RemoveOperationParam, RemoveOperationResult>(reader, HandleRemove);
                            break;
                        case OpCode.GetCurrentDirectory:
                            HandleOperation<GetCurrentDirectoryOperationParam, GetCurrentDirectoryOperationResult>(
                                reader, HandleGetCurrentDirectory);
                            break;
                        case OpCode.PutFile:
                            HandleOperation<PutFileOperationParam, PutFileOperationResult>(reader, HandlePutFile);
                            break;
                        case OpCode.GetFile:
                            HandleOperation<GetFileOperationParam, GetFileOperationResult>(reader, HandleGetFile);
                            break;
                        case OpCode.GetFileSize:
                            HandleOperation<GetFileSizeOperationParam, GetFileSizeOperationResult>(reader,
                                HandleGetFileSize);
                            break;
                        case OpCode.GetConfig:
                            HandleOperation<GetConfigOperationParam, GetConfigOperationResult>(reader, HandleGetConfig);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            catch (EndOfStreamException)
            {
                // we're done
            }
        }

        private delegate void Operation<T, U>(T param, U result);

        private void HandleOperation<T, U>(BinaryReader reader, Operation<T, U> operation)
            where T : ISerializable, new() where U : BaseResult, ISerializable, new()
        {
            T param = new T();
            U result = new U();
            try
            {
                result.Success = true;
                param.FromStream(reader);
                operation(param, result);
            }
            catch
            {
                result.Success = false;
            }

            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                result.ToStream(writer);
            }
        }

        private static string UnifyPath(string basePath, string newPath)
        {
            if (Path.IsPathRooted(newPath))
                return Path.GetFullPath(newPath);
            return Path.GetFullPath(Path.Combine(basePath, newPath));
        }

        private void HandleList(ListOperationParam param, ListOperationResult result)
        {
            DirectoryInfo dir;
            if (param.Path.Length == 0)
                dir = new DirectoryInfo(_currentDir);
            else dir = new DirectoryInfo(param.Path);
            FileSystemInfo[] infos = dir.GetFileSystemInfos();
            foreach (var info in infos)
            {
                FileStat stat = new FileStat();
                stat.FullPath = info.FullName;
                stat.Path = info.Name;
                stat.IsDirectory = info.Attributes.HasFlag(FileAttributes.Directory);
                result.Items.Add(stat);
            }
        }

        private void HandleGetFileSize(GetFileSizeOperationParam param, GetFileSizeOperationResult result)
        {
            param.Path = UnifyPath(_currentDir, param.Path);
            FileInfo fileInfo = new FileInfo(param.Path);
            result.Size = fileInfo.Length;
        }

        private void HandleChangeDirectory(ChangeDirectoryOperationParam param, ChangeDirectoryOperationResult result)
        {
            param.Path = UnifyPath(_currentDir, param.Path);
            FileAttributes attributes = File.GetAttributes(param.Path);
            if (attributes.HasFlag(FileAttributes.Directory))
                _currentDir = param.Path;
        }

        private void HandleMakeDirectory(MakeDirectoryOperationParam param, MakeDirectoryOperationResult result)
        {
            param.Path = UnifyPath(_currentDir, param.Path);
            var dir = Directory.CreateDirectory(param.Path);
            result.Path = dir.FullName;
        }

        private void HandleRename(RenameOperationParam param, RenameOperationResult result)
        {
            param.Source = UnifyPath(_currentDir, param.Source);
            param.Destination = UnifyPath(_currentDir, param.Destination);
            if (File.GetAttributes(param.Source).HasFlag(FileAttributes.Directory))
            {
                Directory.Move(param.Source, param.Destination);
            }
            else
            {
                File.Move(param.Source, param.Destination);
            }
        }

        private void HandleRemove(RemoveOperationParam param, RemoveOperationResult result)
        {
            param.Path = UnifyPath(param.Path, param.Path);
            if (param.IsDirectory)
                Directory.Delete(param.Path, true);
            else
                File.Delete(param.Path);
        }

        private void HandleGetCurrentDirectory(GetCurrentDirectoryOperationParam param,
            GetCurrentDirectoryOperationResult result)
        {
            result.Path = _currentDir;
        }

        private void HandleGetFile(GetFileOperationParam param, GetFileOperationResult result)
        {
            param.Param.Path = UnifyPath(_currentDir, param.Param.Path);
            param.Param.Content = new byte[param.Param.IoLength];
            using (var file = File.OpenRead(param.Param.Path))
            {
                file.Seek(param.Param.FileOffset, SeekOrigin.Begin);
                param.Param.ContentLength = file.Read(param.Param.Content, 0, param.Param.IoLength);
            }

            param.Param.IoLength = param.Param.ContentLength;
            result.Data = param.Param;
        }

        private void HandlePutFile(PutFileOperationParam param, PutFileOperationResult result)
        {
            param.Param.Path = UnifyPath(_currentDir, param.Param.Path);
            using (var file = File.OpenWrite(param.Param.Path))
            {
                if (param.Truncate)
                {
                    file.SetLength(0);
                }

                file.Seek(param.Param.FileOffset, SeekOrigin.Begin);
                file.Write(param.Param.Content, 0, param.Param.IoLength);
            }

            result.WrittenLength = param.Param.IoLength;
        }

        private void HandleGetConfig(GetConfigOperationParam param, GetConfigOperationResult result)
        {
            result.PathSeparator = char.ToString(Path.DirectorySeparatorChar);
        }
    }
}