using AspireForm.ApiCatalog;
using AspireForm.EntityCatalog;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.ApiCatalog;

public sealed class EndpointModelTests
{
    [Fact]
    public void EndpointMutationResult_Ok_marks_success_with_empty_diagnostics_by_default()
    {
        var r = EndpointMutationResult.Ok(["a.cs", "b.cs"]);
        r.Success.Should().BeTrue();
        r.ChangedFiles.Should().BeEquivalentTo(new[] { "a.cs", "b.cs" });
        r.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void EndpointMutationResult_Fail_marks_failure_with_error_diagnostic()
    {
        var r = EndpointMutationResult.Fail("boom", "x.cs");
        r.Success.Should().BeFalse();
        r.ChangedFiles.Should().BeEmpty();
        r.Diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be("error");
        r.Diagnostics[0].Message.Should().Be("boom");
        r.Diagnostics[0].FilePath.Should().Be("x.cs");
    }

    [Fact]
    public void EndpointChangeRequest_subtypes_are_distinguishable_via_pattern_matching()
    {
        EndpointChangeRequest req = new CreateEndpoint("GetBooks", "BooksHandler", "/books", "GET", "Handlers/BooksHandler.cs", "Demo.Handlers");
        var result = req switch
        {
            CreateEndpoint c => $"create:{c.MethodName}",
            _ => "other",
        };
        result.Should().Be("create:GetBooks");
    }

    [Fact]
    public void RouteParameter_stores_name_constraint_and_optional_flag()
    {
        var p = new RouteParameter("id", "int", false);
        p.Name.Should().Be("id");
        p.Constraint.Should().Be("int");
        p.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void EndpointCatalog_holds_endpoints_and_diagnostics()
    {
        var info = new EndpointInfo(
            HandlerTypeName: "BooksHandler",
            MethodName: "GetBooks",
            Route: "/books",
            HttpMethod: "GET",
            Summary: "Returns all books",
            AuthPolicy: null,
            Tags: ["Books"],
            Parameters: [],
            Attributes: [],
            FilePath: "Handlers/BooksHandler.cs");
        var catalog = new EndpointCatalog([info], []);
        catalog.Endpoints.Should().ContainSingle()
            .Which.MethodName.Should().Be("GetBooks");
        catalog.Diagnostics.Should().BeEmpty();
    }
}
