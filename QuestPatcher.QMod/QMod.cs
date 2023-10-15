using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Version = SemanticVersioning.Version;

namespace QuestPatcher.QMod
{
    /// <summary>
    /// Wrapper over a ZIP archive and the QMOd's manifest.
    /// Convenient for loading and modifying QMOD files.
    ///
    /// NOTE: Modifications to the mod's manifest are only saved when disposing the QMod. This also closes the underlying stream.
    /// It is advised to use <see cref="DisposeAsync"/> instead of <see cref="Dispose"/> in asynchronous applications in order to avoid blocking while writing the manifest.
    /// </summary>
    public class QMod : IDisposable, IAsyncDisposable
    {
        private const string ManifestPath = "mod.json";
        
        /// <summary>
        /// The underlying QMOD archive.
        /// There should be no need to manually modify this.
        /// </summary>
        [JsonIgnore]
        public ZipArchive Archive { get; }

        /// <summary>
        /// Version of the QMOD format that this mod was designed for
        /// </summary>
        public string SchemaVersion => _manifest.SchemaVersion;

        /// <summary>
        /// An ID for the mod.
        /// Two mods with the same ID cannot be installed
        /// </summary>
        public string Id { get => _manifest.Id; set => SetValue(_manifest.Id, value, v => _manifest.Id = v); }

        /// <summary>
        /// A human-readable name for the mod
        /// </summary>
        public string Name { get => _manifest.Name; set => SetValue(_manifest.Name, value, v => _manifest.Name = v); }

        /// <summary>
        /// The author of the mod
        /// </summary>
        public string Author { get => _manifest.Author; set => SetValue(_manifest.Author, value, v => _manifest.Author = v); }

        /// <summary>
        /// Version of the mod.
        /// </summary>
        public Version Version { get => _manifest.Version; set => SetValue(_manifest.Version, value, v => _manifest.Version = v); }
        
        /// <summary>
        /// The package ID of the app that the mod is designed for.
        /// If null, this means that the mod works for any app.
        /// </summary>
        public string? PackageId { get => _manifest.PackageId; set => SetValue(_manifest.PackageId, value, v => _manifest.PackageId = v); }

        /// <summary>
        /// The version of the app that the mod is designed for.
        /// If null, this means that the mod doesn't depend on a particular version of an app.
        /// This property is redundant if <see cref="PackageId"/> is null.
        /// </summary>
        public string? PackageVersion { get => _manifest.PackageVersion; set => SetValue(_manifest.PackageVersion, value, v => _manifest.PackageVersion = v); }

        /// <summary>
        /// The modloader this mod uses
        /// If none is specified QuestLoader is returned
        /// </summary>
        public ModLoader? ModLoader
        {
            get
            {
                switch(_manifest.ModLoader)
                {
                    case "QuestLoader":
                        return QuestPatcher.QMod.ModLoader.QuestLoader;
                    case "Scotland2":
                        return QuestPatcher.QMod.ModLoader.Scotland2;
                    default:
                        return QuestPatcher.QMod.ModLoader.QuestLoader;
                }
            }
            set {
                
                SetValue<string>(_manifest.ModLoader, Enum.GetName(typeof(QuestPatcher.QMod.ModLoader), value), v => _manifest.ModLoader = v);
            }
        }

        /// <summary>
        /// Whether or not the mod is a library mod.
        /// Library mods should be automatically uninstalled whenever no mods that depend on them are installed
        /// </summary>
        public bool IsLibrary { get => _manifest.IsLibrary; set => SetValue(_manifest.IsLibrary, value, v => _manifest.IsLibrary = v); }

        /// <summary>
        /// Files copied to the mod loader's mods directory
        /// </summary>
        public IReadOnlyList<string> ModFileNames => _manifest.ModFileNames;
        
        /// <summary>
        /// Files copied to the mod loader's mods directory
        /// </summary>
        public IReadOnlyList<string> LateModFileNames => _manifest.LateModFileNames;

        /// <summary>
        /// Files copied to the mod loader's libraries directory.
        /// Ideally, when uninstalling mods, these files should only be removed if no other installed/enabled mod has a library with the same name.
        /// </summary>
        public IReadOnlyList<string> LibraryFileNames => _manifest.LibraryFileNames;

        /// <summary>
        /// Files copied to arbitrary locations
        /// </summary>
        public IReadOnlyList<FileCopy> FileCopies => _manifest.FileCopies;

