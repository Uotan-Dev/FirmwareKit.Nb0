using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FirmwareKit.Nb0.Tests
{
    public class Nb0ProcessorTests
    {
        /// <summary>
        /// Creates an NB0 stream with the specified entries and an optional .md5 entry
        /// containing MD5 verification records for all non-.md5 entries.
        /// The MD5 record Offset field uses absolute file offsets (from the beginning of the file).
        /// </summary>
        private static MemoryStream CreateNb0WithMd5(bool withCorrectMd5, params (string Name, byte[] Data)[] files)
        {
            var ms = new MemoryStream();

            // Compute MD5 for each entry
            var md5Hashes = new byte[files.Length][];
            for (int i = 0; i < files.Length; i++)
            {
                using var md5 = MD5.Create();
                md5Hashes[i] = md5.ComputeHash(files[i].Data);
            }

            // Total entries = files + .md5 entry
            int totalEntries = files.Length + 1;
            long dataSectionOffset = 4 + (long)totalEntries * Nb0EntryHeader.StructSize;

            // Calculate absolute file offsets for each entry's data
            var absoluteOffsets = new long[files.Length];
            long currentDataOffset = 0;
            for (int i = 0; i < files.Length; i++)
            {
                absoluteOffsets[i] = dataSectionOffset + currentDataOffset;
                currentDataOffset += files[i].Data.Length;
                long remainder = currentDataOffset % 4;
                if (remainder != 0) currentDataOffset += (4 - remainder);
            }

            // Build .md5 entry data: 24 bytes per record (offset u4 + length u4 + md5 16 bytes)
            byte[] md5EntryData;
            using (var md5Ms = new MemoryStream())
            using (var md5Writer = new BinaryWriter(md5Ms, Encoding.ASCII))
            {
                for (int i = 0; i < files.Length; i++)
                {
                    long entryAbsoluteOffset = absoluteOffsets[i];
                    long entryLength = files[i].Data.Length;

                    md5Writer.Write((uint)entryAbsoluteOffset);
                    md5Writer.Write((uint)entryLength);

                    if (withCorrectMd5)
                    {
                        md5Writer.Write(md5Hashes[i]);
                    }
                    else
                    {
                        // Write wrong MD5 for the first entry to cause a mismatch
                        byte[] wrongMd5 = new byte[16];
                        wrongMd5[0] = 0xFF;
                        md5Writer.Write(wrongMd5);
                    }
                }

                md5EntryData = md5Ms.ToArray();
            }

            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write((uint)totalEntries);

                currentDataOffset = 0;
                var headers = new Nb0EntryHeader[totalEntries];

                // Write headers for data entries
                for (int i = 0; i < files.Length; i++)
                {
                    headers[i] = new Nb0EntryHeader
                    {
                        Name = files[i].Name,
                        DataOffset = currentDataOffset,
                        FileSize = files[i].Data.Length
                    };

                    currentDataOffset += files[i].Data.Length;
                    long remainder = currentDataOffset % 4;
                    if (remainder != 0) currentDataOffset += (4 - remainder);
                }

                // Write header for .md5 entry
                headers[files.Length] = new Nb0EntryHeader
                {
                    Name = files[0].Name + ".md5",
                    DataOffset = currentDataOffset,
                    FileSize = md5EntryData.Length
                };

                // Write all headers
                for (int i = 0; i < totalEntries; i++)
                    headers[i].Write(writer);

                // Write data for file entries
                for (int i = 0; i < files.Length; i++)
                {
                    writer.Write(files[i].Data);
                    long padding = (4 - (files[i].Data.Length % 4)) % 4;
                    for (long p = 0; p < padding; p++)
                        writer.Write((byte)0);
                }

                // Write .md5 entry data
                writer.Write(md5EntryData);
                long md5Padding = (4 - (md5EntryData.Length % 4)) % 4;
                for (long p = 0; p < md5Padding; p++)
                    writer.Write((byte)0);
            }

            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Creates an NB0 stream without a .md5 entry.
        /// </summary>
        private static MemoryStream CreateNb0WithoutMd5(params (string Name, byte[] Data)[] files)
        {
            var ms = new MemoryStream();

            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write((uint)files.Length);

                long currentDataOffset = 0;
                var headers = new Nb0EntryHeader[files.Length];

                for (int i = 0; i < files.Length; i++)
                {
                    headers[i] = new Nb0EntryHeader
                    {
                        Name = files[i].Name,
                        DataOffset = currentDataOffset,
                        FileSize = files[i].Data.Length
                    };

                    currentDataOffset += files[i].Data.Length;
                    long remainder = currentDataOffset % 4;
                    if (remainder != 0) currentDataOffset += (4 - remainder);
                }

                for (int i = 0; i < files.Length; i++)
                    headers[i].Write(writer);

                for (int i = 0; i < files.Length; i++)
                {
                    writer.Write(files[i].Data);
                    long padding = (4 - (files[i].Data.Length % 4)) % 4;
                    for (long p = 0; p < padding; p++)
                        writer.Write((byte)0);
                }
            }

            ms.Position = 0;
            return ms;
        }

        [Fact]
        public void CheckFromStream_WithMd5Records_AllEntriesValid()
        {
            byte[] data1 = Encoding.ASCII.GetBytes("Valid data 1");
            byte[] data2 = Encoding.ASCII.GetBytes("Valid data 2");

            using var stream = CreateNb0WithMd5(true, ("boot.img", data1), ("system.img", data2));
            var processor = new Nb0Processor();
            var result = processor.CheckFromStream(stream);

            Assert.True(result.IsAllValid);
            Assert.True(result.HasMd5Records);
            Assert.Equal(3, result.TotalEntries); // 2 data entries + 1 .md5 entry
            Assert.Equal(2, result.ValidEntries); // only data entries have MD5 records
            Assert.Equal(0, result.InvalidEntries);
        }

        [Fact]
        public void CheckFromStream_WithMd5Records_InvalidEntry()
        {
            byte[] data1 = Encoding.ASCII.GetBytes("Valid data 1");
            byte[] data2 = Encoding.ASCII.GetBytes("Valid data 2");

            using var stream = CreateNb0WithMd5(false, ("boot.img", data1), ("system.img", data2));
            var processor = new Nb0Processor();
            var result = processor.CheckFromStream(stream);

            Assert.False(result.IsAllValid);
            Assert.True(result.HasMd5Records);
            Assert.True(result.InvalidEntries > 0);
        }

        [Fact]
        public void CheckFromStream_WithoutMd5Records_AllSkipped()
        {
            byte[] data1 = Encoding.ASCII.GetBytes("No MD5 data 1");
            byte[] data2 = Encoding.ASCII.GetBytes("No MD5 data 2");

            using var stream = CreateNb0WithoutMd5(("boot.img", data1), ("system.img", data2));
            var processor = new Nb0Processor();
            var result = processor.CheckFromStream(stream);

            Assert.False(result.HasMd5Records);
            Assert.True(result.IsAllValid);
            Assert.Equal(0, result.ValidEntries);
            Assert.Equal(0, result.InvalidEntries);

            // All entries should have HasMd5Check = false (skipped)
            foreach (var entryResult in result.EntryResults)
            {
                Assert.False(entryResult.HasMd5Check);
            }
        }

        [Fact]
        public void CheckFromStream_NullStream_ThrowsArgumentNullException()
        {
            var processor = new Nb0Processor();
            Assert.Throws<ArgumentNullException>(() => processor.CheckFromStream(null!));
        }

        [Fact]
        public async Task CheckFromStreamAsync_SameResultAsSync()
        {
            byte[] data = Encoding.ASCII.GetBytes("Async check test");
            using var stream1 = CreateNb0WithMd5(true, ("test.img", data));
            using var stream2 = CreateNb0WithMd5(true, ("test.img", data));

            var processor = new Nb0Processor();
            var syncResult = processor.CheckFromStream(stream1);

            var asyncResult = await processor.CheckFromStreamAsync(stream2, null, TestContext.Current.CancellationToken);

            Assert.Equal(syncResult.TotalEntries, asyncResult.TotalEntries);
            Assert.Equal(syncResult.ValidEntries, asyncResult.ValidEntries);
            Assert.Equal(syncResult.InvalidEntries, asyncResult.InvalidEntries);
            Assert.Equal(syncResult.IsAllValid, asyncResult.IsAllValid);
        }

        [Fact]
        public void Repack_PreservesData()
        {
            byte[] originalData = Encoding.ASCII.GetBytes("Repack test data content");
            using var inputNb0 = CreateNb0WithoutMd5(("boot.img", originalData));

            string tempDir = Path.Combine(Path.GetTempPath(), "Nb0ProcessorTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                string inputPath = Path.Combine(tempDir, "input.nb0");
                string outputPath = Path.Combine(tempDir, "output.nb0");

                using (var fs = new FileStream(inputPath, FileMode.Create))
                    inputNb0.CopyTo(fs);

                var processor = new Nb0Processor();
                processor.Repack(inputPath, outputPath);

                Assert.True(File.Exists(outputPath));

                using var outputStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read);
                var metadata = Nb0Parser.ParseFromStream(outputStream);
                outputStream.Position = 0;

                var extractor = new Nb0Extractor();
                byte[] extracted = extractor.ExtractEntryToBuffer(outputStream, metadata.Entries[0]);
                Assert.Equal(originalData, extracted);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void CheckResult_IsAllValid_WhenAllValid()
        {
            var result = new CheckResult
            {
                TotalEntries = 3,
                ValidEntries = 3,
                InvalidEntries = 0,
                HasMd5Records = true
            };
            Assert.True(result.IsAllValid);
        }

        [Fact]
        public void CheckResult_IsAllValid_WhenSomeInvalid()
        {
            var result = new CheckResult
            {
                TotalEntries = 3,
                ValidEntries = 2,
                InvalidEntries = 1,
                HasMd5Records = true
            };
            Assert.False(result.IsAllValid);
        }

        [Fact]
        public void EntryCheckResult_ToString_Valid()
        {
            var result = new EntryCheckResult
            {
                Name = "boot.img",
                IsValid = true,
                HasMd5Check = true
            };
            string s = result.ToString();
            Assert.Contains("[OK]", s);
            Assert.Contains("boot.img", s);
        }

        [Fact]
        public void EntryCheckResult_ToString_Invalid()
        {
            var result = new EntryCheckResult
            {
                Name = "boot.img",
                IsValid = false,
                HasMd5Check = true,
                ErrorMessage = "MD5 checksum mismatch"
            };
            string s = result.ToString();
            Assert.Contains("[FAIL]", s);
            Assert.Contains("boot.img", s);
        }

        [Fact]
        public void EntryCheckResult_ToString_Skipped()
        {
            var result = new EntryCheckResult
            {
                Name = "boot.img",
                IsValid = true,
                HasMd5Check = false
            };
            string s = result.ToString();
            Assert.Contains("[SKIP]", s);
            Assert.Contains("boot.img", s);
        }
    }
}
