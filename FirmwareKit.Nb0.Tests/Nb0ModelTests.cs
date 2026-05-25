using System;
using System.IO;
using Xunit;

namespace FirmwareKit.Nb0.Tests
{
    public class Nb0ModelTests
    {
        [Fact]
        public void Nb0Entry_NullHeader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Nb0Entry((Nb0EntryHeader)null!));
        }

        [Fact]
        public void Nb0Entry_WithNameAndData_SetsProperties()
        {
            byte[] data = new byte[] { 1, 2, 3, 4, 5 };
            var entry = new Nb0Entry("test.img", data);

            Assert.Equal("test.img", entry.Header.Name);
            Assert.Same(data, entry.Data);
        }

        [Fact]
        public void Nb0Entry_NullName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Nb0Entry(null!, new byte[10]));
        }

        [Fact]
        public void Nb0Entry_EmptyName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new Nb0Entry("", new byte[10]));
        }

        [Fact]
        public void Nb0Entry_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Nb0Entry("test.img", null!));
        }

        [Fact]
        public void Nb0Entry_GetFileSize_ReturnsDataLength()
        {
            var entry = new Nb0Entry("test.img", new byte[100]);
            Assert.Equal(100, entry.GetFileSize());
        }

        [Fact]
        public void Nb0FileEntry_FromHeader_MapsCorrectly()
        {
            var header = new Nb0EntryHeader
            {
                Name = "boot.img",
                DataOffset = 0x1_0000_1000L,
                FileSize = 0x2_0000_2000L
            };

            var fileEntry = Nb0FileEntry.FromHeader(header, 0x1000);

            Assert.Equal("boot.img", fileEntry.Name);
            Assert.Equal(0x2_0000_2000L, fileEntry.Size);
            Assert.Equal(0x1_0000_1000L, fileEntry.Offset);
            Assert.Equal(0x1000, fileEntry.FileDataOffset);
        }

        [Fact]
        public void Nb0Md5Record_Read_WritesAndReadsBack()
        {
            var record = new Nb0Md5Record
            {
                Offset = 0x1000,
                Length = 0x2000,
                Md5Checksum = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }
            };

            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, System.Text.Encoding.ASCII, true))
            {
                record.Write(writer);
            }

            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            var readBack = Nb0Md5Record.Read(reader);

            Assert.Equal(record.Offset, readBack.Offset);
            Assert.Equal(record.Length, readBack.Length);
            Assert.Equal(record.Md5Checksum, readBack.Md5Checksum);
        }

        [Fact]
        public void Nb0Md5Record_StructSize_Is24()
        {
            Assert.Equal(24, Nb0Md5Record.StructSize);
        }

        [Fact]
        public void Nb0Md5Record_Md5Checksum_Is16Bytes()
        {
            var record = new Nb0Md5Record();
            Assert.Equal(16, record.Md5Checksum.Length);
        }

        [Fact]
        public void Nb0Md5Record_Md5Checksum_ReturnsDefensiveCopy()
        {
            var record = new Nb0Md5Record();
            byte[] originalChecksum = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                                         0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
            record.Md5Checksum = originalChecksum;

            byte[] retrieved = record.Md5Checksum;
            retrieved[0] = 0xFF; // Modify the returned copy

            byte[] retrievedAgain = record.Md5Checksum;
            Assert.Equal(0x01, retrievedAgain[0]); // Internal state should not be affected
        }

        [Fact]
        public void ExtractionOptions_Default_VerifyMd5True()
        {
            var options = ExtractionOptions.Default;
            Assert.True(options.VerifyMd5);
        }

        [Fact]
        public void ExtractionOptions_Default_GenerateListFileTrue()
        {
            var options = ExtractionOptions.Default;
            Assert.True(options.GenerateListFile);
        }

        [Fact]
        public void Nb0ExtractionProgress_PropertiesSetCorrectly()
        {
            var progress = new Nb0ExtractionProgress
            {
                TotalEntries = 10,
                CompletedEntries = 5,
                CurrentEntryName = "test.img"
            };
            Assert.Equal(10, progress.TotalEntries);
            Assert.Equal(5, progress.CompletedEntries);
            Assert.Equal("test.img", progress.CurrentEntryName);
        }

        [Fact]
        public void Nb0Entry_NameExactly47AsciiBytes_Succeeds()
        {
            // 47 ASCII characters - should succeed
            string name = new string('a', 47);
            var entry = new Nb0Entry(name, new byte[] { 0x01 });
            Assert.Equal(name, entry.Header.Name);
        }

        [Fact]
        public void Nb0Entry_NameExceeds47AsciiBytes_ThrowsArgumentException()
        {
            // 48 ASCII characters - should fail
            string name = new string('a', 48);
            Assert.Throws<ArgumentException>(() => new Nb0Entry(name, new byte[] { 0x01 }));
        }

        [Fact]
        public void Nb0Entry_NonAsciiName_ThrowsArgumentException()
        {
            // Chinese characters should be rejected
            var ex = Assert.Throws<ArgumentException>(() => new Nb0Entry("测试", new byte[] { 0x01 }));
            Assert.Contains("non-ASCII", ex.Message);
        }

        [Fact]
        public void Nb0Entry_AsciiName_Succeeds()
        {
            // Pure ASCII name should succeed
            var entry = new Nb0Entry("boot.img", new byte[] { 0x01 });
            Assert.Equal("boot.img", entry.Header.Name);
        }
    }
}
