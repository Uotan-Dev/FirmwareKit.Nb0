namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>表示 NB0 固件文件校验操作的进度信息。</para>
    /// Represents progress information for an NB0 firmware file verification operation.
    /// </summary>
    public sealed class Nb0CheckProgress
    {
        /// <summary>
        /// <para>待校验的条目总数。</para>
        /// The total number of entries to verify.
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// <para>已完成校验的条目数。</para>
        /// The number of entries that have been verified.
        /// </summary>
        public int CompletedEntries { get; set; }

        /// <summary>
        /// <para>当前正在校验的条目名称。</para>
        /// The name of the entry currently being verified.
        /// </summary>
        public string CurrentEntryName { get; set; } = string.Empty;
    }
}
