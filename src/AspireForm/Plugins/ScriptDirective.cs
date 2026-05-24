namespace AspireForm.Plugins;

/// <summary>The kind of <c>#:</c> directive declared at the top of a script plugin.</summary>
public enum ScriptDirectiveKind
{
    /// <summary>A NuGet package reference: <c>#:package &lt;id&gt;[@&lt;version&gt;]</c>.</summary>
    Package,
}

/// <summary>A parsed <c>#:</c> directive from a script plugin's source.</summary>
public sealed record ScriptDirective(ScriptDirectiveKind Kind, string PackageId, string Version);
