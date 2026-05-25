using System;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>NB0 固件格式操作中发生的异常的基类。</para>
    /// The base exception class for errors that occur during NB0 firmware format operations.
    /// </summary>
    public class Nb0Exception : Exception
    {
        /// <summary>
        /// <para>使用指定的错误消息初始化 <see cref="Nb0Exception"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0Exception"/> with a specified error message.
        /// </summary>
        /// <param name="message">
        /// <para>描述错误的消息。</para>
        /// The message that describes the error.
        /// </param>
        public Nb0Exception(string message) : base(message) { }

        /// <summary>
        /// <para>使用指定的错误消息和内部异常初始化 <see cref="Nb0Exception"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0Exception"/> with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">
        /// <para>描述错误的消息。</para>
        /// The message that describes the error.
        /// </param>
        /// <param name="innerException">
        /// <para>导致当前异常的内部异常。</para>
        /// The inner exception that caused the current exception.
        /// </param>
        public Nb0Exception(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// <para>当 NB0 固件文件已损坏或格式无效时抛出的异常。</para>
    /// The exception that is thrown when an NB0 firmware file is corrupted or has an invalid format.
    /// </summary>
    public sealed class Nb0CorruptedException : Nb0Exception
    {
        /// <summary>
        /// <para>使用指定的错误消息初始化 <see cref="Nb0CorruptedException"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0CorruptedException"/> with a specified error message.
        /// </summary>
        /// <param name="message">
        /// <para>描述损坏情况的消息。</para>
        /// The message that describes the corruption.
        /// </param>
        public Nb0CorruptedException(string message) : base(message) { }

        /// <summary>
        /// <para>使用指定的错误消息和内部异常初始化 <see cref="Nb0CorruptedException"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0CorruptedException"/> with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">
        /// <para>描述损坏情况的消息。</para>
        /// The message that describes the corruption.
        /// </param>
        /// <param name="innerException">
        /// <para>导致当前异常的内部异常。</para>
        /// The inner exception that caused the current exception.
        /// </param>
        public Nb0CorruptedException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// <para>当 MD5 校验和不匹配时抛出的异常。</para>
    /// The exception that is thrown when an MD5 checksum mismatch is detected.
    /// </summary>
    public sealed class Nb0Md5MismatchException : Nb0Exception
    {
        private readonly byte[] _expectedMd5;
        private readonly byte[] _actualMd5;

        /// <summary>
        /// <para>获取校验失败的条目名称。</para>
        /// Gets the name of the entry that failed verification.
        /// </summary>
        public string EntryName { get; }

        /// <summary>
        /// <para>获取预期的 MD5 校验和的副本。</para>
        /// Gets a copy of the expected MD5 checksum.
        /// </summary>
        public byte[] ExpectedMd5
        {
            get
            {
                var copy = new byte[_expectedMd5.Length];
                Array.Copy(_expectedMd5, copy, _expectedMd5.Length);
                return copy;
            }
        }

        /// <summary>
        /// <para>获取实际的 MD5 校验和的副本。</para>
        /// Gets a copy of the actual MD5 checksum.
        /// </summary>
        public byte[] ActualMd5
        {
            get
            {
                var copy = new byte[_actualMd5.Length];
                Array.Copy(_actualMd5, copy, _actualMd5.Length);
                return copy;
            }
        }

        /// <summary>
        /// <para>使用条目名称、预期和实际的 MD5 校验和初始化 <see cref="Nb0Md5MismatchException"/> 的新实例。</para>
        /// Initializes a new instance of <see cref="Nb0Md5MismatchException"/> with the entry name, expected, and actual MD5 checksums.
        /// </summary>
        /// <param name="entryName">
        /// <para>校验失败的条目名称。</para>
        /// The name of the entry that failed verification.
        /// </param>
        /// <param name="expectedMd5">
        /// <para>预期的 MD5 校验和。</para>
        /// The expected MD5 checksum.
        /// </param>
        /// <param name="actualMd5">
        /// <para>实际的 MD5 校验和。</para>
        /// The actual MD5 checksum.
        /// </param>
        public Nb0Md5MismatchException(string entryName, byte[] expectedMd5, byte[] actualMd5)
            : base($"MD5 mismatch for entry '{entryName}': expected {FormatMd5(expectedMd5)}, got {FormatMd5(actualMd5)}")
        {
            EntryName = entryName;
            _expectedMd5 = (byte[])expectedMd5.Clone();
            _actualMd5 = (byte[])actualMd5.Clone();
        }

        private static string FormatMd5(byte[] md5)
        {
            if (md5 == null) return "(null)";
            var hex = new char[md5.Length * 2];
            for (int i = 0; i < md5.Length; i++)
            {
                hex[i * 2] = GetHexChar(md5[i] >> 4);
                hex[i * 2 + 1] = GetHexChar(md5[i] & 0x0F);
            }
            return new string(hex);
        }

        private static char GetHexChar(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + value - 10);
        }
    }

}
