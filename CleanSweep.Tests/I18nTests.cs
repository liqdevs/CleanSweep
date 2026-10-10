using System.Linq;
using CleanSweep.Core.Localization;
using CleanSweep.Core.Scanning;
using Xunit;

namespace CleanSweep.Tests;

public class I18nTests
{
    public I18nTests()
    {
        I18n.ResetLanguage();
    }

    [Fact]
    public void TestDefaultLanguageIsEnglish()
    {
        Assert.Equal("en", I18n.CurrentLanguage);
        Assert.Contains("en", I18n.Languages);
    }

    [Fact]
    public void TestTranslationChangesWithSelectedLanguage()
    {
        I18n.SetLanguage("uk");
        Assert.Equal("Знайти сміття", I18n.Tr("scan_button"));

        I18n.SetLanguage("ru");
        Assert.Equal("Найти мусор", I18n.Tr("scan_button"));
    }

    [Fact]
    public void TestRiskyCategoriesAreDisabledByDefault()
    {
        var categories = CategoryScanner.BuildCategories().ToDictionary(c => c.Key);

        Assert.False(categories["recycle_bin"].Selected);
        Assert.False(categories["recent_items"].Selected);
        Assert.False(categories["incomplete_downloads"].Selected);
        Assert.False(categories["memory_dumps"].Selected);
    }

    [Fact]
    public void TestPluralCategoryWord()
    {
        Assert.Equal("category", I18n.PluralCategoryWord("en", 1));
        Assert.Equal("categories", I18n.PluralCategoryWord("en", 2));

        Assert.Equal("категория", I18n.PluralCategoryWord("ru", 1));
        Assert.Equal("категории", I18n.PluralCategoryWord("ru", 2));
        Assert.Equal("категорий", I18n.PluralCategoryWord("ru", 5));
        Assert.Equal("категорий", I18n.PluralCategoryWord("ru", 11));
        Assert.Equal("категория", I18n.PluralCategoryWord("ru", 21));
    }

    [Fact]
    public void TestLanguageNormalizationHandlesRegionalCodes()
    {
        Assert.Equal("ru", I18n.NormalizeLanguageCode("ru-RU"));
        Assert.Equal("uk", I18n.NormalizeLanguageCode("UK-UA"));
        Assert.Equal("en", I18n.NormalizeLanguageCode("   "));

        I18n.SetLanguage("fr-FR");
        Assert.Equal("en", I18n.CurrentLanguage);

        I18n.SetLanguage("uk-UA");
        Assert.Equal("Знайти сміття", I18n.Tr("scan_button"));
    }
}

