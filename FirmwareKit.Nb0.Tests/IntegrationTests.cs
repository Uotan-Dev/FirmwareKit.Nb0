using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FirmwareKit.Nb0.Tests
{
    public class IntegrationTests
    {
        /// <summary>
        /// Creates an NB0 file on disk with the specified entries and an optional .md5 entry
        /// containing MD5 verification records for all non-.md5 entries.
        /// The MD5 record Offset field uses absolute file offsets (from the beginning of the file).
        /// </summary>
        private static string CreateNb0FileWithMd5(string outputDir, bool withCorrectMd5, params (string Name, byte[] Data)[] files)
        {
            string nb0Path = Path.Combine(outputDir, "test_with_md5.nb0");

            var md5Hashes = new byte[files.Length][];
            for (int i = 0; i < files.Length; i++)
            {
                using var md5 = MD5.Create();
                md5Hashes[i] = md5.ComputeHash(files[i].Data);
            }

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
                        byte[] wrongMd5 = new byte[16];
                        wrongMd5[0] = 0xFF;
                        md5Writer.Write(wrongMd5);
                    }
                }

                md5EntryData = md5Ms.ToArray();
            }

            using (var fs = new FileStream(nb0Path, FileMode.Create))
            using (var writer = new BinaryWriter(fs, Encoding.ASCII))
            {
                writer.Write((uint)totalEntries);

                currentDataOffset = 0;
                var headers = new Nb0EntryHeader[totalEntries];

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

                headers[files.Length] = new Nb0EntryHeader
                {
                    Name = files[0].Name + ".md5",
                    DataOffset = currentDataOffset,
                    FileSize = md5EntryData.Length
                };

                for (int i = 0; i < totalEntries; i++)
                    headers[i].Write(writer);

                for (int i = 0; i < files.Length; i++)
                {
                    writer.Write(files[i].Data);
                    long padding = (4 - (files[i].Data.Length % 4)) % 4;
                    for (long p = 0; p < padding; p++)
                        writer.Write((byte)0);
                }

                writer.Write(md5EntryData);
                long md5Padding = (4 - (md5EntryData.Length % 4)) % 4;
                for (long p = 0; p < md5Padding; p++)
                    writer.Write((byte)0);
            }

            return nb0Path;
        }

        [Fact]
        public void PackThenExtract_RoundTrip_PreservesData()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_integ_{Guid.NewGuid():N}");
            string nb0File = Path.Combine(outputDir, "test.nb0");
            string extractDir = Path.Combine(outputDir, "extracted");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data1 = Encoding.ASCII.GetBytes("First file content");
                byte[] data2 = Encoding.ASCII.GetBytes("Second file content with more data");
                byte[] data3 = new byte[1000];
                for (int i = 0; i < data3.Length; i++) data3[i] = (byte)(i & 0xFF);

                Nb0Packer.CreateBuilder()
                    .AddEntry("file1.txt", data1)
                    .AddEntry("file2.txt", data2)
                    .AddEntry("binary.dat", data3)
                    .PackTo(nb0File);

                Assert.True(File.Exists(nb0File));

                var metadata = Nb0Parser.Parse(nb0File);
                Assert.Equal(3, metadata.EntryCount);

                var extractor = new Nb0Extractor();
                var result = extractor.Extract(nb0File, extractDir, new ExtractionOptions
                {
                    VerifyMd5 = false,
                    ContinueOnError = true
                });

                Assert.True(result.IsSuccess);
                Assert.Equal(3, result.ExtractedEntries);

                Assert.Equal(data1, File.ReadAllBytes(Path.Combine(extractDir, "file1.txt")));
                Assert.Equal(data2, File.ReadAllBytes(Path.Combine(extractDir, "file2.txt")));
                Assert.Equal(data3, File.ReadAllBytes(Path.Combine(extractDir, "binary.dat")));
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void PackFromDirThenExtract_RoundTrip_PreservesData()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_integ_dir_{Guid.NewGuid():N}");
            string inputDir = Path.Combine(workDir, "input");
            string extractDir = Path.Combine(workDir, "extracted");
            string nb0File = Path.Combine(workDir, "output.nb0");

            try
            {
                Directory.CreateDirectory(inputDir);

                File.WriteAllBytes(Path.Combine(inputDir, "a.bin"), Encoding.ASCII.GetBytes("File A"));
                File.WriteAllBytes(Path.Combine(inputDir, "b.bin"), Encoding.ASCII.GetBytes("File B"));

                Nb0Packer.PackFromDirectory(inputDir, nb0File);

                Assert.True(File.Exists(nb0File));

                var extractor = new Nb0Extractor();
                var result = extractor.Extract(nb0File, extractDir, new ExtractionOptions
                {
                    VerifyMd5 = false,
                    ContinueOnError = true
                });

                Assert.True(result.IsSuccess);
                Assert.Equal("File A", File.ReadAllText(Path.Combine(extractDir, "a.bin")));
                Assert.Equal("File B", File.ReadAllText(Path.Combine(extractDir, "b.bin")));
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }

        [Fact]
        public void Check_AfterPack_WithMd5_AllChecksumsPass()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_integ_check_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data1 = Encoding.ASCII.GetBytes("Check test 1");
                byte[] data2 = Encoding.ASCII.GetBytes("Check test 2");

                string nb0File = CreateNb0FileWithMd5(outputDir, true, ("data1.bin", data1), ("data2.bin", data2));

                var processor = new Nb0Processor();
                var checkResult = processor.Check(nb0File);

                Assert.True(checkResult.IsAllValid);
                Assert.True(checkResult.HasMd5Records);
                Assert.Equal(3, checkResult.TotalEntries); // 2 data + 1 .md5
                Assert.Equal(2, checkResult.ValidEntries);
                Assert.Equal(0, checkResult.InvalidEntries);
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void Check_WithoutMd5_AllSkipped()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_integ_nomd5_{Guid.NewGuid():N}");
            string nb0File = Path.Combine(outputDir, "no_md5.nb0");

            try
            {
                Directory.CreateDirectory(outputDir);

                Nb0Packer.CreateBuilder()
                    .AddEntry("data1.bin", Encoding.ASCII.GetBytes("No MD5 data 1"))
                    .AddEntry("data2.bin", Encoding.ASCII.GetBytes("No MD5 data 2"))
                    .PackTo(nb0File);

                var processor = new Nb0Processor();
                var checkResult = processor.Check(nb0File);

                Assert.False(checkResult.HasMd5Records);
                Assert.True(checkResult.IsAllValid);
                Assert.Equal(0, checkResult.ValidEntries);
                Assert.Equal(0, checkResult.InvalidEntries);

                foreach (var entryResult in checkResult.EntryResults)
                {
                    Assert.False(entryResult.HasMd5Check);
                }
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void FullWorkflow_ParseExtractCheckPack()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_workflow_{Guid.NewGuid():N}");
            string nb0File1 = Path.Combine(workDir, "original.nb0");
            string extractDir = Path.Combine(workDir, "extracted");
            string nb0File2 = Path.Combine(workDir, "repacked.nb0");

            try
            {
                Directory.CreateDirectory(workDir);

                byte[] bootloaderData = Encoding.ASCII.GetBytes("Bootloader data");
                byte[] modemData = Encoding.ASCII.GetBytes("Modem firmware data");
                byte[] nvitemData = Encoding.ASCII.GetBytes("NV item data");

                Nb0Packer.CreateBuilder()
                    .AddEntry("bootloader", bootloaderData)
                    .AddEntry("modem", modemData)
                    .AddEntry("nvitem.bin", nvitemData)
                    .PackTo(nb0File1);

                // Parse
                var metadata = Nb0Parser.Parse(nb0File1);
                Assert.Equal(3, metadata.EntryCount);
                Assert.Equal("Spreadtrum/UNISOC", metadata.InferredFirmwareType);

                // Extract
                var extractor = new Nb0Extractor();
                var extractResult = extractor.Extract(nb0File1, extractDir, new ExtractionOptions
                {
                    VerifyMd5 = false,
                    ContinueOnError = true,
                    GenerateListFile = false
                });
                Assert.True(extractResult.IsSuccess);

                // Verify extracted data
                Assert.Equal(bootloaderData, File.ReadAllBytes(Path.Combine(extractDir, "bootloader")));
                Assert.Equal(modemData, File.ReadAllBytes(Path.Combine(extractDir, "modem")));
                Assert.Equal(nvitemData, File.ReadAllBytes(Path.Combine(extractDir, "nvitem.bin")));

                // Check (no MD5 records in packed file)
                var processor = new Nb0Processor();
                var checkResult = processor.Check(nb0File1);
                Assert.True(checkResult.IsAllValid);
                Assert.False(checkResult.HasMd5Records);

                // Repack from extracted directory
                Nb0Packer.PackFromDirectory(extractDir, nb0File2);
                Assert.True(File.Exists(nb0File2));

                // Verify repacked file has same structure
                var metadata2 = Nb0Parser.Parse(nb0File2);
                Assert.Equal(metadata.EntryCount, metadata2.EntryCount);

                for (int i = 0; i < metadata.EntryCount; i++)
                {
                    Assert.Equal(metadata.Entries[i].Name, metadata2.Entries[i].Name);
                    Assert.Equal(metadata.Entries[i].Size, metadata2.Entries[i].Size);
                }

                // Verify repacked data matches original
                var extractDir2 = Path.Combine(workDir, "extracted2");
                var extractResult2 = extractor.Extract(nb0File2, extractDir2, new ExtractionOptions
                {
                    VerifyMd5 = false,
                    ContinueOnError = true
                });
                Assert.True(extractResult2.IsSuccess);

                Assert.Equal(bootloaderData, File.ReadAllBytes(Path.Combine(extractDir2, "bootloader")));
                Assert.Equal(modemData, File.ReadAllBytes(Path.Combine(extractDir2, "modem")));
                Assert.Equal(nvitemData, File.ReadAllBytes(Path.Combine(extractDir2, "nvitem.bin")));
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }

        [Fact]
        public void FilePathApis_WorkCorrectly()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_filepath_{Guid.NewGuid():N}");
            string nb0File = Path.Combine(workDir, "test.nb0");
            string extractDir = Path.Combine(workDir, "extracted");

            try
            {
                Directory.CreateDirectory(workDir);

                byte[] data = Encoding.ASCII.GetBytes("File path API test data");
                Nb0Packer.CreateBuilder()
                    .AddEntry("test.bin", data)
                    .PackTo(nb0File);

                // Parse using file path
                var metadata = Nb0Parser.Parse(nb0File);
                Assert.Equal(1, metadata.EntryCount);
                Assert.Equal("test.bin", metadata.Entries[0].Name);

                // Extract using file path
                var extractor = new Nb0Extractor();
                var result = extractor.Extract(nb0File, extractDir, new ExtractionOptions { VerifyMd5 = false });
                Assert.True(result.IsSuccess);

                // Verify extracted data
                Assert.Equal(data, File.ReadAllBytes(Path.Combine(extractDir, "test.bin")));
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }

        [Fact]
        public void PackerBuilder_AddFile_WorksCorrectly()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_addfile_{Guid.NewGuid():N}");
            string sourceFile = Path.Combine(workDir, "source.bin");
            string nb0File = Path.Combine(workDir, "output.nb0");
            string extractDir = Path.Combine(workDir, "extracted");

            try
            {
                Directory.CreateDirectory(workDir);
                byte[] data = Encoding.ASCII.GetBytes("AddFile test data");
                File.WriteAllBytes(sourceFile, data);

                Nb0Packer.CreateBuilder()
                    .AddFile(sourceFile)
                    .PackTo(nb0File);

                var metadata = Nb0Parser.Parse(nb0File);
                Assert.Equal(1, metadata.EntryCount);
                Assert.Equal("source.bin", metadata.Entries[0].Name);

                var extractor = new Nb0Extractor();
                extractor.Extract(nb0File, extractDir, new ExtractionOptions { VerifyMd5 = false });
                Assert.Equal(data, File.ReadAllBytes(Path.Combine(extractDir, "source.bin")));
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }

        [Fact]
        public void PackerBuilder_AddDirectory_WorksCorrectly()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_adddir_{Guid.NewGuid():N}");
            string inputDir = Path.Combine(workDir, "input");
            string nb0File = Path.Combine(workDir, "output.nb0");
            string extractDir = Path.Combine(workDir, "extracted");

            try
            {
                Directory.CreateDirectory(inputDir);
                File.WriteAllBytes(Path.Combine(inputDir, "a.bin"), Encoding.ASCII.GetBytes("File A"));
                File.WriteAllBytes(Path.Combine(inputDir, "b.bin"), Encoding.ASCII.GetBytes("File B"));

                Nb0Packer.CreateBuilder()
                    .AddDirectory(inputDir)
                    .PackTo(nb0File);

                var metadata = Nb0Parser.Parse(nb0File);
                Assert.Equal(2, metadata.EntryCount);

                var extractor = new Nb0Extractor();
                var result = extractor.Extract(nb0File, extractDir, new ExtractionOptions { VerifyMd5 = false });
                Assert.True(result.IsSuccess);
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }

        [Fact]
        public void Extract_ContinueOnErrorFalse_Md5Mismatch_Throws()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_md5throw_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data = Encoding.ASCII.GetBytes("Test data");
                string nb0File = CreateNb0FileWithMd5(outputDir, false, ("test.bin", data));

                var extractor = new Nb0Extractor();
                Assert.Throws<Nb0Md5MismatchException>(() =>
                    extractor.Extract(nb0File, Path.Combine(outputDir, "out"), new ExtractionOptions
                    {
                        VerifyMd5 = true,
                        ContinueOnError = false
                    }));
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void PackThenExtract_AlignmentBoundary_PreservesData()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_align_{Guid.NewGuid():N}");
            string nb0File = Path.Combine(workDir, "aligned.nb0");
            string extractDir = Path.Combine(workDir, "extracted");

            try
            {
                Directory.CreateDirectory(workDir);

                // Data that is exactly 4-byte aligned
                byte[] alignedData = new byte[100]; // 100 % 4 == 0
                for (int i = 0; i < alignedData.Length; i++) alignedData[i] = (byte)(i & 0xFF);

                // Data that is NOT 4-byte aligned (1 byte off)
                byte[] unalignedData = new byte[101]; // 101 % 4 == 1
                for (int i = 0; i < unalignedData.Length; i++) unalignedData[i] = (byte)(i & 0xFF);

                // Data that is 3 bytes off from alignment
                byte[] unalignedData2 = new byte[103]; // 103 % 4 == 3
                for (int i = 0; i < unalignedData2.Length; i++) unalignedData2[i] = (byte)(i & 0xFF);

                Nb0Packer.CreateBuilder()
                    .AddEntry("aligned.bin", alignedData)
                    .AddEntry("unaligned1.bin", unalignedData)
                    .AddEntry("unaligned2.bin", unalignedData2)
                    .PackTo(nb0File);

                var extractor = new Nb0Extractor();
                var result = extractor.Extract(nb0File, extractDir, new ExtractionOptions { VerifyMd5 = false });
                Assert.True(result.IsSuccess);
                Assert.Equal(3, result.ExtractedEntries);

                Assert.Equal(alignedData, File.ReadAllBytes(Path.Combine(extractDir, "aligned.bin")));
                Assert.Equal(unalignedData, File.ReadAllBytes(Path.Combine(extractDir, "unaligned1.bin")));
                Assert.Equal(unalignedData2, File.ReadAllBytes(Path.Combine(extractDir, "unaligned2.bin")));
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }

        [Fact]
        public void PackThenExtract_ZeroEntries_RoundTrip()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_empty_{Guid.NewGuid():N}");
            string nb0File = Path.Combine(workDir, "empty.nb0");

            try
            {
                Directory.CreateDirectory(workDir);

                // Pack with no entries
                var packer = new Nb0Packer();
                packer.Pack(nb0File, Array.Empty<Nb0Entry>());

                // Parse
                var metadata = Nb0Parser.Parse(nb0File);
                Assert.Equal(0, metadata.EntryCount);
                Assert.Empty(metadata.Entries);
                Assert.True(metadata.TotalSize > 0);
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }

        [Fact]
        public async Task RepackAsync_PreservesData()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_repackasync_{Guid.NewGuid():N}");
            string nb0File1 = Path.Combine(workDir, "original.nb0");
            string nb0File2 = Path.Combine(workDir, "repacked.nb0");
            string extractDir1 = Path.Combine(workDir, "extracted1");
            string extractDir2 = Path.Combine(workDir, "extracted2");

            try
            {
                Directory.CreateDirectory(workDir);

                byte[] data1 = Encoding.ASCII.GetBytes("Async repack data 1");
                byte[] data2 = Encoding.ASCII.GetBytes("Async repack data 2");

                Nb0Packer.CreateBuilder()
                    .AddEntry("file1.txt", data1)
                    .AddEntry("file2.txt", data2)
                    .PackTo(nb0File1);

                var processor = new Nb0Processor();
                await processor.RepackAsync(nb0File1, nb0File2, TestContext.Current.CancellationToken);

                var extractor = new Nb0Extractor();
                extractor.Extract(nb0File1, extractDir1, new ExtractionOptions { VerifyMd5 = false });
                extractor.Extract(nb0File2, extractDir2, new ExtractionOptions { VerifyMd5 = false });

                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(extractDir1, "file1.txt")),
                    File.ReadAllBytes(Path.Combine(extractDir2, "file1.txt")));
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(extractDir1, "file2.txt")),
                    File.ReadAllBytes(Path.Combine(extractDir2, "file2.txt")));
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }

        [Fact]
        public async Task PackFromDirectoryAsync_WorksCorrectly()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_packdirasync_{Guid.NewGuid():N}");
            string inputDir = Path.Combine(workDir, "input");
            string nb0File = Path.Combine(workDir, "output.nb0");
            string extractDir = Path.Combine(workDir, "extracted");

            try
            {
                Directory.CreateDirectory(inputDir);
                File.WriteAllBytes(Path.Combine(inputDir, "a.bin"), Encoding.ASCII.GetBytes("Async dir A"));
                File.WriteAllBytes(Path.Combine(inputDir, "b.bin"), Encoding.ASCII.GetBytes("Async dir B"));

                await Nb0Packer.PackFromDirectoryAsync(inputDir, nb0File, TestContext.Current.CancellationToken);

                Assert.True(File.Exists(nb0File));

                var metadata = Nb0Parser.Parse(nb0File);
                Assert.Equal(2, metadata.EntryCount);

                var extractor = new Nb0Extractor();
                var result = extractor.Extract(nb0File, extractDir, new ExtractionOptions { VerifyMd5 = false });
                Assert.True(result.IsSuccess);
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }

        [Fact]
        public void Extract_WithMd5_AbsoluteOffsets_VerificationPasses()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_md5abs_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data1 = new byte[100];
                for (int i = 0; i < data1.Length; i++) data1[i] = (byte)(i & 0xFF);
                byte[] data2 = Encoding.ASCII.GetBytes("Second entry data for absolute offset test");

                string nb0File = CreateNb0FileWithMd5(outputDir, true, ("entry1.bin", data1), ("entry2.bin", data2));

                // Verify MD5 records use absolute file offsets
                var metadata = Nb0Parser.Parse(nb0File);
                Assert.True(metadata.HasMd5Records);
                Assert.Equal(2, metadata.Md5Records.Count);

                // The MD5 record Offset should equal the absolute file offset of the entry data
                Assert.Equal((uint)(metadata.DataSectionOffset + metadata.Entries[0].Offset), metadata.Md5Records[0].Offset);
                Assert.Equal((uint)(metadata.DataSectionOffset + metadata.Entries[1].Offset), metadata.Md5Records[1].Offset);

                // Extract with MD5 verification - should pass
                var extractor = new Nb0Extractor();
                var result = extractor.Extract(nb0File, Path.Combine(outputDir, "out"), new ExtractionOptions
                {
                    VerifyMd5 = true,
                    ContinueOnError = false
                });

                Assert.True(result.IsSuccess, $"Extraction should succeed with correct MD5. Errors: {string.Join(", ", result.Errors)}");
                Assert.Equal(3, result.ExtractedEntries);
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void Extract_WithMd5_CorruptedData_DetectsMismatch()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_md5corrupt_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data1 = new byte[100];
                for (int i = 0; i < data1.Length; i++) data1[i] = (byte)(i & 0xFF);
                byte[] data2 = Encoding.ASCII.GetBytes("Second entry data for corruption test");

                string nb0File = CreateNb0FileWithMd5(outputDir, true, ("entry1.bin", data1), ("entry2.bin", data2));

                // Corrupt the first entry's data in the file
                byte[] fileBytes = File.ReadAllBytes(nb0File);
                var metadata = Nb0Parser.Parse(nb0File);
                long corruptOffset = metadata.DataSectionOffset + metadata.Entries[0].Offset;
                fileBytes[corruptOffset] ^= 0xFF; // Flip bits in first byte
                File.WriteAllBytes(nb0File, fileBytes);

                // Extract with MD5 verification - should detect mismatch
                var extractor = new Nb0Extractor();
                var result = extractor.Extract(nb0File, Path.Combine(outputDir, "out"), new ExtractionOptions
                {
                    VerifyMd5 = true,
                    ContinueOnError = true
                });

                Assert.True(result.FailedEntries > 0, "MD5 mismatch should be detected for corrupted entry");
                Assert.Contains(result.Errors, e => e.Contains("MD5 verification failed"));
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void Check_WithMd5_AbsoluteOffsets_AllChecksumsPass()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_checkabs_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data1 = new byte[200];
                for (int i = 0; i < data1.Length; i++) data1[i] = (byte)(i & 0xFF);
                byte[] data2 = Encoding.ASCII.GetBytes("Check absolute offset test data");

                string nb0File = CreateNb0FileWithMd5(outputDir, true, ("data1.bin", data1), ("data2.bin", data2));

                var processor = new Nb0Processor();
                var checkResult = processor.Check(nb0File);

                Assert.True(checkResult.IsAllValid, $"All entries should pass MD5 check. Invalid: {checkResult.InvalidEntries}");
                Assert.True(checkResult.HasMd5Records);
                Assert.Equal(3, checkResult.TotalEntries); // 2 data + 1 .md5
                Assert.Equal(2, checkResult.ValidEntries);
                Assert.Equal(0, checkResult.InvalidEntries);
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void Check_WithMd5_CorruptedData_DetectsMismatch()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_checkcorrupt_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data1 = new byte[100];
                for (int i = 0; i < data1.Length; i++) data1[i] = (byte)(i & 0xFF);
                byte[] data2 = Encoding.ASCII.GetBytes("Check corruption test data");

                string nb0File = CreateNb0FileWithMd5(outputDir, true, ("entry1.bin", data1), ("entry2.bin", data2));

                // Corrupt the second entry's data in the file
                byte[] fileBytes = File.ReadAllBytes(nb0File);
                var metadata = Nb0Parser.Parse(nb0File);
                long corruptOffset = metadata.DataSectionOffset + metadata.Entries[1].Offset;
                fileBytes[corruptOffset] ^= 0xFF;
                File.WriteAllBytes(nb0File, fileBytes);

                var processor = new Nb0Processor();
                var checkResult = processor.Check(nb0File);

                Assert.False(checkResult.IsAllValid, "Corrupted entry should fail MD5 check");
                Assert.Equal(1, checkResult.InvalidEntries);
                Assert.Equal(1, checkResult.ValidEntries);
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void Extract_WithMd5_Md5EntryIsSkippedForVerification()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_md5skip_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data1 = Encoding.ASCII.GetBytes("Data entry 1 for md5 skip test");
                byte[] data2 = Encoding.ASCII.GetBytes("Data entry 2 for md5 skip test");

                string nb0File = CreateNb0FileWithMd5(outputDir, true, ("data1.bin", data1), ("data2.bin", data2));

                // Extract with MD5 verification enabled - .md5 entry should be extracted but NOT MD5-verified
                var extractor = new Nb0Extractor();
                var result = extractor.Extract(nb0File, Path.Combine(outputDir, "out"), new ExtractionOptions
                {
                    VerifyMd5 = true,
                    ContinueOnError = false
                });

                Assert.True(result.IsSuccess, $"Extraction should succeed. Errors: {string.Join(", ", result.Errors)}");
                Assert.Equal(3, result.ExtractedEntries); // 2 data + 1 .md5
                Assert.Equal(0, result.FailedEntries);

                // The .md5 file should be extracted
                Assert.True(File.Exists(Path.Combine(outputDir, "out", "data1.bin.md5")),
                    "The .md5 entry should be extracted to disk");
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void Check_WithMd5_Md5EntryIsSkippedForVerification()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_checkmd5skip_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data1 = Encoding.ASCII.GetBytes("Check md5 skip test data 1");
                byte[] data2 = Encoding.ASCII.GetBytes("Check md5 skip test data 2");

                string nb0File = CreateNb0FileWithMd5(outputDir, true, ("data1.bin", data1), ("data2.bin", data2));

                var processor = new Nb0Processor();
                var checkResult = processor.Check(nb0File);

                Assert.True(checkResult.IsAllValid, $"All entries should pass. Invalid: {checkResult.InvalidEntries}");
                Assert.True(checkResult.HasMd5Records);
                Assert.Equal(3, checkResult.TotalEntries); // 2 data + 1 .md5

                // The .md5 entry should have HasMd5Check = false (skipped) and IsValid = true
                var md5EntryResult = checkResult.EntryResults.Find(e => e.Name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(md5EntryResult);
                Assert.False(md5EntryResult.HasMd5Check, "The .md5 entry should NOT be MD5-verified");
                Assert.True(md5EntryResult.IsValid, "The .md5 entry should be marked as valid (skipped)");

                // Non-.md5 entries should be verified
                Assert.Equal(2, checkResult.ValidEntries);
                Assert.Equal(0, checkResult.InvalidEntries);
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void Md5AbsoluteOffset_RoundTrip_PreservesData()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_md5rt_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                byte[] data1 = new byte[256];
                for (int i = 0; i < data1.Length; i++) data1[i] = (byte)(i & 0xFF);
                byte[] data2 = Encoding.ASCII.GetBytes("MD5 round-trip verification data for entry 2");
                byte[] data3 = new byte[77]; // unaligned size
                for (int i = 0; i < data3.Length; i++) data3[i] = (byte)((i * 7 + 3) & 0xFF);

                string nb0File = CreateNb0FileWithMd5(outputDir, true,
                    ("entry_a.bin", data1), ("entry_b.bin", data2), ("entry_c.bin", data3));

                // Parse and verify MD5 records contain correct absolute offsets
                var metadata = Nb0Parser.Parse(nb0File);
                Assert.True(metadata.HasMd5Records);
                Assert.Equal(3, metadata.Md5Records.Count);

                for (int i = 0; i < 3; i++)
                {
                    uint expectedAbsoluteOffset = (uint)(metadata.DataSectionOffset + metadata.Entries[i].Offset);
                    Assert.Equal(expectedAbsoluteOffset, metadata.Md5Records[i].Offset);
                    Assert.Equal((uint)metadata.Entries[i].Size, metadata.Md5Records[i].Length);
                }

                // Extract with MD5 verification - all entries should pass
                var extractor = new Nb0Extractor();
                var extractDir = Path.Combine(outputDir, "extracted");
                var result = extractor.Extract(nb0File, extractDir, new ExtractionOptions
                {
                    VerifyMd5 = true,
                    ContinueOnError = false
                });

                Assert.True(result.IsSuccess, $"Extraction should succeed. Errors: {string.Join(", ", result.Errors)}");
                Assert.Equal(4, result.ExtractedEntries); // 3 data + 1 .md5
                Assert.Equal(0, result.FailedEntries);

                // Verify extracted data matches original
                Assert.Equal(data1, File.ReadAllBytes(Path.Combine(extractDir, "entry_a.bin")));
                Assert.Equal(data2, File.ReadAllBytes(Path.Combine(extractDir, "entry_b.bin")));
                Assert.Equal(data3, File.ReadAllBytes(Path.Combine(extractDir, "entry_c.bin")));
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void EntryCountPreValidation_TooSmallStream_ThrowsCorruptedException()
        {
            // Create a stream that declares 10 entries but only has 4 bytes (the count itself)
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write((uint)10); // Declare 10 entries
            }
            stream.Position = 0;

            Assert.Throws<Nb0CorruptedException>(() => Nb0Parser.ParseFromStream(stream));
        }

        [Fact]
        public void NonAsciiName_RejectedByPacker()
        {
            byte[] data = Encoding.ASCII.GetBytes("Test data for non-ASCII name");

            Assert.Throws<ArgumentException>(() =>
                Nb0Packer.CreateBuilder()
                    .AddEntry("测试文件.bin", data));
        }

        [Fact]
        public void NonMonotonicOffset_ProducesWarning()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), $"nb0_nonmonotonic_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(outputDir);

                string nb0Path = Path.Combine(outputDir, "nonmonotonic.nb0");

                // Manually craft an NB0 stream with non-monotonic offsets
                byte[] data1 = Encoding.ASCII.GetBytes("First entry data");
                byte[] data2 = Encoding.ASCII.GetBytes("Second entry data");

                using (var fs = new FileStream(nb0Path, FileMode.Create))
                using (var writer = new BinaryWriter(fs, Encoding.ASCII))
                {
                    writer.Write((uint)2); // 2 entries

                    // First entry: offset = 100 (large offset)
                    var header1 = new Nb0EntryHeader
                    {
                        Name = "entry1.bin",
                        DataOffset = 100,
                        FileSize = data1.Length
                    };
                    header1.Write(writer);

                    // Second entry: offset = 0 (smaller than first - non-monotonic)
                    var header2 = new Nb0EntryHeader
                    {
                        Name = "entry2.bin",
                        DataOffset = 0,
                        FileSize = data2.Length
                    };
                    header2.Write(writer);

                    // Write data at the declared offsets
                    long dataSectionStart = 4 + 2 * Nb0EntryHeader.StructSize;

                    // Write entry2 data at offset 0 (relative to data section)
                    fs.Seek(dataSectionStart + 0, SeekOrigin.Begin);
                    writer.Write(data2);
                    long padding2 = (4 - (data2.Length % 4)) % 4;
                    for (long p = 0; p < padding2; p++) writer.Write((byte)0);

                    // Write entry1 data at offset 100 (relative to data section)
                    fs.Seek(dataSectionStart + 100, SeekOrigin.Begin);
                    writer.Write(data1);
                    long padding1 = (4 - (data1.Length % 4)) % 4;
                    for (long p = 0; p < padding1; p++) writer.Write((byte)0);
                }

                var metadata = Nb0Parser.Parse(nb0Path);

                // Verify that a non-monotonic offset warning is present
                Assert.Contains(metadata.Warnings, w => w.Contains("offset") && w.Contains("less than"));
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void FullRoundTrip_WithAllFeatures_WorksEndToEnd()
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"nb0_fullrt_{Guid.NewGuid():N}");
            string extractDir1 = Path.Combine(workDir, "extracted1");
            string nb0File2 = Path.Combine(workDir, "repacked.nb0");
            string extractDir2 = Path.Combine(workDir, "extracted2");

            try
            {
                Directory.CreateDirectory(workDir);

                byte[] bootloaderData = Encoding.ASCII.GetBytes("Bootloader firmware data");
                byte[] modemData = new byte[500];
                for (int i = 0; i < modemData.Length; i++) modemData[i] = (byte)(i & 0xFF);
                byte[] nvitemData = Encoding.ASCII.GetBytes("NV item configuration data");

                // Step 1: Pack with MD5 records using absolute offsets
                string nb0File1 = CreateNb0FileWithMd5(workDir, true,
                    ("bootloader", bootloaderData),
                    ("modem", modemData),
                    ("nvitem.bin", nvitemData));

                // Step 2: Parse and verify metadata
                var metadata = Nb0Parser.Parse(nb0File1);
                Assert.Equal(4, metadata.EntryCount); // 3 data + 1 .md5
                Assert.True(metadata.HasMd5Records);
                Assert.Equal(3, metadata.Md5Records.Count);

                // Verify MD5 record offsets are absolute
                for (int i = 0; i < 3; i++)
                {
                    uint expectedAbsoluteOffset = (uint)(metadata.DataSectionOffset + metadata.Entries[i].Offset);
                    Assert.Equal(expectedAbsoluteOffset, metadata.Md5Records[i].Offset);
                }

                // Step 3: Check (MD5 verification)
                var processor = new Nb0Processor();
                var checkResult = processor.Check(nb0File1);
                Assert.True(checkResult.IsAllValid, $"All entries should pass MD5 check. Invalid: {checkResult.InvalidEntries}");
                Assert.True(checkResult.HasMd5Records);
                Assert.Equal(3, checkResult.ValidEntries);
                Assert.Equal(0, checkResult.InvalidEntries);

                // Step 4: Extract with MD5 verification
                var extractor = new Nb0Extractor();
                var extractResult = extractor.Extract(nb0File1, extractDir1, new ExtractionOptions
                {
                    VerifyMd5 = true,
                    ContinueOnError = false
                });
                Assert.True(extractResult.IsSuccess, $"Extraction should succeed. Errors: {string.Join(", ", extractResult.Errors)}");
                Assert.Equal(4, extractResult.ExtractedEntries);

                // Verify extracted data
                Assert.Equal(bootloaderData, File.ReadAllBytes(Path.Combine(extractDir1, "bootloader")));
                Assert.Equal(modemData, File.ReadAllBytes(Path.Combine(extractDir1, "modem")));
                Assert.Equal(nvitemData, File.ReadAllBytes(Path.Combine(extractDir1, "nvitem.bin")));

                // Step 5: Repack from extracted directory (excluding .md5 file)
                var extractedFiles = Directory.GetFiles(extractDir1)
                    .Where(f => !f.EndsWith(".md5", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                var builder = Nb0Packer.CreateBuilder();
                foreach (string filePath in extractedFiles)
                {
                    builder.AddFile(filePath);
                }
                builder.PackTo(nb0File2);

                // Step 6: Verify repacked file
                var metadata2 = Nb0Parser.Parse(nb0File2);
                Assert.Equal(extractedFiles.Length, metadata2.EntryCount);

                // Step 7: Extract repacked file and verify data integrity
                var extractResult2 = extractor.Extract(nb0File2, extractDir2, new ExtractionOptions
                {
                    VerifyMd5 = false,
                    ContinueOnError = true
                });
                Assert.True(extractResult2.IsSuccess);

                Assert.Equal(bootloaderData, File.ReadAllBytes(Path.Combine(extractDir2, "bootloader")));
                Assert.Equal(modemData, File.ReadAllBytes(Path.Combine(extractDir2, "modem")));
                Assert.Equal(nvitemData, File.ReadAllBytes(Path.Combine(extractDir2, "nvitem.bin")));
            }
            finally
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
        }
    }
}
