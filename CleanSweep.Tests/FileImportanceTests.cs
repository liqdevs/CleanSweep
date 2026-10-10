using System.IO;
using CleanSweep.Core.Cleaning;
using Xunit;

namespace CleanSweep.Tests;

public class FileImportanceTests
{
    [Fact]
    public void ClassifiesWindowsFilesAsSystemManaged()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
        {
            return;
        }

        var assessment = FileImportanceAnalyzer.Analyze(Path.Combine(windows, "System32", "kernel32.dll"));

        Assert.Equal("red", assessment.Risk);
        Assert.Equal("preview_importance_system", assessment.DescriptionKey);
    }

    [Fact]
    public void ClassifiesUserDocumentsAsPersonalData()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            return;
        }

        var assessment = FileImportanceAnalyzer.Analyze(Path.Combine(documents, "project.bin"));

        Assert.Equal("yellow", assessment.Risk);
        Assert.Equal("preview_importance_user", assessment.DescriptionKey);
    }

    [Fact]
    public void ClassifiesCacheFilesAsLikelyRegenerable()
    {
        string path = Path.Combine(Path.GetTempPath(), "Application", "Cache", "large.bin");

        var assessment = FileImportanceAnalyzer.Analyze(path);

        Assert.Equal("green", assessment.Risk);
        Assert.Equal("preview_importance_temporary", assessment.DescriptionKey);
    }

    [Fact]
    public void TreatsUnrecognizedFilesAsUnknownInsteadOfSafe()
    {
        var assessment = FileImportanceAnalyzer.Analyze("C:\\opaque-location\\payload.bin");

        Assert.Equal("yellow", assessment.Risk);
        Assert.Equal("preview_importance_unknown", assessment.DescriptionKey);
    }

    [Theory]
    [InlineData("C:\\hiberfil.sys")]
    [InlineData("C:\\pagefile.sys")]
    [InlineData("C:\\swapfile.sys")]
    [InlineData("C:\\bootmgr")]
    public void ClassifiesWindowsBootAndPagingFilesAsSystemManaged(string path)
    {
        var assessment = FileImportanceAnalyzer.Analyze(path);

        Assert.Equal("red", assessment.Risk);
        Assert.Equal("preview_importance_system", assessment.DescriptionKey);
    }

    [Fact]
    public void ClassifiesSystemVolumeInformationAsSystemManaged()
    {
        var assessment = FileImportanceAnalyzer.Analyze("D:\\System Volume Information\\tracking.log");

        Assert.Equal("red", assessment.Risk);
        Assert.Equal("preview_importance_system", assessment.DescriptionKey);
    }
}
