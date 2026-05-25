using System;
using System.IO;
using System.Text;
using Xunit;

namespace FirmwareKit.Nb0.Tests
{
    public class Nb0EntryHeaderTests
    {
        [Fact]
        public void Read_ValidBinaryData_ParsesCorrectly()
        {
            byte[] nameBytes = new byte[48];
            byte[] encoded = Encoding.ASCII.GetBytes("test_file.bin");
            Array.Copy(encoded, nameBytes, encoded.Length);

            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, true))
            {
                writer.Write(0x00001000u);  // LoDataOffset
                writer.Write(0x00002000u);  // LoFileSize
                writer.Write(0x00000002u);  // HiDataOffset
                writer.Write(0x00000001u);  // HiFileSize
                writer.Write(nameBytes);
            }

            ms.Position = 0;
            using var reader = new BinaryReader(ms, Encoding.ASCII, true);
            var header = Nb0EntryHeader.Read(reader);

            Assert.Equal(0x00001000u, header.LoDataOffset);
            Assert.Equal(0x00002000u, header.LoFileSize);
            Assert.Equal(0x00000002u, header.HiDataOffset);
            Assert.Equal(0x00000001u, header.HiFileSize);
            Assert.Equal("test_file.bin", header.Name);
        }

        [Fact]
        public void Read_Write_Roundtrip()
        {
            var header = new Nb0EntryHeader
            {
                LoDataOffset = 0x1000,
                LoFileSize = 0x2000,
                HiDataOffset = 0x3,
                HiFileSize = 0x4,
                Name = "boot.img"
            };

            byte[] bytes = header.ToBytes();
            Assert.Equal(Nb0EntryHeader.StructSize, bytes.Length);

            using var ms = new MemoryStream(bytes);
            using var reader = new BinaryReader(ms);
            var readBack = Nb0EntryHeader.Read(reader);

            Assert.Equal(header.LoDataOffset, readBack.LoDataOffset);
            Assert.Equal(header.LoFileSize, readBack.LoFileSize);
            Assert.Equal(header.HiDataOffset, readBack.HiDataOffset);
            Assert.Equal(header.HiFileSize, readBack.HiFileSize);
            Assert.Equal(header.Name, readBack.Name);
        }

        [Fact]
        public void DataOffset_CalculatedFromHiAndLo()
        {
            var header = new Nb0EntryHeader
            {
                LoDataOffset = 0x1000,
                HiDataOffset = 0x2
            };

            Assert.Equal(0x2_0000_1000L, header.DataOffset);
        }

        [Fact]
        public void FileSize_CalculatedFromHiAndLo()
        {
            var header = new Nb0EntryHeader
            {
                LoFileSize = 0x2000,
                HiFileSize = 0x1
            };

            Assert.Equal(0x1_0000_2000L, header.FileSize);
        }

        [Fact]
        public void DataOffset_Set_SplitsIntoHiAndLo()
        {
            var header = new Nb0EntryHeader
            {
                DataOffset = 0x1_0000_1000L
            };

            Assert.Equal(0x1000u, header.LoDataOffset);
            Assert.Equal(0x1u, header.HiDataOffset);
        }

        [Fact]
        public void FileSize_Set_SplitsIntoHiAndLo()
        {
            var header = new Nb0EntryHeader
            {
                FileSize = 0x2_0000_2000L
            };

            Assert.Equal(0x2000u, header.LoFileSize);
            Assert.Equal(0x2u, header.HiFileSize);
        }

        [Fact]
        public void Name_TrimmedOfNullChars()
        {
            byte[] nameBytes = new byte[48];
            nameBytes[0] = (byte)'A';
            nameBytes[1] = (byte)'B';
            nameBytes[2] = 0;

            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, true))
            {
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(nameBytes);
            }

            ms.Position = 0;
            using var reader = new BinaryReader(ms, Encoding.ASCII, true);
            var header = Nb0EntryHeader.Read(reader);

            Assert.Equal("AB", header.Name);
        }

        [Fact]
        public void Name_TruncatedIfTooLong()
        {
            string longName = new string('A', 60);
            var header = new Nb0EntryHeader
            {
                Name = longName
            };

            byte[] bytes = header.ToBytes();
            using var ms = new MemoryStream(bytes);
            using var reader = new BinaryReader(ms);
            var readBack = Nb0EntryHeader.Read(reader);

            Assert.True(readBack.Name.Length <= 47);
            Assert.Equal(new string('A', 47), readBack.Name);
        }

        [Fact]
        public void Read_NullReader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Nb0EntryHeader.Read(null!));
        }

        [Fact]
        public void Write_NullWriter_ThrowsArgumentNullException()
        {
            var header = new Nb0EntryHeader();
            Assert.Throws<ArgumentNullException>(() => header.Write(null!));
        }

        [Fact]
        public void StructSize_Is64()
        {
            Assert.Equal(64, Nb0EntryHeader.StructSize);
        }

        [Fact]
        public void NameLength_Is48()
        {
            Assert.Equal(48, Nb0EntryHeader.NameLength);
        }
    }
}
