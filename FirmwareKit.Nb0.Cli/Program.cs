namespace FirmwareKit.Nb0.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return 1;
            }

            string command = args[0].ToLowerInvariant();

            try
            {
                return command switch
                {
                    "list" => HandleList(args),
                    "extract" => HandleExtract(args),
                    "pack" => HandlePack(args),
                    "check" => HandleCheck(args),
                    "json" => HandleJson(args),
                    "repack" => HandleRepack(args),
                    _ => HandleUnknownCommand(command)
                };
            }
            catch (Nb0Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error: {ex.Message}");
#if DEBUG
                Console.Error.WriteLine(ex.StackTrace);
#endif
                return 2;
            }
        }

        static int HandleList(string[] args)
        {
            string filePath = GetRequiredArg(args, 1, "NB0 file path");
            var metadata = Nb0Parser.Parse(filePath);

            Console.WriteLine($"File:       {filePath}");
            Console.WriteLine($"Entries:    {metadata.EntryCount}");
            Console.WriteLine($"Total Size: {metadata.TotalSize} bytes ({metadata.TotalSize / (1024.0 * 1024.0):F2} MB)");
            Console.WriteLine($"Data Start: 0x{metadata.DataSectionOffset:X8}");

            if (!string.IsNullOrEmpty(metadata.InferredFirmwareType))
                Console.WriteLine($"Type:       {metadata.InferredFirmwareType}");
            if (!string.IsNullOrEmpty(metadata.InferredDeviceModel))
                Console.WriteLine($"Device:     {metadata.InferredDeviceModel}");
            if (!string.IsNullOrEmpty(metadata.InferredVersion))
                Console.WriteLine($"Version:    {metadata.InferredVersion}");

            Console.WriteLine();
            Console.WriteLine($"{"Name",-40} {"Size",-14} {"Offset",-12} {"Type"}");
            Console.WriteLine(new string('-', 80));

            foreach (var entry in metadata.Entries)
            {
                string type = entry.InferredType ?? "";
                Console.WriteLine($"{entry.Name,-40} {entry.Size,-14} 0x{entry.Offset:X8}   {type}");
            }

            if (metadata.Warnings.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Warnings:");
                foreach (var w in metadata.Warnings)
                    Console.WriteLine($"  - {w}");
            }

            return 0;
        }

        static int HandleExtract(string[] args)
        {
            string filePath = GetRequiredArg(args, 1, "NB0 file path");
            string outputDir = GetOptionalArg(args, 2) ?? Path.GetFileNameWithoutExtension(filePath) + "_extracted";
            bool noVerify = HasFlag(args, "--no-verify");
            bool strict = HasFlag(args, "--strict");

            Console.WriteLine($"Extracting {filePath} to {outputDir}...");

            var extractor = new Nb0Extractor(new ConsoleProgressReporter());
            var options = new ExtractionOptions
            {
                VerifyMd5 = !noVerify,
                ContinueOnError = !strict,
                GenerateListFile = true
            };

            var result = extractor.Extract(filePath, outputDir, options);

            Console.WriteLine();
            Console.WriteLine($"Extracted {result.ExtractedEntries} of {result.TotalEntries} entries.");

            if (result.Warnings.Count > 0)
            {
                Console.WriteLine("Warnings:");
                foreach (var w in result.Warnings)
                    Console.WriteLine($"  - {w}");
            }

            if (result.Errors.Count > 0)
            {
                Console.WriteLine("Errors:");
                foreach (var e in result.Errors)
                    Console.WriteLine($"  - {e}");
                return 1;
            }

            return 0;
        }

        static int HandlePack(string[] args)
        {
            string inputDir = GetRequiredArg(args, 1, "Input directory");
            string outputFile = GetRequiredArg(args, 2, "Output NB0 file");

            Console.WriteLine($"Packing {inputDir} into {outputFile}...");

            Nb0Packer.PackFromDirectory(inputDir, outputFile);

            Console.WriteLine("Packing completed successfully.");
            return 0;
        }

        static int HandleCheck(string[] args)
        {
            string filePath = GetRequiredArg(args, 1, "NB0 file path");

            var processor = new Nb0Processor();
            var result = processor.Check(filePath);

            Console.WriteLine($"Checking {filePath}...");
            Console.WriteLine($"Entries:    {result.TotalEntries}");
            Console.WriteLine($"Valid:      {result.ValidEntries}");
            Console.WriteLine($"Invalid:    {result.InvalidEntries}");
            Console.WriteLine($"Has MD5:    {result.HasMd5Records}");
            Console.WriteLine();

            foreach (var entry in result.EntryResults)
                Console.WriteLine($"  {entry}");

            if (!result.IsAllValid)
            {
                Console.WriteLine();
                Console.WriteLine("Check FAILED: Some entries have checksum mismatches.");
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("Check PASSED: All entries verified successfully.");
            return 0;
        }

        static int HandleJson(string[] args)
        {
            string filePath = GetRequiredArg(args, 1, "NB0 file path");
            bool compact = HasFlag(args, "--compact");

            string json = Nb0Parser.ParseAsJson(filePath, compact);

            Console.WriteLine(json);
            return 0;
        }

        static int HandleRepack(string[] args)
        {
            string inputNb0 = GetRequiredArg(args, 1, "Input NB0 file");
            string outputNb0 = GetRequiredArg(args, 2, "Output NB0 file");

            Console.WriteLine($"Repacking {inputNb0} to {outputNb0}...");

            var processor = new Nb0Processor();
            processor.Repack(inputNb0, outputNb0);

            Console.WriteLine("Repacking completed successfully.");
            return 0;
        }

        static int HandleUnknownCommand(string command)
        {
            Console.Error.WriteLine($"Unknown command: {command}");
            PrintUsage();
            return 1;
        }

        static string GetRequiredArg(string[] args, int index, string description)
        {
            if (index >= args.Length)
            {
                Console.Error.WriteLine($"Missing argument: {description}");
                PrintUsage();
                Environment.Exit(1);
            }
            return args[index];
        }

        static string? GetOptionalArg(string[] args, int index)
        {
            if (index < args.Length && !args[index].StartsWith('-'))
                return args[index];
            return null;
        }

        static bool HasFlag(string[] args, string flag)
        {
            foreach (var arg in args)
                if (arg.Equals(flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static void PrintUsage()
        {
            Console.WriteLine("FirmwareKit.Nb0 - NB0 Firmware Format Processing Tool");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  FirmwareKit.Nb0.Cli list <nb0_file>");
            Console.WriteLine("    List all entries in the NB0 file with metadata.");
            Console.WriteLine();
            Console.WriteLine("  FirmwareKit.Nb0.Cli extract <nb0_file> [output_dir] [--no-verify] [--strict]");
            Console.WriteLine("    Extract all entries from the NB0 file.");
            Console.WriteLine("    --no-verify  Skip MD5 checksum verification");
            Console.WriteLine("    --strict     Stop on first error (default: continue)");
            Console.WriteLine();
            Console.WriteLine("  FirmwareKit.Nb0.Cli pack <input_dir> <output.nb0>");
            Console.WriteLine("    Pack files from a directory into an NB0 file.");
            Console.WriteLine();
            Console.WriteLine("  FirmwareKit.Nb0.Cli check <nb0_file>");
            Console.WriteLine("    Verify MD5 checksums of all entries in the NB0 file.");
            Console.WriteLine();
            Console.WriteLine("  FirmwareKit.Nb0.Cli json <nb0_file> [--compact]");
            Console.WriteLine("    Output metadata as JSON.");
            Console.WriteLine("    --compact  Output compact JSON without indentation");
            Console.WriteLine();
            Console.WriteLine("  FirmwareKit.Nb0.Cli repack <input.nb0> <output.nb0>");
            Console.WriteLine("    Repack an NB0 file (extract and re-pack).");
        }
    }
}
