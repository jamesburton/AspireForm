namespace AspireForm.Plugin.Auth.Common;

/// <summary>Convention for marker-region names used by AspireForm auth plugins.</summary>
public static class AuthMarkerNames
{
    /// <summary>Returns the marker block name for the given auth variant (e.g. <c>"apikey"</c> -> <c>"auth-apikey"</c>).</summary>
    public static string Marker(string variant) => $"auth-{variant.ToLowerInvariant()}";
}
