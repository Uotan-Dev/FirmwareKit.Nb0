using System;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>提供 CRC-32 校验和的计算功能。此类作为实用工具保留，不再用于 NB0 条目验证。</para>
    /// Provides CRC-32 checksum calculation functionality. This class is kept as a utility and is no longer used for NB0 entry verification.
    /// </summary>
    /// <remarks>
    /// <para>使用多项式 0xEDB88320（即 CRC-32/ISO-HDLC）进行计算。</para>
    /// Uses polynomial 0xEDB88320 (CRC-32/ISO-HDLC) for calculation.
    /// </remarks>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ 0xEDB88320;
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }
            return table;
        }

        /// <summary>
        /// <para>计算指定字节数组的 CRC-32 校验和。</para>
        /// Calculates the CRC-32 checksum of the specified byte array.
        /// </summary>
        /// <param name="data">
        /// <para>要计算校验和的字节数组。</para>
        /// The byte array to calculate the checksum for.
        /// </param>
        /// <returns>
        /// <para>计算得到的 CRC-32 校验和。</para>
        /// The calculated CRC-32 checksum.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="data"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="data"/> is null.
        /// </exception>
        public static uint Calculate(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return Calculate(data, 0, data.Length);
        }

        /// <summary>
        /// <para>计算指定字节数组中某个区间的 CRC-32 校验和。</para>
        /// Calculates the CRC-32 checksum of a region in the specified byte array.
        /// </summary>
        /// <param name="data">
        /// <para>包含要计算数据的字节数组。</para>
        /// The byte array containing the data to calculate.
        /// </param>
        /// <param name="offset">
        /// <para>起始偏移量（字节）。</para>
        /// The starting offset in bytes.
        /// </param>
        /// <param name="length">
        /// <para>要计算的字节数。</para>
        /// The number of bytes to calculate.
        /// </param>
        /// <returns>
        /// <para>计算得到的 CRC-32 校验和。</para>
        /// The calculated CRC-32 checksum.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="data"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="data"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para>当 <paramref name="offset"/> 或 <paramref name="length"/> 超出有效范围时抛出。</para>
        /// Thrown when <paramref name="offset"/> or <paramref name="length"/> is out of valid range.
        /// </exception>
        public static uint Calculate(byte[] data, int offset, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0 || offset + length > data.Length) throw new ArgumentOutOfRangeException(nameof(length));

            uint crc = 0xFFFFFFFF;
            for (int i = offset; i < offset + length; i++)
            {
                crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
            }
            return ~crc;
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        /// <summary>
        /// <para>计算指定只读字节跨度的 CRC-32 校验和。</para>
        /// Calculates the CRC-32 checksum of the specified read-only byte span.
        /// </summary>
        /// <param name="data">
        /// <para>要计算校验和的只读字节跨度。</para>
        /// The read-only byte span to calculate the checksum for.
        /// </param>
        /// <returns>
        /// <para>计算得到的 CRC-32 校验和。</para>
        /// The calculated CRC-32 checksum.
        /// </returns>
        public static uint Calculate(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
            }
            return ~crc;
        }
#endif

        /// <summary>
        /// <para>使用之前的 CRC 值继续计算，更新 CRC-32 校验和。</para>
        /// Continues calculation using a previous CRC value, updating the CRC-32 checksum.
        /// </summary>
        /// <param name="crc">
        /// <para>之前的 CRC 中间值（未取反）。</para>
        /// The previous intermediate CRC value (not bit-inverted).
        /// </param>
        /// <param name="data">
        /// <para>包含要追加计算数据的字节数组。</para>
        /// The byte array containing the data to append.
        /// </param>
        /// <param name="offset">
        /// <para>起始偏移量（字节）。</para>
        /// The starting offset in bytes.
        /// </param>
        /// <param name="length">
        /// <para>要追加计算的字节数。</para>
        /// The number of bytes to append.
        /// </param>
        /// <returns>
        /// <para>更新后的 CRC 中间值（未取反）。</para>
        /// The updated intermediate CRC value (not bit-inverted).
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para>当 <paramref name="data"/> 为 null 时抛出。</para>
        /// Thrown when <paramref name="data"/> is null.
        /// </exception>
        public static uint Update(uint crc, byte[] data, int offset, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            for (int i = offset; i < offset + length; i++)
            {
                crc = (crc >> 8) ^ Table[((crc ^ data[i]) & 0xFF)];
            }
            return crc;
        }

        /// <summary>
        /// <para>返回 CRC-32 计算的初始值。</para>
        /// Returns the initial value for CRC-32 calculation.
        /// </summary>
        /// <returns>
        /// <para>CRC-32 的初始值 0xFFFFFFFF。</para>
        /// The initial CRC-32 value 0xFFFFFFFF.
        /// </returns>
        public static uint Start() => 0xFFFFFFFF;

        /// <summary>
        /// <para>对 CRC 中间值进行最终取反操作，得到最终的 CRC-32 校验和。</para>
        /// Performs the final bit-inversion on the intermediate CRC value to get the final CRC-32 checksum.
        /// </summary>
        /// <param name="crc">
        /// <para>CRC 中间值（未取反）。</para>
        /// The intermediate CRC value (not bit-inverted).
        /// </param>
        /// <returns>
        /// <para>取反后的最终 CRC-32 校验和。</para>
        /// The final CRC-32 checksum after bit-inversion.
        /// </returns>
        public static uint Finish(uint crc) => ~crc;
    }
}
