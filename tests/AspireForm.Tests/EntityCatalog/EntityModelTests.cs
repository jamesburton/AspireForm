using AspireForm.EntityCatalog;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EntityCatalog;

public sealed class EntityModelTests
{
    [Fact]
    public void MutationResult_Ok_marks_success_and_empty_diagnostics_by_default()
    {
        var r = MutationResult.Ok(["a.cs", "b.cs"]);
        r.Success.Should().BeTrue();
        r.ChangedFiles.Should().BeEquivalentTo(new[] { "a.cs", "b.cs" });
        r.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MutationResult_Fail_marks_failure_with_error_diagnostic()
    {
        var r = MutationResult.Fail("boom", "x.cs");
        r.Success.Should().BeFalse();
        r.ChangedFiles.Should().BeEmpty();
        r.Diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be("error");
        r.Diagnostics[0].Message.Should().Be("boom");
        r.Diagnostics[0].FilePath.Should().Be("x.cs");
    }

    [Fact]
    public void AttributeInstance_holds_constructor_and_named_args()
    {
        var attr = new AttributeInstance(
            FullTypeName: "AspireForm.Annotations.DabPermissionAttribute",
            ConstructorArgs: ["anonymous", "read"],
            NamedArgs: new Dictionary<string, object?>());
        attr.ConstructorArgs.Should().HaveCount(2);
        attr.ConstructorArgs[0].Should().Be("anonymous");
        attr.NamedArgs.Should().BeEmpty();
    }

    [Fact]
    public void EntityChangeRequest_subtypes_are_distinguishable_via_pattern_matching()
    {
        EntityChangeRequest req = new CreateEntity("Book", "Demo", "Models/Book.cs");
        var result = req switch
        {
            CreateEntity c => $"create:{c.Name}",
            _ => "other",
        };
        result.Should().Be("create:Book");
    }
}
