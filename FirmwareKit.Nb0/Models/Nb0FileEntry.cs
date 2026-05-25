namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>表示 NB0 固件文件中的一个条目信息。</para>
    /// Represents information about an entry in an NB0 firmware file.
    /// </summary>
    public sealed class Nb0FileEntry
    {
        /// <summary>
        /// <para>获取或设置条目名称。</para>
        /// Gets or sets the entry name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>获取或设置条目数据的大小（字节）。</para>
        /// Gets or sets the size of the entry data in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// <para>获取或设置条目数据在数据段中的偏移量。</para>
        /// Gets or sets the offset of the entry data within the data section.
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// <para>获取或设置数据段在文件中的起始偏移量。</para>
        /// Gets or sets the starting offset of the data section within the file.
        /// </summary>
        public long FileDataOffset { get; set; }

        /// <summary>
        /// <para>获取或设置推断的条目类型。</para>
        /// Gets or sets the inferred entry type.
        /// </summary>
        public string? InferredType { get; set; }

        /// <summary>
        /// <para>从 <see cref="Nb0EntryHeader"/> 创建 <see cref="Nb0FileEntry"/> 实例。</para>
        /// Creates an <see cref="Nb0FileEntry"/> instance from an <see cref="Nb0EntryHeader"/>.
        /// </summary>
        /// <param name="header">
        /// <para>源条目头。</para>
        /// The source entry header.
        /// </param>
        /// <param name="fileDataOffset">
        /// <para>数据段在文件中的起始偏移量。</para>
        /// The starting offset of the data section within the file.
        /// </param>
        /// <returns>
        /// <para>创建的 <see cref="Nb0FileEntry"/> 实例。</para>
        /// The created <see cref="Nb0FileEntry"/> instance.
        /// </returns>
        internal static Nb0FileEntry FromHeader(Nb0EntryHeader header, long fileDataOffset)
        {
            return new Nb0FileEntry
            {
                Name = header.Name,
                Size = header.FileSize,
                Offset = header.DataOffset,
                FileDataOffset = fileDataOffset
            };
        }
    }
}
