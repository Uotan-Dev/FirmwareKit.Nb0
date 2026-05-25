using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FirmwareKit.Nb0.Tests
{
    public class Nb0ExtractorTests
    {
        private static MemoryStream CreateNb0Stream(List<(string Name, byte[] Data)> entries)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write((uint)entries.Count);
                long dataSectionOffset = 4 + entries.Count * 64;
                long currentDataOffset = 0;

                // Write headers
                foreach (var (name, data) in entries)
                {
                    writer.Write((uint)(currentDataOffset & 0xFFFFFFFF));        // LoDataOffset
                    writer.Write((uint)(data.Length & 0xFFFFFFFF));              // LoFileSize
                    writer.Write((uint)((currentDataOffset >> 32) & 0xFFFFFFFF));// HiDataOffset
                    writer.Write((uint)(((long)data.Length >> 32) & 0xFFFFFFFF));    // HiFileSize

                    var nameBytes = new byte[48];
                    var encoded = Encoding.ASCII.GetBytes(name);
                    Array.Copy(encoded, nameBytes, Math.Min(encoded.Length, 47));
                    writer.Write(nameBytes);

                    // Align to 4 bytes
                    currentDataOffset += data.Length;
                    currentDataOffset = (currentDataOffset + 3) & ~3L;
                }

                // Write data
                foreach (var (name, data) in entries)
                {
                    writer.Write(data);
                    // Pad to 4-byte alignment
                    int padding = (int)((4 - (data.Length % 4)) % 4);
                    for (int i = 0; i < padding; i++)
                        writer.Write((byte)0);
                }
            }
            ms.Position = 0;
            return ms;
        }

        private static byte[] CreateMd5RecordData(uint offset, uint length, byte[] md5)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(offset);
            writer.Write(length);
            writer.Write(md5); // 16 bytes
            return ms.ToArray();
        }

        private static byte[] ComputeMd5(byte[] data)
        {
            using var md5 = MD5.Create();
            return md5.ComputeHash(data);
        }

        [Fact]
        public void ExtractFromStream_SingleEntry_ExtractsCorrectly()
        {
            byte[] originalData = Encoding.ASCII.GetBytes("Test extraction data content");
            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", originalData)
            });

            using var tempDir = new TempDirectory();
            var extractor = new Nb0Extractor();
            var result = extractor.ExtractFromStream(stream, tempDir.Path,
                new ExtractionOptions { VerifyMd5 = false, GenerateListFile = false });

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.ExtractedEntries);

            string extractedFile = Path.Combine(tempDir.Path, "boot.img");
            Assert.True(File.Exists(extractedFile));
            byte[] extractedData = File.ReadAllBytes(extractedFile);
            Assert.Equal(originalData, extractedData);
        }

        [Fact]
        public void ExtractFromStream_MultipleEntries_ExtractsAll()
        {
            byte[] data1 = Encoding.ASCII.GetBytes("Boot partition data");
            byte[] data2 = Encoding.ASCII.GetBytes("Recovery partition data");
            byte[] data3 = Encoding.ASCII.GetBytes("System partition data");

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", data1),
                ("recovery.img", data2),
                ("system.img", data3)
            });

            using var tempDir = new TempDirectory();
            var extractor = new Nb0Extractor();
            var result = extractor.ExtractFromStream(stream, tempDir.Path,
                new ExtractionOptions { VerifyMd5 = false, GenerateListFile = false });

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.ExtractedEntries);
            Assert.Equal(data1, File.ReadAllBytes(Path.Combine(tempDir.Path, "boot.img")));
            Assert.Equal(data2, File.ReadAllBytes(Path.Combine(tempDir.Path, "recovery.img")));
            Assert.Equal(data3, File.ReadAllBytes(Path.Combine(tempDir.Path, "system.img")));
        }

        [Fact]
        public void ExtractFromStream_NullStream_ThrowsArgumentNullException()
        {
            var extractor = new Nb0Extractor();
            Assert.Throws<ArgumentNullException>(() => extractor.ExtractFromStream(null!, "C:\\temp"));
        }

        [Fact]
        public void ExtractFromStream_NullOutputDir_ThrowsArgumentNullException()
        {
            var extractor = new Nb0Extractor();
            using var stream = new MemoryStream();
            Assert.Throws<ArgumentNullException>(() => extractor.ExtractFromStream(stream, null!));
        }

        [Fact]
        public void ExtractFromStream_GeneratesListFile()
        {
            byte[] data = Encoding.ASCII.GetBytes("test data");
            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", data)
            });

            using var tempDir = new TempDirectory();
            var extractor = new Nb0Extractor();
            var result = extractor.ExtractFromStream(stream, tempDir.Path,
                new ExtractionOptions { VerifyMd5 = false, GenerateListFile = true });

            string listFile = Path.Combine(tempDir.Path, "nb0_file_list.txt");
            Assert.True(File.Exists(listFile));
            string content = File.ReadAllText(listFile);
            Assert.Contains("boot.img", content);
        }

        [Fact]
        public void ExtractFromStream_SkipListFile_WhenOptionDisabled()
        {
            byte[] data = Encoding.ASCII.GetBytes("test data");
            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", data)
            });

            using var tempDir = new TempDirectory();
            var extractor = new Nb0Extractor();
            var result = extractor.ExtractFromStream(stream, tempDir.Path,
                new ExtractionOptions { VerifyMd5 = false, GenerateListFile = false });

            string listFile = Path.Combine(tempDir.Path, "nb0_file_list.txt");
            Assert.False(File.Exists(listFile));
        }

        [Fact]
        public async Task ExtractFromStreamAsync_SameResultAsSync()
        {
            byte[] data = Encoding.ASCII.GetBytes("Async extraction test data");
            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("test.img", data)
            });

            using var tempDir = new TempDirectory();
            var extractor = new Nb0Extractor();
            var result = await extractor.ExtractFromStreamAsync(stream, tempDir.Path,
                new ExtractionOptions { VerifyMd5 = false, GenerateListFile = false }, null, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.ExtractedEntries);
            Assert.Equal(data, File.ReadAllBytes(Path.Combine(tempDir.Path, "test.img")));
        }

        [Fact]
        public async Task ExtractFromStreamAsync_Cancellation_ThrowsOperationCanceledException()
        {
            byte[] data = new byte[1024 * 1024];
            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("large.img", data)
            });

            using var tempDir = new TempDirectory();
            var extractor = new Nb0Extractor();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                extractor.ExtractFromStreamAsync(stream, tempDir.Path,
                    new ExtractionOptions { VerifyMd5 = false },
                    null, cts.Token));
        }

        [Fact]
        public void ExtractEntryToBuffer_ReturnsCorrectData()
        {
            byte[] originalData = Encoding.ASCII.GetBytes("Buffer extraction test");
            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", originalData)
            });

            var metadata = Nb0Parser.ParseFromStream(stream);
            stream.Position = 0;

            var extractor = new Nb0Extractor();
            byte[] extracted = extractor.ExtractEntryToBuffer(stream, metadata.Entries[0]);

            Assert.Equal(originalData, extracted);
        }

        [Fact]
        public void ExtractEntryToBuffer_NullStream_ThrowsArgumentNullException()
        {
            var extractor = new Nb0Extractor();
            var entry = new Nb0FileEntry();
            Assert.Throws<ArgumentNullException>(() => extractor.ExtractEntryToBuffer(null!, entry));
        }

        [Fact]
        public void ExtractFromStream_WithMd5Verification_ValidMd5_Passes()
        {
            byte[] firmwareData = Encoding.ASCII.GetBytes("firmware content for md5 test");
            byte[] correctMd5 = ComputeMd5(firmwareData);

            // MD5 record offset is an absolute file offset (from the beginning of the file).
            // With 2 entries, data section starts at 4 + 2 * 64 = 132.
            // The firmware entry's data starts at the beginning of the data section, so absolute offset = 132.
            uint absoluteOffset = (uint)(4 + 2 * Nb0EntryHeader.StructSize);
            byte[] md5RecordData = CreateMd5RecordData(absoluteOffset, (uint)firmwareData.Length, correctMd5);

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("firmware.bin", firmwareData),
                ("firmware.md5", md5RecordData)
            });

            using var tempDir = new TempDirectory();
            var extractor = new Nb0Extractor();
            var result = extractor.ExtractFromStream(stream, tempDir.Path,
                new ExtractionOptions { VerifyMd5 = true, ContinueOnError = false, GenerateListFile = false });

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.ExtractedEntries);
            Assert.Equal(0, result.FailedEntries);
        }

        [Fact]
        public void ExtractFromStream_WithMd5Verification_InvalidMd5_Fails()
        {
            byte[] firmwareData = Encoding.ASCII.GetBytes("firmware content for md5 test");
            byte[] wrongMd5 = new byte[16];
            for (int i = 0; i < 16; i++) wrongMd5[i] = 0xFF;

            // MD5 record offset is an absolute file offset (from the beginning of the file).
            uint absoluteOffset = (uint)(4 + 2 * Nb0EntryHeader.StructSize);
            byte[] md5RecordData = CreateMd5RecordData(absoluteOffset, (uint)firmwareData.Length, wrongMd5);

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("firmware.bin", firmwareData),
                ("firmware.md5", md5RecordData)
            });

            using var tempDir = new TempDirectory();
            var extractor = new Nb0Extractor();
            var result = extractor.ExtractFromStream(stream, tempDir.Path,
                new ExtractionOptions { VerifyMd5 = true, ContinueOnError = true, GenerateListFile = false });

            Assert.False(result.IsSuccess);
            Assert.True(result.FailedEntries > 0);
            Assert.Contains(result.Errors, e => e.Contains("MD5 verification failed"));
        }

        [Fact]
        public void ExtractFromStream_WithoutMd5Entry_SkipsMd5Verification()
        {
            byte[] data = Encoding.ASCII.GetBytes("no md5 entry data");
            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", data)
            });

            using var tempDir = new TempDirectory();
            var extractor = new Nb0Extractor();
            var result = extractor.ExtractFromStream(stream, tempDir.Path,
                new ExtractionOptions { VerifyMd5 = true, GenerateListFile = false });

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.ExtractedEntries);
            Assert.Contains(result.Warnings, w => w.Contains("no MD5 records"));
        }

        [Fact]
        public void ExtractionResult_IsSuccess_NoFailures()
        {
            var result = new ExtractionResult { TotalEntries = 3, ExtractedEntries = 3, FailedEntries = 0 };
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ExtractionResult_IsSuccess_WithFailures()
        {
            var result = new ExtractionResult { TotalEntries = 3, ExtractedEntries = 2, FailedEntries = 1 };
            Assert.False(result.IsSuccess);
        }

        private class TempDirectory : IDisposable
        {
            public string Path { get; }

            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Nb0Test_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try { Directory.Delete(Path, true); } catch { }
            }
        }
    }
}
