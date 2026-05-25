using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>提供从 NB0 固件文件中提取条目的功能，支持 MD5 校验、进度报告和异步操作。</para>
    /// Provides functionality to extract entries from NB0 firmware files, with MD5 verification, progress reporting, and async support.
    /// </summary>
    /// <remarks>
    /// <para>此类型的实例方法不是线程安全的。不要从多个线程同时调用同一实例的方法。</para>
    /// Instance methods of this type are not thread-safe. Do not call methods of the same instance from multiple threads concurrently.
    /// </remarks>
    public sealed class Nb0Extractor
    {
        private const int DefaultBufferSize = 81920;

        private readonly int _bufferSize;

        /// <summary>
        /// <para>使用指定的缓冲区大小初始化 <see cref="Nb0Extractor"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0Extractor"/> with the specified buffer size.
        /// </summary>
        /// <param name="bufferSize">
        /// <para>用于文件 I/O 操作的缓冲区大小（字节），最小值为 4096。</para>
        /// The buffer size in bytes for file I/O operations. Minimum value is 4096.
        /// </param>
        public Nb0Extractor(int bufferSize = DefaultBufferSize)
        {
            _bufferSize = Math.Max(bufferSize, 4096);
        }

        /// <summary>
        /// <para>从指定的 NB0 文件中提取所有条目到输出目录。</para>
        /// Extracts all entries from the specified NB0 file to the output directory.
        /// </summary>
        /// <param name="nb0FilePath">
        /// <para>要提取的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to extract.
        /// </param>
        /// <param name="outputDirectory">
        /// <para>提取文件的输出目录路径。</para>
        /// The output directory path for extracted files.
        /// </param>
        /// <param name="options">
        /// <para>可选的提取选项，控制 MD5 校验、错误处理等行为。</para>
        /// Optional extraction options controlling MD5 verification, error handling, etc.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告提取进度信息。</para>
        /// An optional progress reporter for reporting extraction progress.
        /// </param>
        /// <returns>
        /// <para>包含提取结果的 <see cref="ExtractionResult"/> 实例。</para>
        /// An <see cref="ExtractionResult"/> instance containing the extraction results.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="nb0FilePath"/> 或 <paramref name="outputDirectory"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="nb0FilePath"/> or <paramref name="outputDirectory"/> is null or empty.
        /// </exception>
        /// <exception cref="Nb0Md5MismatchException">
        /// <para>当 <see cref="ExtractionOptions.VerifyMd5"/> 为 true 且 MD5 校验失败，且 <see cref="ExtractionOptions.ContinueOnError"/> 为 false 时抛出。</para>
        /// Thrown when <see cref="ExtractionOptions.VerifyMd5"/> is true and MD5 verification fails, and <see cref="ExtractionOptions.ContinueOnError"/> is false.
        /// </exception>
        public ExtractionResult Extract(string nb0FilePath, string outputDirectory, ExtractionOptions? options = null, IProgress<Nb0ExtractionProgress>? progress = null)
        {
            if (nb0FilePath == null) throw new ArgumentNullException(nameof(nb0FilePath));
            if (nb0FilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(nb0FilePath));
            if (outputDirectory == null) throw new ArgumentNullException(nameof(outputDirectory));
            if (outputDirectory.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputDirectory));

            options ??= ExtractionOptions.Default;

            using var stream = new FileStream(nb0FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, FileOptions.SequentialScan);
            return ExtractFromStream(stream, outputDirectory, options, progress);
        }

        /// <summary>
        /// <para>异步从指定的 NB0 文件中提取所有条目到输出目录。</para>
        /// Asynchronously extracts all entries from the specified NB0 file to the output directory.
        /// </summary>
        /// <param name="nb0FilePath">
        /// <para>要提取的 NB0 固件文件路径。</para>
        /// The path of the NB0 firmware file to extract.
        /// </param>
        /// <param name="outputDirectory">
        /// <para>提取文件的输出目录路径。</para>
        /// The output directory path for extracted files.
        /// </param>
        /// <param name="options">
        /// <para>可选的提取选项，控制 MD5 校验、错误处理等行为。</para>
        /// Optional extraction options controlling MD5 verification, error handling, etc.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告提取进度信息。</para>
        /// An optional progress reporter for reporting extraction progress.
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
        /// <para>当 <paramref name="nb0FilePath"/> 或 <paramref name="outputDirectory"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="nb0FilePath"/> or <paramref name="outputDirectory"/> is null or empty.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <para>当操作被取消时抛出。</para>
        /// Thrown when the operation is canceled.
        /// </exception>
        /// <exception cref="Nb0Md5MismatchException">
        /// <para>当 <see cref="ExtractionOptions.VerifyMd5"/> 为 true 且 MD5 校验失败，且 <see cref="ExtractionOptions.ContinueOnError"/> 为 false 时抛出。</para>
        /// Thrown when <see cref="ExtractionOptions.VerifyMd5"/> is true and MD5 verification fails, and <see cref="ExtractionOptions.ContinueOnError"/> is false.
        /// </exception>
        public async Task<ExtractionResult> ExtractAsync(string nb0FilePath, string outputDirectory, ExtractionOptions? options = null, IProgress<Nb0ExtractionProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (nb0FilePath == null) throw new ArgumentNullException(nameof(nb0FilePath));
            if (nb0FilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(nb0FilePath));
            if (outputDirectory == null) throw new ArgumentNullException(nameof(outputDirectory));
            if (outputDirectory.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputDirectory));

            options ??= ExtractionOptions.Default;

            using var stream = new FileStream(nb0FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await ExtractFromStreamAsync(stream, outputDirectory, options, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// <para>从指定的流中提取所有 NB0 条目到输出目录。</para>
        /// Extracts all NB0 entries from the specified stream to the output directory.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流。</para>
        /// The stream containing NB0 firmware data.
        /// </param>
        /// <param name="outputDirectory">
        /// <para>提取文件的输出目录路径。</para>
        /// The output directory path for extracted files.
        /// </param>
        /// <param name="options">
        /// <para>可选的提取选项，控制 MD5 校验、错误处理等行为。</para>
        /// Optional extraction options controlling MD5 verification, error handling, etc.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告提取进度信息。</para>
        /// An optional progress reporter for reporting extraction progress.
        /// </param>
        /// <returns>
        /// <para>包含提取结果的 <see cref="ExtractionResult"/> 实例。</para>
        /// An <see cref="ExtractionResult"/> instance containing the extraction results.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="stream"/> 为 null 或 <paramref name="outputDirectory"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="stream"/> is null or <paramref name="outputDirectory"/> is null or empty.
        /// </exception>
        /// <exception cref="Nb0Md5MismatchException">
        /// <para>当 <see cref="ExtractionOptions.VerifyMd5"/> 为 true 且 MD5 校验失败，且 <see cref="ExtractionOptions.ContinueOnError"/> 为 false 时抛出。</para>
        /// Thrown when <see cref="ExtractionOptions.VerifyMd5"/> is true and MD5 verification fails, and <see cref="ExtractionOptions.ContinueOnError"/> is false.
        /// </exception>
        public ExtractionResult ExtractFromStream(Stream stream, string outputDirectory, ExtractionOptions? options = null, IProgress<Nb0ExtractionProgress>? progress = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (outputDirectory == null) throw new ArgumentNullException(nameof(outputDirectory));
            if (outputDirectory.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputDirectory));

            options ??= ExtractionOptions.Default;

            var metadata = Nb0Parser.ParseFromStream(stream);
            Directory.CreateDirectory(outputDirectory);

            var result = new ExtractionResult { TotalEntries = metadata.Entries.Count };
            bool needMd5 = options.VerifyMd5 && metadata.HasMd5Records;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
            try
            {
                for (int i = 0; i < metadata.Entries.Count; i++)
                {
                    var entry = metadata.Entries[i];

                    progress?.Report(new Nb0ExtractionProgress
                    {
                        TotalEntries = metadata.Entries.Count,
                        CompletedEntries = i,
                        CurrentEntryName = entry.Name
                    });

                    try
                    {
                        bool isMd5Entry = entry.Name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase);
                        var md5Record = needMd5 && !isMd5Entry ? Nb0Md5Helper.FindMd5Record(entry, i, metadata) : null;

                        ExtractEntry(stream, entry, outputDirectory, md5Record, buffer);

                        result.ExtractedEntries++;
                    }
                    catch (Nb0Md5MismatchException)
                    {
                        result.FailedEntries++;
                        result.Errors.Add($"MD5 verification failed for '{entry.Name}'");
                        if (!options.ContinueOnError) throw;
                    }
                    catch (Exception ex)
                    {
                        result.FailedEntries++;
                        result.Errors.Add($"Failed to extract '{entry.Name}': {ex.Message}");
                        if (!options.ContinueOnError) throw;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (options.VerifyMd5 && !metadata.HasMd5Records)
            {
                result.Warnings.Add("MD5 verification requested but no MD5 records found in the NB0 file.");
            }

            if (options.GenerateListFile)
                WriteListFile(metadata, outputDirectory);

            return result;
        }

        /// <summary>
        /// <para>异步从指定的流中提取所有 NB0 条目到输出目录。</para>
        /// Asynchronously extracts all NB0 entries from the specified stream to the output directory.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流。</para>
        /// The stream containing NB0 firmware data.
        /// </param>
        /// <param name="outputDirectory">
        /// <para>提取文件的输出目录路径。</para>
        /// The output directory path for extracted files.
        /// </param>
        /// <param name="options">
        /// <para>可选的提取选项，控制 MD5 校验、错误处理等行为。</para>
        /// Optional extraction options controlling MD5 verification, error handling, etc.
        /// </param>
        /// <param name="progress">
        /// <para>可选的进度报告器，用于报告提取进度信息。</para>
        /// An optional progress reporter for reporting extraction progress.
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
        /// <para>当 <paramref name="stream"/> 为 null 或 <paramref name="outputDirectory"/> 为 null 或空时抛出。</para>
        /// Thrown when <paramref name="stream"/> is null or <paramref name="outputDirectory"/> is null or empty.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <para>当操作被取消时抛出。</para>
        /// Thrown when the operation is canceled.
        /// </exception>
        /// <exception cref="Nb0Md5MismatchException">
        /// <para>当 <see cref="ExtractionOptions.VerifyMd5"/> 为 true 且 MD5 校验失败，且 <see cref="ExtractionOptions.ContinueOnError"/> 为 false 时抛出。</para>
        /// Thrown when <see cref="ExtractionOptions.VerifyMd5"/> is true and MD5 verification fails, and <see cref="ExtractionOptions.ContinueOnError"/> is false.
        /// </exception>
        public async Task<ExtractionResult> ExtractFromStreamAsync(Stream stream, string outputDirectory, ExtractionOptions? options = null, IProgress<Nb0ExtractionProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (outputDirectory == null) throw new ArgumentNullException(nameof(outputDirectory));
            if (outputDirectory.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputDirectory));

            options ??= ExtractionOptions.Default;

            var metadata = await Nb0Parser.ParseFromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(outputDirectory);

            var result = new ExtractionResult { TotalEntries = metadata.Entries.Count };
            bool needMd5 = options.VerifyMd5 && metadata.HasMd5Records;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
            try
            {
                for (int i = 0; i < metadata.Entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = metadata.Entries[i];

                    progress?.Report(new Nb0ExtractionProgress
                    {
                        TotalEntries = metadata.Entries.Count,
                        CompletedEntries = i,
                        CurrentEntryName = entry.Name
                    });

                    try
                    {
                        bool isMd5Entry = entry.Name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase);
                        var md5Record = needMd5 && !isMd5Entry ? Nb0Md5Helper.FindMd5Record(entry, i, metadata) : null;

                        await ExtractEntryAsync(stream, entry, outputDirectory, md5Record, buffer, cancellationToken).ConfigureAwait(false);

                        result.ExtractedEntries++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Nb0Md5MismatchException)
                    {
                        result.FailedEntries++;
                        result.Errors.Add($"MD5 verification failed for '{entry.Name}'");
                        if (!options.ContinueOnError) throw;
                    }
                    catch (Exception ex)
                    {
                        result.FailedEntries++;
                        result.Errors.Add($"Failed to extract '{entry.Name}': {ex.Message}");
                        if (!options.ContinueOnError) throw;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (options.VerifyMd5 && !metadata.HasMd5Records)
            {
                result.Warnings.Add("MD5 verification requested but no MD5 records found in the NB0 file.");
            }

            if (options.GenerateListFile)
                WriteListFile(metadata, outputDirectory);

            return result;
        }

        /// <summary>
        /// <para>从流中提取指定条目的数据到字节数组缓冲区。</para>
        /// Extracts the data of the specified entry from the stream into a byte array buffer.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流。</para>
        /// The stream containing NB0 firmware data.
        /// </param>
        /// <param name="entry">
        /// <para>要提取的条目信息。</para>
        /// The entry information to extract.
        /// </param>
        /// <returns>
        /// <para>包含条目原始数据的字节数组。</para>
        /// A byte array containing the entry's raw data.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="stream"/> 或 <paramref name="entry"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="stream"/> or <paramref name="entry"/> is null.
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// <para>当流在读取完成前结束时抛出。</para>
        /// Thrown when the stream ends before reading is complete.
        /// </exception>
        public byte[] ExtractEntryToBuffer(Stream stream, Nb0FileEntry entry)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            if (entry.Size > int.MaxValue)
                throw new Nb0Exception($"Entry '{entry.Name}' size ({entry.Size} bytes) exceeds the maximum supported size of {int.MaxValue} bytes for in-memory extraction.");

            stream.Seek(entry.FileDataOffset + entry.Offset, SeekOrigin.Begin);

            byte[] data = new byte[entry.Size];
            StreamHelper.ReadExactly(stream, data, 0, data.Length);

            return data;
        }

        /// <summary>
        /// <para>异步从流中提取指定条目的数据到字节数组缓冲区。</para>
        /// Asynchronously extracts the data of the specified entry from the stream into a byte array buffer.
        /// </summary>
        /// <param name="stream">
        /// <para>包含 NB0 固件数据的流。</para>
        /// The stream containing NB0 firmware data.
        /// </param>
        /// <param name="entry">
        /// <para>要提取的条目信息。</para>
        /// The entry information to extract.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token for canceling the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>包含条目原始数据的字节数组。</para>
        /// A byte array containing the entry's raw data.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="stream"/> 或 <paramref name="entry"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="stream"/> or <paramref name="entry"/> is null.
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// <para>当流在读取完成前结束时抛出。</para>
        /// Thrown when the stream ends before reading is complete.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <para>当操作被取消时抛出。</para>
        /// Thrown when the operation is canceled.
        /// </exception>
        public async Task<byte[]> ExtractEntryToBufferAsync(Stream stream, Nb0FileEntry entry, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            if (entry.Size > int.MaxValue)
                throw new Nb0Exception($"Entry '{entry.Name}' size ({entry.Size} bytes) exceeds the maximum supported size of {int.MaxValue} bytes for in-memory extraction.");

            stream.Seek(entry.FileDataOffset + entry.Offset, SeekOrigin.Begin);

            byte[] data = new byte[entry.Size];
            await StreamHelper.ReadExactlyAsync(stream, data, 0, data.Length, cancellationToken).ConfigureAwait(false);

            return data;
        }

        private void ExtractEntry(Stream stream, Nb0FileEntry entry, string outputDirectory, Nb0Md5Record? md5Record, byte[] buffer)
        {
            string outputPath = Path.Combine(outputDirectory, SanitizeFileName(entry.Name));
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            stream.Seek(entry.FileDataOffset + entry.Offset, SeekOrigin.Begin);

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, _bufferSize);

            if (md5Record != null)
            {
                byte[] actualMd5 = StreamCopyWithMd5(stream, fs, entry.Size, buffer);
                if (!md5Record.IsChecksumEqual(actualMd5))
                {
                    throw new Nb0Md5MismatchException(entry.Name, md5Record.Md5Checksum, actualMd5);
                }
            }
            else
            {
                StreamCopy(stream, fs, entry.Size, buffer);
            }
        }

        private async Task ExtractEntryAsync(Stream stream, Nb0FileEntry entry, string outputDirectory, Nb0Md5Record? md5Record, byte[] buffer, CancellationToken cancellationToken)
        {
            string outputPath = Path.Combine(outputDirectory, SanitizeFileName(entry.Name));
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            stream.Seek(entry.FileDataOffset + entry.Offset, SeekOrigin.Begin);

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, _bufferSize, FileOptions.Asynchronous);

            if (md5Record != null)
            {
                byte[] actualMd5 = await StreamCopyWithMd5Async(stream, fs, entry.Size, buffer, cancellationToken).ConfigureAwait(false);
                if (!md5Record.IsChecksumEqual(actualMd5))
                {
                    throw new Nb0Md5MismatchException(entry.Name, md5Record.Md5Checksum, actualMd5);
                }
            }
            else
            {
                await StreamCopyAsync(stream, fs, entry.Size, buffer, cancellationToken).ConfigureAwait(false);
            }
        }

        private static void StreamCopy(Stream source, Stream destination, long size, byte[] buffer)
        {
            long remaining = size;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(remaining, buffer.Length);
                int read = source.Read(buffer, 0, toRead);
                if (read == 0)
                    throw new EndOfStreamException($"Unexpected end of stream while copying entry data. {size - remaining} of {size} bytes read.");
                destination.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private static async Task StreamCopyAsync(Stream source, Stream destination, long size, byte[] buffer, CancellationToken cancellationToken)
        {
            long remaining = size;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(remaining, buffer.Length);
                int read = await source.ReadAsync(buffer, 0, toRead, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new EndOfStreamException($"Unexpected end of stream while copying entry data. {size - remaining} of {size} bytes read.");
                await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                remaining -= read;
            }
        }

        /// <summary>
        /// <para>将流数据拷贝到目标流的同时增量计算 MD5 哈希，实现一次 I/O 完成提取与校验。</para>
        /// Copies stream data to the destination while incrementally computing the MD5 hash, achieving extraction and verification in a single I/O pass.
        /// </summary>
        /// <param name="source">
        /// <para>源数据流。</para>
        /// The source stream.
        /// </param>
        /// <param name="destination">
        /// <para>目标输出流。</para>
        /// The destination output stream.
        /// </param>
        /// <param name="size">
        /// <para>要拷贝的字节数。</para>
        /// The number of bytes to copy.
        /// </param>
        /// <param name="buffer">
        /// <para>用于读取数据的缓冲区。</para>
        /// The buffer used for reading data.
        /// </param>
        /// <returns>
        /// <para>计算得到的 MD5 哈希值。</para>
        /// The computed MD5 hash.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// <para>当源流在读取完成前结束时抛出。</para>
        /// Thrown when the source stream ends before reading is complete.
        /// </exception>
        private static byte[] StreamCopyWithMd5(Stream source, Stream destination, long size, byte[] buffer)
        {
#if NETSTANDARD2_0
            using (var md5 = new MD5CryptoServiceProvider())
            {
                md5.Initialize();
                long remaining = size;
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, buffer.Length);
                    int read = source.Read(buffer, 0, toRead);
                    if (read == 0)
                        throw new EndOfStreamException($"Unexpected end of stream while copying entry data. {size - remaining} of {size} bytes read.");
                    md5.TransformBlock(buffer, 0, read, buffer, 0);
                    destination.Write(buffer, 0, read);
                    remaining -= read;
                }
                md5.TransformFinalBlock(buffer, 0, 0);
                return md5.Hash!;
            }
#else
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5))
            {
                long remaining = size;
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, buffer.Length);
                    int read = source.Read(buffer, 0, toRead);
                    if (read == 0)
                        throw new EndOfStreamException($"Unexpected end of stream while copying entry data. {size - remaining} of {size} bytes read.");
                    hash.AppendData(buffer, 0, read);
                    destination.Write(buffer, 0, read);
                    remaining -= read;
                }
                return hash.GetHashAndReset();
            }
