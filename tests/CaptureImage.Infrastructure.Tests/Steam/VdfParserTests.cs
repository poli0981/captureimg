using CaptureImage.Infrastructure.Steam;
using FluentAssertions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Steam;

public class VdfParserTests
{
    [Fact]
    public void Parse_SingleLeaf_ReturnsLeafNode()
    {
        var node = VdfParser.Parse("\"key\" \"value\"");

        node.Key.Should().Be("key");
        node.IsLeaf.Should().BeTrue();
        node.Value.Should().Be("value");
    }

    [Fact]
    public void Parse_SingleEmptyBranch_ReturnsBranchNode()
    {
        var node = VdfParser.Parse("\"root\" { }");

        node.Key.Should().Be("root");
        node.IsLeaf.Should().BeFalse();
        node.Children.Should().BeEmpty();
    }

    [Fact]
    public void Parse_BranchWithLeaves_ExtractsValues()
    {
        const string input = """
            "root"
            {
                "first"  "1"
                "second" "2"
            }
            """;

        var node = VdfParser.Parse(input);

        node.Children.Should().HaveCount(2);
        node.ValueOf("first").Should().Be("1");
        node.ValueOf("second").Should().Be("2");
    }

    [Fact]
    public void Parse_NestedBranches_AreAccessibleViaIndexer()
    {
        const string input = """
            "root"
            {
                "outer"
                {
                    "inner"
                    {
                        "deep" "xyz"
                    }
                }
            }
            """;

        var node = VdfParser.Parse(input);

        node["outer"]!["inner"]!["deep"]!.Value.Should().Be("xyz");
    }

    [Fact]
    public void Parse_EscapeSequencesInStrings_AreUnescaped()
    {
        const string input = "\"root\" { \"path\" \"C:\\\\Program Files (x86)\\\\Steam\" }";

        var node = VdfParser.Parse(input);

        node.ValueOf("path").Should().Be(@"C:\Program Files (x86)\Steam");
    }

    [Fact]
    public void Parse_LineComments_AreIgnored()
    {
        const string input = """
            "root"
            {
                // this is a comment
                "key" "value" // trailing comment
            }
            """;

        var node = VdfParser.Parse(input);

        node.ValueOf("key").Should().Be("value");
    }

    [Fact]
    public void Parse_RealLibraryFoldersSnippet_ExtractsEveryLibraryPath()
    {
        // Verbatim snippet of a real Steam libraryfolders.vdf (two libraries, with apps).
        const string input = """
            "libraryfolders"
            {
                "contentstatsid"    "3169058392974474741"
                "0"
                {
                    "path"    "C:\\Program Files (x86)\\Steam"
                    "label"   ""
                    "contentid"    "3169058392974474741"
                    "totalsize"    "0"
                    "apps"
                    {
                        "228980"    "375500000"
                        "250820"    "3244853555"
                    }
                }
                "1"
                {
                    "path"    "D:\\SteamLibrary"
                    "label"   ""
                    "contentid"    "1234567890123456789"
                    "totalsize"    "500000000000"
                    "apps"
                    {
                        "570"    "100000000000"
                    }
                }
            }
            """;

        var node = VdfParser.Parse(input);

        node.Key.Should().Be("libraryfolders");
        node["0"]!.ValueOf("path").Should().Be(@"C:\Program Files (x86)\Steam");
        node["1"]!.ValueOf("path").Should().Be(@"D:\SteamLibrary");
        node["0"]!["apps"]!.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_RealAppManifestSnippet_ExtractsFields()
    {
        const string input = """
            "AppState"
            {
                "appid"    "570"
                "Universe" "1"
                "name"     "Dota 2"
                "StateFlags" "4"
                "installdir" "dota 2 beta"
                "LastUpdated" "1743112489"
            }
            """;

        var node = VdfParser.Parse(input);

        node.Key.Should().Be("AppState");
        node.ValueOf("appid").Should().Be("570");
        node.ValueOf("name").Should().Be("Dota 2");
        node.ValueOf("installdir").Should().Be("dota 2 beta");
    }

    [Fact]
    public void Parse_ConditionalBlockAfterValue_IsTolerated()
    {
        const string input = """
            "root"
            {
                "key" "value" [$WIN32]
                "other" "data"
            }
            """;

        var node = VdfParser.Parse(input);

        node.ValueOf("key").Should().Be("value");
        node.ValueOf("other").Should().Be("data");
    }

    [Fact]
    public void Parse_DuplicateKeys_LastOneWins()
    {
        const string input = """
            "root"
            {
                "key" "first"
                "key" "second"
            }
            """;

        var node = VdfParser.Parse(input);

        node.ValueOf("key").Should().Be("second");
    }

    [Fact]
    public void Parse_EmptyDocument_Throws()
    {
        var act = () => VdfParser.Parse("   ");

        act.Should().Throw<VdfParseException>();
    }

    [Fact]
    public void Parse_UnterminatedBranch_Throws()
    {
        var act = () => VdfParser.Parse("\"root\" { \"key\" \"value\"");

        act.Should().Throw<VdfParseException>();
    }

    [Fact]
    public void Parse_UnterminatedString_Throws()
    {
        var act = () => VdfParser.Parse("\"key\" \"unterminated");

        act.Should().Throw<VdfParseException>();
    }

    [Fact]
    public void BranchChildren_IgnoresLeafChildren()
    {
        const string input = """
            "libraryfolders"
            {
                "contentstatsid" "12345"
                "0" { "path" "C:\\Steam" }
                "1" { "path" "D:\\Steam2" }
            }
            """;

        var node = VdfParser.Parse(input);

        var branches = System.Linq.Enumerable.ToList(node.BranchChildren());
        branches.Should().HaveCount(2);
        branches[0].ValueOf("path").Should().Be(@"C:\Steam");
        branches[1].ValueOf("path").Should().Be(@"D:\Steam2");
    }
}
