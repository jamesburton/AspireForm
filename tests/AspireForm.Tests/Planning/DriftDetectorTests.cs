using AspireForm.Planning;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class DriftDetectorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-drift").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ComputeChecksum_is_stable_and_matches_known_sha256()
    {
        var path = Write("a.txt", "hello");
        DriftDetector.ComputeChecksum(path)
            .Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    [Fact]
    public void IsAbsent_returns_true_for_a_missing_file_and_false_for_a_present_one()
    {
        DriftDetector.IsAbsent(Path.Combine(_dir, "ghost.txt")).Should().BeTrue();
        var present = Write("there.txt", "x");
        DriftDetector.IsAbsent(present).Should().BeFalse();
    }

    [Fact]
    public void HasDrifted_returns_true_when_on_disk_checksum_differs_from_baseline()
    {
        var path = Write("a.txt", "current");
        const string baseline = "0000000000000000000000000000000000000000000000000000000000000000";
        DriftDetector.HasDrifted(path, baseline).Should().BeTrue();
    }

    [Fact]
    public void HasDrifted_returns_false_when_checksums_match()
    {
        var path = Write("a.txt", "hello");
        var hash = DriftDetector.ComputeChecksum(path);
        DriftDetector.HasDrifted(path, hash).Should().BeFalse();
    }

    [Fact]
    public void HasDrifted_returns_true_when_the_file_has_been_deleted()
    {
        DriftDetector.HasDrifted(Path.Combine(_dir, "deleted.txt"), "anyhash").Should().BeTrue();
    }
}
