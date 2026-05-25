# FirmwareKit.Nb0

NB0 firmware format parser, extractor, packer and validator for Spreadtrum/UNISOC devices.

## Installation

```bash
dotnet add package FirmwareKit.Nb0
```

## Features

- Parse NB0 firmware file metadata
- Extract entries from NB0 files with MD5 verification
- Pack entries into NB0 format files
- Validate MD5 checksums
- Streaming file operations for low memory footprint
- Async/await support with cancellation tokens
- .NET Standard 2.0+ and .NET 8.0+ compatibility
- AOT-compatible (NET 8.0+)

## Quick Start

### Parse

```csharp
var metadata = Nb0Parser.Parse("firmware.nb0");
Console.WriteLine($"Entries: {metadata.EntryCount}");
foreach (var entry in metadata.Entries)
{
    Console.WriteLine($"  {entry.Name}: {entry.Size} bytes [{entry.InferredType}]");
}
```

### Extract

```csharp
var extractor = new Nb0Extractor();
var result = extractor.Extract("firmware.nb0", "output_dir");
Console.WriteLine($"Extracted: {result.ExtractedEntries}/{result.TotalEntries}");
```

### Pack

```csharp
var packer = new Nb0Packer();
packer.Pack("output.nb0", entries);

// Or use the builder API
Nb0Packer.CreateBuilder()
    .AddEntry("bootloader", bootData)
    .AddFileStream("modem", "modem.img")
    .PackTo("output.nb0");
```

### Verify

```csharp
var processor = new Nb0Processor();
var checkResult = processor.Check("firmware.nb0");
Console.WriteLine($"Valid: {checkResult.IsAllValid}");
```

## Supported Targets

- .NET 10.0
- .NET 9.0
- .NET 8.0
- .NET Standard 2.1
- .NET Standard 2.0

## License

MIT
