using System;
using System.IO;
using System.Text;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>表示 FIH Mobile NB0 格式中的条目头，描述单个固件条目的偏移量、大小和名称。</para>
    /// Represents an entry header in the FIH Mobile NB0 format, describing the offset, size, and name of a single firmware entry.
    /// </summary>
    /// <remarks>
    /// <para>二进制布局（共 64 字节）：</para>
    /// Binary layout (64 bytes total):
    /// <list type="table">
    /// <listheader><term>偏移/Offset</term><description>大小/Size | 字段/Field | 类型/Type</description></listheader>
    /// <item><term>0</term><description>4 | LoDataOffset | uint32</description></item>
    /// <item><term>4</term><description>4 | LoFileSize | uint32</description></item>
    /// <item><term>8</term><description>4 | HiDataOffset | uint32</description></item>
    /// <item><term>12</term><description>4 | HiFileSize | uint32</description></item>
    /// <item><term>16</term><description>48 | Name | ASCII</description></item>
    /// </list>
    /// </remarks>
    public sealed class Nb0EntryHeader
    {
        /// <summary>
        /// <para>条目头结构的固定大小（字节）。</para>
        /// The fixed size of the entry header structure in bytes.
        /// </summary>
        public const int StructSize = 64;

        /// <summary>
        /// <para>名称字段的最大长度（字节）。</para>
        /// The maximum length of the name field in bytes.
        /// </summary>
        public const int NameLength = 48;

        /// <summary>
        /// <para>数据偏移量的低 32 位。</para>
        /// The low 32 bits of the data offset.
        /// </summary>
        public uint LoDataOffset { get; set; }

        /// <summary>
        /// <para>文件大小的低 32 位。</para>
        /// The low 32 bits of the file size.
        /// </summary>
        public uint LoFileSize { get; set; }

        /// <summary>
        /// <para>数据偏移量的高 32 位。</para>
        /// The high 32 bits of the data offset.
        /// </summary>
        public uint HiDataOffset { get; set; }

        /// <summary>
        /// <para>文件大小的高 32 位。</para>
        /// The high 32 bits of the file size.
        /// </summary>
        public uint HiFileSize { get; set; }

        /// <summary>
        /// <para>以 null 结尾的 ASCII 文件名。</para>
        /// The null-terminated ASCII filename.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>获取计算后的 64 位数据偏移量，由 LoDataOffset 和 HiDataOffset 组合而成。</para>
        /// Gets the computed 64-bit data offset, combined from LoDataOffset and HiDataOffset.
        /// </summary>
        /// <value>
        /// <para>64 位数据偏移量。</para>
        /// The 64-bit data offset.
        /// </value>
        public long DataOffset
        {
            get => LoDataOffset + ((long)HiDataOffset << 32);
            set
            {
                LoDataOffset = (uint)(value & 0xFFFFFFFF);
                HiDataOffset = (uint)((value >> 32) & 0xFFFFFFFF);
            }
        }

        /// <summary>
        /// <para>获取计算后的 64 位文件大小，由 LoFileSize 和 HiFileSize 组合而成。</para>
        /// Gets the computed 64-bit file size, combined from LoFileSize and HiFileSize.
        /// </summary>
        /// <value>
        /// <para>64 位文件大小。</para>
        /// The 64-bit file size.
        /// </value>
        public long FileSize
        {
            get => LoFileSize + ((long)HiFileSize << 32);
            set
            {
                LoFileSize = (uint)(value & 0xFFFFFFFF);
                HiFileSize = (uint)((value >> 32) & 0xFFFFFFFF);
            }
        }

        /// <summary>
        /// <para>从二进制读取器中读取并构造一个 <see cref="Nb0EntryHeader"/> 实例。</para>
        /// Reads and constructs an <see cref="Nb0EntryHeader"/> instance from a binary reader.
        /// </summary>
        /// <param name="reader">
        /// <para>要读取的 <see cref="BinaryReader"/> 实例。</para>
        /// The <see cref="BinaryReader"/> instance to read from.
        /// </param>
        /// <returns>
        /// <para>读取到的 <see cref="Nb0EntryHeader"/> 实例。</para>
        /// The read <see cref="Nb0EntryHeader"/> instance.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="reader"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="reader"/> is null.
        /// </exception>
        public static Nb0EntryHeader Read(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var header = new Nb0EntryHeader();
            header.LoDataOffset = reader.ReadUInt32();
            header.LoFileSize = reader.ReadUInt32();
            header.HiDataOffset = reader.ReadUInt32();
            header.HiFileSize = reader.ReadUInt32();

            byte[] nameBytes = reader.ReadBytes(NameLength);
            header.Name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ');

            return header;
        }

        /// <summary>
        /// <para>将当前条目头写入指定的二进制写入器。</para>
        /// Writes the current entry header to the specified binary writer.
        /// </summary>
        /// <param name="writer">
        /// <para>要写入的 <see cref="BinaryWriter"/> 实例。</para>
        /// The <see cref="BinaryWriter"/> instance to write to.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="writer"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="writer"/> is null.
        /// </exception>
        public void Write(BinaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.Write(LoDataOffset);
            writer.Write(LoFileSize);
            writer.Write(HiDataOffset);
            writer.Write(HiFileSize);

            byte[] nameBytes = new byte[NameLength];
            byte[] encoded = Encoding.ASCII.GetBytes(Name);
            int copyLen = Math.Min(encoded.Length, NameLength - 1);
            Array.Copy(encoded, nameBytes, copyLen);
            for (int i = copyLen; i < NameLength; i++)
                nameBytes[i] = 0;
            writer.Write(nameBytes, 0, NameLength);
        }

        /// <summary>
        /// <para>将条目头部直接写入预分配的字节缓冲区，避免中间 MemoryStream 分配。</para>
        /// Writes the entry header directly to a pre-allocated byte buffer, avoiding intermediate MemoryStream allocation.
        /// </summary>
        /// <param name="buffer">
        /// <para>目标缓冲区，长度至少为 <see cref="StructSize"/> (64) 字节。</para>
        /// The target buffer, must be at least <see cref="StructSize"/> (64) bytes long.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para><paramref name="buffer"/> 为 <c>null</c>。</para>
        /// <paramref name="buffer"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="buffer"/> 长度小于 <see cref="StructSize"/>。</para>
        /// <paramref name="buffer"/> length is less than <see cref="StructSize"/>.
        /// </exception>
        public void WriteToBuffer(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < StructSize) throw new ArgumentException($"Buffer must be at least {StructSize} bytes.", nameof(buffer));

            WriteUInt32LE(buffer, 0, LoDataOffset);
            WriteUInt32LE(buffer, 4, LoFileSize);
            WriteUInt32LE(buffer, 8, HiDataOffset);
            WriteUInt32LE(buffer, 12, HiFileSize);

            // Write name - encode directly into target buffer to avoid intermediate allocation
            int encodedLen = Encoding.ASCII.GetBytes(Name, 0, Name.Length, buffer, 16);
            int copyLen = Math.Min(encodedLen, NameLength - 1);
            for (int i = copyLen; i < NameLength; i++)
                buffer[16 + i] = 0;
        }

        private static void WriteUInt32LE(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        /// <summary>
        /// <para>从字节缓冲区直接读取条目头部，避免中间 MemoryStream 分配。</para>
        /// Reads an entry header directly from a byte buffer, avoiding intermediate MemoryStream allocation.
        /// </summary>
        /// <param name="buffer">
        /// <para>源字节缓冲区。</para>
        /// The source byte buffer.
        /// </param>
        /// <param name="offset">
        /// <para>缓冲区中的起始偏移量。默认为 0。</para>
        /// The starting offset within the buffer. Defaults to 0.
        /// </param>
        /// <returns>
        /// <para>读取到的 <see cref="Nb0EntryHeader"/> 实例。</para>
        /// The read <see cref="Nb0EntryHeader"/> instance.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para><paramref name="buffer"/> 为 <c>null</c>。</para>
        /// <paramref name="buffer"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>从偏移量开始缓冲区剩余长度小于 <see cref="StructSize"/>。</para>
        /// The remaining buffer length from offset is less than <see cref="StructSize"/>.
        /// </exception>
        public static Nb0EntryHeader ReadFromBuffer(byte[] buffer, int offset = 0)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length - offset < StructSize) throw new ArgumentException($"Buffer must have at least {StructSize} bytes from offset.", nameof(buffer));

            var header = new Nb0EntryHeader();
            header.LoDataOffset = ReadUInt32LE(buffer, offset);
            header.LoFileSize = ReadUInt32LE(buffer, offset + 4);
            header.HiDataOffset = ReadUInt32LE(buffer, offset + 8);
            header.HiFileSize = ReadUInt32LE(buffer, offset + 12);

            // Read name - find null terminator
            int nameLen = 0;
            for (int i = 0; i < NameLength; i++)
            {
                if (buffer[offset + 16 + i] == 0) break;
                nameLen++;
            }
            header.Name = Encoding.ASCII.GetString(buffer, offset + 16, nameLen);

            return header;
        }

        private static uint ReadUInt32LE(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
        }

        /// <summary>
        /// <para>将当前条目头序列化为字节数组。</para>
        /// Serializes the current entry header to a byte array.
        /// </summary>
        /// <returns>
        /// <para>包含条目头二进制数据的字节数组。</para>
        /// A byte array containing the binary data of the entry header.
        /// </returns>
        public byte[] ToBytes()
        {
            using var ms = new MemoryStream(StructSize);
            using var writer = new BinaryWriter(ms);
            Write(writer);
            return ms.ToArray();
        }

        /// <summary>
        /// <para>返回表示当前条目头的字符串。</para>
        /// Returns a string that represents the current entry header.
        /// </summary>
        /// <returns>
        /// <para>包含偏移量、大小和名称信息的格式化字符串。</para>
        /// A formatted string containing offset, size, and name information.
        /// </returns>
        public override string ToString()
        {
            return $"offset=0x{DataOffset:X16} size=0x{FileSize:X16} name='{Name}'";
        }
    }
}
