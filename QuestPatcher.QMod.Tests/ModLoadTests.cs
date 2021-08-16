using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Xunit;

namespace QuestPatcher.QMod.Tests
{
    public class ModLoadTests
    {
        private async Task<QMod> LoadModFromResource(string name, bool failOnMissingStatedFile = true, bool failOnMissingStatedCover = false, ZipArchiveMode? archiveMode = null)
        {
            await using Stream modStream = ResourceUtils.OpenResource(name);
            return await QMod.ParseAsync(modStream, failOnMissingStatedFile, failOnMissingStatedCover, archiveMode);
        }

        [Fact]
        public async Task TestContainsFiles()
        {
            // Test that this does not throw, as all of the files exist
            await using QMod doesNotThrow = await LoadModFromResource("testContainsFiles.qmod", true, true);
            
            // Test that no files have been removed, as they all exist in the archive
            await using QMod mod = await LoadModFromResource("testContainsFiles.qmod", false, false);
            Assert.Equal(new List<string>{"libexample-mod.so"}, mod.ModFileNames);
            Assert.Equal(new List<string>{"libmy-library.so"}, mod.LibraryFileNames);
            Assert.NotNull(mod.CoverImagePath);
        }

        [Fact]
        public async Task TestMissingManifest()
        {
            await Assert.ThrowsAsync<InvalidModException>(async () => await LoadModFromResource("testMissingManifest.qmod"));
        }

        [Fact]
        public async Task TestNonReadableStream()
        {
            await using Stream nonReadable = new StreamWrapper
            {
                OverrideCanRead = false,
                OverrideCanSeek = false,
                OverrideCanWrite = true // Just writing is not allowed
            };

            await Assert.ThrowsAsync<ArgumentException>(async () => { await QMod.ParseAsync(nonReadable); });
        }

        // Read only streams should lead to a read only QMOD
        [Fact]
        public async Task TestReadOnlyStream()
        {
            await using Stream readonlyStream = new StreamWrapper(ResourceUtils.OpenResource("testContainsFiles.qmod"))
            {
                OverrideCanRead = true,
                OverrideCanSeek = false,
                OverrideCanWrite = false
            };
            
            await using QMod mod = await QMod.ParseAsync(readonlyStream);
            Assert.Equal(ZipArchiveMode.Read, mod.ArchiveMode);
        }
        
        // A readable, writable, seekable stream should lead to ZipArchiveMode.Update
        [Fact]
        public async Task TestSeekableStream()
        {
            await using QMod mod = await ModCreateTests.OpenWritableQMod("testContainsFiles.qmod");
            Assert.Equal(ZipArchiveMode.Update, mod.ArchiveMode);
        }

        // ZipArchiveMode.Create is not allowed for parsing qmods, since it does not support reading
        [Fact]
        public async Task TestCreateArchiveMode()
        {
            await using Stream stream = new MemoryStream();
            using ZipArchive archive = new(stream, ZipArchiveMode.Create);

            await Assert.ThrowsAsync<ArgumentException>(async () => { await QMod.ParseAsync(archive); });
            await Assert.ThrowsAsync<ArgumentException>(async () => { await QMod.ParseAsync(stream, true, false, ZipArchiveMode.Create); });
        }

        [Fact]
        public async Task TestMissingCover()
        {
            // Test that this does not throw, but sets the cover to null
            await using QMod mod = await LoadModFromResource("testMissingCover.qmod", true, false);
            Assert.Null(mod.CoverImagePath);
            
            // Test that this does throw, as the cover is missing and we have set to throw
            await Assert.ThrowsAsync<ModMissingFileException>(async () =>
            {
                await LoadModFromResource("testMissingCover.qmod", true, true);
            });
        }
        
        [Fact]
        public async Task TestMissingModFile()
        {
            // Test that this does not throw, but removes the mod file from the list
            await using QMod mod = await LoadModFromResource("testMissingModFile.qmod", false, true);
            Assert.Empty(mod.ModFileNames);
            
            // Test that this does throw, as a mod file is missing and we have set to throw
            await Assert.ThrowsAsync<ModMissingFileException>(async () =>
            {
                await LoadModFromResource("testMissingModFile.qmod", true, true);
            });
        }
        
        [Fact]
        public async Task TestMissingLibraryFile()
        {
            // Test that this does not throw, but removes the library file from the list
            await using QMod mod = await LoadModFromResource("testMissingLibFile.qmod", false, true);
            Assert.Empty(mod.LibraryFileNames);
            
            // Test that this does throw, as a library file is missing and we have set to throw
            await Assert.ThrowsAsync<ModMissingFileException>(async () =>
            {
                await LoadModFromResource("testMissingLibFile.qmod", true, true);
            });
        }
        
        [Fact]
        public async Task TestMissingFileCopy()
        {
            // Test that this does not throw, but removes the file copy from the list
            await using QMod mod = await LoadModFromResource("testMissingFileCopy.qmod", false, true);
            Assert.Empty(mod.FileCopies);
            
            // Test that this does throw, as a file copy is missing and we have set to throw
            await Assert.ThrowsAsync<ModMissingFileException>(async () =>
            {
                await LoadModFromResource("testMissingFileCopy.qmod", true, true);
            });
        }
    }
}