using System.Text.RegularExpressions;
using TypeForge.Core.Branding;
using Xunit;

namespace TypeForge.Tests.Branding;

public sealed class EmptyStateCatalogTests
{
    private static readonly int[] RequiredScales = [100, 200];

    [Fact]
    public void Every_kind_has_content()
    {
        foreach (EmptyStateKind kind in Enum.GetValues<EmptyStateKind>())
        {
            EmptyStateContent content = EmptyStateCatalog.Get(kind);
            Assert.Equal(kind, content.Kind);
            Assert.False(string.IsNullOrWhiteSpace(content.Title));
            Assert.False(string.IsNullOrWhiteSpace(content.Message));
        }
    }

    [Fact]
    public void Unknown_kind_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EmptyStateCatalog.Get((EmptyStateKind)999));
    }

    [Fact]
    public void Asset_stems_are_unique_kebab_case()
    {
        IReadOnlyCollection<EmptyStateContent> all = EmptyStateCatalog.All;
        Assert.Equal(all.Count, all.Select(c => c.AssetStem).Distinct().Count());
        Assert.All(all, c => Assert.Matches(new Regex("^[a-z0-9]+(-[a-z0-9]+)*$"), c.AssetStem));
    }

    [Fact]
    public void Copy_has_no_exclamation_marks()
    {
        Assert.All(EmptyStateCatalog.All, c =>
        {
            Assert.DoesNotContain('!', c.Title);
            Assert.DoesNotContain('!', c.Message);
        });
    }

    [Fact]
    public void Illustration_files_exist_for_required_scales()
    {
        string? root = FindRepoRoot();
        Assert.NotNull(root);
        string dir = Path.Combine(root!, "src", "TypeForge.App", "Assets", "Illustrations");

        foreach (EmptyStateContent c in EmptyStateCatalog.All)
        {
            foreach (int scale in RequiredScales)
            {
                string file = Path.Combine(dir, $"{c.AssetStem}.scale-{scale}.png");
                Assert.True(File.Exists(file), $"Missing asset: {file}");
            }
        }
    }

    private static string? FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TypeForge.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName;
    }
}
