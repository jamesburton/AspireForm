using AspireForm.Annotations;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Annotations;

public sealed class ApiAnnotationsTests
{
    [Fact]
    public void ApiEndpointAttribute_stores_route_and_default_method()
    {
        var attr = new ApiEndpointAttribute("/books");
        attr.Route.Should().Be("/books");
        attr.Method.Should().Be("GET");
    }

    [Fact]
    public void ApiEndpointAttribute_stores_explicit_method()
    {
        var attr = new ApiEndpointAttribute("/books", "POST");
        attr.Route.Should().Be("/books");
        attr.Method.Should().Be("POST");
    }

    [Fact]
    public void ApiAuthAttribute_stores_policy()
    {
        var attr = new ApiAuthAttribute("admin");
        attr.Policy.Should().Be("admin");
    }

    [Fact]
    public void ApiAuthAttribute_accepts_anonymous_sentinel()
    {
        var attr = new ApiAuthAttribute("anonymous");
        attr.Policy.Should().Be("anonymous");
    }

    [Fact]
    public void ApiTagAttribute_stores_tag()
    {
        var attr = new ApiTagAttribute("Books");
        attr.Tag.Should().Be("Books");
    }

    [Fact]
    public void ApiSummaryAttribute_stores_summary()
    {
        var attr = new ApiSummaryAttribute("Returns all books");
        attr.Summary.Should().Be("Returns all books");
    }
}
