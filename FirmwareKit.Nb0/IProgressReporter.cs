namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>定义进度报告器的接口，用于在 NB0 文件操作过程中报告进度信息。</para>
    /// Defines the interface for progress reporters, used to report progress information during NB0 file operations.
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// <para>报告当前操作的进度信息。</para>
        /// Reports progress information for the current operation.
        /// </summary>
        /// <param name="report">
        /// <para>包含当前进度详细信息的 <see cref="ProgressReport"/> 实例。</para>
        /// The <see cref="ProgressReport"/> instance containing current progress details.
        /// </param>
        void Report(ProgressReport report);
    }

    /// <summary>
    /// <para>表示 NB0 文件操作过程中的进度报告，包含操作名称、当前项、字节进度等信息。</para>
    /// Represents a progress report during NB0 file operations, containing operation name, current item, byte progress, and other information.
    /// </summary>
    public sealed class ProgressReport
    {
        /// <summary>
        /// <para>获取或设置当前操作的名称。</para>
        /// Gets or sets the name of the current operation.
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// <para>获取或设置当前正在处理的条目名称。</para>
        /// Gets or sets the name of the entry currently being processed.
        /// </summary>
        public string CurrentItem { get; set; } = string.Empty;

        /// <summary>
        /// <para>获取或设置当前处理的条目索引（从 0 开始）。</para>
        /// Gets or sets the index of the entry currently being processed (0-based).
        /// </summary>
        public int CurrentIndex { get; set; }

        /// <summary>
        /// <para>获取或设置要处理的条目总数。</para>
        /// Gets or sets the total number of entries to process.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// <para>获取或设置已处理的字节数。</para>
        /// Gets or sets the number of bytes processed.
        /// </summary>
        public long BytesProcessed { get; set; }

        /// <summary>
        /// <para>获取或设置总字节数。</para>
        /// Gets or sets the total number of bytes.
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// <para>获取当前进度百分比。优先基于字节进度计算，若总字节数为 0 则基于条目索引计算。</para>
        /// Gets the current progress percentage. Calculated based on byte progress first; if total bytes is 0, calculated based on entry index.
        /// </summary>
        /// <value>
        /// <para>进度百分比，范围 0.0 到 100.0。</para>
        /// The progress percentage, ranging from 0.0 to 100.0.
        /// </value>
        public double Progress => TotalBytes > 0 ? (double)BytesProcessed / TotalBytes * 100.0 : (TotalCount > 0 ? (double)CurrentIndex / TotalCount * 100.0 : 0);

        /// <summary>
        /// <para>获取或设置可选的自定义进度消息。</para>
        /// Gets or sets an optional custom progress message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// <para>返回表示当前进度报告的字符串。</para>
        /// Returns a string that represents the current progress report.
        /// </summary>
        /// <returns>
        /// <para>包含进度百分比、操作名称和当前项信息的格式化字符串。</para>
        /// A formatted string containing progress percentage, operation name, and current item information.
        /// </returns>
        public override string ToString()
        {
            string pct = Progress.ToString("F1");
            if (!string.IsNullOrEmpty(Message))
                return $"[{pct}%] {Operation}: {Message}";
            return $"[{pct}%] {Operation}: {CurrentItem} ({CurrentIndex}/{TotalCount})";
        }
    }
}
