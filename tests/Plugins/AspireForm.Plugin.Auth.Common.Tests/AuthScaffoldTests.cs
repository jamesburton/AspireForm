using AspireForm.Plugin.Auth.Common;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Auth.Common.Tests;

public sealed class AuthScaffoldTests
{
    [Fact]
    public void RenderRegistrationComment_includes_variant_and_project_name()
    {
        var content = AuthScaffold.RenderRegistrationComment("apikey", "MyApp");
        content.Should().Contain("apikey").And.Contain("MyApp");
        content.Should().Contain("AddAuthentication").And.Contain("UseAuthentication");
    }

    [Fact]
    public void RenderRegistrationComment_capitalises_variant_in_AddXyz_call()
    {
        var content = AuthScaffold.RenderRegistrationComment("magiclink", "X");
        content.Should().Contain("AddMagiclink");
    }
}

public sealed class AuthMarkerNamesTests
{
    [Fact]
    public void Marker_prefixes_variant_with_auth_dash()
    {
        AuthMarkerNames.Marker("ApiKey").Should().Be("auth-apikey");
        AuthMarkerNames.Marker("magiclink").Should().Be("auth-magiclink");
        AuthMarkerNames.Marker("entra").Should().Be("auth-entra");
    }
}