        /// <summary>
        /// Dependencies of the mod
        /// </summary>
        public List<Dependency> Dependencies { get => _manifest.Dependencies; set => SetValue(_manifest.Dependencies, value, v => _manifest.Dependencies = v); }
        
        /// <summary>
        /// File copy extensions to be registered by this mod
        /// </summary>
        public List<CopyExtension> CopyExtensions { get => _manifest.CopyExtensions; set => SetValue(_manifest.CopyExtensions, value, v => _manifest.CopyExtensions = v); }
        
        /// <summary>
        /// A short description of what the mod does
        /// </summary>
        public string? Description { get => _manifest.Description; set => SetValue(_manifest.Description, value, v => _manifest.Description = v); }

        /// <summary>
        /// If the mod was ported from another platform, this is the author of the port.
        /// </summary>
        public string? Porter { get => _manifest.Porter; set => SetValue(_manifest.Porter, value, v => _manifest.Porter = v); }

        /// <summary>
        /// Gets or sets the path of the cover image of the QMOD.
        /// If setting to a non-null value from a non-null value, this will rename the cover.
        /// If setting to a null value from a non-null value, this will delete the cover.
        /// 
        /// Setting to a non-null value from a null value is not allowed, as this would lead to a cover path pointing to a file which does not exist.
        /// Instead, <see cref="WriteCoverImageAsync"/> should be used to create a valid cover image from a stream.
        /// </summary>
        /// <exception cref="InvalidOperationException">If attempting to set to a non-null value when the current value is null, or if the new path already exists</exception>
        public string? CoverImagePath
        {
            get => _manifest.CoverImagePath;
            
            // TODO: Probably best to move this to a separate class as another property, instead of so much complexity in a setter.
            // TODO: This would also allow the copy to be asynchronous
            set
            {
                if (!CanSaveManifest)
                {
                    throw new InvalidOperationException("Cannot change cover image path when of read only QMOD");
                }

                if (ArchiveMode == ZipArchiveMode.Create)
                {
                    throw new InvalidOperationException("Cannot change cover image of QMOD with archive set to ZipArchiveMode.Create");
                }
                
                // Sanity check
                if (_manifest.CoverImagePath == value) { return; }
                if (_manifest.CoverImagePath == null)
                {
                    throw new InvalidOperationException("Cannot set cover image path when no cover image file exists. Write the cover image using WriteCoverImageAsync first!");
                }

                ZipArchiveEntry? oldCoverEntry = Archive.GetEntry(_manifest.CoverImagePath);
                _manifest.CoverImagePath = value;
                ManifestModified = true;

                if (oldCoverEntry == null)
                {
                    // Cover entry with a non-null path did not exist.
                    // This should never happen, as we make sure that the cover exists when loading the QMOD.
                    // Fallback to just returning, the new path has already been set
                    return;
                }
                
                // Setting the path to null should just delete the cover image, as this indicates no cover image.
                if (value == null)
                {
                    oldCoverEntry.Delete();
                    return;
                }

                // Check that the new name does not already exist
                if (Archive.GetEntry(value) != null)
                {
                    throw new InvalidOperationException($"File with name {value} already exists within the QMOD. Cannot rename the cover to this");
                }
                
                // If we're not deleting the cover by setting to null, we need to copy the cover image to the new location
                ZipArchiveEntry newCoverEntry = Archive.CreateEntry(value);
                using Stream oldCoverStream = oldCoverEntry.Open();
                using Stream newCoverStream = newCoverEntry.Open();
                oldCoverStream.CopyTo(newCoverStream);
                
                // Remove the old cover image
                oldCoverEntry.Delete();
            }
        }
        
        /// <summary>
        /// True if a QMOD property has been changed that requires a manifest save to take effect within the archive.
        /// The manifest is saved when the QMOD is disposed. Ideally, <code>await using</code> should be used to save the manifest asynchronously.
        /// </summary>
        public bool ManifestModified { get; private set; }

        /// <summary>
        /// The mode of the underlying ZIP archive.
        ///
        /// If set to Read, no writing operations are permitted on the QMOD.
        /// If set to Create, mod/library/file copy/cover entries cannot be opened/read - only writing to newly created entries is permitted.
        /// If set to Update, all QMOD functionality is supported.
        /// </summary>
        public ZipArchiveMode ArchiveMode => Archive.Mode;

