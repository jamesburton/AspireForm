using Xunit;

namespace AspireForm.Tests.Cli;

/// <summary>
/// Test collection that serializes execution. Tests joined to this collection mutate
/// process-wide state (e.g. <see cref="System.Console.SetOut(System.IO.TextWriter)"/>) and
/// must not run concurrently with each other.
/// </summary>
[CollectionDefinition(nameof(ConsoleCaptureCollection), DisableParallelization = true)]
public sealed class ConsoleCaptureCollection;
