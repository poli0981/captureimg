// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text;
using AwesomeAssertions;
using CaptureImage.Infrastructure.Steam;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Steam;

/// <summary>
/// <see cref="VdfParser"/>: Valve KeyValues text as Steam writes it, the tolerated extras
/// (comments, conditionals, escapes) and the malformed or hostile inputs it must reject with
/// <see cref="VdfParseException"/> instead of crashing.
/// </summary>
public class VdfParserTests
{
    [Fact]
    public void Parse_SingleLeaf_ReturnsALeafNode()
    {
        var node = VdfParser.Parse("\"key\" \"value\"");

        node.Key.Should().Be("key");
        node.IsLeaf.Should().BeTrue();
        node.Value.Should().Be("value");
        node.Children.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyBranch_ReturnsABranchNode()
    {
        var node = VdfParser.Parse("\"root\" { }");

        node.Key.Should().Be("root");
        node.IsLeaf.Should().BeFalse();
        node.Value.Should().BeNull();
        node.Children.Should().BeEmpty();
    }

    [Fact]
    public void Parse_BranchWithLeaves_ExposesTheirValues()
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
        node.ValueOf("missing").Should().BeNull();
        node["missing"].Should().BeNull();
    }

    [Fact]
    public void Parse_NestedBranches_AreReachableThroughTheIndexer()
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