        private bool CanSaveManifest => ArchiveMode != ZipArchiveMode.Read;

        private readonly QModManifest _manifest;
        private bool _disposed;
        
        private QMod(ZipArchive archive, QModManifest manifest)
        {
            Archive = archive;

            _manifest = manifest;
        }

        /// <summary>
        /// Creates a new QMod, saved to the given stream.
        /// </summary>
        /// <param name="stream">Stream to save to, must be seekable and writable, as we are creating a new mod which needs to be saved</param>
        /// <param name="archiveMode">The mode to load the archive with, if set to null, the "best" archive mode will be selected. This is Update if the stream supports reading, writing and seeking, Create if writing is available but seeking is not.</param>
        /// <param name="id">ID of the mod, cannot contain whitespace</param>
        /// <param name="name">Human readable name of the mod</param>
        /// <param name="version">Semver Version of the mod</param>
        /// <param name="packageId">ID of the Android app this mod is intended for</param>
        /// <param name="packageVersion">Version of the Android ap this mod is intended for</param>
        /// <param name="author">Author of the mod</param>
        /// <exception cref="ArgumentException">If the given stream does not support  writing</exception>
        public QMod(Stream stream, string id, string name, Version version, string? packageId, string? packageVersion, string author, ZipArchiveMode? archiveMode = null)
            : this(new ZipArchive(stream, FindArchiveMode(stream, archiveMode, "Cannot create a QMOD using a stream that does not support writing", "Cannot create a QMOD using ZipArchiveMode.Read", false, true)), new QModManifest(id, name, version, packageId, packageVersion, author))
        {
            ManifestModified = true;
        }

        /// <summary>
        /// Clones the manifest, and returns the clone.
        /// </summary>
        /// <returns>A deep clone of the current manifest of this mod</returns>
        public QModManifest GetManifest()
        {
            return _manifest.DeepClone();
        }

        private void SetValue<T>(T currentValue, T newValue, Action<T> setValue)
        {
            if (!CanSaveManifest)
            {
                throw new InvalidOperationException("Cannot set a manifest property on a read only QMOD");
            }
            if (EqualityComparer<T>.Default.Equals(currentValue, newValue)) { return; }

            setValue(newValue);
            ManifestModified = true;
        }
        
        [AssertionMethod]
        private void RemoveMissing<T>(string fileTypeName, bool throwExceptions, List<T> objects, Func<T, string> getName)
        {
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                T item = objects[i];
                string name = getName(item);
                if (Archive.GetEntry(name) == null)
                {
                    if (throwExceptions)
                    {
                        throw new ModMissingFileException($"Missing stated {fileTypeName} {name} in manifest");
                    }
                    objects.RemoveAt(i);
                }
            }
        }

        [AssertionMethod]
        private static ZipArchiveMode FindArchiveMode(Stream stream, ZipArchiveMode? statedMode, string? failMessage, string? invalidStatedErrorMessage, bool allowReadMode, bool allowCreateMode)
        {
            if (statedMode == ZipArchiveMode.Read && !allowReadMode || statedMode == ZipArchiveMode.Create && !allowCreateMode)
            {
                throw new ArgumentException(invalidStatedErrorMessage);
            }

            if (stream.CanRead && stream.CanWrite && stream.CanSeek)
            {
                return ZipArchiveMode.Update;
            }

            if (stream.CanWrite && allowCreateMode)
            {
                return ZipArchiveMode.Create;
            }

            if (stream.CanRead && allowReadMode)
            {
                return ZipArchiveMode.Read;
            }

            throw new ArgumentException(failMessage);
        }
        
        /// <summary>
        /// Checks that all mod files, library files and files copies exist in the archive.
        /// </summary>
        /// <param name="throwExceptions">If true, exceptions will be thrown when missing files are found. Otherwise, they will simply be removed from the manifest.</param>
        /// <exception cref="FileNotFoundException">If any stated mods, libraries, or file copies, do not exist and <paramref name="throwExceptions"/> is true</exception>
        private void VerifyStatedFilesExist(bool throwExceptions)
        {
            RemoveMissing("mod file", throwExceptions, _manifest.ModFileNames, name => name);
            RemoveMissing("library file", throwExceptions, _manifest.LibraryFileNames, name => name);
            RemoveMissing("file copy", throwExceptions, _manifest.FileCopies, fileCopy => fileCopy.Name);
        }

