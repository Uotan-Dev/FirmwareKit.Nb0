using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FirmwareKit.Nb0.Tests
{
    public class Nb0ParserTests
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

        [Fact]
        public void ParseFromStream_SingleEntry_ParsesCorrectly()
        {
            byte[] data = Encoding.ASCII.GetBytes("Hello NB0 World");
            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", data)
            });

            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.Equal(1, metadata.EntryCount);
            Assert.Single(metadata.Entries);
            Assert.Equal("boot.img", metadata.Entries[0].Name);
            Assert.Equal(data.Length, metadata.Entries[0].Size);
            Assert.Equal(0, metadata.Entries[0].Offset);
        }

        [Fact]
        public void ParseFromStream_MultipleEntries_ParsesAll()
        {
            byte[] data1 = Encoding.ASCII.GetBytes("Boot data");
            byte[] data2 = Encoding.ASCII.GetBytes("Recovery data");
            byte[] data3 = Encoding.ASCII.GetBytes("System data");

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", data1),
                ("recovery.img", data2),
                ("system.img", data3)
            });

            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.Equal(3, metadata.EntryCount);
            Assert.Equal(3, metadata.Entries.Count);
            Assert.Equal("boot.img", metadata.Entries[0].Name);
            Assert.Equal("recovery.img", metadata.Entries[1].Name);
            Assert.Equal("system.img", metadata.Entries[2].Name);
            Assert.Equal(data1.Length, metadata.Entries[0].Size);
            Assert.Equal(data2.Length, metadata.Entries[1].Size);
            Assert.Equal(data3.Length, metadata.Entries[2].Size);
        }

        [Fact]
        public void ParseFromStream_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Nb0Parser.ParseFromStream(null!));
        }

        [Fact]
        public void ParseFromStream_EmptyNb0_ParsesZeroEntries()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(0u);
            }
            ms.Position = 0;

            var metadata = Nb0Parser.ParseFromStream(ms);
            Assert.Equal(0, metadata.EntryCount);
            Assert.Empty(metadata.Entries);
        }

        [Fact]
        public void ParseFromStream_WithBootMagic_InfersType()
        {
            byte[] bootData = new byte[64];
            byte[] android = Encoding.ASCII.GetBytes("ANDROID!");
            Array.Copy(android, 0, bootData, 0, android.Length);

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", bootData)
            });
            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.Equal("boot", metadata.Entries[0].InferredType);
        }

        [Fact]
        public void ParseFromStream_WithSparseMagic_InfersType()
        {
            byte[] sparseData = new byte[64];
            sparseData[0] = 0xED;
            sparseData[1] = 0x26;
            sparseData[2] = 0xFF;
            sparseData[3] = 0x3A;

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("system.img", sparseData)
            });
            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.Equal("sparse", metadata.Entries[0].InferredType);
        }

        [Fact]
        public void ParseFromStream_WithExt4Magic_InfersType()
        {
            byte[] ext4Data = new byte[2048];
            ext4Data[0x438] = 0x53;
            ext4Data[0x439] = 0xEF;

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("system.img", ext4Data)
            });
            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.Equal("ext4", metadata.Entries[0].InferredType);
        }

        [Fact]
        public void ParseFromStream_DuplicateNames_GeneratesWarning()
        {
            byte[] data1 = Encoding.ASCII.GetBytes("data1");
            byte[] data2 = Encoding.ASCII.GetBytes("data2");

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", data1),
                ("boot.img", data2)
            });
            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.Contains(metadata.Warnings, w => w.Contains("Duplicate"));
        }

        [Fact]
        public void ParseFromStream_ZeroSizeEntry_GeneratesWarning()
        {
            byte[] emptyData = Array.Empty<byte>();

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("empty.img", emptyData)
            });
            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.Contains(metadata.Warnings, w => w.Contains("zero size"));
        }

        [Fact]
        public void ParseFromStream_WithMd5Entry_ParsesMd5Records()
        {
            byte[] firmwareData = Encoding.ASCII.GetBytes("firmware content here");
            byte[] md5Checksum = new byte[16];
            for (int i = 0; i < 16; i++) md5Checksum[i] = (byte)(i + 1);

            byte[] md5RecordData = CreateMd5RecordData(0, (uint)firmwareData.Length, md5Checksum);

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("firmware.bin", firmwareData),
                ("firmware.md5", md5RecordData)
            });

            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.True(metadata.HasMd5Records);
            Assert.Single(metadata.Md5Records);
            Assert.Equal(0u, metadata.Md5Records[0].Offset);
            Assert.Equal((uint)firmwareData.Length, metadata.Md5Records[0].Length);
            Assert.Equal(md5Checksum, metadata.Md5Records[0].Md5Checksum);
        }

        [Fact]
        public void ParseFromStream_WithoutMd5Entry_NoMd5Records()
        {
            byte[] data = Encoding.ASCII.GetBytes("some data");

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("boot.img", data)
            });

            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.False(metadata.HasMd5Records);
            Assert.Empty(metadata.Md5Records);
        }

        [Fact]
        public async Task ParseFromStreamAsync_SameResultAsSync()
        {
            byte[] data = Encoding.ASCII.GetBytes("Async test data");
            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("test.img", data)
            });

            var syncResult = Nb0Parser.ParseFromStream(stream);
            stream.Position = 0;

            var asyncResult = await Nb0Parser.ParseFromStreamAsync(stream, TestContext.Current.CancellationToken);

            Assert.Equal(syncResult.EntryCount, asyncResult.EntryCount);
            Assert.Equal(syncResult.Entries.Count, asyncResult.Entries.Count);
            for (int i = 0; i < syncResult.Entries.Count; i++)
            {
                Assert.Equal(syncResult.Entries[i].Name, asyncResult.Entries[i].Name);
                Assert.Equal(syncResult.Entries[i].Size, asyncResult.Entries[i].Size);
                Assert.Equal(syncResult.Entries[i].Offset, asyncResult.Entries[i].Offset);
            }
        }

        [Fact]
        public void ValidateMetadata_NegativeEntryCount_ThrowsCorruptedException()
        {
            var metadata = new Nb0Metadata { EntryCount = -1 };
            Assert.Throws<Nb0CorruptedException>(() => Nb0Parser.ValidateMetadata(metadata));
        }

        [Fact]
        public void ValidateMetadata_HighEntryCount_GeneratesWarning()
        {
            var metadata = new Nb0Metadata { EntryCount = 2000 };
            Nb0Parser.ValidateMetadata(metadata);
            Assert.Contains(metadata.Warnings, w => w.Contains("Unusually high"));
        }

        [Fact]
        public void InferFirmwareInfo_SpreadtrumEntry_SetsType()
        {
            var metadata = new Nb0Metadata();
            metadata.Entries.Add(new Nb0FileEntry { Name = "fdl1.bin" });
            metadata.Entries.Add(new Nb0FileEntry { Name = "fdl2.bin" });
            metadata.Entries.Add(new Nb0FileEntry { Name = "modem.bin" });

            Nb0Parser.InferFirmwareInfo(metadata);

            Assert.Equal("Spreadtrum/UNISOC", metadata.InferredFirmwareType);
        }

        [Fact]
        public void InferFirmwareInfo_Sc88Name_InferredAsSpreadtrum()
        {
            var metadata = new Nb0Metadata();
            metadata.Entries.Add(new Nb0FileEntry { Name = "sc8810_fdl.bin" });
            Nb0Parser.InferFirmwareInfo(metadata);
            Assert.Equal("Spreadtrum/UNISOC", metadata.InferredFirmwareType);
        }

        [Fact]
        public void ParseFromStream_SetsTotalSize()
        {
            var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("test.bin", new byte[] { 0x01, 0x02, 0x03 })
            });
            var metadata = Nb0Parser.ParseFromStream(stream);
            Assert.True(metadata.TotalSize > 0, "TotalSize should be greater than 0 after parsing");
            Assert.Equal(stream.Length, metadata.TotalSize);
        }

        [Fact]
        public void ValidateMetadata_OverlappingOffsets_AddsWarning()
        {
            var metadata = new Nb0Metadata { EntryCount = 2 };
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry1", Offset = 0, Size = 100 });
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry2", Offset = 50, Size = 100 });
            Nb0Parser.ValidateMetadata(metadata);
            Assert.Contains(metadata.Warnings, w => w.Contains("Overlapping data"));
        }

        [Fact]
        public void ValidateMetadata_OffsetExceedsFile_AddsWarning()
        {
            var metadata = new Nb0Metadata { EntryCount = 1, DataSectionOffset = 68 };
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry1", Offset = 0, Size = 1000 });
            Nb0Parser.ValidateMetadata(metadata, 100); // File is only 100 bytes
            Assert.Contains(metadata.Warnings, w => w.Contains("extends beyond file end"));
        }

        [Fact]
        public void ValidateMetadata_NoOverlap_NoWarning()
        {
            var metadata = new Nb0Metadata { EntryCount = 2 };
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry1", Offset = 0, Size = 100 });
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry2", Offset = 100, Size = 100 });
            Nb0Parser.ValidateMetadata(metadata);
            Assert.DoesNotContain(metadata.Warnings, w => w.Contains("Overlapping data"));
        }

        [Fact]
        public void ParseFromStream_Md5RecordCountMismatch_GeneratesWarning()
        {
            // Create 2 firmware entries + 1 .md5 entry with only 1 MD5 record (mismatch)
            byte[] firmwareData1 = Encoding.ASCII.GetBytes("firmware1");
            byte[] firmwareData2 = Encoding.ASCII.GetBytes("firmware2");
            byte[] md5Checksum = new byte[16];
            for (int i = 0; i < 16; i++) md5Checksum[i] = (byte)(i + 1);

            // Only 1 MD5 record for 2 non-md5 entries -> mismatch
            byte[] md5RecordData = CreateMd5RecordData(0, (uint)firmwareData1.Length, md5Checksum);

            using var stream = CreateNb0Stream(new List<(string, byte[])>
            {
                ("firmware1.bin", firmwareData1),
                ("firmware2.bin", firmwareData2),
                ("firmware.md5", md5RecordData)
            });

            var metadata = Nb0Parser.ParseFromStream(stream);

            Assert.Contains(metadata.Warnings, w => w.Contains("MD5 record count") && w.Contains("does not match"));
        }

        [Fact]
        public void ValidateMetadata_NonMonotonicOffsets_AddsWarning()
        {
            var metadata = new Nb0Metadata { EntryCount = 2 };
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry1", Offset = 200, Size = 100 });
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry2", Offset = 100, Size = 100 });
            Nb0Parser.ValidateMetadata(metadata);
            Assert.Contains(metadata.Warnings, w => w.Contains("is less than previous entry"));
        }

        [Fact]
        public void ValidateMetadata_MonotonicOffsets_NoWarning()
        {
            var metadata = new Nb0Metadata { EntryCount = 3 };
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry1", Offset = 0, Size = 100 });
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry2", Offset = 100, Size = 100 });
            metadata.Entries.Add(new Nb0FileEntry { Name = "entry3", Offset = 200, Size = 100 });
            Nb0Parser.ValidateMetadata(metadata);
            Assert.DoesNotContain(metadata.Warnings, w => w.Contains("is less than previous entry"));
        }

        [Fact]
        public void ParseFromStream_EntryCountExceedsIntMaxValue_ThrowsCorruptedException()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // Write a uint value > int.MaxValue (0x80000001 = 2147483649)
                writer.Write(0x80000001u);
            }
            ms.Position = 0;

            var ex = Assert.Throws<Nb0CorruptedException>(() => Nb0Parser.ParseFromStream(ms));
            Assert.Contains("exceeds maximum supported value", ex.Message);
        }

        [Fact]
        public async Task ParseFromStreamAsync_EntryCountExceedsIntMaxValue_ThrowsCorruptedException()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(0x80000001u);
            }
            ms.Position = 0;

            var ex = await Assert.ThrowsAsync<Nb0CorruptedException>(
                () => Nb0Parser.ParseFromStreamAsync(ms, TestContext.Current.CancellationToken));
            Assert.Contains("exceeds maximum supported value", ex.Message);
        }

        [Fact]
        public void ParseFromStream_FileTooSmallForEntryCount_ThrowsCorruptedException()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // Declare 100 entries but only write the 4-byte count
                writer.Write(100u);
            }
            ms.Position = 0;

            var ex = Assert.Throws<Nb0CorruptedException>(() => Nb0Parser.ParseFromStream(ms));
            Assert.Contains("File too small", ex.Message);
        }

        [Fact]
        public async Task ParseFromStreamAsync_FileTooSmallForEntryCount_ThrowsCorruptedException()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(100u);
            }
            ms.Position = 0;

            var ex = await Assert.ThrowsAsync<Nb0CorruptedException>(
                () => Nb0Parser.ParseFromStreamAsync(ms, TestContext.Current.CancellationToken));
            Assert.Contains("File too small", ex.Message);
        }

        [Fact]
        public void ParseFromStream_EntryCountReadAsUnsigned_ParsesCorrectly()
        {
            // Create a stream with entry count = 1 written as uint
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(1u); // entry count as uint
                // Write one entry header (64 bytes)
                writer.Write(0u);        // LoDataOffset
                writer.Write(5u);        // LoFileSize
                writer.Write(0u);        // HiDataOffset
                writer.Write(0u);        // HiFileSize
                var nameBytes = new byte[48];
                var encoded = Encoding.ASCII.GetBytes("test.bin");
                Array.Copy(encoded, nameBytes, Math.Min(encoded.Length, 47));
                writer.Write(nameBytes);
                // Write data section (5 bytes + 3 padding for alignment)
                writer.Write(Encoding.ASCII.GetBytes("hello"));
                writer.Write(new byte[3]);
            }
            ms.Position = 0;

            var metadata = Nb0Parser.ParseFromStream(ms);
            Assert.Equal(1, metadata.EntryCount);
            Assert.Single(metadata.Entries);
            Assert.Equal("test.bin", metadata.Entries[0].Name);
        }

        private static string CreateNb0TempFile(List<(string Name, byte[] Data)> entries)
        {
            var tempFile = Path.GetTempFileName();
            using var stream = CreateNb0Stream(entries);
            using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fs);
            return tempFile;
        }

        [Fact]
        public void ParseAsJson_ProducesValidJson()
        {
            byte[] data = Encoding.ASCII.GetBytes("firmware data for json test");
            var tempFile = CreateNb0TempFile(new List<(string, byte[])>
            {
                ("boot.img", data),
                ("system.img", Encoding.ASCII.GetBytes("system data"))
            });

            try
            {
                string json = Nb0Parser.ParseAsJson(tempFile);

                Assert.Contains("\"entryCount\"", json);
                Assert.Contains("\"totalSize\"", json);
                Assert.Contains("\"dataSectionOffset\"", json);
                Assert.Contains("\"entries\"", json);
                Assert.Contains("\"boot.img\"", json);
                Assert.Contains("\"system.img\"", json);
                Assert.Contains("\"offset\"", json);
                Assert.Contains("\"size\"", json);
                Assert.StartsWith("{", json);
                Assert.EndsWith("}", json.TrimEnd());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ParseAsJson_CompactMode_NoExtraWhitespace()
        {
            byte[] data = Encoding.ASCII.GetBytes("compact test data");
            var tempFile = CreateNb0TempFile(new List<(string, byte[])>
            {
                ("boot.img", data)
            });

            try
            {
                string json = Nb0Parser.ParseAsJson(tempFile, compact: true);

                Assert.DoesNotContain(Environment.NewLine, json);
                Assert.Contains("\"entryCount\"", json);
                Assert.Contains("\"boot.img\"", json);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ParseAsJson_SpecialCharacters_Escaped()
        {
            byte[] data = Encoding.ASCII.GetBytes("special char test");
            var tempFile = CreateNb0TempFile(new List<(string, byte[])>
            {
                ("boot\"img\nrecovery.bin", data)
            });

            try
            {
                string json = Nb0Parser.ParseAsJson(tempFile);

                Assert.Contains("\\\"", json);
                Assert.Contains("\\n", json);
                Assert.DoesNotContain("\"boot\"img", json);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