        Child(Child(node, "outer"), "inner").ValueOf("deep").Should().Be("xyz");
        node.ValueOf("outer").Should().BeNull("a branch has no scalar value");
    }

    [Fact]
    public void Parse_KeysAreCaseInsensitive()
    {
        var node = VdfParser.Parse("\"AppState\" { \"InstallDir\" \"Game\" }");

        node.ValueOf("installdir").Should().Be("Game");
        node.ValueOf("INSTALLDIR").Should().Be("Game");
    }

    [Fact]
    public void Parse_TabsAndWindowsLineEndings_AreWhitespace()
    {
        var node = VdfParser.Parse("\"AppState\"\r\n{\r\n\t\"appid\"\t\t\"570\"\r\n\t\"name\"\t\t\"Dota 2\"\r\n}\r\n");

        node.ValueOf("appid").Should().Be("570");
        node.ValueOf("name").Should().Be("Dota 2");
    }

    [Fact]
    public void Parse_EscapeSequences_AreUnescaped()
    {
        const string input = "\"root\" { \"path\" \"C:\\\\Program Files (x86)\\\\Steam\" \"quote\" \"say \\\"hi\\\"\" \"controls\" \"a\\nb\\tc\\rd\" \"unknown\" \"\\q\" }";

        var node = VdfParser.Parse(input);

        node.ValueOf("path").Should().Be(@"C:\Program Files (x86)\Steam");
        node.ValueOf("quote").Should().Be("say \"hi\"");
        node.ValueOf("controls").Should().Be("a\nb\tc\rd");
        node.ValueOf("unknown").Should().Be("q", "an unknown escape keeps the escaped character");
    }

    [Fact]
    public void Parse_LineComments_AreIgnored()
    {
        const string input = """
            // header comment
            "root"
            {
                // this is a comment
                "key" "value" // trailing comment
                "url" "https://example.com/a//b"
            }
            // footer
            """;

        var node = VdfParser.Parse(input);

        node.ValueOf("key").Should().Be("value");
        node.ValueOf("url").Should().Be("https://example.com/a//b", "slashes inside a string are not a comment");
    }

    [Fact]
    public void Parse_ConditionalBlocksAfterValues_AreTolerated()
    {
        const string input = """
            "root"
            {
                "key" "value" [$WIN32]
                "other" "data" [$OSX||$LINUX]
                "last" "x"
            }
            """;

        var node = VdfParser.Parse(input);

        node.ValueOf("key").Should().Be("value");
        node.ValueOf("other").Should().Be("data");
        node.ValueOf("last").Should().Be("x");
    }

    [Fact]
    public void Parse_DuplicateKeys_LastOneWins()
    {
        var node = VdfParser.Parse("\"root\" { \"key\" \"first\" \"KEY\" \"second\" }");

        node.Children.Should().ContainSingle();
        node.ValueOf("key").Should().Be("second");
    }

    [Fact]
    public void Parse_SeveralTopLevelNodes_AreWrappedInASyntheticRoot()
    {
        var node = VdfParser.Parse("\"first\" { \"a\" \"1\" } \"second\" \"2\"");

        node.Key.Should().Be("<root>");
        node.Children.Keys.Should().BeEquivalentTo("first", "second");
        Child(node, "first").ValueOf("a").Should().Be("1");
        node.ValueOf("second").Should().Be("2");
    }

    [Fact]
    public void Parse_EmptyStrings_AreValidKeysAndValues()
    {
        var node = VdfParser.Parse("\"root\" { \"label\" \"\" \"\" \"anonymous\" }");

        node.ValueOf("label").Should().BeEmpty();
        node.ValueOf(string.Empty).Should().Be("anonymous");
    }

    [Fact]
    public void Parse_RealLibraryFoldersFile_ExposesEveryLibrary()
    {
        const string input = """
            "libraryfolders"
            {
                "contentstatsid"        "3169058392974474741"
                "0"
                {
                    "path"        "C:\\Program Files (x86)\\Steam"
                    "label"        ""
                    "contentid"        "3169058392974474741"
                    "totalsize"        "0"
                    "update_clean_bytes_tally"        "7812290713"
                    "time_last_update_verified"        "1743090106"
                    "apps"
                    {
                        "228980"        "375500000"
                        "250820"        "3244853555"
                    }
                }
                "1"
                {
                    "path"        "D:\\SteamLibrary"
                    "label"        ""
                    "contentid"        "1234567890123456789"
                    "totalsize"        "500000000000"
                    "apps"
                    {
                        "570"        "100000000000"
                    }
                }
            }
            """;

        var node = VdfParser.Parse(input);

        node.Key.Should().Be("libraryfolders");
        Child(node, "0").ValueOf("path").Should().Be(@"C:\Program Files (x86)\Steam");
        Child(node, "1").ValueOf("path").Should().Be(@"D:\SteamLibrary");
        Child(Child(node, "0"), "apps").Children.Should().HaveCount(2);
        node.BranchChildren().Select(b => b.Key).Should().BeEquivalentTo("0", "1");
    }

    [Fact]
    public void Parse_RealAppManifest_ExposesItsFields()
    {
        const string input = """
            "AppState"
            {
                "appid"        "570"
                "Universe"        "1"
                "name"        "Dota 2"
                "StateFlags"        "4"
                "installdir"        "dota 2 beta"
                "LastUpdated"        "1743112489"
                "UserConfig"
                {
                    "language"        "english"
                }
            }
            """;

        var node = VdfParser.Parse(input);

        node.Key.Should().Be("AppState");
        node.ValueOf("appid").Should().Be("570");
        node.ValueOf("name").Should().Be("Dota 2");
        node.ValueOf("installdir").Should().Be("dota 2 beta");
        Child(node, "UserConfig").ValueOf("language").Should().Be("english");
    }

    [Fact]
    public void BranchChildren_SkipsLeaves()
    {
        const string input = """
            "libraryfolders"
            {
                "contentstatsid" "12345"
                "0" { "path" "C:\\Steam" }
                "1" { "path" "D:\\Steam2" }
            }
            """;

        var branches = VdfParser.Parse(input).BranchChildren().ToList();

        branches.Should().HaveCount(2);
        branches.Select(b => b.ValueOf("path")).Should().BeEquivalentTo(@"C:\Steam", @"D:\Steam2");
    }

    [Fact]
    public void Parse_NestingAtTheDepthLimit_IsAccepted()
    {
        // 64 nested branches put the innermost leaf at depth 64, the deepest level allowed.
        var node = VdfParser.Parse(Nested(64));

        var current = node;
        for (var i = 1; i < 64; i++)
        {
            current = Child(current, "k" + i);
        }
        current.ValueOf("leaf").Should().Be("bottom");
    }

    [Fact]
    public void Parse_NestingBeyondTheDepthLimit_IsRejected()
    {
        var act = () => VdfParser.Parse(Nested(65));

        act.Should().Throw<VdfParseException>().WithMessage("*64*");
    }

    [Fact]
    public void Parse_HostileDeeplyNestedInput_ThrowsInsteadOfOverflowingTheStack()
    {
        var hostile = new StringBuilder().Insert(0, "\"k\" { ", 100_000).ToString();

        var act = () => VdfParser.Parse(hostile);

        act.Should().Throw<VdfParseException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n\t ")]
    [InlineData("// only a comment")]
    [InlineData("\"root\" { \"key\" \"value\"")]
    [InlineData("\"key\" \"unterminated")]
    [InlineData("\"key\"")]
    [InlineData("\"key\" value")]
    [InlineData("key \"value\"")]
    [InlineData("}")]
    [InlineData("\"root\" { \"key\" }")]
    [InlineData("\"root\" { { } }")]
    [InlineData("\"key\" \"value\" junk")]
    [InlineData("\"escape at the end\\")]
    public void Parse_MalformedInput_ThrowsVdfParseException(string input)
    {
        var act = () => VdfParser.Parse(input);

        act.Should().Throw<VdfParseException>()
            .Which.Position.Should().BeInRange(0, input.Length);
    }

    [Fact]
    public void VdfParseException_IsAFormatException()
    {
        // SteamLibraryScanner catches FormatException, so the parser's exception must be one.
        var act = () => VdfParser.Parse("\"key\"");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNull()
    {
        var act = () => VdfParser.Parse(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static VdfNode Child(VdfNode node, string key)
    {
        var child = node[key];
        child.Should().NotBeNull("'{0}' should have a child '{1}'", node.Key, key);
        return child;
    }

    /// <summary><paramref name="levels"/> nested branches <c>"k0" { "k1" { … { "leaf" "bottom" } … } }</c>.</summary>
    private static string Nested(int levels)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < levels; i++)
        {
            sb.Append("\"k").Append(i).Append("\" { ");
        }
        sb.Append("\"leaf\" \"bottom\" ");
        sb.Append('}', levels);
        return sb.ToString();
    }
}
