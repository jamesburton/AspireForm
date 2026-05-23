namespace AspireForm.State;

/// <summary>The persisted last-known state of an AspireForm-managed project.</summary>
public sealed class AspireFormState
{
    /// <summary>The state-file schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Tracked blocks, keyed by block name.</summary>
    public Dictionary<string, BlockState> Blocks { get; set; } = new();
}

/// <summary>The tracked state of a single resource or module block.</summary>
public sealed class BlockState
{
    /// <summary>The provider type, e.g. <c>sqlserver</c>.</summary>
    public required string Type { get; set; }

    /// <summary>The block kind: <c>resource</c> or <c>module</c>.</summary>
    public required string Kind { get; set; }

    /// <summary>Files emitted for this block, keyed by repo-relative path.</summary>
    public Dictionary<string, FileState> Files { get; set; } = new();
}

/// <summary>The tracked state of a single generated file.</summary>
public sealed class FileState
{
    /// <summary>The file's ownership mode: <c>managed</c>, <c>scaffold</c>, or <c>merge</c>.</summary>
    public required string OwnershipMode { get; set; }

    /// <summary>SHA-256 (hex) of the content AspireForm last generated for this file.</summary>
    public required string Checksum { get; set; }

    /// <summary>For <c>merge</c>-mode files: the last-generated content, used as the 3-way-merge baseline.</summary>
    public string? Baseline { get; set; }
}

/// <summary>Raised when the state file cannot be read or is corrupt.</summary>
public sealed class StateException : Exception
{
    /// <summary>Initializes the exception with a message and an inner cause.</summary>
    public StateException(string message, Exception inner) : base(message, inner) { }
}