        /// <summary>
        /// Loads a QMOD's info from the given archive, and performs verification checks to make sure that it is valid.
        /// The QMOD owns the archive, and disposing the QMOD will dispose the underlying archive.
        /// Do not close the archive manually.
        ///
        /// The archive must not use ZipArchiveMode.Create, as this does not allow reading, specifically of the manifest.
        /// Create mode is only supported when creating a new QMOD.
        /// </summary>
        /// <param name="archive">Archive to load the QMOD from</param>
        /// <param name="failOnMissingStatedFile">Whether to throw if the manifest states a mod, library or file copy that doesn't exist in the archive</param>
        /// <param name="failOnMissingStatedCover">Whether to throw if the manifest states a cover that doesn't exist in the archive</param>
        /// <returns>The loaded QMOD</returns>
        /// <exception cref="InvalidModException">If the mod is missing its manifest</exception>
        /// <exception cref="ModMissingFileException">if a mod/lib/file copy is missing and <paramref name="failOnMissingStatedFile"/> is true, if a cover is in the manifest but not in the archive and <paramref name="failOnMissingStatedCover"/> is true</exception>
        /// <exception cref="ArgumentException">If the archive supplied uses ZipArchiveMode.Create</exception>
        public static async Task<QMod> ParseAsync(ZipArchive archive, bool failOnMissingStatedFile = true, bool failOnMissingStatedCover = false)
        {
            if (archive.Mode == ZipArchiveMode.Create)
            {
                throw new ArgumentException("Cannot create a QMOD using an archive in create mode");
            }
            
            ZipArchiveEntry? manifestEntry = archive.GetEntry(ManifestPath);
            if (manifestEntry == null)
            {
                throw new InvalidModException($"Mod archive did not contain a manifest at {ManifestPath}");
            }

            // Load the manifest as a QMod
            await using Stream manifestStream = manifestEntry.Open();
            QModManifest manifest = await QModManifest.ParseAsync(manifestStream);
            
            
            // If the mod states a cover image, and no file exists with that path..
            if (manifest.CoverImagePath != null && archive.GetEntry(manifest.CoverImagePath) == null)
            {
                if (failOnMissingStatedCover)
                {
                    throw new ModMissingFileException($"Mod stated cover image {manifest.CoverImagePath}, however this fle did not exist!");
                }
                // Set to a null cover image, since none exists
                manifest.CoverImagePath = null;
            }

            QMod qmod = new QMod(archive, manifest);
            
            // Remove missing mods/libraries/file copies (or throw an exception if specified)
            qmod.VerifyStatedFilesExist(failOnMissingStatedFile);

            return qmod;
        }

        /// <summary>
        /// Parses a QMOD from the given stream.
        /// The QMOD owns the stream, as it needs to keep the ZIP file open.
        /// Do not dispose the stream before disposing the QMOD.
        ///
        /// The stream must support reading at minimum.
        /// If the stream also supports seeking and writing, then the QMOD will be able to be modified.
        /// </summary>
        /// <param name="stream">Stream to load the archive from, must be seekable</param>
        /// <param name="archiveMode">The mode to load the archive with, if set to null, the "best" archive mode will be selected. This is Update if the stream supports reading, writing and seeking, and Read if reading is available. Create mode is not supported, as it does not support reading, so the manifest cannot be created</param>
        /// <param name="failOnMissingStatedFile">Whether to throw if the manifest states a mod, library or file copy that doesn't exist in the archive</param>
        /// <param name="failOnMissingStatedCover">Whether to throw if the manifest states a cover that doesn't exist in the archive</param>
        /// <returns>The loaded QMOD</returns>
        /// <exception cref="ArgumentException">If the stream does not support reading, or if archiveMode is set to Create</exception>
        /// <exception cref="FormatException">If the mod is missing its manifest</exception>
        /// <exception cref="FileNotFoundException">if a mod/lib/file copy is missing and <paramref name="failOnMissingStatedFile"/> is true, if a cover is in the manifest but not in the archive and <paramref name="failOnMissingStatedCover"/> is true.</exception>
        public static Task<QMod> ParseAsync(Stream stream, bool failOnMissingStatedFile = true, bool failOnMissingStatedCover = false, ZipArchiveMode? archiveMode = null)
        {
            // Load the QMOD as a ZIP file
            ZipArchive archive = new ZipArchive(stream, FindArchiveMode(stream, archiveMode, "Cannot parse a QMOD from a stream which does not support reading", "Cannot parse a QMOD using ZipArchiveMode.Create", true, false));

            return ParseAsync(archive, failOnMissingStatedFile, failOnMissingStatedCover);
        }

