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
    /// <para>提供将固件条目打包为 NB0 格式文件的功能。</para>
    /// Provides functionality to pack firmware entries into NB0 format files.
    /// </summary>
    /// <remarks>
    /// <para>NB0 格式将所有条目数据按 4 字节对齐方式写入，每个条目头为 64 字节。</para>
    /// The NB0 format writes all entry data with 4-byte alignment, with each entry header being 64 bytes.
    /// </remarks>
    public sealed class Nb0Packer
    {
        private const int DefaultBufferSize = 81920;

        private static readonly byte[] PaddingBuffer = { 0, 0, 0 };

        private readonly int _bufferSize;

        /// <summary>
        /// <para>使用指定的缓冲区大小初始化 <see cref="Nb0Packer"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0Packer"/> with the specified buffer size.
        /// </summary>
        /// <param name="bufferSize">
        /// <para>用于文件 I/O 操作的缓冲区大小（字节）。最小值为 4096。</para>
        /// The buffer size in bytes for file I/O operations. Minimum value is 4096.
        /// </param>
        public Nb0Packer(int bufferSize = DefaultBufferSize)
        {
            _bufferSize = Math.Max(bufferSize, 4096);
        }

        /// <summary>
        /// <para>将指定的条目列表打包为 NB0 文件并写入到指定路径。</para>
        /// Packs the specified entry list into an NB0 file and writes it to the specified path.
        /// </summary>
        /// <param name="outputFilePath">
        /// <para>输出 NB0 文件的完整路径。</para>
        /// The full path of the output NB0 file.
        /// </param>
        /// <param name="entries">
        /// <para>要打包的 <see cref="Nb0Entry"/> 条目集合。</para>
        /// The collection of <see cref="Nb0Entry"/> entries to pack.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="outputFilePath"/> 为 null 或空字符串，或 <paramref name="entries"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="outputFilePath"/> is null or empty, or <paramref name="entries"/> is null.
        /// </exception>
        public void Pack(string outputFilePath, IEnumerable<Nb0Entry> entries)
        {
            if (outputFilePath == null) throw new ArgumentNullException(nameof(outputFilePath));
            if (outputFilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputFilePath));
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            using var stream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, _bufferSize);
            PackToStream(stream, entries);
        }

        /// <summary>
        /// <para>异步将指定的条目列表打包为 NB0 文件并写入到指定路径。</para>
        /// Asynchronously packs the specified entry list into an NB0 file and writes it to the specified path.
        /// </summary>
        /// <param name="outputFilePath">
        /// <para>输出 NB0 文件的完整路径。</para>
        /// The full path of the output NB0 file.
        /// </param>
        /// <param name="entries">
        /// <para>要打包的 <see cref="Nb0Entry"/> 条目集合。</para>
        /// The collection of <see cref="Nb0Entry"/> entries to pack.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于监视取消请求的令牌。</para>
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <para>表示异步打包操作的任务。</para>
        /// A task representing the asynchronous pack operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="outputFilePath"/> 为 null 或空字符串，或 <paramref name="entries"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="outputFilePath"/> is null or empty, or <paramref name="entries"/> is null.
        /// </exception>
        public async Task PackAsync(string outputFilePath, IEnumerable<Nb0Entry> entries, CancellationToken cancellationToken = default)
        {
            if (outputFilePath == null) throw new ArgumentNullException(nameof(outputFilePath));
            if (outputFilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputFilePath));
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            using var stream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, _bufferSize, FileOptions.Asynchronous);
            await PackToStreamAsync(stream, entries, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// <para>将指定的条目列表打包为 NB0 格式并写入到指定的流中。</para>
        /// Packs the specified entry list into NB0 format and writes it to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// <para>要写入的目标流。该流必须支持写入操作。</para>
        /// The target stream to write to. The stream must support write operations.
        /// </param>
        /// <param name="entries">
        /// <para>要打包的 <see cref="Nb0Entry"/> 条目集合。</para>
        /// The collection of <see cref="Nb0Entry"/> entries to pack.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="stream"/> 为 null 或 <paramref name="entries"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="stream"/> is null or <paramref name="entries"/> is null.
        /// </exception>
        public void PackToStream(Stream stream, IEnumerable<Nb0Entry> entries)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            var entryList = new List<Nb0Entry>(entries);
            WriteNb0Core(stream, entryList);
        }

        /// <summary>
        /// <para>异步将指定的条目列表打包为 NB0 格式并写入到指定的流中。</para>
        /// Asynchronously packs the specified entry list into NB0 format and writes it to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// <para>要写入的目标流。该流必须支持写入操作。</para>
        /// The target stream to write to. The stream must support write operations.
        /// </param>
        /// <param name="entries">
        /// <para>要打包的 <see cref="Nb0Entry"/> 条目集合。</para>
        /// The collection of <see cref="Nb0Entry"/> entries to pack.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于监视取消请求的令牌。</para>
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <para>表示异步打包操作的任务。</para>
        /// A task representing the asynchronous pack operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="stream"/> 为 null 或 <paramref name="entries"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="stream"/> is null or <paramref name="entries"/> is null.
        /// </exception>
        public async Task PackToStreamAsync(Stream stream, IEnumerable<Nb0Entry> entries, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            var entryList = new List<Nb0Entry>(entries);
            await WriteNb0CoreAsync(stream, entryList, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// <para>创建一个 <see cref="Nb0PackerBuilder"/> 实例，用于流式地构建和打包 NB0 文件。</para>
        /// Creates an <see cref="Nb0PackerBuilder"/> instance for fluently building and packing NB0 files.
        /// </summary>
        /// <returns>
        /// <para>新的 <see cref="Nb0PackerBuilder"/> 实例。</para>
        /// A new <see cref="Nb0PackerBuilder"/> instance.
        /// </returns>
        public static Nb0PackerBuilder CreateBuilder()
        {
            return new Nb0PackerBuilder();
        }

        /// <summary>
        /// <para>将指定目录中的所有文件打包为 NB0 文件。采用流式两阶段方法，不会将所有文件数据同时加载到内存中。</para>
        /// Packs all files in the specified directory into an NB0 file. Uses a streaming two-phase approach that does not load all file data into memory simultaneously.
        /// </summary>
        /// <param name="sourceDirectory">
        /// <para>包含要打包文件的源目录路径。</para>
        /// The source directory path containing files to pack.
        /// </param>
        /// <param name="outputFilePath">
        /// <para>输出 NB0 文件的完整路径。</para>
        /// The full path of the output NB0 file.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="sourceDirectory"/> 或 <paramref name="outputFilePath"/> 为 null 或空字符串时抛出。</para>
        /// Thrown when <paramref name="sourceDirectory"/> or <paramref name="outputFilePath"/> is null or empty.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        /// <para>当 <paramref name="sourceDirectory"/> 指定的目录不存在时抛出。</para>
        /// Thrown when the directory specified by <paramref name="sourceDirectory"/> does not exist.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>当目录中未找到任何文件，或条目名称包含非 ASCII 字符、超过最大长度限制或存在重复名称时抛出。</para>
        /// Thrown when no files are found in the directory, or when entry names contain non-ASCII characters, exceed maximum length, or have duplicate names.
        /// </exception>
        public static void PackFromDirectory(string sourceDirectory, string outputFilePath)
        {
            if (sourceDirectory == null) throw new ArgumentNullException(nameof(sourceDirectory));
            if (sourceDirectory.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(sourceDirectory));
            if (outputFilePath == null) throw new ArgumentNullException(nameof(outputFilePath));
            if (outputFilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputFilePath));
            if (!Directory.Exists(sourceDirectory)) throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

            var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
                throw new ArgumentException($"No files found in directory '{sourceDirectory}'.", nameof(sourceDirectory));

            // Phase 1: Collect file info and validate entry names
            var fileEntries = new List<(string EntryName, string FilePath, long FileSize)>(files.Length);
            var names = new HashSet<string>();
            foreach (var filePath in files)
            {
                var fileInfo = new FileInfo(filePath);
                string relativePath = StreamHelper.GetRelativePath(sourceDirectory, filePath);
                string entryName = relativePath.Replace('\\', '/');

                // Validate entry name
                if (entryName.Length == 0)
                    throw new ArgumentException($"Entry name for file '{filePath}' is empty.", nameof(sourceDirectory));
                foreach (char c in entryName)
                {
                    if (c > 127)
                        throw new ArgumentException($"Entry name '{entryName}' contains non-ASCII character '{c}' (U+{((int)c):X4}). Entry names must be ASCII only.", nameof(sourceDirectory));
                }
                int nameByteCount = Encoding.ASCII.GetByteCount(entryName);
                if (nameByteCount > Nb0EntryHeader.NameLength - 1)
                    throw new ArgumentException($"Entry name '{entryName}' exceeds maximum length of {Nb0EntryHeader.NameLength - 1} ASCII bytes. Actual: {nameByteCount} bytes.", nameof(sourceDirectory));

                if (!names.Add(entryName))
                    throw new ArgumentException($"Duplicate entry name: '{entryName}'", nameof(sourceDirectory));

                fileEntries.Add((entryName, filePath, fileInfo.Length));
            }

            // Phase 2: Write entry count and headers
            using var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize);
            using var writer = new BinaryWriter(outputStream, Encoding.ASCII, leaveOpen: true);

            writer.Write((uint)fileEntries.Count);

            long currentDataOffset = 0;
            foreach (var (entryName, _, fileSize) in fileEntries)
            {
                var header = new Nb0EntryHeader
                {
                    LoDataOffset = (uint)(currentDataOffset & 0xFFFFFFFF),
                    HiDataOffset = (uint)(currentDataOffset >> 32),
                    LoFileSize = (uint)(fileSize & 0xFFFFFFFF),
                    HiFileSize = (uint)(fileSize >> 32),
                    Name = entryName
                };

                byte[] headerBuffer = new byte[Nb0EntryHeader.StructSize];
                header.WriteToBuffer(headerBuffer);
                writer.Write(headerBuffer, 0, Nb0EntryHeader.StructSize);

                currentDataOffset += fileSize;
                long padding = (4 - (currentDataOffset % 4)) % 4;
                currentDataOffset += padding;
            }

            // Phase 3: Stream-copy each file's data
            byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            try
            {
                foreach (var (_, filePath, fileSize) in fileEntries)
                {
                    if (fileSize > 0)
                    {
                        using var inputFile = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
                        long remaining = fileSize;
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(remaining, copyBuffer.Length);
                            int read = inputFile.Read(copyBuffer, 0, toRead);
                            if (read == 0)
                                throw new EndOfStreamException($"Unexpected end of file '{filePath}'. {fileSize - remaining} of {fileSize} bytes read.");
                            writer.Write(copyBuffer, 0, read);
                            remaining -= read;
                        }
                    }

                    // Write padding
                    long padding = (4 - (fileSize % 4)) % 4;
                    if (padding > 0)
                        writer.Write(PaddingBuffer, 0, (int)padding);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copyBuffer);
            }
        }

        /// <summary>
        /// <para>异步将指定目录中的所有文件打包为 NB0 文件。采用流式两阶段方法，不会将所有文件数据同时加载到内存中。</para>
        /// Asynchronously packs all files in the specified directory into an NB0 file. Uses a streaming two-phase approach that does not load all file data into memory simultaneously.
        /// </summary>
        /// <param name="sourceDirectory">
        /// <para>包含要打包文件的源目录路径。</para>
        /// The source directory path containing files to pack.
        /// </param>
        /// <param name="outputFilePath">
        /// <para>输出 NB0 文件的完整路径。</para>
        /// The full path of the output NB0 file.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于监视取消请求的令牌。</para>
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <para>表示异步打包操作的任务。</para>
        /// A task representing the asynchronous pack operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="sourceDirectory"/> 或 <paramref name="outputFilePath"/> 为 null 或空字符串时抛出。</para>
        /// Thrown when <paramref name="sourceDirectory"/> or <paramref name="outputFilePath"/> is null or empty.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        /// <para>当 <paramref name="sourceDirectory"/> 指定的目录不存在时抛出。</para>
        /// Thrown when the directory specified by <paramref name="sourceDirectory"/> does not exist.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>当目录中未找到任何文件，或条目名称包含非 ASCII 字符、超过最大长度限制或存在重复名称时抛出。</para>
        /// Thrown when no files are found in the directory, or when entry names contain non-ASCII characters, exceed maximum length, or have duplicate names.
        /// </exception>
        public static async Task PackFromDirectoryAsync(string sourceDirectory, string outputFilePath, CancellationToken cancellationToken = default)
        {
            if (sourceDirectory == null) throw new ArgumentNullException(nameof(sourceDirectory));
            if (sourceDirectory.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(sourceDirectory));
            if (outputFilePath == null) throw new ArgumentNullException(nameof(outputFilePath));
            if (outputFilePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(outputFilePath));
            if (!Directory.Exists(sourceDirectory)) throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

            var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
                throw new ArgumentException($"No files found in directory '{sourceDirectory}'.", nameof(sourceDirectory));

            // Phase 1: Collect file info and validate entry names
            var fileEntries = new List<(string EntryName, string FilePath, long FileSize)>(files.Length);
            var names = new HashSet<string>();
            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileInfo = new FileInfo(filePath);
                string relativePath = StreamHelper.GetRelativePath(sourceDirectory, filePath);
                string entryName = relativePath.Replace('\\', '/');

                // Validate entry name
                if (entryName.Length == 0)
                    throw new ArgumentException($"Entry name for file '{filePath}' is empty.", nameof(sourceDirectory));
                foreach (char c in entryName)
                {
                    if (c > 127)
                        throw new ArgumentException($"Entry name '{entryName}' contains non-ASCII character '{c}' (U+{((int)c):X4}). Entry names must be ASCII only.", nameof(sourceDirectory));
                }
                int nameByteCount = Encoding.ASCII.GetByteCount(entryName);
                if (nameByteCount > Nb0EntryHeader.NameLength - 1)
                    throw new ArgumentException($"Entry name '{entryName}' exceeds maximum length of {Nb0EntryHeader.NameLength - 1} ASCII bytes. Actual: {nameByteCount} bytes.", nameof(sourceDirectory));

                if (!names.Add(entryName))
                    throw new ArgumentException($"Duplicate entry name: '{entryName}'", nameof(sourceDirectory));

                fileEntries.Add((entryName, filePath, fileInfo.Length));
            }

            // Phase 2: Write entry count and headers
            using var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, FileOptions.Asynchronous);

            byte[] countBytes = BitConverter.GetBytes((uint)fileEntries.Count);
            await outputStream.WriteAsync(countBytes, 0, countBytes.Length, cancellationToken).ConfigureAwait(false);

            long currentDataOffset = 0;
            byte[] headerBuffer = new byte[Nb0EntryHeader.StructSize];
            foreach (var (entryName, _, fileSize) in fileEntries)
            {
                var header = new Nb0EntryHeader
                {
                    LoDataOffset = (uint)(currentDataOffset & 0xFFFFFFFF),
                    HiDataOffset = (uint)(currentDataOffset >> 32),
                    LoFileSize = (uint)(fileSize & 0xFFFFFFFF),
                    HiFileSize = (uint)(fileSize >> 32),
                    Name = entryName
                };

                header.WriteToBuffer(headerBuffer);
                await outputStream.WriteAsync(headerBuffer, 0, Nb0EntryHeader.StructSize, cancellationToken).ConfigureAwait(false);

                currentDataOffset += fileSize;
                long padding = (4 - (currentDataOffset % 4)) % 4;
                currentDataOffset += padding;
            }

            // Phase 3: Stream-copy each file's data
            byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            try
            {
                foreach (var (_, filePath, fileSize) in fileEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (fileSize > 0)
                    {
                        using var inputFile = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        long remaining = fileSize;
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(remaining, copyBuffer.Length);
                            int read = await inputFile.ReadAsync(copyBuffer, 0, toRead, cancellationToken).ConfigureAwait(false);
                            if (read == 0)
                                throw new EndOfStreamException($"Unexpected end of file '{filePath}'. {fileSize - remaining} of {fileSize} bytes read.");
                            await outputStream.WriteAsync(copyBuffer, 0, read, cancellationToken).ConfigureAwait(false);
                            remaining -= read;
                        }
                    }

                    // Write padding
                    long padding = (4 - (fileSize % 4)) % 4;
                    if (padding > 0)
                        await outputStream.WriteAsync(PaddingBuffer, 0, (int)padding, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copyBuffer);
            }
        }

        private void WriteNb0Core(Stream stream, List<Nb0Entry> entries)
        {
            // Validate unique entry names
            var names = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (!names.Add(entry.Header.Name))
                    throw new ArgumentException($"Duplicate entry name: '{entry.Header.Name}'", nameof(entries));
            }

            using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

            writer.Write((uint)entries.Count);

            long currentDataOffset = 0;
            var headers = new Nb0EntryHeader[entries.Count];

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                long fileSize = entry.GetFileSize();

                headers[i] = new Nb0EntryHeader
                {
                    Name = entry.Header.Name,
                    DataOffset = currentDataOffset,
                    FileSize = fileSize
                };

                currentDataOffset += fileSize;
                long remainder = currentDataOffset % 4;
                if (remainder != 0) currentDataOffset += (4 - remainder);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                headers[i].Write(writer);
            }

            byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    long fileSize = entry.GetFileSize();

                    if (entry.FilePath != null)
                    {
                        // Stream from file
                        using var inputFile = new FileStream(entry.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
                        long remaining = fileSize;
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(remaining, copyBuffer.Length);
                            int read = inputFile.Read(copyBuffer, 0, toRead);
                            if (read == 0)
                                throw new EndOfStreamException($"Unexpected end of file '{entry.FilePath}'. {fileSize - remaining} of {fileSize} bytes read.");
                            writer.Write(copyBuffer, 0, read);
                            remaining -= read;
                        }
                    }
                    else
                    {
                        byte[] writeData = entry.Data ?? Array.Empty<byte>();
                        writer.Write(writeData);
                    }

                    // Write padding
                    long padding = (4 - (fileSize % 4)) % 4;
                    if (padding > 0)
                        writer.Write(PaddingBuffer, 0, (int)padding);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copyBuffer);
            }
        }

        private async Task WriteNb0CoreAsync(Stream stream, List<Nb0Entry> entries, CancellationToken cancellationToken)
        {
            // Validate unique entry names
            var names = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (!names.Add(entry.Header.Name))
                    throw new ArgumentException($"Duplicate entry name: '{entry.Header.Name}'", nameof(entries));
            }

            // Write entry count
            byte[] countBytes = BitConverter.GetBytes((uint)entries.Count);
            await stream.WriteAsync(countBytes, 0, countBytes.Length, cancellationToken).ConfigureAwait(false);

            long currentDataOffset = 0;
            var headers = new Nb0EntryHeader[entries.Count];

            for (int i = 0; i < entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = entries[i];
                long fileSize = entry.GetFileSize();

                headers[i] = new Nb0EntryHeader
                {
                    Name = entry.Header.Name,
                    DataOffset = currentDataOffset,
                    FileSize = fileSize
                };

                currentDataOffset += fileSize;
                long remainder = currentDataOffset % 4;
                if (remainder != 0) currentDataOffset += (4 - remainder);
            }

            // Write headers
            byte[] headerBuffer = new byte[Nb0EntryHeader.StructSize];
            for (int i = 0; i < entries.Count; i++)
            {
                headers[i].WriteToBuffer(headerBuffer);
                await stream.WriteAsync(headerBuffer, 0, Nb0EntryHeader.StructSize, cancellationToken).ConfigureAwait(false);
            }

            // Write data
            byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = entries[i];
                    long fileSize = entry.GetFileSize();

                    if (entry.FilePath != null)
                    {
                        // Stream from file
                        using var inputFile = new FileStream(entry.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        long remaining = fileSize;
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(remaining, copyBuffer.Length);
                            int read = await inputFile.ReadAsync(copyBuffer, 0, toRead, cancellationToken).ConfigureAwait(false);
                            if (read == 0)
                                throw new EndOfStreamException($"Unexpected end of file '{entry.FilePath}'. {fileSize - remaining} of {fileSize} bytes read.");
                            await stream.WriteAsync(copyBuffer, 0, read, cancellationToken).ConfigureAwait(false);
                            remaining -= read;
                        }
                    }
                    else
                    {
                        byte[] writeData = entry.Data ?? Array.Empty<byte>();
                        await stream.WriteAsync(writeData, 0, writeData.Length, cancellationToken).ConfigureAwait(false);
                    }

                    // Write padding
                    long padding = (4 - (fileSize % 4)) % 4;
                    if (padding > 0)
                        await stream.WriteAsync(PaddingBuffer, 0, (int)padding, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copyBuffer);
            }
        }
    }

    /// <summary>
    /// <para>提供流式 API 来逐步添加条目并构建 NB0 文件。</para>
    /// Provides a fluent API to incrementally add entries and build NB0 files.
    /// </summary>
    public sealed class Nb0PackerBuilder
    {
        private readonly List<Nb0Entry> _entries = new();

        internal Nb0PackerBuilder()
        {
        }

        /// <summary>
        /// <para>添加一个具有指定名称和数据的条目。</para>
        /// Adds an entry with the specified name and data.
        /// </summary>
        /// <param name="name">
        /// <para>条目名称。</para>
        /// The entry name.
        /// </param>
        /// <param name="data">
        /// <para>条目的原始数据。</para>
        /// The raw data of the entry.
        /// </param>
        /// <returns>
        /// <para>当前 <see cref="Nb0PackerBuilder"/> 实例，用于链式调用。</para>
        /// The current <see cref="Nb0PackerBuilder"/> instance for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="name"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="name"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>当 <paramref name="name"/> 为空字符串时抛出。</para>
        /// Thrown when <paramref name="name"/> is empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="data"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="data"/> is null.
        /// </exception>
        public Nb0PackerBuilder AddEntry(string name, byte[] data)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (name.Length == 0) throw new ArgumentException("Entry name cannot be empty.", nameof(name));
            if (data == null) throw new ArgumentNullException(nameof(data));

            int nameByteCount = Encoding.ASCII.GetByteCount(name);
            if (nameByteCount > Nb0EntryHeader.NameLength - 1)
                throw new ArgumentException($"Entry name exceeds maximum length of {Nb0EntryHeader.NameLength - 1} ASCII bytes. Actual: {nameByteCount} bytes.", nameof(name));

            _entries.Add(new Nb0Entry(name, data));
            return this;
        }

        /// <summary>
        /// <para>将指定文件作为流式条目添加到构建器中。打包时将从文件流式读取数据，不会将整个文件预加载到内存中。</para>
        /// Adds the specified file as a streaming entry to the builder. Data will be read from the file at pack time without loading the entire file into memory.
        /// </summary>
        /// <param name="name">
        /// <para>条目名称。</para>
        /// The entry name.
        /// </param>
        /// <param name="filePath">
        /// <para>要添加的文件的完整路径。</para>
        /// The full path of the file to add.
        /// </param>
        /// <returns>
        /// <para>当前 <see cref="Nb0PackerBuilder"/> 实例，用于链式调用。</para>
        /// The current <see cref="Nb0PackerBuilder"/> instance for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="name"/> 或 <paramref name="filePath"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="name"/> or <paramref name="filePath"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>当 <paramref name="name"/> 为空字符串时抛出。</para>
        /// Thrown when <paramref name="name"/> is empty.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// <para>当 <paramref name="filePath"/> 指定的文件不存在时抛出。</para>
        /// Thrown when the file specified by <paramref name="filePath"/> does not exist.
        /// </exception>
        public Nb0PackerBuilder AddFileStream(string name, string filePath)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (name.Length == 0) throw new ArgumentException("Entry name cannot be empty.", nameof(name));
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}", filePath);

            int nameByteCount = Encoding.ASCII.GetByteCount(name);
            if (nameByteCount > Nb0EntryHeader.NameLength - 1)
                throw new ArgumentException($"Entry name exceeds maximum length of {Nb0EntryHeader.NameLength - 1} ASCII bytes. Actual: {nameByteCount} bytes.", nameof(name));

            var entry = new Nb0Entry(name, Array.Empty<byte>());
            entry.FilePath = filePath;
            _entries.Add(entry);
            return this;
        }

        /// <summary>
        /// <para>将指定文件作为条目添加到构建器中。</para>
        /// Adds the specified file as an entry to the builder.
        /// </summary>
        /// <param name="filePath">
        /// <para>要添加的文件的完整路径。</para>
        /// The full path of the file to add.
        /// </param>
        /// <param name="entryName">
        /// <para>条目名称。如果为 null，则使用文件名作为条目名称。</para>
        /// The entry name. If null, the file name is used as the entry name.
        /// </param>
        /// <returns>
        /// <para>当前 <see cref="Nb0PackerBuilder"/> 实例，用于链式调用。</para>
        /// The current <see cref="Nb0PackerBuilder"/> instance for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="filePath"/> 为 null 或空字符串时抛出。</para>
        /// Thrown when <paramref name="filePath"/> is null or empty.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// <para>当 <paramref name="filePath"/> 指定的文件不存在时抛出。</para>
        /// Thrown when the file specified by <paramref name="filePath"/> does not exist.
        /// </exception>
        public Nb0PackerBuilder AddFile(string filePath, string? entryName = null)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (filePath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}", filePath);

            string name = entryName ?? Path.GetFileName(filePath);
            byte[] data = File.ReadAllBytes(filePath);
            _entries.Add(new Nb0Entry(name, data));
            return this;
        }

        /// <summary>
        /// <para>将指定目录中的所有文件作为条目添加到构建器中。</para>
        /// Adds all files in the specified directory as entries to the builder.
        /// </summary>
        /// <param name="directoryPath">
        /// <para>要扫描的目录路径。</para>
        /// The directory path to scan.
        /// </param>
        /// <param name="prefix">
        /// <para>条目名称的前缀。如果为 null，则不添加前缀。</para>
        /// The prefix for entry names. If null, no prefix is added.
        /// </param>
        /// <returns>
        /// <para>当前 <see cref="Nb0PackerBuilder"/> 实例，用于链式调用。</para>
        /// The current <see cref="Nb0PackerBuilder"/> instance for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="directoryPath"/> 为 null 或空字符串时抛出。</para>
        /// Thrown when <paramref name="directoryPath"/> is null or empty.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        /// <para>当 <paramref name="directoryPath"/> 指定的目录不存在时抛出。</para>
        /// Thrown when the directory specified by <paramref name="directoryPath"/> does not exist.
        /// </exception>
        public Nb0PackerBuilder AddDirectory(string directoryPath, string? prefix = null)
        {
            if (directoryPath == null) throw new ArgumentNullException(nameof(directoryPath));
            if (directoryPath.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(directoryPath));
            if (!Directory.Exists(directoryPath)) throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

            foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = StreamHelper.GetRelativePath(directoryPath, filePath);
                string entryName = !string.IsNullOrEmpty(prefix) ? $"{prefix}/{relativePath}" : relativePath;
                byte[] data = File.ReadAllBytes(filePath);
                _entries.Add(new Nb0Entry(entryName, data));
            }

            return this;
        }

        /// <summary>
        /// <para>将所有已添加的条目打包为 NB0 文件并写入到指定路径。</para>
        /// Packs all added entries into an NB0 file and writes it to the specified path.
        /// </summary>
        /// <param name="outputFilePath">
        /// <para>输出 NB0 文件的完整路径。</para>
        /// The full path of the output NB0 file.
        /// </param>
        public void PackTo(string outputFilePath)
        {
            var packer = new Nb0Packer();
            packer.Pack(outputFilePath, _entries);
        }

        /// <summary>
        /// <para>异步将所有已添加的条目打包为 NB0 文件并写入到指定路径。</para>
        /// Asynchronously packs all added entries into an NB0 file and writes it to the specified path.
        /// </summary>
        /// <param name="outputFilePath">
        /// <para>输出 NB0 文件的完整路径。</para>
        /// The full path of the output NB0 file.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于监视取消请求的令牌。</para>
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <para>表示异步打包操作的任务。</para>
        /// A task representing the asynchronous pack operation.
        /// </returns>
        public async Task PackToAsync(string outputFilePath, CancellationToken cancellationToken = default)
        {
            var packer = new Nb0Packer();
            await packer.PackAsync(outputFilePath, _entries, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// <para>将所有已添加的条目打包为 NB0 格式并写入到指定的流中。</para>
        /// Packs all added entries into NB0 format and writes it to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// <para>要写入的目标流。</para>
        /// The target stream to write to.
        /// </param>
        public void PackToStream(Stream stream)
        {
            var packer = new Nb0Packer();
            packer.PackToStream(stream, _entries);
        }

        /// <summary>
        /// <para>异步将所有已添加的条目打包为 NB0 格式并写入到指定的流中。</para>
        /// Asynchronously packs all added entries into NB0 format and writes it to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// <para>要写入的目标流。</para>
        /// The target stream to write to.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于监视取消请求的令牌。</para>
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <para>表示异步打包操作的任务。</para>
        /// A task representing the asynchronous pack operation.
        /// </returns>
        public async Task PackToStreamAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            var packer = new Nb0Packer();
            await packer.PackToStreamAsync(stream, _entries, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static class StreamHelper
    {
        internal static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                    throw new EndOfStreamException($"Unexpected end of stream. Expected {count} bytes, got {totalRead}.");
                totalRead += read;
            }
        }

        internal static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new EndOfStreamException($"Unexpected end of stream. Expected {count} bytes, got {totalRead}.");
                totalRead += read;
            }
        }

