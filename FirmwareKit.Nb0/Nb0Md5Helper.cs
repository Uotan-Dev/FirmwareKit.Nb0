namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>提供 NB0 固件文件中 MD5 校验记录的查找和比较功能。</para>
    /// Provides functionality to find and compare MD5 verification records in NB0 firmware files.
    /// </summary>
    internal static class Nb0Md5Helper
    {
        /// <summary>
        /// <para>根据偏移量或条目索引查找对应的 MD5 记录。</para>
        /// Finds the corresponding MD5 record by offset or entry index.
        /// </summary>
        /// <param name="entry">条目信息。</param>
        /// <param name="entryIndex">条目索引。</param>
        /// <param name="metadata">元数据。</param>
        /// <returns>匹配的 MD5 记录，如果未找到则返回 null。</returns>
        internal static Nb0Md5Record? FindMd5Record(Nb0FileEntry entry, int entryIndex, Nb0Metadata metadata)
        {
            // First try to match by offset using dictionary (O(1))
            long absoluteOffset = entry.FileDataOffset + entry.Offset;
            if (absoluteOffset >= 0 && absoluteOffset <= uint.MaxValue)
            {
                if (metadata.Md5RecordByOffset != null && metadata.Md5RecordByOffset.TryGetValue((uint)absoluteOffset, out var record))
                    return record;
            }

            // Fall back to linear search by offset if dictionary not built
            for (int i = 0; i < metadata.Md5Records.Count; i++)
            {
                if (absoluteOffset >= 0 && absoluteOffset <= uint.MaxValue && metadata.Md5Records[i].Offset == (uint)absoluteOffset)
                    return metadata.Md5Records[i];
            }

            // Fall back to matching by index
            if (entryIndex < metadata.Md5Records.Count)
                return metadata.Md5Records[entryIndex];

            return null;
        }
    }
}
