using FluentAssertions;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.Tests;

public class SharedFileTests
{
    [Fact]
    public void A_file_being_read_can_still_be_deleted_by_other_programs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lustrum-shared-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "content");

        using (SharedFile.OpenRead(path))
        {
            var delete = () => File.Delete(path);

            delete.Should().NotThrow();
        }

        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void Read_all_text_strips_nothing_but_decodes_utf8()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lustrum-shared-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "naïve");
        try
        {
            SharedFile.ReadAllText(path).Should().Be("naïve");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
