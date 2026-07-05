using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Infrastructure.Settings;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Settings;

[TestClass]
public sealed class WslConfigResourceLimitsStoreTests
{
    private static WslConfigResourceLimitsStore CreateSut(FakeWslConfigFileAccessor accessor) => new(accessor);

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    // ---- Read ----

    [TestMethod]
    public async Task GetAsync_FileDoesNotExist_ReturnsDefaults()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = null });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.IsTrue(limits.IsDefault);
    }

    [TestMethod]
    public async Task GetAsync_NoWsl2Section_ReturnsDefaults()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[boot]\r\nsystemd=true\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.IsTrue(limits.IsDefault);
    }

    [TestMethod]
    public async Task GetAsync_EmptyWsl2Section_ReturnsDefaults()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.IsTrue(limits.IsDefault);
    }

    [TestMethod]
    public async Task GetAsync_MemoryInGigabytes_ParsesToMegabytes()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=4GB\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(4096, limits.MemoryMegabytes);
    }

    [TestMethod]
    public async Task GetAsync_MemoryInMegabytes_ParsesValue()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=512MB\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(512, limits.MemoryMegabytes);
    }

    [TestMethod]
    public async Task GetAsync_MemoryLowercaseUnit_ParsesValue()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=8gb\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(8192, limits.MemoryMegabytes);
    }

    [TestMethod]
    public async Task GetAsync_MemoryBareNumber_TreatedAsMegabytes()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=2048\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(2048, limits.MemoryMegabytes);
    }

    [TestMethod]
    public async Task GetAsync_ProcessorsValue_ParsesValue()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nprocessors=4\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(4, limits.ProcessorCount);
    }

    [TestMethod]
    public async Task GetAsync_BothValues_ParsesBoth()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=4GB\r\nprocessors=2\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(4096, limits.MemoryMegabytes);
        Assert.AreEqual(2, limits.ProcessorCount);
    }

    [TestMethod]
    public async Task GetAsync_CaseInsensitiveSectionAndKeys_ParsesValues()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[WSL2]\r\nMEMORY=4GB\r\nPROCESSORS=2\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(4096, limits.MemoryMegabytes);
        Assert.AreEqual(2, limits.ProcessorCount);
    }

    [TestMethod]
    public async Task GetAsync_WhitespaceAroundEquals_ParsesValues()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory =  4GB\r\nprocessors =\t2\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(4096, limits.MemoryMegabytes);
        Assert.AreEqual(2, limits.ProcessorCount);
    }

    [TestMethod]
    public async Task GetAsync_DuplicateWsl2Sections_LastValueWins()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=2GB\r\n[wsl2]\r\nmemory=4GB\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(4096, limits.MemoryMegabytes);
    }

    [TestMethod]
    public async Task GetAsync_KeysOutsideWsl2Section_AreIgnored()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[boot]\r\nmemory=1GB\r\n[wsl2]\r\nprocessors=2\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.IsNull(limits.MemoryMegabytes);
        Assert.AreEqual(2, limits.ProcessorCount);
    }

    [TestMethod]
    public async Task GetAsync_CommentsAndUnrelatedKeys_AreIgnored()
    {
        // Arrange
        var content = "[wsl2]\r\n# this is a comment\r\nswap=0\r\nmemory=4GB\r\n";
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = content });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(4096, limits.MemoryMegabytes);
        Assert.IsNull(limits.ProcessorCount);
    }

    [TestMethod]
    public async Task GetAsync_EmptyMemoryValue_TreatedAsUnspecified()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=\r\n" });

        // Act
        var limits = await sut.GetAsync();

        // Assert
        Assert.IsTrue(limits.IsDefault);
    }

    [TestMethod]
    public async Task GetAsync_InvalidMemoryValue_ThrowsWslSettingsAccessException()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=notanumber\r\n" });

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<WslSettingsAccessException>(() => sut.GetAsync());
        StringAssert.Contains(ex.Message, "notanumber");
    }

    [TestMethod]
    public async Task GetAsync_MemoryValueOverflowsInt_ThrowsWslSettingsAccessException()
    {
        // 4096TB in megabytes exceeds int.MaxValue; it must be rejected, not silently wrapped to 0.
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=4096TB\r\n" });

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<WslSettingsAccessException>(() => sut.GetAsync());
        StringAssert.Contains(ex.Message, "4096TB");
    }

    [TestMethod]
    public async Task GetAsync_InvalidProcessorsValue_ThrowsWslSettingsAccessException()
    {
        // Arrange
        var sut = CreateSut(new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nprocessors=abc\r\n" });

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<WslSettingsAccessException>(() => sut.GetAsync());
        StringAssert.Contains(ex.Message, "abc");
    }

    [TestMethod]
    public async Task GetAsync_AccessorReadThrows_ThrowsWslSettingsAccessExceptionPreservingInner()
    {
        // Arrange
        var inner = new IOException("read failed");
        var sut = CreateSut(new FakeWslConfigFileAccessor { ReadException = inner });

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<WslSettingsAccessException>(() => sut.GetAsync());
        Assert.AreSame(inner, ex.InnerException);
    }

    // ---- Write ----

    [TestMethod]
    public async Task SaveAsync_NoExistingFile_WritesWsl2SectionWithValues()
    {
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = null };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(4096, 2));

        // Assert
        Assert.IsNotNull(accessor.WrittenContent);
        StringAssert.Contains(accessor.WrittenContent!, "[wsl2]");
        StringAssert.Contains(accessor.WrittenContent!, "memory=4096MB");
        StringAssert.Contains(accessor.WrittenContent!, "processors=2");
    }

    [TestMethod]
    public async Task SaveAsync_NoWsl2SectionButOtherSections_CreatesWsl2AndPreservesOthers()
    {
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = "[boot]\r\nsystemd=true\r\n" };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(2048, 1));

        // Assert
        StringAssert.Contains(accessor.WrittenContent!, "[boot]");
        StringAssert.Contains(accessor.WrittenContent!, "systemd=true");
        StringAssert.Contains(accessor.WrittenContent!, "[wsl2]");
        StringAssert.Contains(accessor.WrittenContent!, "memory=2048MB");
        StringAssert.Contains(accessor.WrittenContent!, "processors=1");
    }

    [TestMethod]
    public async Task SaveAsync_ExistingWsl2Section_UpdatesValuesWithoutDuplicating()
    {
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=1024MB\r\nprocessors=1\r\n" };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(4096, 2));

        // Assert
        StringAssert.Contains(accessor.WrittenContent!, "memory=4096MB");
        StringAssert.Contains(accessor.WrittenContent!, "processors=2");
        Assert.AreEqual(1, CountOccurrences(accessor.WrittenContent!, "memory="));
        Assert.AreEqual(1, CountOccurrences(accessor.WrittenContent!, "processors="));
    }

    [TestMethod]
    public async Task SaveAsync_PreservesCommentsAndUnrelatedSectionsAndKeys()
    {
        // Arrange
        var content = "# top comment\r\n[boot]\r\nsystemd=true\r\n\r\n[wsl2]\r\nmemory=1024MB\r\nswap=0\r\n";
        var accessor = new FakeWslConfigFileAccessor { Content = content };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(2048, null));

        // Assert
        StringAssert.Contains(accessor.WrittenContent!, "# top comment");
        StringAssert.Contains(accessor.WrittenContent!, "[boot]");
        StringAssert.Contains(accessor.WrittenContent!, "systemd=true");
        StringAssert.Contains(accessor.WrittenContent!, "swap=0");
        StringAssert.Contains(accessor.WrittenContent!, "memory=2048MB");
    }

    [TestMethod]
    public async Task SaveAsync_NullMemory_RemovesMemoryKeyButKeepsProcessors()
    {
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=4GB\r\nprocessors=2\r\n" };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(null, 4));

        // Assert
        Assert.AreEqual(0, CountOccurrences(accessor.WrittenContent!, "memory="));
        StringAssert.Contains(accessor.WrittenContent!, "processors=4");
    }

    [TestMethod]
    public async Task SaveAsync_DefaultLimits_RemovesBothKeys()
    {
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=4GB\r\nprocessors=2\r\n" };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(WslContainersDesktop.Domain.WslResourceLimits.Defaults);

        // Assert
        Assert.AreEqual(0, CountOccurrences(accessor.WrittenContent!, "memory="));
        Assert.AreEqual(0, CountOccurrences(accessor.WrittenContent!, "processors="));
    }

    [TestMethod]
    public async Task SaveAsync_DuplicateWsl2Sections_RemovesStaleKeysAndInsertsIntoFirstSectionPreservingUnrelatedKeys()
    {
        // Arrange
        var content =
            "[wsl2]\r\nkernelCommandLine=quiet\r\nmemory=1GB\r\n" +
            "[wsl2]\r\nswap=0\r\nmemory=2GB\r\nprocessors=8\r\n";
        var accessor = new FakeWslConfigFileAccessor { Content = content };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(4096, 2));

        // Assert
        var written = accessor.WrittenContent!;
        Assert.AreEqual(1, CountOccurrences(written, "memory="));
        Assert.AreEqual(1, CountOccurrences(written, "processors="));
        StringAssert.Contains(written, "memory=4096MB");
        StringAssert.Contains(written, "processors=2");
        Assert.IsFalse(written.Contains("1GB", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(written.Contains("2GB", StringComparison.OrdinalIgnoreCase));

        // Unrelated keys in both duplicate sections are preserved.
        StringAssert.Contains(written, "kernelCommandLine=quiet");
        StringAssert.Contains(written, "swap=0");

        // New resource keys are inserted into the FIRST [wsl2] section (before the second header).
        var firstSection = written.IndexOf("[wsl2]", StringComparison.OrdinalIgnoreCase);
        var secondSection = written.IndexOf("[wsl2]", firstSection + 1, StringComparison.OrdinalIgnoreCase);
        Assert.IsGreaterThanOrEqualTo(0, secondSection, "Expected both [wsl2] sections to be preserved.");
        var memoryIndex = written.IndexOf("memory=4096MB", StringComparison.Ordinal);
        Assert.IsTrue(memoryIndex < secondSection, "Expected new memory key to land in the first [wsl2] section.");
    }

    [TestMethod]
    public async Task SaveAsync_ResourceKeysInOtherSections_AreNotTouched()
    {
        // A naive global memory=/processors= remover would incorrectly strip these [boot] keys.
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = "[boot]\r\nmemory=1GB\r\nprocessors=1\r\n[wsl2]\r\nprocessors=4\r\n" };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(4096, 2));

        // Assert
        var written = accessor.WrittenContent!;
        StringAssert.Contains(written, "[boot]");
        StringAssert.Contains(written, "memory=1GB");
        StringAssert.Contains(written, "processors=1");
        StringAssert.Contains(written, "memory=4096MB");
        StringAssert.Contains(written, "processors=2");
    }

    [TestMethod]
    public async Task SaveAsync_NullProcessor_RemovesProcessorKeyButKeepsMemory()
    {
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nmemory=4GB\r\nprocessors=2\r\n" };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(2048, null));

        // Assert
        Assert.AreEqual(0, CountOccurrences(accessor.WrittenContent!, "processors="));
        StringAssert.Contains(accessor.WrittenContent!, "memory=2048MB");
    }

    [TestMethod]
    public async Task SaveAsync_MemoryAlwaysWrittenInMegabytes()
    {
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = null };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(8192, null));

        // Assert
        StringAssert.Contains(accessor.WrittenContent!, "memory=8192MB");
    }

    [TestMethod]
    public async Task SaveAsync_RoundTripFromCleanFile_GetReturnsSavedValues()
    {
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = null };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(4096, 2));
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(4096, limits.MemoryMegabytes);
        Assert.AreEqual(2, limits.ProcessorCount);
    }

    [TestMethod]
    public async Task SaveAsync_RoundTripPreservesUnrelatedKeys()
    {
        // Arrange
        var accessor = new FakeWslConfigFileAccessor { Content = "[wsl2]\r\nswap=0\r\nmemory=1024MB\r\n" };
        var sut = CreateSut(accessor);

        // Act
        await sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(4096, 2));
        var limits = await sut.GetAsync();

        // Assert
        Assert.AreEqual(4096, limits.MemoryMegabytes);
        Assert.AreEqual(2, limits.ProcessorCount);
        StringAssert.Contains(accessor.WrittenContent!, "swap=0");
    }

    [TestMethod]
    public async Task SaveAsync_AccessorWriteThrows_ThrowsWslSettingsAccessException()
    {
        // Arrange
        var inner = new IOException("write failed");
        var accessor = new FakeWslConfigFileAccessor { Content = null, WriteException = inner };
        var sut = CreateSut(accessor);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<WslSettingsAccessException>(
            () => sut.SaveAsync(new WslContainersDesktop.Domain.WslResourceLimits(4096, 2)));
        Assert.AreSame(inner, ex.InnerException);
    }
}