        /// <summary>
        /// Opens the cover image of the QMOD
        /// </summary>
        /// <returns>The stream for reading/writing to the cover</returns>
        /// <exception cref="NullReferenceException">If the cover image path is null</exception>
        public Stream OpenCoverImage()
        {
            if (CoverImagePath == null)
            {
                throw new NullReferenceException("Could not open the cover image as the CoverImagePath was null");
            }

            return OpenFile(CoverImagePath);
        }


        /// <summary>
        /// Opens the mod file with the given path.
        /// </summary>
        /// <param name="path">The path of the mod file to open</param>
        /// <returns>A stream for reading and writing to the mod file</returns>
        /// <exception cref="FileNotFoundException">If the given path does not point to a mod file</exception>
        public Stream OpenModFile(string path)
        {
            if (!_manifest.ModFileNames.Contains(path))
            {
                throw new FileNotFoundException($"Cannot open mod file {path} as it does not exist");
            }

            return OpenFile(path);
        }

        /// <summary>
        /// Deletes the mod file with the given path.
        /// </summary>
        /// <param name="path">The path of the mod file to delete</param>
        /// <exception cref="FileNotFoundException">If the given path does not point to a mod file</exception>
        public void DeleteModFile(string path)
        {
            if (!CanSaveManifest)
            {
                throw new InvalidOperationException("Cannot delete mod file - QMOD is read only");
            }
            
            if (!_manifest.ModFileNames.Contains(path))
            {
                throw new FileNotFoundException($"Cannot delete mod file {path} as it does not exist");
            }
            
            Archive.GetEntry(path)?.Delete();
            _manifest.ModFileNames.Remove(path);
        }

        /// <summary>
        /// Creates a mod file from the given stream.
        /// The stream will be copied into the entry for the mod file.
        /// If the mod file already exists, it will be overwritten.
        /// </summary>
        /// <param name="path">Path of the mod file</param>
        /// <param name="sourceData">The stream to load the mod file's data from</param>
        /// <exception cref="ArgumentException">If a non-mod file in the qmod already exists with the given path</exception>
        public async Task CreateModFileAsync(string path, Stream sourceData)
        {
            if (!CanSaveManifest)
            {
                throw new InvalidOperationException("Cannot create mod file - QMOD is read only");
            }
            
            ZipArchiveEntry? existing = Archive.GetEntry(path);
            if (existing != null)
            {
                if(ModFileNames.Contains(path))
                {
                    DeleteEntry(existing, true);
                }   
                else
                {
                    throw new ArgumentException($"Cannot create mod file with path {path} as it is already taken up by another file in the qmod");
                }
            }

            ZipArchiveEntry entry = Archive.CreateEntry(path);
            await using Stream modStream = entry.Open();
            await sourceData.CopyToAsync(modStream);
            if (!_manifest.ModFileNames.Contains(path))
            {
                _manifest.ModFileNames.Add(path);
            }
        }

        /// <summary>
        /// Opens the library file with the given path.
        /// </summary>
        /// <param name="path">The path of the library file to open</param>
        /// <returns>A stream for reading and writing to the library file</returns>
        /// <exception cref="FileNotFoundException">If the given path does not point to a library file</exception>
        public Stream OpenLibraryFile(string path)
        {
            if (!_manifest.LibraryFileNames.Contains(path))
            {
                throw new FileNotFoundException($"Cannot open library file {path} as it does not exist");
            }

            return OpenFile(path);
        }

        /// <summary>
        /// Deletes the library file with the given path.
        /// </summary>
        /// <param name="path">The path of the library file to delete</param>
        /// <exception cref="FileNotFoundException">If the given path does not point to a library file</exception>
        public void DeleteLibraryFile(string path)
        {
            if (!CanSaveManifest)
            {
                throw new InvalidOperationException("Cannot delete library file - QMOD is read only");
            }
            
            if (!_manifest.LibraryFileNames.Contains(path))
            {
                throw new FileNotFoundException($"Cannot delete library file {path} as it does not exist");
            }
            
            Archive.GetEntry(path)?.Delete();
            _manifest.LibraryFileNames.Remove(path);
        }

