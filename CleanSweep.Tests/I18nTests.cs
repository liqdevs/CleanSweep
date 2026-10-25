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
        Assert.False(categories["app_caches"].Selected);
        Assert.False(categories["developer_caches"].Selected);
    }

    [Fact]
    public void TestNewCacheCategoriesHaveTranslationsInEveryLanguage()
    {
        foreach (string language in I18n.Languages)
        {
            I18n.SetLanguage(language);

            Assert.NotEqual("cat_app_caches_name", I18n.Tr("cat_app_caches_name"));
            Assert.NotEqual("cat_app_caches_desc", I18n.Tr("cat_app_caches_desc"));
            Assert.NotEqual("cat_developer_caches_name", I18n.Tr("cat_developer_caches_name"));
            Assert.NotEqual("cat_developer_caches_desc", I18n.Tr("cat_developer_caches_desc"));
        }
    }

    [Fact]
    public void MaintenanceToolsHaveTranslationsInEveryLanguage()
    {
        foreach (string language in I18n.Languages)
        {
            I18n.SetLanguage(language);

            Assert.NotEqual("maintenance_title", I18n.Tr("maintenance_title"));
            Assert.NotEqual("maintenance_duplicates", I18n.Tr("maintenance_duplicates"));
            Assert.NotEqual("maintenance_empty_folders", I18n.Tr("maintenance_empty_folders"));
        }
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
