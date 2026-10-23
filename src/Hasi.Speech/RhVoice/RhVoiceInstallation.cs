namespace Hasi.Speech.RhVoice;

public enum BinaryArchitecture
{
    Unknown,
    X86,
    X64,
    Arm64,
}

/// <summary>
/// An RHVoice installation found under the folder given with <c>--rhvoice-path</c>: a RHVoice.dll matching
/// HASI's architecture, plus the voice and language data folders (those containing voice.info or language.info).
/// The layout of the official NVDA add-ons works as is (lib/x64/RHVoice.dll, data/, langdata/).
/// </summary>
public sealed record RhVoiceInstallation(string DllPath, BinaryArchitecture Architecture, IReadOnlyList<string> ResourcePaths)
{
    public static BinaryArchitecture ProcessArchitecture => Environment.Is64BitProcess
        ? System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64
            ? BinaryArchitecture.Arm64
            : BinaryArchitecture.X64
        : BinaryArchitecture.X86;

    /// <summary>Finds the installation, or explains in <paramref name="problem"/> why none is usable.</summary>
    public static RhVoiceInstallation? Find(string root, out string? problem)
    {
        problem = null;
        if (!Directory.Exists(root))
        {
            problem = $"folder not found: {root}";
            return null;
        }

        var dlls = Directory.GetFiles(root, "RHVoice.dll", SearchOption.AllDirectories)
            .Select(path => (Path: path, Architecture: ReadArchitecture(path)))
            .ToList();
        var match = dlls.FirstOrDefault(d => d.Architecture == ProcessArchitecture);
        if (match.Path is null)
        {
            problem = dlls.Count == 0
                ? "no RHVoice.dll found"
                : $"no RHVoice.dll for {ProcessArchitecture}; found: {string.Join(", ", dlls.Select(d => $"{d.Architecture} {d.Path}"))}";
            return null;
        }

        var resources = Directory.GetFiles(root, "*.info", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) is "voice.info" or "language.info")
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!resources.Any(r => File.Exists(Path.Combine(r, "voice.info"))))
        {
            problem = "no voice data (voice.info) found";
            return null;
        }
        return new RhVoiceInstallation(match.Path, match.Architecture, resources);
    }

    /// <summary>Reads the machine type from the PE header of a DLL.</summary>
    public static BinaryArchitecture ReadArchitecture(string path)
    {
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            return ReadArchitecture(reader);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            return BinaryArchitecture.Unknown;
        }
    }

    public static BinaryArchitecture ReadArchitecture(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.ReadUInt16() != 0x5A4D) return BinaryArchitecture.Unknown; // "MZ"
        reader.BaseStream.Position = 0x3C;
        reader.BaseStream.Position = reader.ReadInt32();
        if (reader.ReadUInt32() != 0x00004550) return BinaryArchitecture.Unknown; // "PE\0\0"
        return reader.ReadUInt16() switch
        {
            0x014C => BinaryArchitecture.X86,
            0x8664 => BinaryArchitecture.X64,
            0xAA64 => BinaryArchitecture.Arm64,
            _ => BinaryArchitecture.Unknown,
        };
    }
}
