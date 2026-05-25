using System;
using System.IO;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>表示 FIH Mobile NB0 格式中的 MD5 校验记录，包含数据偏移量、数据长度和 MD5 校验和。</para>
    /// Represents an MD5 verification record in the FIH Mobile NB0 format, containing data offset, data length, and MD5 checksum.
    /// </summary>
    /// <remarks>
    /// <para>二进制布局（共 24 字节）：</para>
    /// Binary layout (24 bytes total):
    /// <list type="table">
    /// <listheader><term>偏移/Offset</term><description>大小/Size | 字段/Field | 类型/Type</description></listheader>
    /// <item><term>0</term><description>4 | Offset | uint32</description></item>
    /// <item><term>4</term><description>4 | Length | uint32</description></item>
    /// <item><term>8</term><description>16 | Md5Checksum | byte[]</description></item>
    /// </list>
    /// </remarks>
    public sealed class Nb0Md5Record
    {
        /// <summary>
        /// <para>MD5 校验记录结构的固定大小（字节）。</para>
        /// The fixed size of the MD5 record structure in bytes.
        /// </summary>
        public const int StructSize = 24;

        private const int Md5Size = 16;

        /// <summary>
        /// <para>获取或设置数据偏移量。</para>
        /// Gets or sets the data offset.
        /// </summary>
        public uint Offset { get; set; }

        /// <summary>
        /// <para>获取或设置数据长度。</para>
        /// Gets or sets the data length.
        /// </summary>
        public uint Length { get; set; }

        private byte[] _md5Checksum = new byte[Md5Size];

        /// <summary>
        /// <para>获取或设置 MD5 校验和（16 字节）。获取时返回防御性副本，设置时创建输入数组的副本。</para>
        /// Gets or sets the MD5 checksum (16 bytes). Getting returns a defensive copy; setting creates a copy of the input array.
        /// </summary>
        public byte[] Md5Checksum
        {
            get
            {
                var copy = new byte[_md5Checksum.Length];
                Array.Copy(_md5Checksum, copy, _md5Checksum.Length);
                return copy;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                _md5Checksum = new byte[Md5Size];
                Array.Copy(value, _md5Checksum, Math.Min(value.Length, Md5Size));
            }
        }

        /// <summary>
        /// <para>从二进制读取器中读取并构造一个 <see cref="Nb0Md5Record"/> 实例。</para>
        /// Reads and constructs an <see cref="Nb0Md5Record"/> instance from a binary reader.
        /// </summary>
        /// <param name="reader">
        /// <para>要读取的 <see cref="BinaryReader"/> 实例。</para>
        /// The <see cref="BinaryReader"/> instance to read from.
        /// </param>
        /// <returns>
        /// <para>读取到的 <see cref="Nb0Md5Record"/> 实例。</para>
        /// The read <see cref="Nb0Md5Record"/> instance.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="reader"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="reader"/> is null.
        /// </exception>
        public static Nb0Md5Record Read(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var record = new Nb0Md5Record();
            record.Offset = reader.ReadUInt32();
            record.Length = reader.ReadUInt32();
            record.ReadMd5ChecksumDirect(reader);

            return record;
        }

        /// <summary>
        /// <para>直接从二进制读取器中读取 MD5 校验和到内部字段，避免属性 setter 的防御性拷贝。</para>
        /// Reads the MD5 checksum directly from the binary reader into the internal field, avoiding the defensive copy in the property setter.
        /// </summary>
        /// <param name="reader">
        /// <para>要读取的 <see cref="BinaryReader"/> 实例。</para>
        /// The <see cref="BinaryReader"/> instance to read from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="reader"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="reader"/> is null.
        /// </exception>
        internal void ReadMd5ChecksumDirect(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            byte[] readBytes = reader.ReadBytes(Md5Size);
            int copyLen = Math.Min(readBytes.Length, Md5Size);
            Array.Copy(readBytes, _md5Checksum, copyLen);
        }

        /// <summary>
        /// <para>将当前 MD5 校验记录写入指定的二进制写入器。</para>
        /// Writes the current MD5 record to the specified binary writer.
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

            writer.Write(Offset);
            writer.Write(Length);
            writer.Write(_md5Checksum);
        }

        /// <summary>
        /// <para>判断指定的字节数组是否与当前记录的 MD5 校验和相等，不创建防御性副本。</para>
        /// Determines whether the specified byte array equals the current record's MD5 checksum without creating a defensive copy.
        /// </summary>
        /// <param name="other">
        /// <para>要比较的字节数组。</para>
        /// The byte array to compare.
        /// </param>
        /// <returns>
        /// <para>如果相等则为 <c>true</c>，否则为 <c>false</c>。</para>
        /// <c>true</c> if equal; otherwise, <c>false</c>.
        /// </returns>
        public bool IsChecksumEqual(byte[] other)
        {
            if (other == null || other.Length != _md5Checksum.Length)
                return false;
            for (int i = 0; i < _md5Checksum.Length; i++)
            {
                if (_md5Checksum[i] != other[i])
                    return false;
            }
            return true;
        }
    }
}
