namespace AspireForm.Plugin.Auth.Common;

/// <summary>Helpers shared across AspireForm auth plugins for rendering scaffold + managed content consistently.</summary>
public static class AuthScaffold
{
    /// <summary>Renders a multi-line comment block describing where + how to wire AddAuthentication / UseAuthentication for the given auth variant.</summary>
    public static string RenderRegistrationComment(string variant, string projectName) => $$"""
        // {{variant}} auth scaffolded by AspireForm.
        // In your service project's Program.cs, add:
        //   builder.Services.AddAuthentication(...).Add{{Capitalise(variant)}}(/* options */);
        //   app.UseAuthentication();
        //   app.UseAuthorization();
        // See the {{variant}}Setup.cs scaffold in the same directory for a starter helper.
        // Wire this auth block to your service project from {{projectName}}.AppHost.
        """;

    private static string Capitalise(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
