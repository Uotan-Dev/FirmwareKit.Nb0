using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FirmwareKit.Nb0.Tests
{
    public class Nb0PackerTests
    {
        [Fact]
        public void PackToStream_SingleEntry_ProducesValidNb0()
        {
            byte[] data = Encoding.ASCII.GetBytes("Pack test data");
            var entry = new Nb0Entry("test.img", data);
            var packer = new Nb0Packer();

            using var ms = new MemoryStream();
            packer.PackToStream(ms, new[] { entry });

            ms.Position = 0;
            var metadata = Nb0Parser.ParseFromStream(ms);
            Assert.Equal(1, metadata.EntryCount);
            Assert.Equal("test.img", metadata.Entries[0].Name);
        }

        [Fact]
        public void PackToStream_MultipleEntries_AllPresent()
        {
            var entries = new[]
            {
                new Nb0Entry("boot.img", Encoding.ASCII.GetBytes("boot data")),
                new Nb0Entry("recovery.img", Encoding.ASCII.GetBytes("recovery data")),
                new Nb0Entry("system.img", Encoding.ASCII.GetBytes("system data")),
            };

            var packer = new Nb0Packer();
            using var ms = new MemoryStream();
            packer.PackToStream(ms, entries);

            ms.Position = 0;
            var metadata = Nb0Parser.ParseFromStream(ms);
            Assert.Equal(3, metadata.EntryCount);
            Assert.Equal("boot.img", metadata.Entries[0].Name);
            Assert.Equal("recovery.img", metadata.Entries[1].Name);
            Assert.Equal("system.img", metadata.Entries[2].Name);
        }

        [Fact]
        public void PackThenExtract_Roundtrip_PreservesData()
        {
            byte[] originalData = new byte[2048];
            for (int i = 0; i < originalData.Length; i++) originalData[i] = (byte)(i % 256);

            var entry = new Nb0Entry("roundtrip.img", originalData);
            var packer = new Nb0Packer();

            using var ms = new MemoryStream();
            packer.PackToStream(ms, new[] { entry });

            ms.Position = 0;
            var metadata = Nb0Parser.ParseFromStream(ms);
            ms.Position = 0;

            var extractor = new Nb0Extractor();
            byte[] extracted = extractor.ExtractEntryToBuffer(ms, metadata.Entries[0]);

            Assert.Equal(originalData, extracted);
        }

        [Fact]
        public void PackThenExtract_MultipleEntriesRoundtrip_AllDataPreserved()
        {
            var data1 = Encoding.ASCII.GetBytes("First partition data");
            var data2 = Encoding.ASCII.GetBytes("Second partition data - longer content here");
            var data3 = new byte[512];
            for (int i = 0; i < data3.Length; i++) data3[i] = (byte)(i % 128);

            var entries = new[]
            {
                new Nb0Entry("first.img", data1),
                new Nb0Entry("second.img", data2),
                new Nb0Entry("third.img", data3),
            };

            var packer = new Nb0Packer();
            using var ms = new MemoryStream();
            packer.PackToStream(ms, entries);

            ms.Position = 0;
            var metadata = Nb0Parser.ParseFromStream(ms);
            ms.Position = 0;

            var extractor = new Nb0Extractor();
            Assert.Equal(data1, extractor.ExtractEntryToBuffer(ms, metadata.Entries[0]));
            Assert.Equal(data2, extractor.ExtractEntryToBuffer(ms, metadata.Entries[1]));
            Assert.Equal(data3, extractor.ExtractEntryToBuffer(ms, metadata.Entries[2]));
        }

        [Fact]
        public void PackToStream_NullStream_ThrowsArgumentNullException()
        {
            var packer = new Nb0Packer();
            Assert.Throws<ArgumentNullException>(() => packer.PackToStream(null!, new Nb0Entry[0]));
        }

        [Fact]
        public void PackToStream_NullEntries_ThrowsArgumentNullException()
        {
            var packer = new Nb0Packer();
            using var ms = new MemoryStream();
            Assert.Throws<ArgumentNullException>(() => packer.PackToStream(ms, null!));
        }

        [Fact]
        public async Task PackToStreamAsync_SameResultAsSync()
        {
            byte[] data = Encoding.ASCII.GetBytes("Async pack test");
            var entry = new Nb0Entry("async_test.img", data);
            var packer = new Nb0Packer();

            using var ms = new MemoryStream();
            await packer.PackToStreamAsync(ms, new[] { entry }, TestContext.Current.CancellationToken);

            ms.Position = 0;
            var metadata = Nb0Parser.ParseFromStream(ms);
            Assert.Equal(1, metadata.EntryCount);
            Assert.Equal("async_test.img", metadata.Entries[0].Name);
        }

        [Fact]
        public async Task PackToStreamAsync_Cancellation_ThrowsOperationCanceledException()
        {
            byte[] data = new byte[1024 * 1024];
            var entry = new Nb0Entry("large.img", data);
            var packer = new Nb0Packer();

            using var ms = new MemoryStream();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                packer.PackToStreamAsync(ms, new[] { entry }, cts.Token));
        }

        [Fact]
        public void CreateBuilder_AddEntry_ProducesValidNb0()
        {
            byte[] data = Encoding.ASCII.GetBytes("Builder test data");

            using var ms = new MemoryStream();
            Nb0Packer.CreateBuilder()
                .AddEntry("built.img", data)
                .PackToStream(ms);

            ms.Position = 0;
            var metadata = Nb0Parser.ParseFromStream(ms);
            Assert.Equal(1, metadata.EntryCount);
            Assert.Equal("built.img", metadata.Entries[0].Name);
        }

        [Fact]
        public void CreateBuilder_AddEntry_NullName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Nb0Packer.CreateBuilder().AddEntry(null!, new byte[10]));
        }

        [Fact]
        public void CreateBuilder_AddEntry_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Nb0Packer.CreateBuilder().AddEntry("test.img", null!));
        }

        [Fact]
        public void Pack_EmptyEntryList_ProducesZeroEntryNb0()
        {
            var packer = new Nb0Packer();
            using var ms = new MemoryStream();
            packer.PackToStream(ms, Array.Empty<Nb0Entry>());

            ms.Position = 0;
            var metadata = Nb0Parser.ParseFromStream(ms);
            Assert.Equal(0, metadata.EntryCount);
        }

        [Fact]
        public void PackFromDirectory_PacksAllFiles()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "Nb0PackerTest_" + Guid.NewGuid().ToString("N"));
            string outputDir = Path.Combine(Path.GetTempPath(), "Nb0PackerOutput_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(outputDir);
                File.WriteAllBytes(Path.Combine(tempDir, "boot.img"), Encoding.ASCII.GetBytes("boot data"));
                File.WriteAllBytes(Path.Combine(tempDir, "system.img"), Encoding.ASCII.GetBytes("system data"));

                string outputPath = Path.Combine(outputDir, "output.nb0");
                Nb0Packer.PackFromDirectory(tempDir, outputPath);

                Assert.True(File.Exists(outputPath));

                var metadata = Nb0Parser.Parse(outputPath);
                Assert.Equal(2, metadata.EntryCount);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
                try { Directory.Delete(outputDir, true); } catch { }
            }
        }

        [Fact]
        public void PackToStream_LargeData_ProducesValidNb0()
        {
            byte[] data = new byte[8192];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 256);

            var entry = new Nb0Entry("large.img", data);
            var packer = new Nb0Packer();

            using var ms = new MemoryStream();
            packer.PackToStream(ms, new[] { entry });

            ms.Position = 0;
            var metadata = Nb0Parser.ParseFromStream(ms);
            Assert.Equal(1, metadata.EntryCount);
            Assert.Equal("large.img", metadata.Entries[0].Name);
            Assert.Equal(data.Length, metadata.Entries[0].Size);

            ms.Position = 0;
            var extractor = new Nb0Extractor();
            byte[] extracted = extractor.ExtractEntryToBuffer(ms, metadata.Entries[0]);
            Assert.Equal(data, extracted);
        }

        [Fact]
        public void AddEntry_NameTooLong_ThrowsArgumentException()
        {
            string longName = new string('x', 48);
            var builder = Nb0Packer.CreateBuilder();
            Assert.Throws<ArgumentException>(() => builder.AddEntry(longName, new byte[] { 0x01 }));
        }
    }
}
