using System;
using System.Text;
using Xunit;

namespace FirmwareKit.Nb0.Tests
{
    public class Crc32Tests
    {
        [Fact]
        public void Calculate_EmptyArray_ReturnsZero()
        {
            uint crc = Crc32.Calculate(Array.Empty<byte>());
            Assert.Equal(0u, crc);
        }

        [Fact]
        public void Calculate_KnownData_ReturnsExpectedCrc()
        {
            byte[] data = Encoding.ASCII.GetBytes("123456789");
            uint crc = Crc32.Calculate(data);
            Assert.Equal(0xCBF43926u, crc);
        }

        [Fact]
        public void Calculate_WithOffset_WorksCorrectly()
        {
            byte[] data = new byte[] { 0xFF, 0xFF, (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', 0xFF };
            uint crc = Crc32.Calculate(data, 2, 9);
            Assert.Equal(0xCBF43926u, crc);
        }

        [Fact]
        public void Calculate_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Crc32.Calculate(null!));
        }

        [Fact]
        public void Calculate_InvalidOffset_ThrowsArgumentOutOfRangeException()
        {
            byte[] data = new byte[10];
            Assert.Throws<ArgumentOutOfRangeException>(() => Crc32.Calculate(data, -1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => Crc32.Calculate(data, 11, 1));
        }

        [Fact]
        public void Calculate_InvalidLength_ThrowsArgumentOutOfRangeException()
        {
            byte[] data = new byte[10];
            Assert.Throws<ArgumentOutOfRangeException>(() => Crc32.Calculate(data, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Crc32.Calculate(data, 5, 10));
        }

        [Fact]
        public void Update_IncrementalMatchesOneShot()
        {
            byte[] data = Encoding.ASCII.GetBytes("Hello, World! This is a test of incremental CRC32 calculation.");
            uint oneShot = Crc32.Calculate(data);

            uint incremental = Crc32.Start();
            incremental = Crc32.Update(incremental, data, 0, 10);
            incremental = Crc32.Update(incremental, data, 10, data.Length - 10);
            incremental = Crc32.Finish(incremental);

            Assert.Equal(oneShot, incremental);
        }

        [Fact]
        public void Start_Returns0xFFFFFFFF()
        {
            Assert.Equal(0xFFFFFFFFu, Crc32.Start());
        }

        [Fact]
        public void Finish_InvertsCrc()
        {
            uint crc = 0x12345678;
            Assert.Equal(~crc, Crc32.Finish(crc));
        }
    }
}
