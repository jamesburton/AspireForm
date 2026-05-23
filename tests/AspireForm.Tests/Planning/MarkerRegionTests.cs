using AspireForm.Planning;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class MarkerRegionTests
{
    private const string Anchor = "builder.Build().Run();";

    private const string Empty = """
        var builder = DistributedApplication.CreateBuilder(args);

        builder.Build().Run();
        """;

    [Fact]
    public void Insert_adds_a_new_region_before_the_anchor()
    {
        var result = MarkerRegion.UpsertBeforeAnchor(Empty, blockName: "sql",
            innerContent: "var sql = builder.AddSqlServer(\"sql\");", anchor: Anchor);

        result.Should().Contain("// <aspireform:block=sql>")
              .And.Contain("var sql = builder.AddSqlServer(\"sql\");")
              .And.Contain("// </aspireform:block=sql>");

        var sqlIdx = result.IndexOf("// <aspireform:block=sql>", StringComparison.Ordinal);
        var anchorIdx = result.IndexOf(Anchor, StringComparison.Ordinal);
        sqlIdx.Should().BeLessThan(anchorIdx);
    }

    [Fact]
    public void Insert_then_upsert_replaces_inner_content_without_duplicating_the_region()
    {
        var afterInsert = MarkerRegion.UpsertBeforeAnchor(Empty, "sql",
            "var sql = builder.AddSqlServer(\"sql\");", Anchor);

        var afterUpdate = MarkerRegion.UpsertBeforeAnchor(afterInsert, "sql",
            "var sql = builder.AddSqlServer(\"sql\").AddDatabase(\"appdb\");", Anchor);

        // The new content is present; the old line is gone; only one region for 'sql'.
        afterUpdate.Should().Contain("AddDatabase(\"appdb\")");
        afterUpdate.Should().NotContain("var sql = builder.AddSqlServer(\"sql\");\n");
        var matches = System.Text.RegularExpressions.Regex.Matches(
            afterUpdate, @"// <aspireform:block=sql>");
        matches.Count.Should().Be(1);
    }

    [Fact]
    public void Two_different_blocks_can_coexist_in_the_same_file()
    {
        var step1 = MarkerRegion.UpsertBeforeAnchor(Empty, "sql", "S", Anchor);
        var step2 = MarkerRegion.UpsertBeforeAnchor(step1, "data", "D", Anchor);

        step2.Should().Contain("// <aspireform:block=sql>")
             .And.Contain("// <aspireform:block=data>");
    }

    [Fact]
    public void Remove_deletes_a_region_when_present_and_is_a_noop_otherwise()
    {
        var withRegion = MarkerRegion.UpsertBeforeAnchor(Empty, "sql", "X", Anchor);
        var removed = MarkerRegion.Remove(withRegion, "sql");
        removed.Should().NotContain("aspireform:block=sql");

        var stillEmpty = MarkerRegion.Remove(Empty, "sql");
        stillEmpty.Should().Be(Empty);
    }

    [Fact]
    public void TryReadInner_returns_the_inner_content_of_an_existing_region()
    {
        var withRegion = MarkerRegion.UpsertBeforeAnchor(Empty, "sql", "abc", Anchor);
        MarkerRegion.TryReadInner(withRegion, "sql", out var inner).Should().BeTrue();
        inner.Should().Be("abc");
    }

    [Fact]
    public void Upsert_throws_when_the_anchor_is_absent_and_no_existing_region_for_the_block()
    {
        var noAnchor = "// nothing to anchor to\n";
        var act = () => MarkerRegion.UpsertBeforeAnchor(noAnchor, "sql", "X", Anchor);
        act.Should().Throw<InvalidOperationException>().WithMessage("*anchor*");
    }
}