        /// <summary>
        /// Creates a library file from the given stream.
        /// The stream will be copied into the entry for the library file.
        /// If the library file already exists, it will be overwritten.
        /// </summary>
        /// <param name="path">Path of the library file within the QMOD</param>
        /// <param name="sourceData">The stream to load the library file's data from</param>
        /// <exception cref="ArgumentException">If a non-library file in the qmod already exists with the given path</exception>
        public async Task CreateLibraryFileAsync(string path, Stream sourceData)
        {
            if (!CanSaveManifest)
            {
                throw new InvalidOperationException("Cannot create library file - QMOD is read only");
            }
            
            ZipArchiveEntry? existing = Archive.GetEntry(path);
            if (existing != null)
            {
                if(LibraryFileNames.Contains(path))
                {
                    DeleteEntry(existing, true);
                }   
                else
                {
                    throw new ArgumentException($"Cannot create library file with path {path} as it is already taken up by another file in the qmod");
                }
            }

            ZipArchiveEntry entry = Archive.CreateEntry(path);
            await using Stream libStream = entry.Open();
            await sourceData.CopyToAsync(libStream);
            if (!_manifest.LibraryFileNames.Contains(path))
            {
                _manifest.LibraryFileNames.Add(path);
            }
        }
        
        /// <summary>
        /// Opens the given file copy for reading or writing.
        /// </summary>
        /// <param name="fileCopy">The file copy to open</param>
        /// <returns>A stream for reading and writing to the file copy's origin file</returns>
        /// <exception cref="FileNotFoundException">If the given file copy does not exist</exception>
        public Stream OpenFileCopy(FileCopy fileCopy)
        {
            if (!_manifest.FileCopies.Contains(fileCopy))
            {
                throw new FileNotFoundException($"Cannot open file copy with name {fileCopy.Name} as it does not exist");
            }

            return OpenFile(fileCopy.Name);
        }

        /// <summary>
        /// Removes the given file copy from the file copies list.
        /// If there are no other file copies with the file copy's origin file, the origin file will also be deleted.
        /// </summary>
        /// <param name="fileCopy">The file copy to delete</param>
        /// <exception cref="FileNotFoundException">If the given file copy does not exist</exception>
        public void RemoveFileCopy(FileCopy fileCopy)
        {
            if (!CanSaveManifest)
            {
                throw new InvalidOperationException("Cannot remove file copy - QMOD is read only");
            }
            
            if (!_manifest.FileCopies.Contains(fileCopy))
            {
                throw new FileNotFoundException($"Cannot remove file copy with name {fileCopy.Name} as it does not exist");
            }

            if (!_manifest.FileCopies.Any(copy => copy.Name == fileCopy.Name && copy != fileCopy))
            {
                DeleteEntry(Archive.GetEntry(fileCopy.Name));
            }
            _manifest.FileCopies.Remove(fileCopy);
        }

        /// <summary>
        /// Adds the given file copy to the manifest.
        /// If a file copy already exists with the same origin file name, it will be overwritten.
        /// </summary>
        /// <param name="fileCopy">File copy to add</param>
        /// <param name="sourceData">The stream to load the file copy's data from. Can be null if using the same origin file as an existing file copy.</param>
        /// <exception cref="ArgumentException">If another non-file-copy file already exists in the qmod with the name in the file copy, or if sourceData is null and no other file copy exists with the given origin file name</exception>
        public async Task AddFileCopyAsync(FileCopy fileCopy, Stream? sourceData = null)
        {
            if (!CanSaveManifest)
            {
                throw new InvalidOperationException("Cannot add file copy - QMOD is read only");
            }
            
            bool copyExistsWithSameOrigin = FileCopies.Any(copy => copy.Name == fileCopy.Name);
            
            // If no file copies already exist with the same origin file
            if (!copyExistsWithSameOrigin && sourceData == null)
            {
                // The source data cannot be null here, since otherwise we cannot create the file copy's origin file
                throw new ArgumentException($"No file copy existed with the origin file {fileCopy.Name}, and no stream was provided to create the origin file from");
            }

            if (sourceData != null)
            {
                ZipArchiveEntry? existingEntry = Archive.GetEntry(fileCopy.Name);
                if (existingEntry != null)
                {
                    if (copyExistsWithSameOrigin) // If this existing file is a file copy, we delete it in order to overwrite
                    {
                        DeleteEntry(existingEntry, true);
                    }
                    else
                    {
                        // Otherwise, we throw an exception as we don't want to overwrite unrelated files
                        throw new ArgumentException($"Cannot create file copy with origin file {fileCopy.Name}, as a file already exists in the qmod with this path");
                    }
                }

                ZipArchiveEntry entry = Archive.CreateEntry(fileCopy.Name);
                await using Stream originStream = entry.Open();
                await sourceData.CopyToAsync(originStream);
            }

            // We deliberately do not check the origin file name here, as multiple file copies are allowed to have the same origin file
            if (!_manifest.FileCopies.Contains(fileCopy))
            {
                _manifest.FileCopies.Add(fileCopy);
            }
        }
        
