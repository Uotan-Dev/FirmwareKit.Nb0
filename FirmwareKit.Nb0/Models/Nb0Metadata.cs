using System;
using System.Collections.Generic;
using System.Text;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>表示 NB0 固件文件的元数据信息，包含条目列表、推断的固件信息及 MD5 校验记录。</para>
    /// Represents metadata information of an NB0 firmware file, containing entry list, inferred firmware info, and MD5 verification records.
    /// </summary>
    public sealed class Nb0Metadata
    {
        private Dictionary<uint, Nb0Md5Record>? _md5RecordByOffset;

        /// <summary>
        /// <para>获取或设置条目数量。</para>
        /// Gets or sets the number of entries.
        /// </summary>
        public int EntryCount { get; set; }

        /// <summary>
        /// <para>获取或设置文件总大小（字节）。</para>
        /// Gets or sets the total size of the file in bytes.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// <para>获取或设置数据段在文件中的起始偏移量。</para>
        /// Gets or sets the starting offset of the data section within the file.
        /// </summary>
        public long DataSectionOffset { get; set; }

        /// <summary>
        /// <para>获取或设置推断的固件类型。</para>
        /// Gets or sets the inferred firmware type.
        /// </summary>
        public string? InferredFirmwareType { get; set; }

        /// <summary>
        /// <para>获取或设置推断的设备型号。</para>
        /// Gets or sets the inferred device model.
        /// </summary>
        public string? InferredDeviceModel { get; set; }

        /// <summary>
        /// <para>获取或设置推断的版本号。</para>
        /// Gets or sets the inferred version.
        /// </summary>
        public string? InferredVersion { get; set; }

        /// <summary>
        /// <para>获取或设置 NB0 文件中的条目列表。</para>
        /// Gets or sets the list of entries in the NB0 file.
        /// </summary>
        public List<Nb0FileEntry> Entries { get; set; } = new();

        /// <summary>
        /// <para>获取或设置从 .md5 条目中解析出的 MD5 校验记录列表。</para>
        /// Gets or sets the list of MD5 verification records parsed from the .md5 entry.
        /// </summary>
        public List<Nb0Md5Record> Md5Records { get; set; } = new();

        /// <summary>
        /// <para>获取一个值，指示是否存在 MD5 校验记录。</para>
        /// Gets a value indicating whether MD5 verification records are present.
        /// </summary>
        /// <value>
        /// <para>如果存在 MD5 校验记录，则为 <c>true</c>；否则为 <c>false</c>。</para>
        /// <c>true</c> if MD5 verification records are present; otherwise, <c>false</c>.
        /// </value>
        public bool HasMd5Records => Md5Records.Count > 0;

        /// <summary>
        /// <para>构建 MD5 记录的偏移量索引字典，用于快速查找。</para>
        /// Builds the offset index dictionary for MD5 records for fast lookup.
        /// </summary>
        internal void BuildMd5RecordIndex()
        {
            _md5RecordByOffset = new Dictionary<uint, Nb0Md5Record>(Md5Records.Count);
            foreach (var record in Md5Records)
            {
                _md5RecordByOffset[record.Offset] = record;
            }
        }

        /// <summary>
        /// <para>获取按偏移量索引的 MD5 记录字典。</para>
        /// Gets the MD5 record dictionary indexed by offset.
        /// </summary>
        internal Dictionary<uint, Nb0Md5Record>? Md5RecordByOffset => _md5RecordByOffset;

        /// <summary>
        /// <para>获取或设置解析过程中产生的警告信息列表。</para>
        /// Gets or sets the list of warnings generated during parsing.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// <para>返回表示当前 NB0 元数据的字符串，包含条目信息和 MD5 校验记录。</para>
        /// Returns a string that represents the current NB0 metadata, including entry information and MD5 verification records.
        /// </summary>
        /// <returns>
        /// <para>包含格式化元数据信息的字符串。</para>
        /// A string containing formatted metadata information.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("NB0 Firmware Metadata");
            sb.AppendLine($"  Entry Count:        {EntryCount}");
            sb.AppendLine($"  Total Size:         {TotalSize} bytes ({TotalSize / (1024.0 * 1024.0):F2} MB)");
            sb.AppendLine($"  Data Section Offset: 0x{DataSectionOffset:X16}");

            if (!string.IsNullOrEmpty(InferredFirmwareType))
                sb.AppendLine($"  Firmware Type:      {InferredFirmwareType}");
            if (!string.IsNullOrEmpty(InferredDeviceModel))
                sb.AppendLine($"  Device Model:       {InferredDeviceModel}");
            if (!string.IsNullOrEmpty(InferredVersion))
                sb.AppendLine($"  Version:            {InferredVersion}");

            sb.AppendLine();
            sb.AppendLine("Entries:");
            foreach (var entry in Entries)
            {
                string type = !string.IsNullOrEmpty(entry.InferredType) ? $" [{entry.InferredType}]" : "";
                sb.AppendLine($"  {entry.Name}: offset=0x{entry.Offset:X16}, size=0x{entry.Size:X16}{type}");
            }

            if (HasMd5Records)
            {
                sb.AppendLine();
                sb.AppendLine($"MD5 Records ({Md5Records.Count}):");
                foreach (var md5 in Md5Records)
                {
                    string hex = Nb0Parser.FormatMd5Hex(md5.Md5Checksum);
                    sb.AppendLine($"  offset=0x{md5.Offset:X8}, length=0x{md5.Length:X8}, md5={hex}");
                }
            }

            if (Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Warnings:");
                foreach (var w in Warnings)
                    sb.AppendLine($"  - {w}");
            }

            return sb.ToString();
        }
    }
}
