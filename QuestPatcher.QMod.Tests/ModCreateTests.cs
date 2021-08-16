using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using SemanticVersioning;
using Xunit;
using Version = SemanticVersioning.Version;

namespace QuestPatcher.QMod.Tests
{
    public class ModCreateTests
    {
        public static async Task<QMod> OpenWritableQMod(string resourceName)
        {
            await using Stream resourceStream = ResourceUtils.OpenResource(resourceName);
            MemoryStream writableStream = new();
            await resourceStream.CopyToAsync(writableStream);

            return await QMod.ParseAsync(writableStream);
        }

        private async Task PerformCreateFileTest(QMod mod, IEnumerable<string> shouldBeWithinOnce, Stream contentToWrite, string entryName, Func<Task> createFile, bool allowMultipleEntries = false)
        {
            contentToWrite.Position = 0;
            await createFile();

            ZipArchiveEntry? entry = mod.Archive.GetEntry(entryName);
            Assert.NotNull(entry);
            await using Stream stream = entry.Open();

            await using MemoryStream resultStream = new();
            await stream.CopyToAsync(resultStream);
            resultStream.Position = 0;
            byte[] resultA = resultStream.ToArray();

            await using MemoryStream expectedStream = new();
            contentToWrite.Position = 0;
            await contentToWrite.CopyToAsync(expectedStream);
            expectedStream.Position = 0;
            byte[] resultB = expectedStream.ToArray();
            
            Assert.Equal(resultA, resultB);

            int numberOfTimesContained = 0;
            foreach (string str in shouldBeWithinOnce)
            {
                if (str == entryName)
                {
                    numberOfTimesContained++;
                }
            }

            if (allowMultipleEntries)
            {
                Assert.NotEqual(0, numberOfTimesContained);
            }
            else
            {
                Assert.Equal(1, numberOfTimesContained);
            }
        }
        
        [Fact]
        public void TestCreateFromNonWritableStream()
        {
            using Stream wrapped = new StreamWrapper(new MemoryStream())
            {
                OverrideCanRead = true,
                OverrideCanWrite = false,
                OverrideCanSeek = false
            };

            Assert.Throws<ArgumentException>(() => new QMod(wrapped, "my-mod", "MyMod", new Version(2, 0, 0), "com.my.game", "1.0.0", "Lauriethefish"));
        }
        
        [Fact]
        public void TestCreateFromNonSeekableStream()
        {
            using Stream wrapped = new StreamWrapper(new MemoryStream())
            {
                OverrideCanRead = false,
                OverrideCanWrite = true,
                OverrideCanSeek = false
            };

            QMod mod = new(wrapped, "my-mod", "MyMod", new Version(2, 0, 0), "com.my.game", "1.0.0", "Lauriethefish");
            Assert.Equal(ZipArchiveMode.Create, mod.ArchiveMode);
        }
        
        [Fact]
        public void TestCreateFromSeekableReadableStream()
        {
            using Stream wrapped = new StreamWrapper(new MemoryStream())
            {
                OverrideCanRead = true,
                OverrideCanWrite = true,
                OverrideCanSeek = true
            };

            QMod mod = new(wrapped, "my-mod", "MyMod", new Version(2, 0, 0), "com.my.game", "1.0.0", "Lauriethefish");
            Assert.Equal(ZipArchiveMode.Update, mod.ArchiveMode);
        }

        [Fact]
        public void TestCreateStatingReadMode()
        {
            using Stream stream = new MemoryStream(); // Dummy stream
            
            Assert.Throws<ArgumentException>(() => new QMod(stream, "my-mod", "MyMod", new Version(2, 0, 0), "com.my.game", "1.0.0", "Lauriethefish", ZipArchiveMode.Read));
        }