        /// <summary>
        /// Opens a file within the QMOD archive.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <param name="overrideCreate">Whether or not to open the entry even if the archive is set to ZipArchiveMode.Create</param>
        /// <returns>The stream of the file, ownership is passed to the caller</returns>
        /// <exception cref="FileNotFoundException">If the file does not exist</exception>
        /// <exception cref="InvalidModException">If the archive is set to ZipArchiveMode.Create and overrideCreate is false</exception>
        private Stream OpenFile(string path, bool overrideCreate = false)
        {
            if (ArchiveMode == ZipArchiveMode.Create && !overrideCreate)
            {
                throw new InvalidOperationException("Cannot open a file on a QMOD with an archive set to ZipArchiveMode.Create");
            }
            
            ZipArchiveEntry? entry = Archive.GetEntry(path);
            if (entry == null)
            {
                throw new FileNotFoundException($"Unable to find file with path {path} in QMOD");
            }

            return entry.Open();
        }

        /// <summary>
        /// Creates or overwrites the mod's cover image with the given name.
        /// </summary>
        /// <param name="name">Name of the cover image within the QMOD</param>
        /// <param name="coverStream">Stream to load the cover from</param>
        public async Task WriteCoverImageAsync(string name, Stream coverStream)
        {
            if (!CanSaveManifest)
            {
                throw new InvalidOperationException("Cannot write mod cover image, as the archive is read only");
            }
            
            // Make sure that the new cover doesn't already exist if we are changing its name
            if (name != CoverImagePath && Archive.GetEntry(name) != null)
            {
                throw new InvalidOperationException($"Cannot set cover image at path {name}, as a file already exists there");
            }

            // Remove the existing cover image
            if (CoverImagePath != null)
            {
                // This shouldn't be null, the code verifies that any cover image stated in the manifest actually exists, but to be on the safe side..
                Archive.GetEntry(CoverImagePath)?.Delete();
            }

            // Copy the stream into the cover entry
            ZipArchiveEntry newCoverEntry = Archive.CreateEntry(name);
            await using Stream stream = newCoverEntry.Open();
            await coverStream.CopyToAsync(stream);

            // We deliberately do not use the property setter here, as it handles renaming of cover images
            if (CoverImagePath != name)
            {
                _manifest.CoverImagePath = name;
                ManifestModified = true;
            }
        }

        private void DeleteEntry(ZipArchiveEntry? entry, bool isOverwriting = false)
        {
            if (entry == null) { return; }
            if (ArchiveMode == ZipArchiveMode.Create)
            {
                throw new InvalidOperationException(isOverwriting ? "Cannot overwrite file in a QMOD with an archive set to ZipArchiveMode.Create" : "Cannot delete a file in a QMOD with an archive set to ZipArchiveMode.Create");
            }
            entry.Delete();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) { return; }

            // Save the manifest if it has been modified
            if (disposing && ManifestModified)
            {
                using Stream manifestStream = GetManifestOverwritingStream();
                _manifest.Save(manifestStream);
            }
            
            Archive.Dispose();
            _disposed = true;
        }
        
        
        protected virtual async ValueTask DisposeAsyncCore()
        {
            // Save the manifest if it has been modified
            if (ManifestModified)
            {
                await using Stream manifestStream = GetManifestOverwritingStream();
                await _manifest.SaveAsync(manifestStream);
            }
        }

        private Stream GetManifestOverwritingStream()
        {
            Archive.GetEntry(ManifestPath)?.Delete();
            return Archive.CreateEntry(ManifestPath).Open();
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            
            Dispose(false);
            GC.SuppressFinalize(this);
        }
    }
}