#endif
        }

        /// <summary>
        /// <para>异步将流数据拷贝到目标流的同时增量计算 MD5 哈希，实现一次 I/O 完成提取与校验。</para>
        /// Asynchronously copies stream data to the destination while incrementally computing the MD5 hash, achieving extraction and verification in a single I/O pass.
        /// </summary>
        /// <param name="source">
        /// <para>源数据流。</para>
        /// The source stream.
        /// </param>
        /// <param name="destination">
        /// <para>目标输出流。</para>
        /// The destination output stream.
        /// </param>
        /// <param name="size">
        /// <para>要拷贝的字节数。</para>
        /// The number of bytes to copy.
        /// </param>
        /// <param name="buffer">
        /// <para>用于读取数据的缓冲区。</para>
        /// The buffer used for reading data.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>计算得到的 MD5 哈希值。</para>
        /// The computed MD5 hash.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// <para>当源流在读取完成前结束时抛出。</para>
        /// Thrown when the source stream ends before reading is complete.
        /// </exception>
        private static async Task<byte[]> StreamCopyWithMd5Async(Stream source, Stream destination, long size, byte[] buffer, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0
            using (var md5 = new MD5CryptoServiceProvider())
            {
                md5.Initialize();
                long remaining = size;
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, buffer.Length);
                    int read = await source.ReadAsync(buffer, 0, toRead, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new EndOfStreamException($"Unexpected end of stream while copying entry data. {size - remaining} of {size} bytes read.");
                    md5.TransformBlock(buffer, 0, read, buffer, 0);
                    await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    remaining -= read;
                }
                md5.TransformFinalBlock(buffer, 0, 0);
                return md5.Hash!;
            }