        [Fact]
        public void TestCreateAndSave()
        {
            using MemoryStream stream = new();

            {
                using QMod mod = new(stream, "my-mod", "MyMod", new Version(2, 0, 0), "com.my.game", "1.0.0", "Lauriethefish", ZipArchiveMode.Create); // Create mode is used because it's more restrictive, and we want to make sure this still works
            }

            using Stream newStream = new MemoryStream(stream.ToArray());
            
            {
                using QMod mod = QMod.ParseAsync(newStream).Result;
                Assert.Equal("my-mod", mod.Id);
                Assert.Equal("MyMod", mod.Name);
                Assert.Equal("2.0.0", mod.Version.ToString());
                Assert.Equal("com.my.game", mod.PackageId);
                Assert.Equal("1.0.0", mod.PackageVersion);
                Assert.Equal("Lauriethefish", mod.Author);
            }
        }

        private Stream GetExampleFileContent()
        {
            Stream content = new MemoryStream();
            StreamWriter writer = new(content);
            writer.WriteLineAsync("Example file content");
            writer.Flush();

            return content;
        }
        
        // Tests that adding files to QMODs works correctly
        [Fact]
        public async Task TestAddingFiles()
        {
            await using QMod mod = await OpenWritableQMod("testContainsFiles.qmod");
            Stream content = GetExampleFileContent();

            await PerformCreateFileTest(mod, mod.ModFileNames, content, "myModFile.so", async () => await mod.CreateModFileAsync("myModFile.so", content));
            await PerformCreateFileTest(mod, mod.LibraryFileNames, content, "myLibFile.so", async () => await mod.CreateLibraryFileAsync("myLibFile.so", content));

            FileCopy copy = new("myFileCopy.txt", "myDestination.txt");
            await PerformCreateFileTest(mod, mod.FileCopies.Select(otherCopy => otherCopy.Name), content, "myFileCopy.txt", async () => await mod.AddFileCopyAsync(copy, content));
        }

        // Tests that overwriting files in QMODs does not append/write incorrectly
        // Also makes sure that it does not add the file multiple times
        [Fact]
        public async Task TestOverwritingFiles()
        {
            await using QMod mod = await OpenWritableQMod("testContainsFiles.qmod");
            await using Stream content = GetExampleFileContent();
            
            await PerformCreateFileTest(mod, mod.ModFileNames, content, "libexample-mod.so", async () => await mod.CreateModFileAsync("libexample-mod.so", content));
            await PerformCreateFileTest(mod, mod.LibraryFileNames, content, "libmy-library.so", async () => await mod.CreateLibraryFileAsync("libmy-library.so", content));

            // Even though this file copy has a different destination, it should still overwrite as the origin is the same as an existing file copy
            FileCopy copy = new("myFile.png", "myDestination.txt");
            await PerformCreateFileTest(mod, mod.FileCopies.Select(otherCopy => otherCopy.Name), content, "myFile.png", async () => await mod.AddFileCopyAsync(copy, content), true);
        }

        // Tests that operations that modify a QMOD throw InvalidOperationException if the QMOD is read only
        [Fact]
        public async Task TestReadonlyModification()
        {
            await using Stream readonlyStream = new StreamWrapper(ResourceUtils.OpenResource("testContainsFiles.qmod"))
            {
                OverrideCanRead = true,
                OverrideCanSeek = false,
                OverrideCanWrite = false
            };

            await using QMod mod = await QMod.ParseAsync(readonlyStream);
            MemoryStream dummyStream = new();

            Assert.Throws<InvalidOperationException>(() => mod.Author = "Somebody else");
            Assert.Throws<InvalidOperationException>(() => mod.CoverImagePath = "myOtherCover.png");
            
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await mod.CreateModFileAsync("myFile.so", dummyStream));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await mod.CreateLibraryFileAsync("myFile.so", dummyStream));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await mod.WriteCoverImageAsync("myCover.png", dummyStream));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await mod.AddFileCopyAsync(new FileCopy("myOtherFile.txt", "destination.txt"), dummyStream));
        }
    }
}