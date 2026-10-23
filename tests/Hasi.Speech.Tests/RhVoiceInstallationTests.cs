using Hasi.Speech.RhVoice;

namespace Hasi.Speech.Tests;

public sealed class RhVoiceInstallationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hasi-rhvoice-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>A minimal PE header: "MZ", e_lfanew at 0x3C, "PE\0\0", machine type.</summary>
    private static byte[] FakeDll(ushort machine)
    {
        var bytes = new byte[0x100];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        BitConverter.GetBytes(0x80).CopyTo(bytes, 0x3C);
        "PE\0\0"u8.ToArray().CopyTo(bytes, 0x80);
        BitConverter.GetBytes(machine).CopyTo(bytes, 0x84);
        return bytes;
    }

    private void Create(string relative, byte[]? content = null)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content ?? []);
    }

    [Theory]
    [InlineData((ushort)0x8664, BinaryArchitecture.X64)]
    [InlineData((ushort)0x014C, BinaryArchitecture.X86)]
    [InlineData((ushort)0xAA64, BinaryArchitecture.Arm64)]
    public void Reads_the_architecture_from_the_pe_header(ushort machine, BinaryArchitecture expected)
    {
        using var reader = new BinaryReader(new MemoryStream(FakeDll(machine)));

        Assert.Equal(expected, RhVoiceInstallation.ReadArchitecture(reader));
    }

    [Fact]
    public void Finds_the_dll_matching_hasi_and_the_data_folders_of_the_nvda_add_on_layout()
    {
        Create(@"engine\synthDrivers\RHVoice\lib\x86\RHVoice.dll", FakeDll(0x014C));
        Create(@"engine\synthDrivers\RHVoice\lib\x64\RHVoice.dll", FakeDll(0x8664));
        Create(@"paul\data\voice.info");
        Create(@"paul\langdata\language.info");

        var found = RhVoiceInstallation.Find(_root, out var problem);

        Assert.Null(problem);
        Assert.Equal(BinaryArchitecture.X64, found!.Architecture);
        Assert.EndsWith(@"x64\RHVoice.dll", found.DllPath);
        Assert.Equal(2, found.ResourcePaths.Count);
    }

    [Fact]
    public void Explains_when_only_a_dll_of_the_other_architecture_exists()
    {
        Create(@"lib\x86\RHVoice.dll", FakeDll(0x014C));
        Create(@"paul\data\voice.info");

        Assert.Null(RhVoiceInstallation.Find(_root, out var problem));
        Assert.Contains("no RHVoice.dll for X64", problem);
    }

    [Fact]
    public void Explains_when_the_voice_data_is_missing()
    {
        Create(@"lib\x64\RHVoice.dll", FakeDll(0x8664));

        Assert.Null(RhVoiceInstallation.Find(_root, out var problem));
        Assert.Equal("no voice data (voice.info) found", problem);
    }

    [Fact]
    public void Hasi_runs_as_a_64_bit_process()
    {
        Assert.Equal(BinaryArchitecture.X64, RhVoiceInstallation.ProcessArchitecture);
    }
}
