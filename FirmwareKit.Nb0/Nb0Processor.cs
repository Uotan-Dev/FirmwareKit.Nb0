using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>提供 NB0 固件文件的解析、提取、校验和重新打包功能。</para>
    /// Provides parsing, extraction, verification, and repacking functionality for NB0 firmware files.
    /// </summary>
    public sealed class Nb0Processor
    {
        private const int RepackBufferSize = 65536;

        private static readonly byte[] PaddingBuffer = { 0, 0, 0 };

        private readonly byte[] _checkBuffer = new byte[65536];
        private readonly Nb0Extractor _extractor = new Nb0Extractor();

        /// <summary>
        /// <para>初始化 <see cref="Nb0Processor"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0Processor"/>.
        /// </summary>
        public Nb0Processor()
        {
        }

        /// <summary>
        /// <para>解析指定路径的 NB0 固件文件，返回元数据信息。</para>
        /// Parses the NB0 firmware file at the specified path and returns metadata information.
        /// </summary>
        /// <param name="filePath">
        /// <para>要解析的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to parse.
        /// </param>
        /// <returns>
        /// <para>包含 NB0 文件元数据的 <see cref="Nb0Metadata"/> 实例。</para>
        /// An <see cref="Nb0Metadata"/> instance containing the NB0 file metadata.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="filePath"/> is null or empty.
        /// </exception>
        public Nb0Metadata Parse(string filePath)
        {
            return Nb0Parser.Parse(filePath);
        }

        /// <summary>
        /// <para>异步解析指定路径的 NB0 固件文件，返回元数据信息。</para>
        /// Asynchronously parses the NB0 firmware file at the specified path and returns metadata information.
        /// </summary>
        /// <param name="filePath">
        /// <para>要解析的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to parse.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token for canceling the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>包含 NB0 文件元数据的 <see cref="Nb0Metadata"/> 实例。</para>
        /// An <see cref="Nb0Metadata"/> instance containing the NB0 file metadata.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="filePath"/> is null or empty.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <para>当操作被取消时抛出。</para>
        /// Thrown when the operation is canceled.
        /// </exception>
        public Task<Nb0Metadata> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Nb0Parser.ParseAsync(filePath, cancellationToken);
        }

        /// <summary>
        /// <para>从指定的 NB0 文件中提取所有条目到输出目录。</para>
        /// Extracts all entries from the specified NB0 file to the output directory.
        /// </summary>
        /// <param name="filePath">
        /// <para>要提取的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to extract.
        /// </param>
        /// <param name="outputDirectory">
        /// <para>提取文件的输出目录路径。</para>
        /// The output directory path for extracted files.
        /// </param>
        /// <param name="options">
        /// <para>可选的提取选项。</para>
        /// Optional extraction options.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告提取操作的进度。</para>
        /// An optional progress reporter for reporting extraction operation progress.
        /// </param>
        /// <returns>
        /// <para>包含提取结果的 <see cref="ExtractionResult"/> 实例。</para>
        /// An <see cref="ExtractionResult"/> instance containing the extraction results.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 或 <paramref name="outputDirectory"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="filePath"/> or <paramref name="outputDirectory"/> is null or empty.
        /// </exception>
        public ExtractionResult Extract(string filePath, string outputDirectory, ExtractionOptions? options = null, IProgress<Nb0ExtractionProgress>? progress = null)
        {
            return _extractor.Extract(filePath, outputDirectory, options, progress);
        }

        /// <summary>
        /// <para>异步从指定的 NB0 文件中提取所有条目到输出目录。</para>
        /// Asynchronously extracts all entries from the specified NB0 file to the output directory.
        /// </summary>
        /// <param name="filePath">
        /// <para>要提取的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to extract.
        /// </param>
        /// <param name="outputDirectory">
        /// <para>提取文件的输出目录路径。</para>
        /// The output directory path for extracted files.
        /// </param>
        /// <param name="options">
        /// <para>可选的提取选项。</para>
        /// Optional extraction options.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告提取操作的进度。</para>
        /// An optional progress reporter for reporting extraction operation progress.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token for canceling the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>包含提取结果的 <see cref="ExtractionResult"/> 实例。</para>
        /// An <see cref="ExtractionResult"/> instance containing the extraction results.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 或 <paramref name="outputDirectory"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="filePath"/> or <paramref name="outputDirectory"/> is null or empty.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <para>当操作被取消时抛出。</para>
        /// Thrown when the operation is canceled.
        /// </exception>
        public Task<ExtractionResult> ExtractAsync(string filePath, string outputDirectory, ExtractionOptions? options = null, IProgress<Nb0ExtractionProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return _extractor.ExtractAsync(filePath, outputDirectory, options, progress, cancellationToken);
        }

        /// <summary>
        /// <para>校验指定 NB0 固件文件中所有条目的 MD5 完整性。</para>
        /// Verifies the MD5 integrity of all entries in the specified NB0 firmware file.
        /// </summary>
        /// <param name="filePath">
        /// <para>要校验的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to verify.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告校验操作的进度。</para>
        /// An optional progress reporter for reporting verification operation progress.
        /// </param>
        /// <returns>
        /// <para>包含每个条目校验结果的 <see cref="CheckResult"/> 实例。</para>
        /// A <see cref="CheckResult"/> instance containing the verification results for each entry.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="filePath"/> is null or empty.
        /// </exception>
        public CheckResult Check(string filePath, IProgress<Nb0CheckProgress>? progress = null)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (filePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(filePath));

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            return CheckFromStream(stream, progress);
        }

        /// <summary>
        /// <para>异步校验指定 NB0 固件文件中所有条目的 MD5 完整性。</para>
        /// Asynchronously verifies the MD5 integrity of all entries in the specified NB0 firmware file.
        /// </summary>
        /// <param name="filePath">
        /// <para>要校验的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to verify.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告校验操作的进度。</para>
        /// An optional progress reporter for reporting verification operation progress.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token for canceling the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>包含每个条目校验结果的 <see cref="CheckResult"/> 实例。</para>
        /// A <see cref="CheckResult"/> instance containing the verification results for each entry.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="filePath"/> is null or empty.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <para>当操作被取消时抛出。</para>
        /// Thrown when the operation is canceled.
        /// </exception>
        public async Task<CheckResult> CheckAsync(string filePath, IProgress<Nb0CheckProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (filePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(filePath));

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            return await CheckFromStreamAsync(stream, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// <para>从流中校验 NB0 固件文件中所有条目的 MD5 完整性。</para>
        /// Verifies the MD5 integrity of all entries in the NB0 firmware file from a stream.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流。</para>
        /// The stream containing NB0 firmware data.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告校验操作的进度。</para>
        /// An optional progress reporter for reporting verification operation progress.
        /// </param>
        /// <returns>
        /// <para>包含每个条目校验结果的 <see cref="CheckResult"/> 实例。</para>
        /// A <see cref="CheckResult"/> instance containing the verification results for each entry.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="stream"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="stream"/> is null.
        /// </exception>
        public CheckResult CheckFromStream(Stream stream, IProgress<Nb0CheckProgress>? progress = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var metadata = Nb0Parser.ParseFromStream(stream);
            var result = new CheckResult
            {
                TotalEntries = metadata.Entries.Count,
                HasMd5Records = metadata.HasMd5Records
            };

            for (int i = 0; i < metadata.Entries.Count; i++)
            {
                var entry = metadata.Entries[i];

                progress?.Report(new Nb0CheckProgress
                {
                    TotalEntries = metadata.Entries.Count,
                    CompletedEntries = i,
                    CurrentEntryName = entry.Name
                });

                EntryCheckResult entryResult;
                if (entry.Name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase))
                {
                    entryResult = new EntryCheckResult
                    {
                        Name = entry.Name,
                        HasMd5Check = false,
                        IsValid = true
                    };
                }
                else
                {
                    entryResult = CheckEntry(stream, entry, i, metadata);
                }
                result.EntryResults.Add(entryResult);

                if (entryResult.HasMd5Check)
                {
                    if (entryResult.IsValid)
                        result.ValidEntries++;
                    else
                        result.InvalidEntries++;
                }
            }

            return result;
        }

        /// <summary>
        /// <para>异步从流中校验 NB0 固件文件中所有条目的 MD5 完整性。</para>
        /// Asynchronously verifies the MD5 integrity of all entries in the NB0 firmware file from a stream.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流。</para>
        /// The stream containing NB0 firmware data.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告校验操作的进度。</para>
        /// An optional progress reporter for reporting verification operation progress.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token for canceling the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>包含每个条目校验结果的 <see cref="CheckResult"/> 实例。</para>
        /// A <see cref="CheckResult"/> instance containing the verification results for each entry.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="stream"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="stream"/> is null.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <para>当操作被取消时抛出。</para>
        /// Thrown when the operation is canceled.
        /// </exception>
        public async Task<CheckResult> CheckFromStreamAsync(Stream stream, IProgress<Nb0CheckProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var metadata = await Nb0Parser.ParseFromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
            var result = new CheckResult
            {
                TotalEntries = metadata.Entries.Count,
                HasMd5Records = metadata.HasMd5Records
            };

            for (int i = 0; i < metadata.Entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = metadata.Entries[i];

                progress?.Report(new Nb0CheckProgress
                {
                    TotalEntries = metadata.Entries.Count,
                    CompletedEntries = i,
                    CurrentEntryName = entry.Name
                });

                EntryCheckResult entryResult;
                if (entry.Name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase))
                {
                    entryResult = new EntryCheckResult
                    {
                        Name = entry.Name,
                        HasMd5Check = false,
                        IsValid = true
                    };
                }
                else
                {
                    entryResult = await CheckEntryAsync(stream, entry, i, metadata, cancellationToken).ConfigureAwait(false);
                }
                result.EntryResults.Add(entryResult);

                if (entryResult.HasMd5Check)
                {
                    if (entryResult.IsValid)
                        result.ValidEntries++;
                    else
                        result.InvalidEntries++;
                }
            }

            return result;
        }

        /// <summary>
        /// <para>将 NB0 固件文件解析为 JSON 格式字符串。</para>
        /// Parses the NB0 firmware file into a JSON format string.
        /// </summary>
        /// <param name="filePath">
        /// <para>要解析的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to parse.
        /// </param>
        /// <param name="compact">
        /// <para>是否使用紧凑格式输出 JSON。默认为 false。</para>
        /// Whether to output JSON in compact format. Default is false.
        /// </param>
        /// <returns>
        /// <para>表示 NB0 元数据的 JSON 字符串。</para>
        /// A JSON string representing the NB0 metadata.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="filePath"/> is null or empty.
        /// </exception>
        public string ParseAsJson(string filePath, bool compact = false)
        {
            return Nb0Parser.ParseAsJson(filePath, compact);
        }

        /// <summary>
        /// <para>异步将 NB0 固件文件解析为 JSON 格式字符串。</para>
        /// Asynchronously parses the NB0 firmware file into a JSON format string.
        /// </summary>
        /// <param name="filePath">
        /// <para>要解析的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to parse.
        /// </param>
        /// <param name="compact">
        /// <para>是否使用紧凑格式输出 JSON。默认为 false。</para>
        /// Whether to output JSON in compact format. Default is false.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token for canceling the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>表示 NB0 元数据的 JSON 字符串。</para>
        /// A JSON string representing the NB0 metadata.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="filePath"/> is null or empty.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <para>当操作被取消时抛出。</para>
        /// Thrown when the operation is canceled.
        /// </exception>
        public Task<string> ParseAsJsonAsync(string filePath, bool compact = false, CancellationToken cancellationToken = default)
        {
            return Nb0Parser.ParseAsJsonAsync(filePath, compact, cancellationToken);
        }

        /// <summary>
        /// <para>将指定的 NB0 固件文件重新打包为新的 NB0 文件。使用流式拷贝方式，避免将所有条目数据同时加载到内存中。</para>
        /// Repacks the specified NB0 firmware file into a new NB0 file. Uses streaming copy to avoid loading all entry data into memory simultaneously.
        /// </summary>
        /// <param name="inputFilePath">
        /// <para>输入的 NB0 固件文件路径。</para>
        /// The input NB0 firmware file path.
        /// </param>
        /// <param name="outputFilePath">
        /// <para>输出的 NB0 固件文件路径。</para>
        /// The output NB0 firmware file path.
        /// </param>
        /// <param name="progress">
        /// <para>用于接收打包进度通知的进度报告器。</para>
        /// The progress reporter to receive pack progress notifications.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="inputFilePath"/> 或 <paramref name="outputFilePath"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="inputFilePath"/> or <paramref name="outputFilePath"/> is null or empty.
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// <para>当输入流在读取完成前结束时抛出。</para>
        /// Thrown when the input stream ends before reading is complete.
        /// </exception>
        public void Repack(string inputFilePath, string outputFilePath, IProgress<Nb0PackProgress>? progress = null)
        {
            if (inputFilePath == null) throw new ArgumentNullException(nameof(inputFilePath));
            if (inputFilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(inputFilePath));
            if (outputFilePath == null) throw new ArgumentNullException(nameof(outputFilePath));
            if (outputFilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputFilePath));

            var metadata = Nb0Parser.Parse(inputFilePath);

            using var inputStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, RepackBufferSize, FileOptions.SequentialScan);
            using var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, RepackBufferSize);
            using var writer = new BinaryWriter(outputStream, Encoding.ASCII, leaveOpen: true);

            // Write entry count
            writer.Write((uint)metadata.Entries.Count);

            // Calculate data offsets and build headers
            long currentDataOffset = 0;
            var headers = new Nb0EntryHeader[metadata.Entries.Count];
            var sourceOffsets = new long[metadata.Entries.Count];

            for (int i = 0; i < metadata.Entries.Count; i++)
            {
                var entry = metadata.Entries[i];

                headers[i] = new Nb0EntryHeader
                {
                    Name = entry.Name,
                    DataOffset = currentDataOffset,
                    FileSize = entry.Size
                };

                sourceOffsets[i] = entry.FileDataOffset + entry.Offset;

                currentDataOffset += entry.Size;
                long remainder = currentDataOffset % 4;
                if (remainder != 0) currentDataOffset += (4 - remainder);
            }

            // Write all headers
            for (int i = 0; i < headers.Length; i++)
            {
                headers[i].Write(writer);
            }

            // Stream-copy each entry's data from input to output
            byte[] buffer = ArrayPool<byte>.Shared.Rent(RepackBufferSize);
            try
            {
                for (int i = 0; i < metadata.Entries.Count; i++)
                {
                    long size = metadata.Entries[i].Size;
                    if (size == 0) continue;

                    inputStream.Seek(sourceOffsets[i], SeekOrigin.Begin);
                    long remaining = size;
                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(remaining, buffer.Length);
                        int read = inputStream.Read(buffer, 0, toRead);
                        if (read == 0)
                            throw new EndOfStreamException($"Unexpected end of stream while repacking entry '{metadata.Entries[i].Name}'. {size - remaining} of {size} bytes read.");
                        writer.Write(buffer, 0, read);
                        remaining -= read;
                    }

                    // Write 4-byte alignment padding
                    long padding = (4 - (size % 4)) % 4;
                    if (padding > 0)
                        writer.Write(PaddingBuffer, 0, (int)padding);

                    progress?.Report(new Nb0PackProgress
                    {
                        TotalEntries = metadata.Entries.Count,
                        CompletedEntries = i + 1,
                        CurrentEntryName = metadata.Entries[i].Name
                    });
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// <para>异步将指定的 NB0 固件文件重新打包为新的 NB0 文件。使用流式拷贝方式，避免将所有条目数据同时加载到内存中。</para>
        /// Asynchronously repacks the specified NB0 firmware file into a new NB0 file. Uses streaming copy to avoid loading all entry data into memory simultaneously.
        /// </summary>
        /// <param name="inputFilePath">
        /// <para>输入的 NB0 固件文件路径。</para>
        /// The input NB0 firmware file path.
        /// </param>
        /// <param name="outputFilePath">
        /// <para>输出的 NB0 固件文件路径。</para>
        /// The output NB0 firmware file path.
        /// </param>
        /// <param name="progress">
        /// <para>用于接收打包进度通知的进度报告器。</para>
        /// The progress reporter to receive pack progress notifications.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token for canceling the asynchronous operation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="inputFilePath"/> 或 <paramref name="outputFilePath"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="inputFilePath"/> or <paramref name="outputFilePath"/> is null or empty.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <para>当操作被取消时抛出。</para>
        /// Thrown when the operation is canceled.
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// <para>当输入流在读取完成前结束时抛出。</para>
        /// Thrown when the input stream ends before reading is complete.
        /// </exception>
        public async Task RepackAsync(string inputFilePath, string outputFilePath, IProgress<Nb0PackProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (inputFilePath == null) throw new ArgumentNullException(nameof(inputFilePath));
            if (inputFilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(inputFilePath));
            if (outputFilePath == null) throw new ArgumentNullException(nameof(outputFilePath));
            if (outputFilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputFilePath));

            var metadata = await Nb0Parser.ParseAsync(inputFilePath, cancellationToken).ConfigureAwait(false);

            using var inputStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, RepackBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, RepackBufferSize, FileOptions.Asynchronous);

            // Write entry count
            byte[] countBytes = BitConverter.GetBytes((uint)metadata.Entries.Count);
            await outputStream.WriteAsync(countBytes, 0, countBytes.Length, cancellationToken).ConfigureAwait(false);

            // Calculate data offsets and build headers
            long currentDataOffset = 0;
            var headers = new Nb0EntryHeader[metadata.Entries.Count];
            var sourceOffsets = new long[metadata.Entries.Count];

            for (int i = 0; i < metadata.Entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = metadata.Entries[i];

                headers[i] = new Nb0EntryHeader
                {
                    Name = entry.Name,
                    DataOffset = currentDataOffset,
                    FileSize = entry.Size
                };

                sourceOffsets[i] = entry.FileDataOffset + entry.Offset;

                currentDataOffset += entry.Size;
                long remainder = currentDataOffset % 4;
                if (remainder != 0) currentDataOffset += (4 - remainder);
            }

            // Write all headers
            byte[] headerBuffer = new byte[Nb0EntryHeader.StructSize];
            for (int i = 0; i < headers.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                headers[i].WriteToBuffer(headerBuffer);
                await outputStream.WriteAsync(headerBuffer, 0, Nb0EntryHeader.StructSize, cancellationToken).ConfigureAwait(false);
            }

            // Stream-copy each entry's data from input to output
            byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(RepackBufferSize);
            try
            {
                for (int i = 0; i < metadata.Entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long size = metadata.Entries[i].Size;
                    if (size == 0) continue;

                    inputStream.Seek(sourceOffsets[i], SeekOrigin.Begin);
                    long remaining = size;
                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(remaining, copyBuffer.Length);
                        int read = await inputStream.ReadAsync(copyBuffer, 0, toRead, cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                            throw new EndOfStreamException($"Unexpected end of stream while repacking entry '{metadata.Entries[i].Name}'. {size - remaining} of {size} bytes read.");
                        await outputStream.WriteAsync(copyBuffer, 0, read, cancellationToken).ConfigureAwait(false);
                        remaining -= read;
                    }

                    // Write 4-byte alignment padding
                    long padding = (4 - (size % 4)) % 4;
                    if (padding > 0)
                        await outputStream.WriteAsync(PaddingBuffer, 0, (int)padding, cancellationToken).ConfigureAwait(false);

                    progress?.Report(new Nb0PackProgress
                    {
                        TotalEntries = metadata.Entries.Count,
                        CompletedEntries = i + 1,
                        CurrentEntryName = metadata.Entries[i].Name
                    });
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copyBuffer);
            }
        }

        private EntryCheckResult CheckEntry(Stream stream, Nb0FileEntry entry, int entryIndex, Nb0Metadata metadata)
        {
            var md5Record = Nb0Md5Helper.FindMd5Record(entry, entryIndex, metadata);

            if (md5Record == null)
            {
                return new EntryCheckResult
                {
                    Name = entry.Name,
                    HasMd5Check = false,
                    IsValid = true
                };
            }

            try
            {
                stream.Seek(entry.FileDataOffset + entry.Offset, SeekOrigin.Begin);
                byte[] actualMd5 = StreamHelper.ComputeMd5FromStream(stream, entry.Size, _checkBuffer);

                bool isValid = md5Record.IsChecksumEqual(actualMd5);

                var result = new EntryCheckResult
                {
                    Name = entry.Name,
                    IsValid = isValid,
                    HasMd5Check = true,
                    ErrorMessage = isValid ? null : "MD5 checksum mismatch"
                };
                result.SetMd5Values(md5Record.Md5Checksum, actualMd5);
                return result;
            }
            catch (Exception ex)
            {
                var result = new EntryCheckResult
                {
                    Name = entry.Name,
                    HasMd5Check = true,
                    IsValid = false,
                    ErrorMessage = ex.Message
                };
                result.SetMd5Values(md5Record.Md5Checksum, null);
                return result;
            }
        }

        private async Task<EntryCheckResult> CheckEntryAsync(Stream stream, Nb0FileEntry entry, int entryIndex, Nb0Metadata metadata, CancellationToken cancellationToken)
        {
            var md5Record = Nb0Md5Helper.FindMd5Record(entry, entryIndex, metadata);

            if (md5Record == null)
            {
                return new EntryCheckResult
                {
                    Name = entry.Name,
                    HasMd5Check = false,
                    IsValid = true
                };
            }

            try
            {
                stream.Seek(entry.FileDataOffset + entry.Offset, SeekOrigin.Begin);
                byte[] actualMd5 = await StreamHelper.ComputeMd5FromStreamAsync(stream, entry.Size, _checkBuffer, cancellationToken).ConfigureAwait(false);

                bool isValid = md5Record.IsChecksumEqual(actualMd5);

                var result = new EntryCheckResult
                {
                    Name = entry.Name,
                    IsValid = isValid,
                    HasMd5Check = true,
                    ErrorMessage = isValid ? null : "MD5 checksum mismatch"
                };
                result.SetMd5Values(md5Record.Md5Checksum, actualMd5);
                return result;
            }
            catch (Exception ex)
            {
                var result = new EntryCheckResult
                {
                    Name = entry.Name,
                    HasMd5Check = true,
                    IsValid = false,
                    ErrorMessage = ex.Message
                };
                result.SetMd5Values(md5Record.Md5Checksum, null);
                return result;
            }
        }
    }

    /// <summary>
    /// <para>表示 NB0 固件文件 MD5 校验操作的整体结果，包含每个条目的校验详情。</para>
    /// Represents the overall result of an NB0 firmware file MD5 verification operation, containing per-entry verification details.
    /// </summary>
    public sealed class CheckResult
    {
        /// <summary>
        /// <para>获取或设置条目总数。</para>
        /// Gets or sets the total number of entries.
        /// </summary>
        /// <value>
        /// <para>条目总数。</para>
        /// The total number of entries.
        /// </value>
        public int TotalEntries { get; set; }

        /// <summary>
        /// <para>获取或设置通过 MD5 校验的条目数。</para>
        /// Gets or sets the number of entries that passed MD5 verification.
        /// </summary>
        /// <value>
        /// <para>通过校验的条目数。</para>
        /// The number of entries that passed verification.
        /// </value>
        public int ValidEntries { get; set; }

        /// <summary>
        /// <para>获取或设置未通过 MD5 校验的条目数。</para>
        /// Gets or sets the number of entries that failed MD5 verification.
        /// </summary>
        /// <value>
        /// <para>未通过校验的条目数。</para>
        /// The number of entries that failed verification.
        /// </value>
        public int InvalidEntries { get; set; }

        /// <summary>
        /// <para>获取或设置每个条目的校验结果列表。</para>
        /// Gets or sets the list of per-entry verification results.
        /// </summary>
        /// <value>
        /// <para>条目校验结果列表。</para>
        /// The list of per-entry verification results.
        /// </value>
        public List<EntryCheckResult> EntryResults { get; set; } = new();

        /// <summary>
        /// <para>获取一个值，指示所有具有 MD5 记录的条目是否均通过了校验。</para>
        /// Gets a value indicating whether all entries with MD5 records passed verification.
        /// </summary>
        /// <value>
        /// <para>如果没有无效条目则为 <c>true</c>；否则为 <c>false</c>。</para>
        /// <c>true</c> if there are no invalid entries; otherwise, <c>false</c>.
        /// </value>
        public bool IsAllValid => InvalidEntries == 0;

        /// <summary>
        /// <para>获取或设置一个值，指示 NB0 文件中是否存在 MD5 校验记录。</para>
        /// Gets or sets a value indicating whether MD5 verification records are present in the NB0 file.
        /// </summary>
        /// <value>
        /// <para>如果存在 MD5 记录则为 <c>true</c>；否则为 <c>false</c>。</para>
        /// <c>true</c> if MD5 records are present; otherwise, <c>false</c>.
        /// </value>
        public bool HasMd5Records { get; set; }

        /// <summary>
        /// <para>返回表示当前校验结果的字符串。</para>
        /// Returns a string that represents the current verification result.
        /// </summary>
        /// <returns>
        /// <para>包含校验统计信息的格式化字符串。</para>
        /// A formatted string containing verification statistics.
        /// </returns>
        public override string ToString()
        {
            if (!HasMd5Records)
                return $"Check result: {TotalEntries} entries, no MD5 records found";

            return $"Check result: {TotalEntries} entries, {ValidEntries} valid, {InvalidEntries} invalid";
        }
    }

    /// <summary>
    /// <para>表示 NB0 固件文件中单个条目的 MD5 校验结果。</para>
    /// Represents the MD5 verification result of a single entry in an NB0 firmware file.
    /// </summary>
    public sealed class EntryCheckResult
    {
        private byte[]? _expectedMd5;
        private byte[]? _actualMd5;

        /// <summary>
        /// <para>获取或设置条目名称。</para>
        /// Gets or sets the entry name.
        /// </summary>
        /// <value>
        /// <para>条目名称。</para>
        /// The entry name.
        /// </value>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>获取或设置从 .md5 条目中读取的预期 MD5 校验和。</para>
        /// Gets or sets the expected MD5 checksum read from the .md5 entry.
        /// </summary>
        /// <value>
        /// <para>预期的 MD5 校验和（16 字节），如果无 MD5 记录则为 <c>null</c>。</para>
        /// The expected MD5 checksum (16 bytes), or <c>null</c> if no MD5 record exists.
        /// </value>
        public byte[]? ExpectedMd5
        {
            get => _expectedMd5 != null ? (byte[])_expectedMd5.Clone() : null;
            set => _expectedMd5 = value != null ? (byte[])value.Clone() : null;
        }

        /// <summary>
        /// <para>获取或设置计算得到的实际 MD5 校验和。</para>
        /// Gets or sets the computed actual MD5 checksum.
        /// </summary>
        /// <value>
        /// <para>实际计算的 MD5 校验和（16 字节），如果校验未执行则为 <c>null</c>。</para>
        /// The computed actual MD5 checksum (16 bytes), or <c>null</c> if verification was not performed.
        /// </value>
        public byte[]? ActualMd5
        {
            get => _actualMd5 != null ? (byte[])_actualMd5.Clone() : null;
            set => _actualMd5 = value != null ? (byte[])value.Clone() : null;
        }

        /// <summary>
        /// <para>获取或设置一个值，指示该条目是否通过了 MD5 校验。</para>
        /// Gets or sets a value indicating whether the entry passed MD5 verification.
        /// </summary>
        /// <value>
        /// <para>如果通过校验则为 <c>true</c>；否则为 <c>false</c>。</para>
        /// <c>true</c> if verification passed; otherwise, <c>false</c>.
        /// </value>
        public bool IsValid { get; set; }

        /// <summary>
        /// <para>获取或设置校验过程中的错误信息。</para>
        /// Gets or sets the error message during verification.
        /// </summary>
        /// <value>
        /// <para>错误信息，如果无错误则为 <c>null</c>。</para>
        /// The error message, or <c>null</c> if no error occurred.
        /// </value>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// <para>获取或设置一个值，指示该条目是否有对应的 MD5 校验记录。</para>
        /// Gets or sets a value indicating whether the entry has a corresponding MD5 verification record.
        /// </summary>
        /// <value>
        /// <para>如果有 MD5 记录则为 <c>true</c>；否则为 <c>false</c>。</para>
        /// <c>true</c> if an MD5 record exists; otherwise, <c>false</c>.
        /// </value>
        public bool HasMd5Check { get; set; }

        /// <summary>
        /// <para>直接设置 MD5 值到内部字段，跳过属性的防御性拷贝。用于内部赋值优化。</para>
        /// Sets MD5 values directly to internal fields, skipping defensive copies in property setters. Used for internal assignment optimization.
        /// </summary>
        /// <param name="expected">
        /// <para>预期的 MD5 校验和。</para>
        /// The expected MD5 checksum.
        /// </param>
        /// <param name="actual">
        /// <para>实际计算的 MD5 校验和。</para>
        /// The computed actual MD5 checksum.
        /// </param>
        internal void SetMd5Values(byte[]? expected, byte[]? actual)
        {
            _expectedMd5 = expected;
            _actualMd5 = actual;
        }

        /// <summary>
        /// <para>返回表示当前条目校验结果的字符串，包含状态标记。</para>
        /// Returns a string that represents the current entry verification result, with a status tag.
        /// </summary>
        /// <returns>
        /// <para>格式化字符串：[OK] 表示通过，[FAIL] 表示失败，[SKIP] 表示无 MD5 记录。</para>
        /// A formatted string: [OK] for passed, [FAIL] for failed, [SKIP] for no MD5 record.
        /// </returns>
        public override string ToString()
        {
            if (!HasMd5Check)
                return $"[SKIP] {Name} (no MD5 record)";

            if (IsValid)
                return $"[OK] {Name}";

            return $"[FAIL] {Name}: {ErrorMessage}";
        }
    }
}
