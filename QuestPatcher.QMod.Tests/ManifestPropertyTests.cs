using System;
using Xunit;

namespace QuestPatcher.QMod.Tests
{
    public class ManifestPropertyTests
    {
        private QModManifest GetTestManifest() => new QModManifest("example-mod", "2.0.0", "ExampleMod", "com.my.game", "1.0.0");
        private Dependency GetTestDependency() => new Dependency("my-dependency");
        
        [Fact]
        public void TestManifestSetVersion()
        {
            QModManifest manifest = GetTestManifest();
            manifest.VersionString = "3.0.0"; // Valid version string, should not fail

            Assert.Throws<ArgumentException>(() => manifest.VersionString = "3.0.0.0"); // Example invalid version string, should fail
        }

        [Fact]
        public void TestManifestSetId()
        {
            QModManifest manifest = GetTestManifest();
            manifest.Id = "my-other-id"; // Valid ID, as it does not contain whitespace

            Assert.Throws<ArgumentException>(() => manifest.Id = "ID containing spaces"); // Invalid ID, contains whitespace
        }

        /*
        // TODO: SemanticVersioning library does not throw on an invalid version range
        // TODO: The schema also does not contain a regex for it
        [Fact]
        public void TestDependencySetVersion()
        {
            Dependency dependency = GetTestDependency();
            dependency.VersionRangeString = "^0.1.0"; // Valid version range string, should not fail

            Assert.Throws<ArgumentException>(() => dependency.VersionRangeString = "^0.1.0.0"); // Example invalid version range string, should fail
        }*/
        
        [Fact]
        public void TestDependencySetId()
        {
            Dependency dependency = GetTestDependency();
            dependency.Id = "my-other-dependency"; // Valid ID, as it does not contain whitespace

            Assert.Throws<ArgumentException>(() => dependency.Id = "ID containing spaces"); // Invalid ID, contains whitespace
        }
        
        [Fact]
        public void TestDependencySetDownloadString()
        {
            const string exampleUri = "https://example.com";
            
            Dependency dependency = GetTestDependency();
            dependency.DownloadUrlString = exampleUri; // Valid URI
            Assert.Equal(new Uri(exampleUri), dependency.DownloadIfMissing);

            // Setting download URL string to null should set the underlying URI to null
            dependency.DownloadUrlString = null;
            Assert.Null(dependency.DownloadIfMissing);
            
            Assert.Throws<ArgumentException>(() => dependency.DownloadUrlString = "https:/example.com"); // Invalid URI, should throw
        }
    }
}