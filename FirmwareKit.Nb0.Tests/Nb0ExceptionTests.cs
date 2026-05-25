using System;
using Xunit;

namespace FirmwareKit.Nb0.Tests
{
    public class Nb0ExceptionTests
    {
        [Fact]
        public void Nb0Exception_Message_SetCorrectly()
        {
            var ex = new Nb0Exception("test message");
            Assert.Equal("test message", ex.Message);
        }

        [Fact]
        public void Nb0Exception_WithInnerException_PreservesInner()
        {
            var inner = new InvalidOperationException("inner");
            var ex = new Nb0Exception("outer", inner);
            Assert.Equal("outer", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void Nb0Md5MismatchException_ContainsEntryInfo()
        {
            byte[] expectedMd5 = new byte[] { 0xAB, 0xCD, 0xEF, 0x01, 0x23, 0x45, 0x67, 0x89, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            byte[] actualMd5 = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            var ex = new Nb0Md5MismatchException("boot.img", expectedMd5, actualMd5);
            Assert.Equal("boot.img", ex.EntryName);
            Assert.Equal(expectedMd5, ex.ExpectedMd5);
            Assert.Equal(actualMd5, ex.ActualMd5);
        }

        [Fact]
        public void Nb0Md5MismatchException_ToString_ContainsHexMd5()
        {
            byte[] expectedMd5 = new byte[] { 0xAB, 0xCD, 0xEF, 0x01, 0x23, 0x45, 0x67, 0x89, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            byte[] actualMd5 = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            var ex = new Nb0Md5MismatchException("boot.img", expectedMd5, actualMd5);
            string s = ex.ToString();

            Assert.Contains("boot.img", s);
            Assert.Contains("abcdef01", s);
            Assert.Contains("12345678", s);
        }

        [Fact]
        public void Nb0Md5MismatchException_PropertiesReturnDefensiveCopy()
        {
            byte[] expected = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
            byte[] actual = new byte[] { 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20 };

            var ex = new Nb0Md5MismatchException("test", expected, actual);

            // Modify returned arrays
            ex.ExpectedMd5[0] = 0xFF;
            ex.ActualMd5[0] = 0xFF;

            // Original values should be unchanged
            Assert.Equal(0x01, ex.ExpectedMd5[0]);
            Assert.Equal(0x11, ex.ActualMd5[0]);
        }

        [Fact]
        public void Nb0CorruptedException_IsSubclassOfNb0Exception()
        {
            var ex = new Nb0CorruptedException("corrupted data");
            Assert.True(ex is Nb0Exception);
            Assert.Equal("corrupted data", ex.Message);
        }


    }
}
