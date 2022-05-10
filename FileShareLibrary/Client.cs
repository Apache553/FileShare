using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FileShareLibrary
{
    public class Client : IDisposable

    {
        private Socket _socket;

        private NetworkStream _stream;

        public void ConnectRemote(string host, int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var result = _socket.BeginConnect(host, port, null, null);
            result.AsyncWaitHandle.WaitOne(5000);
            if (_socket.Connected)
            {
                _socket.EndConnect(result);
            }
            else
            {
                _socket.Close();
                throw new TimeoutException();
            }

            _stream = new NetworkStream(_socket);
        }

        public void Disconnect()
        {
            _socket?.Close(3);
            _socket = null;
            _stream = null;
        }

        public ListOperationResult List(string path = "")
        {
            Header header = new Header
            {
                OpCode = OpCode.List
            };
            ListOperationParam param = new ListOperationParam
            {
                Path = path
            };
            using (BinaryWriter writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            ListOperationResult result = new ListOperationResult();
            using (BinaryReader reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public GetFileSizeOperationResult GetFileSize(string path)
        {
            Header header = new Header
            {
                OpCode = OpCode.GetFileSize
            };
            GetFileSizeOperationParam param = new GetFileSizeOperationParam
            {
                Path = path
            };
            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            GetFileSizeOperationResult result = new GetFileSizeOperationResult();
            using (var reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public ChangeDirectoryOperationResult ChangeDirectory(string path)
        {
            Header header = new Header
            {
                OpCode = OpCode.ChangeDirectory
            };
            ChangeDirectoryOperationParam param = new ChangeDirectoryOperationParam
            {
                Path = path
            };
            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            ChangeDirectoryOperationResult result = new ChangeDirectoryOperationResult();
            using (var reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public MakeDirectoryOperationResult MakeDirectory(string path)
        {
            Header header = new Header { OpCode = OpCode.MakeDirectory };
            MakeDirectoryOperationParam param = new MakeDirectoryOperationParam { Path = path };
            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            MakeDirectoryOperationResult result = new MakeDirectoryOperationResult();
            using (var reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public RenameOperationResult Rename(string source, string destination)
        {
            Header header = new Header { OpCode = OpCode.Rename };
            RenameOperationParam param = new RenameOperationParam { Source = source, Destination = destination };
            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            RenameOperationResult result = new RenameOperationResult();
            using (var reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public RemoveOperationResult Remove(string path, bool isDirectory)
        {
            Header header = new Header { OpCode = OpCode.Remove };
            RemoveOperationParam param = new RemoveOperationParam { Path = path, IsDirectory = isDirectory };
            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            RemoveOperationResult result = new RemoveOperationResult();
            using (var reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public GetCurrentDirectoryOperationResult GetCurrentDirectory()
        {
            Header header = new Header { OpCode = OpCode.GetCurrentDirectory };
            GetCurrentDirectoryOperationParam param = new GetCurrentDirectoryOperationParam();
            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            GetCurrentDirectoryOperationResult result = new GetCurrentDirectoryOperationResult();
            using (var reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public GetFileOperationResult GetFile(string path, int offset, int length)
        {
            Header header = new Header
            {
                OpCode = OpCode.GetFile
            };
            GetFileOperationParam param = new GetFileOperationParam();
            param.Param.Path = path;
            param.Param.FileOffset = offset;
            param.Param.IoLength = length;
            param.Param.ContentOffset = 0;

            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            GetFileOperationResult result = new GetFileOperationResult();
            using (var reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public PutFileOperationResult PutFile(string path, int offset, int length, byte[] data, int dataOffset,
            bool truncate)
        {
            Header header = new Header
            {
                OpCode = OpCode.PutFile
            };
            PutFileOperationParam param = new PutFileOperationParam();
            param.Param.Path = path;
            param.Param.FileOffset = offset;
            param.Param.IoLength = length;
            param.Param.Content = data;
            param.Param.ContentLength = dataOffset + length;
            param.Param.ContentOffset = dataOffset;
            param.Truncate = truncate;

            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            PutFileOperationResult result = new PutFileOperationResult();
            using (var reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public GetConfigOperationResult GetConfig()
        {
            Header header = new Header { OpCode = OpCode.GetConfig };
            GetConfigOperationParam param = new GetConfigOperationParam();
            using (var writer = new BinaryWriter(_stream, Encoding.UTF8, true))
            {
                header.ToStream(writer);
                param.ToStream(writer);
            }

            GetConfigOperationResult result = new GetConfigOperationResult();
            using (var reader = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                result.FromStream(reader);
            }

            return result;
        }

        public void Dispose()
        {
            _socket?.Dispose();
            _stream?.Dispose();
        }
    }
}