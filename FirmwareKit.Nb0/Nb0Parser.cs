using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>提供 FIH Mobile NB0 固件文件的解析功能，包括条目头解析、魔数检测、固件信息推断及 MD5 校验记录解析。</para>
    /// Provides parsing functionality for FIH Mobile NB0 firmware files, including entry header parsing, magic number detection, firmware info inference, and MD5 verification record parsing.
    /// </summary>
    public static class Nb0Parser
    {
        private static readonly (byte[] Magic, string TypeName)[] KnownMagicSignatures = new[]
        {
            (new byte[] { 0x1F, 0x8B }, "gzip"),
            (new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "zip"),
            (new byte[] { 0x41, 0x4E, 0x44, 0x52, 0x4F, 0x49, 0x44 }, "boot"),
            (new byte[] { 0xED, 0x26, 0xFF, 0x3A }, "sparse"),
            (new byte[] { 0xD0, 0x0D, 0xFE, 0xED }, "dtb"),
            (new byte[] { 0x53, 0xEF }, "ext4"),
            (new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, "ELF"),
            (new byte[] { 0x41, 0x56, 0x42, 0x30 }, "AVB"),
            (new byte[] { 0x73, 0x71, 0x73, 0x68 }, "squashfs"),
        };

        /// <summary>
        /// <para>解析指定路径的 NB0 固件文件，返回元数据信息。</para>
        /// Parses the NB0 firmware file at the specified path and returns metadata information.
        /// </summary>
        /// <param name="filePath">
        /// <para>要解析的 NB0 文件路径。</para>
        /// The path of the NB0 file to parse.
        /// </param>
        /// <returns>
        /// <para>包含解析结果的 <see cref="Nb0Metadata"/> 实例。</para>
        /// An <see cref="Nb0Metadata"/> instance containing the parsing results.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 为 null 或空字符串时抛出。</para>
        /// Thrown when <paramref name="filePath"/> is null or empty.
        /// </exception>
        public static Nb0Metadata Parse(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (filePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(filePath));
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            return ParseFromStream(stream);
        }

        /// <summary>
        /// <para>异步解析指定路径的 NB0 固件文件，返回元数据信息。</para>
        /// Asynchronously parses the NB0 firmware file at the specified path and returns metadata information.
        /// </summary>
        /// <param name="filePath">
        /// <para>要解析的 NB0 文件路径。</para>
        /// The path of the NB0 file to parse.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>包含解析结果的 <see cref="Nb0Metadata"/> 实例。</para>
        /// An <see cref="Nb0Metadata"/> instance containing the parsing results.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 为 null 或空字符串时抛出。</para>
        /// Thrown when <paramref name="filePath"/> is null or empty.
        /// </exception>
        public static async Task<Nb0Metadata> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (filePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(filePath));
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            return await ParseFromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// <para>从流中解析 NB0 固件文件，返回元数据信息。</para>
        /// Parses an NB0 firmware file from a stream and returns metadata information.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流。</para>
        /// The stream containing NB0 firmware data.
        /// </param>
        /// <returns>
        /// <para>包含解析结果的 <see cref="Nb0Metadata"/> 实例。</para>
        /// An <see cref="Nb0Metadata"/> instance containing the parsing results.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="stream"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="stream"/> is null.
        /// </exception>
        public static Nb0Metadata ParseFromStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            return ParseCore(reader, stream);
        }

        /// <summary>
        /// <para>异步从流中解析 NB0 固件文件，返回元数据信息。</para>
        /// Asynchronously parses an NB0 firmware file from a stream and returns metadata information.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流。</para>
        /// The stream containing NB0 firmware data.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>包含解析结果的 <see cref="Nb0Metadata"/> 实例。</para>
        /// An <see cref="Nb0Metadata"/> instance containing the parsing results.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="stream"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="stream"/> is null.
        /// </exception>
        public static async Task<Nb0Metadata> ParseFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var buffer = new byte[Nb0EntryHeader.StructSize];
            await StreamHelper.ReadExactlyAsync(stream, buffer, 0, 4, cancellationToken).ConfigureAwait(false);
            uint rawCount = BitConverter.ToUInt32(buffer, 0);
            if (rawCount > int.MaxValue)
                throw new Nb0CorruptedException($"Entry count exceeds maximum supported value: {rawCount}");
            int entryCount = (int)rawCount;

            if (stream.CanSeek)
            {
                long requiredSize = 4 + (long)entryCount * Nb0EntryHeader.StructSize;
                if (requiredSize > stream.Length)
                    throw new Nb0CorruptedException($"File too small to contain declared entry count: {entryCount} entries require at least {requiredSize} bytes, but file is only {stream.Length} bytes");
            }

            var metadata = new Nb0Metadata
            {
                EntryCount = entryCount,
                DataSectionOffset = 4 + (long)entryCount * Nb0EntryHeader.StructSize
            };

            for (int i = 0; i < entryCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await StreamHelper.ReadExactlyAsync(stream, buffer, 0, Nb0EntryHeader.StructSize, cancellationToken).ConfigureAwait(false);
                var header = Nb0EntryHeader.ReadFromBuffer(buffer, 0);

                var fileEntry = Nb0FileEntry.FromHeader(header, metadata.DataSectionOffset);
                fileEntry.InferredType = DetectTypeFromMagic(stream, metadata.DataSectionOffset + header.DataOffset);
                metadata.Entries.Add(fileEntry);
            }

            TryParseMd5Entry(stream, metadata);

            InferFirmwareInfo(metadata);
            ValidateMetadata(metadata, stream.CanSeek ? stream.Length : 0);

            // Calculate TotalSize
            if (stream.CanSeek)
            {
                metadata.TotalSize = stream.Length;
            }
            else if (metadata.Entries.Count > 0)
            {
                var lastEntry = metadata.Entries[metadata.Entries.Count - 1];
                long lastEntryEnd = lastEntry.Offset + lastEntry.Size;
                long remainder = lastEntryEnd % 4;
                if (remainder != 0) lastEntryEnd += (4 - remainder);
                metadata.TotalSize = metadata.DataSectionOffset + lastEntryEnd;
            }
            else
            {
                metadata.TotalSize = metadata.DataSectionOffset;
            }

            return metadata;
        }

        /// <summary>
        /// <para>解析指定路径的 NB0 固件文件，并以 JSON 格式返回元数据。</para>
        /// Parses the NB0 firmware file at the specified path and returns metadata in JSON format.
        /// </summary>
        /// <param name="filePath">
        /// <para>要解析的 NB0 文件路径。</para>
        /// The path of the NB0 file to parse.
        /// </param>
        /// <param name="compact">
        /// <para>是否使用紧凑格式输出 JSON。默认为 <c>false</c>。</para>
        /// Whether to output JSON in compact format. Defaults to <c>false</c>.
        /// </param>
        /// <returns>
        /// <para>包含元数据信息的 JSON 字符串。</para>
        /// A JSON string containing metadata information.
        /// </returns>
        public static string ParseAsJson(string filePath, bool compact = false)
        {
            var metadata = Parse(filePath);
            return MetadataToJson(metadata, compact);
        }

        /// <summary>
        /// <para>异步解析指定路径的 NB0 固件文件，并以 JSON 格式返回元数据。</para>
        /// Asynchronously parses the NB0 firmware file at the specified path and returns metadata in JSON format.
        /// </summary>
        /// <param name="filePath">
        /// <para>要解析的 NB0 文件路径。</para>
        /// The path of the NB0 file to parse.
        /// </param>
        /// <param name="compact">
        /// <para>是否使用紧凑格式输出 JSON。默认为 <c>false</c>。</para>
        /// Whether to output JSON in compact format. Defaults to <c>false</c>.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>包含元数据信息的 JSON 字符串。</para>
        /// A JSON string containing metadata information.
        /// </returns>
        public static async Task<string> ParseAsJsonAsync(string filePath, bool compact = false, CancellationToken cancellationToken = default)
        {
            var metadata = await ParseAsync(filePath, cancellationToken).ConfigureAwait(false);
            return MetadataToJson(metadata, compact);
        }

        /// <summary>
        /// <para>使用 <see cref="BinaryReader"/> 解析 NB0 固件文件的核心逻辑，包括条目头读取、魔数检测、MD5 校验记录解析、固件信息推断和元数据验证。</para>
        /// Core parsing logic for NB0 firmware files using a <see cref="BinaryReader"/>, including entry header reading, magic number detection, MD5 verification record parsing, firmware info inference, and metadata validation.
        /// </summary>
        /// <param name="reader">
        /// <para>用于读取二进制数据的 <see cref="BinaryReader"/> 实例。</para>
        /// The <see cref="BinaryReader"/> instance for reading binary data.
        /// </param>
        /// <param name="stream">
        /// <para>底层数据流，用于魔数检测时的随机访问。</para>
        /// The underlying data stream for random access during magic number detection.
        /// </param>
        /// <returns>
        /// <para>包含解析结果的 <see cref="Nb0Metadata"/> 实例。</para>
        /// An <see cref="Nb0Metadata"/> instance containing the parsing results.
        /// </returns>
        internal static Nb0Metadata ParseCore(BinaryReader reader, Stream stream)
        {
            byte[] countBytes = reader.ReadBytes(4);
            uint rawCount = BitConverter.ToUInt32(countBytes, 0);
            if (rawCount > int.MaxValue)
                throw new Nb0CorruptedException($"Entry count exceeds maximum supported value: {rawCount}");
            int entryCount = (int)rawCount;

            if (stream.CanSeek)
            {
                long requiredSize = 4 + (long)entryCount * Nb0EntryHeader.StructSize;
                if (requiredSize > stream.Length)
                    throw new Nb0CorruptedException($"File too small to contain declared entry count: {entryCount} entries require at least {requiredSize} bytes, but file is only {stream.Length} bytes");
            }

            var metadata = new Nb0Metadata
            {
                EntryCount = entryCount,
                DataSectionOffset = 4 + (long)entryCount * Nb0EntryHeader.StructSize
            };

            for (int i = 0; i < entryCount; i++)
            {
                var header = Nb0EntryHeader.Read(reader);
                var fileEntry = Nb0FileEntry.FromHeader(header, metadata.DataSectionOffset);
                fileEntry.InferredType = DetectTypeFromMagic(stream, metadata.DataSectionOffset + header.DataOffset);
                metadata.Entries.Add(fileEntry);
            }

            TryParseMd5Entry(stream, metadata);

            InferFirmwareInfo(metadata);
            ValidateMetadata(metadata, stream.CanSeek ? stream.Length : 0);

            // Calculate TotalSize
            if (stream.CanSeek)
            {
                metadata.TotalSize = stream.Length;
            }
            else if (metadata.Entries.Count > 0)
            {
                var lastEntry = metadata.Entries[metadata.Entries.Count - 1];
                long lastEntryEnd = lastEntry.Offset + lastEntry.Size;
                long remainder = lastEntryEnd % 4;
                if (remainder != 0) lastEntryEnd += (4 - remainder);
                metadata.TotalSize = metadata.DataSectionOffset + lastEntryEnd;
            }
            else
            {
                metadata.TotalSize = metadata.DataSectionOffset;
            }

            return metadata;
        }

        /// <summary>
        /// <para>从数据流中检测指定偏移量处的魔数签名，推断条目数据类型。</para>
        /// Detects magic number signatures at the specified offset in the data stream to infer the entry data type.
        /// </summary>
        /// <param name="stream">
        /// <para>要检测的数据流。</para>
        /// The data stream to detect.
        /// </param>
        /// <param name="offset">
        /// <para>要检测的流中的偏移量。</para>
        /// The offset in the stream to detect.
        /// </param>
        /// <returns>
        /// <para>检测到的类型名称；如果无法识别则返回 <c>null</c>。</para>
        /// The detected type name; or <c>null</c> if unrecognized.
        /// </returns>
        internal static string? DetectTypeFromMagic(Stream stream, long offset)
        {
            long originalPosition = 0;
            try
            {
                originalPosition = stream.Position;
                byte[] magic = new byte[8];

                stream.Seek(offset, SeekOrigin.Begin);
                int read = stream.Read(magic, 0, magic.Length);

                if (read >= 2)
                {
                    foreach (var (sig, typeName) in KnownMagicSignatures)
                    {
                        if (read >= sig.Length)
                        {
                            bool match = true;
                            for (int i = 0; i < sig.Length; i++)
                            {
                                if (magic[i] != sig[i]) { match = false; break; }
                            }
                            if (match)
                            {
                                stream.Seek(originalPosition, SeekOrigin.Begin);
                                return typeName;
                            }
                        }
                    }
                }

                if (read >= 8)
                {
                    if (offset + 0x438 < stream.Length)
                    {
                        stream.Seek(offset + 0x438, SeekOrigin.Begin);
                        // Reuse the same magic buffer for ext4 detection (only need 2 bytes)
                        int ext4Read = stream.Read(magic, 0, 2);
                        if (ext4Read >= 2 && magic[0] == 0x53 && magic[1] == 0xEF)
                        {
                            stream.Seek(originalPosition, SeekOrigin.Begin);
                            return "ext4";
                        }
                    }
                }

                stream.Seek(originalPosition, SeekOrigin.Begin);
                return null;
            }
            catch (IOException)
            {
                try { stream.Seek(originalPosition, SeekOrigin.Begin); } catch { }
                return null;
            }
            catch (NotSupportedException)
            {
                try { stream.Seek(originalPosition, SeekOrigin.Begin); } catch { }
                return null;
            }
            catch (ObjectDisposedException)
            {
                try { stream.Seek(originalPosition, SeekOrigin.Begin); } catch { }
                return null;
            }
        }

        /// <summary>
        /// <para>根据条目名称推断固件类型、设备型号和版本信息。</para>
        /// Infers firmware type, device model, and version information based on entry names.
        /// </summary>
        /// <param name="metadata">
        /// <para>要推断的元数据实例。</para>
        /// The metadata instance to infer.
        /// </param>
        internal static void InferFirmwareInfo(Nb0Metadata metadata)
        {
            foreach (var entry in metadata.Entries)
            {
                string name = entry.Name;
                if (name.IndexOf("spreadtrum", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("unisoc", StringComparison.OrdinalIgnoreCase) >= 0 || (name.IndexOf("sc", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("88", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    metadata.InferredFirmwareType = "Spreadtrum/UNISOC";
                }
                if (name.IndexOf("uwe", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("udf", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("fdl", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    metadata.InferredFirmwareType ??= "Spreadtrum/UNISOC";
                }
                if (name.IndexOf("boot", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("bootloader", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    metadata.InferredFirmwareType ??= "Android Boot";
                }
                if (name.IndexOf("modem", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("nvitem", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    metadata.InferredFirmwareType ??= "Spreadtrum/UNISOC";
                }
            }
        }

        /// <summary>
        /// <para>验证元数据的完整性，检查条目数量、名称唯一性和数据有效性，并将问题添加到警告列表。</para>
        /// Validates metadata integrity, checking entry count, name uniqueness, and data validity, adding issues to the warnings list.
        /// </summary>
        /// <param name="metadata">
        /// <para>要验证的元数据实例。</para>
        /// The metadata instance to validate.
        /// </param>
        /// <param name="streamLength">
        /// <para>数据流的长度，用于检测条目偏移是否超出文件末尾。默认为 0，表示不进行文件末尾溢出检测。</para>
        /// The length of the data stream, used to detect if entry offsets exceed the file end. Defaults to 0, which skips file-end overflow detection.
        /// </param>
        /// <exception cref="Nb0CorruptedException">
        /// <para>当条目数量为负数时抛出。</para>
        /// Thrown when the entry count is negative.
        /// </exception>
        internal static void ValidateMetadata(Nb0Metadata metadata, long streamLength = 0)
        {
            if (metadata.EntryCount < 0)
                throw new Nb0CorruptedException($"Invalid entry count: {metadata.EntryCount}");

            if (metadata.EntryCount > 1024)
                metadata.Warnings.Add($"Unusually high entry count: {metadata.EntryCount}");

            var names = new HashSet<string>();
            foreach (var entry in metadata.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    metadata.Warnings.Add($"Entry at offset 0x{entry.Offset:X16} has an empty name");

                if (!names.Add(entry.Name))
                    metadata.Warnings.Add($"Duplicate entry name: '{entry.Name}'");

                if (entry.Size == 0)
                    metadata.Warnings.Add($"Entry '{entry.Name}' has zero size");

                if (entry.Offset < 0)
                    metadata.Warnings.Add($"Entry '{entry.Name}' has negative offset");
            }

            // Check for overlapping data ranges - O(n log n) using sorted intervals
            if (metadata.Entries.Count > 1)
            {
                var sorted = new List<(long Offset, long End, string Name)>(metadata.Entries.Count);
                foreach (var entry in metadata.Entries)
                {
                    if (entry.Size > 0)
                        sorted.Add((entry.Offset, entry.Offset + entry.Size, entry.Name));
                }
                if (sorted.Count > 1)
                {
                    sorted.Sort((a, b) => a.Offset.CompareTo(b.Offset));
                    for (int i = 0; i < sorted.Count - 1; i++)
                    {
                        if (sorted[i].End > sorted[i + 1].Offset)
                        {
                            metadata.Warnings.Add($"Overlapping data between '{sorted[i].Name}' and '{sorted[i + 1].Name}'");
                        }
                    }
                }
            }

            // Check for non-monotonically increasing offsets
            for (int i = 1; i < metadata.Entries.Count; i++)
            {
                if (metadata.Entries[i].Offset < metadata.Entries[i - 1].Offset)
                {
                    metadata.Warnings.Add($"Entry '{metadata.Entries[i].Name}' offset (0x{metadata.Entries[i].Offset:X}) is less than previous entry '{metadata.Entries[i - 1].Name}' offset (0x{metadata.Entries[i - 1].Offset:X})");
                }
            }

            // Check for data offsets exceeding file size
            if (streamLength > 0)
            {
                foreach (var entry in metadata.Entries)
                {
                    long entryEnd = metadata.DataSectionOffset + entry.Offset + entry.Size;
                    if (entryEnd > streamLength)
                    {
                        metadata.Warnings.Add($"Entry '{entry.Name}' data extends beyond file end (offset 0x{entry.Offset:X}, size {entry.Size}, file size {streamLength})");
                    }
                }
            }
        }

        /// <summary>
        /// <para>检查最后一个条目是否为 .md5 条目，如果是则解析其数据为 MD5 校验记录列表。</para>
        /// Checks whether the last entry is a .md5 entry, and if so, parses its data into a list of MD5 verification records.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流。</para>
        /// The stream containing NB0 firmware data.
        /// </param>
        /// <param name="metadata">
        /// <para>已解析的元数据实例，MD5 校验记录将存储在其中。</para>
        /// The parsed metadata instance where MD5 verification records will be stored.
        /// </param>
        private static void TryParseMd5Entry(Stream stream, Nb0Metadata metadata)
        {
            if (metadata.Entries.Count == 0)
                return;

            var lastEntry = metadata.Entries[metadata.Entries.Count - 1];
            if (!lastEntry.Name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                // Position the stream at the .md5 entry data before parsing
                stream.Seek(metadata.DataSectionOffset + lastEntry.Offset, SeekOrigin.Begin);
                metadata.Md5Records = ParseMd5Records(stream, lastEntry);
                metadata.BuildMd5RecordIndex();

                // Validate MD5 record count matches non-md5 entry count
                int nonMd5EntryCount = metadata.Entries.Count - 1; // Exclude the .md5 entry itself
                if (metadata.Md5Records.Count != nonMd5EntryCount)
                {
                    metadata.Warnings.Add($"MD5 record count ({metadata.Md5Records.Count}) does not match non-md5 entry count ({nonMd5EntryCount})");
                }
            }
            catch (Exception ex)
            {
                metadata.Warnings.Add($"Failed to parse MD5 records from entry '{lastEntry.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// <para>从数据流中解析指定 MD5 条目的校验记录，每条记录为 24 字节（4 字节偏移量 + 4 字节长度 + 16 字节 MD5 校验和）。直接从流中读取，避免中间缓冲区。</para>
        /// Parses MD5 verification records from the data stream for the specified MD5 entry. Each record is 24 bytes (4-byte offset + 4-byte length + 16-byte MD5 checksum). Reads directly from the stream without an intermediate buffer.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流，已定位到 .md5 条目数据的起始位置。</para>
        /// The stream containing NB0 firmware data, already positioned at the start of the .md5 entry data.
        /// </param>
        /// <param name="md5Entry">
        /// <para>表示 .md5 条目的 <see cref="Nb0FileEntry"/> 实例。</para>
        /// The <see cref="Nb0FileEntry"/> instance representing the .md5 entry.
        /// </param>
        /// <returns>
        /// <para>解析得到的 <see cref="Nb0Md5Record"/> 列表。</para>
        /// The list of parsed <see cref="Nb0Md5Record"/> instances.
        /// </returns>
        internal static List<Nb0Md5Record> ParseMd5Records(Stream stream, Nb0FileEntry md5Entry)
        {
            var records = new List<Nb0Md5Record>();

            // The stream is already positioned at the .md5 entry data
            // Read MD5 records directly from the stream
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            int recordCount = (int)(md5Entry.Size / Nb0Md5Record.StructSize);
            for (int i = 0; i < recordCount; i++)
            {
                var record = Nb0Md5Record.Read(reader);
                records.Add(record);
            }

            return records;
        }

        private static string MetadataToJson(Nb0Metadata metadata, bool compact)
        {
            var sb = new StringBuilder();
            string indent = compact ? "" : "  ";
            string newline = compact ? "" : Environment.NewLine;

            sb.Append("{").Append(newline);
            sb.Append($"{indent}\"entryCount\": {metadata.EntryCount},").Append(newline);
            sb.Append($"{indent}\"totalSize\": {metadata.TotalSize},").Append(newline);
            sb.Append($"{indent}\"dataSectionOffset\": {metadata.DataSectionOffset},").Append(newline);

            if (!string.IsNullOrEmpty(metadata.InferredFirmwareType))
                sb.Append($"{indent}\"inferredFirmwareType\": \"{metadata.InferredFirmwareType}\",").Append(newline);
            if (!string.IsNullOrEmpty(metadata.InferredDeviceModel))
                sb.Append($"{indent}\"inferredDeviceModel\": \"{metadata.InferredDeviceModel}\",").Append(newline);
            if (!string.IsNullOrEmpty(metadata.InferredVersion))
                sb.Append($"{indent}\"inferredVersion\": \"{metadata.InferredVersion}\",").Append(newline);

            sb.Append($"{indent}\"entries\": [").Append(newline);
            for (int i = 0; i < metadata.Entries.Count; i++)
            {
                var e = metadata.Entries[i];
                string i2 = indent + indent;
                sb.Append($"{i2}{{").Append(newline);
                sb.Append($"{i2}{indent}\"name\": \"{EscapeJson(e.Name)}\",").Append(newline);
                sb.Append($"{i2}{indent}\"offset\": {e.Offset},").Append(newline);
                sb.Append($"{i2}{indent}\"size\": {e.Size}");
                if (!string.IsNullOrEmpty(e.InferredType))
                    sb.Append(",").Append(newline).Append($"{i2}{indent}\"inferredType\": \"{e.InferredType}\"");
                sb.Append(newline);
                sb.Append($"{i2}}}{(i < metadata.Entries.Count - 1 ? "," : "")}").Append(newline);
            }
            sb.Append($"{indent}],").Append(newline);

            if (metadata.HasMd5Records)
            {
                sb.Append($"{indent}\"md5Records\": [").Append(newline);
                for (int i = 0; i < metadata.Md5Records.Count; i++)
                {
                    var r = metadata.Md5Records[i];
                    string i2 = indent + indent;
                    string hex = FormatMd5Hex(r.Md5Checksum);
                    sb.Append($"{i2}{{").Append(newline);
                    sb.Append($"{i2}{indent}\"offset\": {r.Offset},").Append(newline);
                    sb.Append($"{i2}{indent}\"length\": {r.Length},").Append(newline);
                    sb.Append($"{i2}{indent}\"md5Checksum\": \"{hex}\"").Append(newline);
                    sb.Append($"{i2}}}{(i < metadata.Md5Records.Count - 1 ? "," : "")}").Append(newline);
                }
                sb.Append($"{indent}],").Append(newline);
            }

            if (metadata.Warnings.Count > 0)
            {
                sb.Append($"{indent}\"warnings\": [").Append(newline);
                for (int i = 0; i < metadata.Warnings.Count; i++)
                {
                    sb.Append($"{indent}{indent}\"{EscapeJson(metadata.Warnings[i])}\"{(i < metadata.Warnings.Count - 1 ? "," : "")}").Append(newline);
                }
                sb.Append($"{indent}]").Append(newline);
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        /// <summary>
        /// <para>将字节数组格式化为小写十六进制字符串，避免 BitConverter.ToString 和 Replace 分配。</para>
        /// Formats a byte array as a lowercase hexadecimal string, avoiding BitConverter.ToString and Replace allocations.
        /// </summary>
        /// <param name="checksum">
        /// <para>要格式化的字节数组。</para>
        /// The byte array to format.
        /// </param>
        /// <returns>
        /// <para>小写十六进制字符串；如果输入为 <c>null</c> 则返回空字符串。</para>
        /// A lowercase hexadecimal string; or an empty string if the input is <c>null</c>.
        /// </returns>
        internal static string FormatMd5Hex(byte[] checksum)
        {
            if (checksum == null) return string.Empty;
            char[] chars = new char[checksum.Length * 2];
            const string hexDigits = "0123456789abcdef";
            for (int i = 0; i < checksum.Length; i++)
            {
                chars[i * 2] = hexDigits[checksum[i] >> 4];
                chars[i * 2 + 1] = hexDigits[checksum[i] & 0x0F];
            }
            return new string(chars);
        }

    }
}
