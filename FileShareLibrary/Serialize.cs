using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileShareLibrary
{
    public enum OpCode : byte
    {
        List = 0,
        ChangeDirectory = 1,
        MakeDirectory = 2,
        Rename = 3,
        Remove = 4,
        GetCurrentDirectory = 5,
        PutFile = 6,
        GetFile = 7,
        GetFileSize = 8,
        GetConfig = 9
    }


    public interface ISerializable
    {
        public void ToStream(BinaryWriter writer);

        public void FromStream(BinaryReader reader);
    }

    public class Header : ISerializable
    {
        public OpCode OpCode { get; set; }
        public UInt32 Length { get; set; } = 0;

        public void ToStream(BinaryWriter writer)
        {
            writer.Write((byte)OpCode);
            writer.Write(Length);
        }

        public void FromStream(BinaryReader reader)
        {
            OpCode = (OpCode)reader.ReadByte();
            Length = reader.ReadUInt32();
        }
    }

    public class BaseResult : ISerializable
    {
        public bool Success { get; set; } = true;

        public virtual void ToStream(BinaryWriter writer)
        {
            writer.Write(Success);
        }

        public virtual void FromStream(BinaryReader reader)
        {
            Success = reader.ReadBoolean();
        }
    }

    public class FileStat : ISerializable
    {
        public string FullPath { get; set; } = "";
        public string Path { get; set; } = "";
        public long Size { get; set; } = 0;
        public bool IsDirectory { get; set; } = false;

        public void ToStream(BinaryWriter writer)
        {
            writer.Write(FullPath);
            writer.Write(Path);
            writer.Write(Size);
            writer.Write(IsDirectory);
        }

        public void FromStream(BinaryReader reader)
        {
            FullPath = reader.ReadString();
            Path = reader.ReadString();
            Size = reader.ReadInt64();
            IsDirectory = reader.ReadBoolean();
        }
    }

    public class ListOperationParam : ISerializable
    {
        public string Path { get; set; } = "";

        public void ToStream(BinaryWriter writer)
        {
            writer.Write(Path);
        }

        public void FromStream(BinaryReader reader)
        {
            Path = reader.ReadString();
        }
    }

    public class ListOperationResult : BaseResult, ISerializable
    {
        public List<FileStat> Items { get; set; } = new List<FileStat>();

        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
            writer.Write(Items.Count);
            foreach (var item in Items)
            {
                item.ToStream(writer);
            }
        }

        public override void FromStream(BinaryReader reader)
        {
            Items.Clear();
            base.FromStream(reader);
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                FileStat item = new FileStat();
                item.FromStream(reader);
                Items.Add(item);
            }
        }
    }

    public class ChangeDirectoryOperationParam : ISerializable
    {
        public string Path { get; set; } = "";

        public void ToStream(BinaryWriter writer)
        {
            writer.Write(Path);
        }

        public void FromStream(BinaryReader reader)
        {
            Path = reader.ReadString();
        }
    }

    public class ChangeDirectoryOperationResult : BaseResult, ISerializable
    {
        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
        }

        public override void FromStream(BinaryReader reader)
        {
            base.FromStream(reader);
        }
    }

    public class MakeDirectoryOperationParam : ISerializable
    {
        public string Path { get; set; } = "";

        public void ToStream(BinaryWriter writer)
        {
            writer.Write(Path);
        }

        public void FromStream(BinaryReader reader)
        {
            Path = reader.ReadString();
        }
    }

    public class MakeDirectoryOperationResult : BaseResult, ISerializable
    {
        public string Path { get; set; } = "";

        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
            writer.Write(Path);
        }

        public override void FromStream(BinaryReader reader)
        {
            base.FromStream(reader);
            Path = reader.ReadString();
        }
    }

    public class RenameOperationParam : ISerializable
    {
        public string Source { get; set; } = "";
        public string Destination { get; set; } = "";

        public void ToStream(BinaryWriter writer)
        {
            writer.Write(Source);
            writer.Write(Destination);
        }

        public void FromStream(BinaryReader reader)
        {
            Source = reader.ReadString();
            Destination = reader.ReadString();
        }
    }

    public class RenameOperationResult : BaseResult, ISerializable
    {
        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
        }

        public override void FromStream(BinaryReader reader)
        {
            base.FromStream(reader);
        }
    }

    public class RemoveOperationParam : ISerializable
    {
        public string Path { get; set; } = "";

        public bool IsDirectory { get; set; } = false;

        public void ToStream(BinaryWriter writer)
        {
            writer.Write(Path);
            writer.Write(IsDirectory);
        }

        public void FromStream(BinaryReader reader)
        {
            Path = reader.ReadString();
            IsDirectory = reader.ReadBoolean();
        }
    }

    public class RemoveOperationResult : BaseResult, ISerializable
    {
        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
        }

        public override void FromStream(BinaryReader reader)
        {
            base.FromStream(reader);
        }
    }

    public class GetCurrentDirectoryOperationParam : ISerializable
    {
        public void ToStream(BinaryWriter writer)
        {
        }

        public void FromStream(BinaryReader reader)
        {
        }
    }

    public class GetCurrentDirectoryOperationResult : BaseResult, ISerializable
    {
        public string Path { get; set; } = "";

        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
            writer.Write(Path);
        }

        public override void FromStream(BinaryReader reader)
        {
            base.FromStream(reader);
            Path = reader.ReadString();
        }
    }

    public class FileIoOperationParam : ISerializable
    {
        public string Path { get; set; } = "";
        public int FileOffset { get; set; } = 0;
        public int IoLength { get; set; } = 0;
        public int ContentOffset { get; set; } = 0;
        public int ContentLength { get; set; } = 0;
        public byte[] Content { get; set; } = null;

        public void ToStream(BinaryWriter writer)
        {
            writer.Write(Path);
            writer.Write(FileOffset);
            writer.Write(IoLength);
            if (Content == null)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(ContentLength - ContentOffset);
                writer.Write(Content, ContentOffset, ContentLength - ContentOffset);
            }
        }

        public void FromStream(BinaryReader reader)
        {
            Path = reader.ReadString();
            FileOffset = reader.ReadInt32();
            IoLength = reader.ReadInt32();
            ContentOffset = 0;
            ContentLength = reader.ReadInt32();
            Content = reader.ReadBytes(ContentLength);
            if (Content.Length != ContentLength)
                throw new EndOfStreamException();
        }
    }

    public class PutFileOperationParam : ISerializable
    {
        public bool Truncate { get; set; } = false;
        public FileIoOperationParam Param { get; set; } = new FileIoOperationParam();

        public void ToStream(BinaryWriter writer)
        {
            writer.Write(Truncate);
            Param.ToStream(writer);
        }

        public void FromStream(BinaryReader reader)
        {
            Truncate = reader.ReadBoolean();
            Param.FromStream(reader);
        }
    }

    public class PutFileOperationResult : BaseResult, ISerializable
    {
        public int WrittenLength { get; set; } = 0;

        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
            writer.Write(WrittenLength);
        }

        public override void FromStream(BinaryReader reader)
        {
            base.FromStream(reader);
            WrittenLength = reader.ReadInt32();
        }
    }

    public class GetFileOperationParam : ISerializable
    {
        public FileIoOperationParam Param { get; set; } = new FileIoOperationParam();

        public void ToStream(BinaryWriter writer)
        {
            Param.ToStream(writer);
        }

        public void FromStream(BinaryReader reader)
        {
            Param.FromStream(reader);
        }
    }

    public class GetFileOperationResult : BaseResult, ISerializable
    {
        public FileIoOperationParam Data { get; set; } = new FileIoOperationParam();

        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
            Data.ToStream(writer);
        }

        public override void FromStream(BinaryReader reader)
        {
            base.FromStream(reader);
            Data.FromStream(reader);
        }
    }

    public class GetFileSizeOperationParam : ISerializable
    {
        public string Path { get; set; } = "";

        public void ToStream(BinaryWriter writer)
        {
            writer.Write(Path);
        }

        public void FromStream(BinaryReader reader)
        {
            Path = reader.ReadString();
        }
    }

    public class GetFileSizeOperationResult : BaseResult, ISerializable
    {
        public long Size { get; set; } = 0;

        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
            writer.Write(Size);
        }

        public override void FromStream(BinaryReader reader)
        {
            base.FromStream(reader);
            Size = reader.ReadInt64();
        }
    }

    public class GetConfigOperationParam : ISerializable
    {
        public void ToStream(BinaryWriter writer)
        {
        }

        public void FromStream(BinaryReader reader)
        {
        }
    }

    public class GetConfigOperationResult : BaseResult, ISerializable
    {
        public string PathSeparator { get; set; }

        public override void ToStream(BinaryWriter writer)
        {
            base.ToStream(writer);
            writer.Write(PathSeparator);
        }

        public override void FromStream(BinaryReader reader)
        {
            base.FromStream(reader);
            PathSeparator = reader.ReadString();
        }
    }
}