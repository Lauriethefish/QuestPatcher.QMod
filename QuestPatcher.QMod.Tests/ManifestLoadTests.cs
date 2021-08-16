using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SemanticVersioning;
using Xunit;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace QuestPatcher.QMod.Tests
{
    public class ManifestLoadTests
    {
        [Fact]
        public async Task TestManifestLoad()
        {
            await using Stream manifestStream = ResourceUtils.OpenResource("exampleMod.json");
            QModManifest manifest = await QModManifest.ParseAsync(manifestStream);

            Assert.Equal("example-mod", manifest.Id);
            Assert.Equal("ExampleMod", manifest.Name);
            Assert.Equal("2.0.0", manifest.VersionString);
            Assert.Equal("com.my.game", manifest.PackageId);
            Assert.Equal("1.0.0", manifest.PackageVersion);
            Assert.Equal("Lauriethefish", manifest.Author);
            Assert.Equal("myCover.png", manifest.CoverImagePath);
            Assert.Equal("Larrythefish", manifest.Porter);
            Assert.Equal("Example mod", manifest.Description);
            Assert.Equal(new List<string> {"libexample-mod.so"}, manifest.ModFileNames);
            Assert.Equal(new List<string> {"libmy-library.so"}, manifest.LibraryFileNames);

            Assert.Equal(1, manifest.Dependencies.Count);
            Dependency dependency = manifest.Dependencies[0];
            Assert.Equal("my-dependency", dependency.Id);
            Assert.Equal("^0.1.0", dependency.VersionRangeString);
            Assert.Equal("https://somesite.com/my_dependency_0_1_0.qmod", dependency.DownloadUrlString);

            Assert.Equal(1, manifest.FileCopies.Count);
            FileCopy fileCopy = manifest.FileCopies[0];
            Assert.Equal("myFile.png", fileCopy.Name);
            Assert.Equal("/sdcard/ModData/com.my.game/myFile.png", fileCopy.Destination);
        }

        [Fact]
        public async Task TestInvalidSchemaVersionLoad()
        {
            await using Stream manifestStream = ResourceUtils.OpenResource("invalidSchemaVersion.json");

            await Assert.ThrowsAsync<UnsupportedSchemaVersionException>(async () =>
            {
                await QModManifest.ParseAsync(manifestStream);
            });
        }

        [Fact]
        public async Task TestSchemaValidation()
        {
            await using Stream manifestStream = ResourceUtils.OpenResource("invalidMod.json");

            await Assert.ThrowsAsync<InvalidModException>(async () => { await QModManifest.ParseAsync(manifestStream); });
        }

        [Fact]
        public async Task TestInvalidJson()
        {
            await using Stream manifestStream = ResourceUtils.OpenResource("invalidJson.json");

            await Assert.ThrowsAsync<InvalidModException>(async () =>
            {
                try
                {
                    await QModManifest.ParseAsync(manifestStream);
                }
                catch (InvalidModException ex)
                {
                    Assert.IsAssignableFrom<JsonException>(ex.InnerException);
                    throw;
                }
            });
        }
    }
}