using System;
using System.IO;
using System.Text;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>表示 NB0 固件文件中的一个条目，包含头信息和数据。</para>
    /// Represents an entry in an NB0 firmware file, containing header information and data.
    /// </summary>
    public sealed class Nb0Entry
    {
        /// <summary>
        /// <para>获取条目头信息。</para>
        /// Gets the entry header information.
        /// </summary>
        public Nb0EntryHeader Header { get; }

        /// <summary>
        /// <para>获取或设置条目的原始数据。</para>
        /// Gets or sets the raw data of the entry.
        /// </summary>
        public byte[]? Data { get; set; }

        /// <summary>
        /// <para>获取或设置条目数据的文件路径。设置后，打包时将从文件流式读取数据，而非使用 <see cref="Data"/> 属性。</para>
        /// Gets or sets the file path for the entry data. When set, data will be read from the file at pack time instead of using the <see cref="Data"/> property.
        /// </summary>
        internal string? FilePath { get; set; }

        /// <summary>
        /// <para>使用指定的条目头初始化 <see cref="Nb0Entry"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0Entry"/> with the specified entry header.
        /// </summary>
        /// <param name="header">
        /// <para>条目头信息。</para>
        /// The entry header information.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="header"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="header"/> is null.
        /// </exception>
        public Nb0Entry(Nb0EntryHeader header)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
        }

        /// <summary>
        /// <para>使用指定的名称和数据初始化 <see cref="Nb0Entry"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0Entry"/> with the specified name and data.
        /// </summary>
        /// <param name="name">
        /// <para>条目名称。</para>
        /// The entry name.
        /// </param>
        /// <param name="data">
        /// <para>条目的原始数据。</para>
        /// The raw data of the entry.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="name"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="name"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>当 <paramref name="name"/> 为空字符串，或包含非 ASCII 字符时抛出。</para>
        /// Thrown when <paramref name="name"/> is empty, or contains non-ASCII characters.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="data"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="data"/> is null.
        /// </exception>
        public Nb0Entry(string name, byte[] data)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (name.Length == 0) throw new ArgumentException("Entry name cannot be empty.", nameof(name));
            if (data == null) throw new ArgumentNullException(nameof(data));

            // Validate that name contains only ASCII characters
            foreach (char c in name)
            {
                if (c > 127)
                    throw new ArgumentException($"Entry name contains non-ASCII character '{c}' (U+{((int)c):X4}). Entry names must be ASCII only.", nameof(name));
            }

            int nameByteCount = Encoding.ASCII.GetByteCount(name);
            if (nameByteCount > Nb0EntryHeader.NameLength - 1)
                throw new ArgumentException($"Entry name exceeds maximum length of {Nb0EntryHeader.NameLength - 1} ASCII bytes. Actual: {nameByteCount} bytes.", nameof(name));

            Header = new Nb0EntryHeader
            {
                Name = name,
                FileSize = data.Length
            };
            Data = data;
        }

        /// <summary>
        /// <para>获取条目的文件大小。如果数据为 null 则返回 0。</para>
        /// Gets the file size of the entry. Returns 0 if data is null.
        /// </summary>
        /// <returns>
        /// <para>数据长度（字节），若数据为 null 则为 0。</para>
        /// The data length in bytes, or 0 if data is null.
        /// </returns>
        public long GetFileSize()
        {
            if (Data != null)
                return Data.Length;
            if (FilePath != null)
            {
                var fi = new FileInfo(FilePath);
                return fi.Exists ? fi.Length : 0;
            }
            return 0;
        }
    }
}
