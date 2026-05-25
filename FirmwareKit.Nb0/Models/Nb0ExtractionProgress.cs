namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>表示 NB0 固件文件提取操作的进度信息。</para>
    /// Represents progress information for an NB0 firmware file extraction operation.
    /// </summary>
    public sealed class Nb0ExtractionProgress
    {
        /// <summary>
        /// <para>待提取的条目总数。</para>
        /// The total number of entries to extract.
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// <para>已完成提取的条目数。</para>
        /// The number of entries that have been extracted.
        /// </summary>
        public int CompletedEntries { get; set; }

        /// <summary>
        /// <para>当前正在提取的条目名称。</para>
        /// The name of the entry currently being extracted.
        /// </summary>
        public string CurrentEntryName { get; set; } = string.Empty;
    }
}
