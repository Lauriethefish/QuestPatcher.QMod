using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace QuestPatcher.QMod.Tests
{
    public class ModSaveTests
    {
        [Fact]
        public async Task TestManifestModifiedAfterPropertyChanged()
        {
            await using QMod qmod = await ModCreateTests.OpenWritableQMod("testContainsFiles.qmod");

            qmod.Author = "Somebody else";
            Assert.True(qmod.ManifestModified);
        }
        
        [Fact]
        public async Task TestManifestNotModifiedInitially()
        {
            await using QMod qmod = await ModCreateTests.OpenWritableQMod("testContainsFiles.qmod");
            Assert.False(qmod.ManifestModified);
        }

        // Tests that manifest properties are saved when using DisposeAsync
        [Fact]
        public async Task TestManifestPropertySavedAsync()
        {
            await using MemoryStream modStream = new();
            await using Stream originModStream = ResourceUtils.OpenResource("testContainsFiles.qmod");
            
            await originModStream.CopyToAsync(modStream);
            modStream.Position = 0;

            await using (QMod mod = await QMod.ParseAsync(modStream))
            {
                mod.Author = "Somebody else";
            } // QMod goes out of scope and is disposed, so should be saved
            
            await using Stream newModStream = new MemoryStream(modStream.ToArray());
            await using QMod reloadedMod = await QMod.ParseAsync(newModStream);

            Assert.Equal("Somebody else", reloadedMod.Author);
        }
        
        // Tests that manifest properties are saved when using Dispose
        [Fact]
        public void TestManifestPropertySaved()
        {
            using MemoryStream modStream = new();
            using Stream originModStream = ResourceUtils.OpenResource("testContainsFiles.qmod");
            
            originModStream.CopyTo(modStream);
            modStream.Position = 0;

            using (QMod mod = QMod.ParseAsync(modStream).Result)
            {
                mod.Author = "Somebody else";
            } // QMod goes out of scope and is disposed, so should be saved
            
            using Stream newModStream = new MemoryStream(modStream.ToArray());
            using QMod reloadedMod = QMod.ParseAsync(newModStream).Result;

            Assert.Equal("Somebody else", reloadedMod.Author);
        }
    }
}