#if NETSTANDARD2_0
        public static string GetRelativePath(string relativeTo, string path)
        {
            if (relativeTo == null) throw new ArgumentNullException(nameof(relativeTo));
            if (relativeTo.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(relativeTo));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Length == 0) throw new ArgumentException("Value cannot be an empty string.", nameof(path));

            string fullRelativeTo = Path.GetFullPath(relativeTo);
            string fullPath = Path.GetFullPath(path);

            if (!fullPath.StartsWith(fullRelativeTo, StringComparison.OrdinalIgnoreCase))
                return fullPath;

            int startIndex = fullRelativeTo.Length;
            if (startIndex < fullPath.Length && (fullRelativeTo[startIndex - 1] == Path.DirectorySeparatorChar || fullRelativeTo[startIndex - 1] == Path.AltDirectorySeparatorChar))
            {
                // relativeTo already ends with separator
            }
            else if (startIndex < fullPath.Length && (fullPath[startIndex] == Path.DirectorySeparatorChar || fullPath[startIndex] == Path.AltDirectorySeparatorChar))
            {
                startIndex++;
            }

            return fullPath.Substring(startIndex).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
#else
        public static string GetRelativePath(string relativeTo, string path) => Path.GetRelativePath(relativeTo, path);
#endif

        /// <summary>
        /// <para>使用流式方式从流中计算指定长度的数据的 MD5 哈希。</para>
        /// Computes MD5 hash from the stream for the specified length using a streaming approach.
        /// </summary>
        /// <param name="stream">
        /// <para>要读取数据的流。</para>
        /// The stream to read data from.
        /// </param>
        /// <param name="length">
        /// <para>要计算哈希的数据长度。</para>
        /// The length of data to compute the hash for.
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
        /// <para>当流在读取完成前结束时抛出。</para>
        /// Thrown when the stream ends before reading is complete.
        /// </exception>
        internal static byte[] ComputeMd5FromStream(Stream stream, long length, byte[] buffer)
        {
            using (var md5 = MD5.Create())
            {
                md5.Initialize();
                long remaining = length;
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, buffer.Length);
                    int read = stream.Read(buffer, 0, toRead);
                    if (read == 0)
                        throw new EndOfStreamException($"Unexpected end of stream while computing MD5. {length - remaining} of {length} bytes read.");
                    md5.TransformBlock(buffer, 0, read, buffer, 0);
                    remaining -= read;
                }
                md5.TransformFinalBlock(buffer, 0, 0);
                return md5.Hash!;
            }
        }

        /// <summary>
        /// <para>异步使用流式方式从流中计算指定长度的数据的 MD5 哈希。</para>
        /// Asynchronously computes MD5 hash from the stream for the specified length using a streaming approach.
        /// </summary>
        /// <param name="stream">
        /// <para>要读取数据的流。</para>
        /// The stream to read data from.
        /// </param>
        /// <param name="length">
        /// <para>要计算哈希的数据长度。</para>
        /// The length of data to compute the hash for.
        /// </param>
        /// <param name="buffer">
        /// <para>用于读取数据的缓冲区。</para>
        /// The buffer used for reading data.
        /// </param>
        /// <param name="cancellationToken">
        /// <para>用于取消异步操作的取消令牌。</para>
        /// A cancellation token for canceling the asynchronous operation.
        /// </param>
        /// <returns>
        /// <para>计算得到的 MD5 哈希值。</para>
        /// The computed MD5 hash.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// <para>当流在读取完成前结束时抛出。</para>
        /// Thrown when the stream ends before reading is complete.
        /// </exception>
        internal static async Task<byte[]> ComputeMd5FromStreamAsync(Stream stream, long length, byte[] buffer, CancellationToken cancellationToken)
        {
            using (var md5 = MD5.Create())
            {
                md5.Initialize();
                long remaining = length;
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, buffer.Length);
                    int read = await stream.ReadAsync(buffer, 0, toRead, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new EndOfStreamException($"Unexpected end of stream while computing MD5. {length - remaining} of {length} bytes read.");
                    md5.TransformBlock(buffer, 0, read, buffer, 0);
                    remaining -= read;
                }
                md5.TransformFinalBlock(buffer, 0, 0);
                return md5.Hash!;
            }
        }
    }
}
