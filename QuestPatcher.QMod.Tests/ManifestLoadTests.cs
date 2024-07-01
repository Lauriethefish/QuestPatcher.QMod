using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

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
            Assert.Equal(new List<string> { "libmy-latemod.so" }, manifest.LateModFileNames);

            Assert.Equal(ModLoader.Scotland2, manifest.ModLoader);

            Assert.Equal(2, manifest.Dependencies.Count);
            Dependency requiredDependency = manifest.Dependencies[0];
            Assert.Equal("my-dependency", requiredDependency.Id);
            Assert.Equal("^0.1.0", requiredDependency.VersionRangeString);
            Assert.Equal("https://somesite.com/my_dependency_0_1_0.qmod", requiredDependency.DownloadUrlString);
            Assert.True(requiredDependency.Required);
            Dependency optionalDependency = manifest.Dependencies[1];
            Assert.Equal("my-optional-dependency", optionalDependency.Id);
            Assert.Equal("^0.1.0", optionalDependency.VersionRangeString);
            Assert.Equal("https://somesite.com/my_optional_dependency_0_1_0.qmod", optionalDependency.DownloadUrlString);
            Assert.False(optionalDependency.Required);

            Assert.Single(manifest.FileCopies);
            FileCopy fileCopy = manifest.FileCopies[0];
            Assert.Equal("myFile.png", fileCopy.Name);
            Assert.Equal("/sdcard/ModData/com.my.game/myFile.png", fileCopy.Destination);

            Assert.Single(manifest.CopyExtensions);
            CopyExtension extension = manifest.CopyExtensions[0];
            Assert.Equal("gtmap", extension.Extension);
            Assert.Equal("/sdcard/ModData/com.AnotherAxiom.GorillaTag/Mods/MonkeMapLoader/CustomMaps/", extension.Destination);
        }

        [Fact]
        public async Task TestNoModloaderSpecified()
        {
            await using Stream manifestStream = ResourceUtils.OpenResource("noModloader.json");
            QModManifest manifest = await QModManifest.ParseAsync(manifestStream);

            // QuestLoader should be the default
            Assert.Equal(ModLoader.QuestLoader, manifest.ModLoader);
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