#else
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5))
            {
                long remaining = size;
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, buffer.Length);
                    int read = await source.ReadAsync(buffer, 0, toRead, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new EndOfStreamException($"Unexpected end of stream while copying entry data. {size - remaining} of {size} bytes read.");
                    hash.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    remaining -= read;
                }
                return hash.GetHashAndReset();
            }
#endif
        }

        internal static void WriteListFile(Nb0Metadata metadata, string outputDirectory)
        {
            string listPath = Path.Combine(outputDirectory, "nb0_file_list.txt");
            using var writer = new StreamWriter(listPath, false, Encoding.UTF8);
            writer.WriteLine($"# NB0 File List - Generated by FirmwareKit.Nb0");
            writer.WriteLine($"# Entry Count: {metadata.EntryCount}");
            writer.WriteLine($"# Data Section Offset: 0x{metadata.DataSectionOffset:X8}");
            writer.WriteLine();
            foreach (var entry in metadata.Entries)
            {
                string type = !string.IsNullOrEmpty(entry.InferredType) ? $" [{entry.InferredType}]" : "";
                writer.WriteLine($"{entry.Name}\t{entry.Size}\t0x{entry.Offset:X8}{type}");
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            bool changed = false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (Array.IndexOf(invalidChars, c) >= 0)
                {
                    sb.Append('_');
                    changed = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
            return changed ? sb.ToString() : name;
        }
    }

    /// <summary>
    /// <para>提供 NB0 固件文件提取操作的配置选项，包括 MD5 校验、错误处理和列表文件生成。</para>
    /// Provides configuration options for NB0 firmware file extraction operations, including MD5 verification, error handling, and list file generation.
    /// </summary>
    public sealed class ExtractionOptions
    {
        /// <summary>
        /// <para>获取默认的提取选项实例。</para>
        /// Gets the default extraction options instance.
        /// </summary>
        /// <value>
        /// <para>默认的 <see cref="ExtractionOptions"/> 实例。</para>
        /// The default <see cref="ExtractionOptions"/> instance.
        /// </value>
        public static ExtractionOptions Default => new();

        /// <summary>
        /// <para>获取或设置一个值，指示当提取过程中发生错误时是否继续处理后续条目。</para>
        /// Gets or sets a value indicating whether to continue processing subsequent entries when an error occurs during extraction.
        /// </summary>
        /// <value>
        /// <para>如果继续处理则为 <c>true</c>；否则为 <c>false</c>。默认值为 <c>true</c>。</para>
        /// <c>true</c> to continue processing; otherwise, <c>false</c>. The default value is <c>true</c>.
        /// </value>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// <para>获取或设置一个值，指示提取后是否验证每个条目的 MD5 校验和。</para>
        /// Gets or sets a value indicating whether to verify the MD5 checksum of each entry after extraction.
        /// </summary>
        /// <value>
        /// <para>如果验证 MD5 校验和则为 <c>true</c>；否则为 <c>false</c>。默认值为 <c>true</c>。</para>
        /// <c>true</c> to verify MD5 checksums; otherwise, <c>false</c>. The default value is <c>true</c>.
        /// </value>
        /// <remarks>
        /// <para>MD5 校验需要 NB0 文件中包含 .md5 条目。如果文件中没有 MD5 记录，校验将被跳过并添加警告。</para>
        /// MD5 verification requires the NB0 file to contain a .md5 entry. If no MD5 records are present in the file, verification will be skipped and a warning will be added.
        /// </remarks>
        public bool VerifyMd5 { get; set; } = true;

        /// <summary>
        /// <para>获取或设置一个值，指示提取完成后是否生成文件列表文件。</para>
        /// Gets or sets a value indicating whether to generate a file list file after extraction is complete.
        /// </summary>
        /// <value>
        /// <para>如果生成列表文件则为 <c>true</c>；否则为 <c>false</c>。默认值为 <c>true</c>。</para>
        /// <c>true</c> to generate a list file; otherwise, <c>false</c>. The default value is <c>true</c>.
        /// </value>
        public bool GenerateListFile { get; set; } = true;
    }

    /// <summary>
    /// <para>表示 NB0 固件文件提取操作的结果，包含提取统计信息和错误列表。</para>
    /// Represents the result of an NB0 firmware file extraction operation, containing extraction statistics and error list.
    /// </summary>
    public sealed class ExtractionResult
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
        /// <para>获取或设置成功提取的条目数。</para>
        /// Gets or sets the number of successfully extracted entries.
        /// </summary>
        /// <value>
        /// <para>成功提取的条目数。</para>
        /// The number of successfully extracted entries.
        /// </value>
        public int ExtractedEntries { get; set; }

        /// <summary>
        /// <para>获取或设置提取失败的条目数。</para>
        /// Gets or sets the number of entries that failed to extract.
        /// </summary>
        /// <value>
        /// <para>提取失败的条目数。</para>
        /// The number of entries that failed to extract.
        /// </value>
        public int FailedEntries { get; set; }

        /// <summary>
        /// <para>获取或设置提取过程中产生的错误信息列表。</para>
        /// Gets or sets the list of error messages generated during extraction.
        /// </summary>
        /// <value>
        /// <para>错误信息列表。</para>
        /// The list of error messages.
        /// </value>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// <para>获取或设置提取过程中产生的警告信息列表。</para>
        /// Gets or sets the list of warning messages generated during extraction.
        /// </summary>
        /// <value>
        /// <para>警告信息列表。</para>
        /// The list of warning messages.
        /// </value>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// <para>获取一个值，指示所有条目是否均已成功提取。</para>
        /// Gets a value indicating whether all entries were successfully extracted.
        /// </summary>
        /// <value>
        /// <para>如果没有失败的条目则为 <c>true</c>；否则为 <c>false</c>。</para>
        /// <c>true</c> if no entries failed; otherwise, <c>false</c>.
        /// </value>
        public bool IsSuccess => FailedEntries == 0;

        /// <summary>
        /// <para>返回表示当前提取结果的字符串。</para>
        /// Returns a string that represents the current extraction result.
        /// </summary>
        /// <returns>
        /// <para>包含提取统计信息的格式化字符串。</para>
        /// A formatted string containing extraction statistics.
        /// </returns>
        public override string ToString()
        {
            return $"Extraction complete: {ExtractedEntries}/{TotalEntries} extracted, {FailedEntries} failed";
        }
    }
}
