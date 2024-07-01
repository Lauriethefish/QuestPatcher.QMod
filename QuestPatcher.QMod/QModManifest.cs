using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Json.Schema;
using Version = SemanticVersioning.Version;

namespace QuestPatcher.QMod
{
    /// <summary>
    /// Represents the <code>mod.json</code> manifest of a <code>.qmod</code> file.
    /// </summary>
    public class QModManifest
    {
        /// <summary>
        /// Version of the QMOD format that this mod was designed for
        /// </summary>
        [JsonPropertyName("_QPVersion")]
        public string SchemaVersion { get; private set; }

        /// <summary>
        /// An ID for the mod.
        /// Two mods with the same ID cannot be installed
        /// </summary>
        public string Id
        {
            get => _id;
            set
            {
                if (_id == value) { return; }
                if (value.ContainsWhitespace()) { throw new ArgumentException($"Cannot set ID of QMOD to a value containing whitespace ({value})"); }
                _id = value;
            }
        }
        private string _id;

        /// <summary>
        /// A human-readable name for the mod
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The author of the mod
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// The string representing the semver version of this mod.
        /// Simply converts/parses the <see cref="Version"/> property to/from a string.
        /// </summary>
        [JsonPropertyName("version")]
        public string? VersionString
        {
            get => Version.ToString();
            set => Version = Version.Parse(value);
        }

        /// <summary>
        /// Version of the mod.
        /// </summary>
        [JsonIgnore]
        public Version Version { get; set; }
        
        /// <summary>
        /// The package ID of the app that the mod is designed for.
        /// If null, this means that the mod works for any app.
        /// </summary>
        public string? PackageId { get; set; }

        /// <summary>
        /// The version of the app that the mod is designed for.
        /// If null, this means that the mod doesn't depend on a particular version of an app.
        /// This property is redundant if <see cref="PackageId"/> is null.
        /// </summary>
        public string? PackageVersion { get; set; }

        /// <summary>
        /// Whether or not the mod is a library mod.
        /// Library mods should be automatically uninstalled whenever no mods that depend on them are installed
        /// </summary>
        public bool IsLibrary { get; set; }

        /// <summary>
        /// Modloader this mod is made for. Either of 'QuestLoader' or 'Scotland2'
        /// </summary>
        [JsonPropertyName("modloader")]
        [JsonConverter(typeof(ModLoaderJsonConverter))]
        public ModLoader ModLoader { get; set; } = ModLoader.QuestLoader;

        /// <summary>
        /// Files copied to the mod loader's early mods directory
        /// </summary>
        [JsonPropertyName("modFiles")]
        public List<string> ModFileNames { get; set; } = new List<string>();


        /// <summary>
        /// Files copied to the mod loader's late mods directory
        /// </summary>
        [JsonPropertyName("lateModFiles")]
        public List<string> LateModFileNames { get; set; } = new List<string>();
        
        /// <summary>
        /// Files copied to the mod loader's libraries directory.
        /// Ideally, when uninstalling mods, these files should only be removed if no other installed/enabled mod has a library with the same name.
        /// </summary>
        [JsonPropertyName("libraryFiles")]
        public List<string> LibraryFileNames { get; set; } = new List<string>();

        /// <summary>
        /// Files copied to arbitrary locations
        /// </summary>
        public List<FileCopy> FileCopies { get; set; } = new List<FileCopy>();

        /// <summary>
        /// Dependencies of the mod
        /// </summary>
        public List<Dependency> Dependencies { get; set; } = new List<Dependency>();

        /// <summary>
        /// File copy extensions to be registered by this mod
        /// </summary>
        public List<CopyExtension> CopyExtensions { get; set; } = new List<CopyExtension>();
        
        /// <summary>
        /// A short description of what the mod does
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Path of the cover image that should be inside the QMOD archive
        /// </summary>
        [JsonPropertyName("coverImage")]
        public string? CoverImagePath { get; set; }
        
        /// <summary>
        /// If the mod was ported from another platform, this is the author of the port.
        /// </summary>
        public string? Porter { get; set; }


        private static readonly JsonSchema Schema;

        /// <summary>
        /// QMOD schema versions that are loadable by this class.
        /// TODO: Load these from the schema instead of hard coding them, since this actually defines which are permitted
        /// </summary>
        private static readonly HashSet<string> SupportedSchemaVersions = new[]
        {
            "0.1.0",
            "0.1.1",
            "0.1.2",
            "1.0.0",
            "1.1.0",
            "1.2.0"
        }.ToHashSet();

        private const string LatestSchemaVersion = "1.0.0";

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        /// <summary>
        /// Saves this manifest to the given stream.
        /// </summary>
        /// <param name="stream">The stream to save to</param>
        public Task SaveAsync(Stream stream)
        {
            return JsonSerializer.SerializeAsync(stream, this, SerializerOptions);
        }

        /// <summary>
        /// Saves this manifest to the given stream.
        /// </summary>
        /// <param name="stream">The stream to save to</param>
        public void Save(Stream stream)
        {
            Utf8JsonWriter jsonWriter = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(jsonWriter, this, SerializerOptions);
        }

        /// <summary>
        /// Creates a new manifest.
        /// </summary>
        /// <param name="id">ID of the mod, cannot contain whitespace</param>
        /// <param name="name">Human readable name of the mod</param>
        /// <param name="version">Semver Version of the mod</param>
        /// <param name="packageId">ID of the Android app this mod is intended for</param>
        /// <param name="packageVersion">Version of the Android ap this mod is intended for</param>
        /// <param name="author">Author of the mod</param>
        public QModManifest(string id, string name, Version version, string? packageId, string? packageVersion, string author)
        {
            SchemaVersion = LatestSchemaVersion;
            
            Id = id;
            Name = name;
            Version = version;
            PackageId = packageId;
            PackageVersion = packageVersion;
            Author = author;
            
            // _id has been set by assigning the property.
            // We don't just assign the field directly as we need to check that the ID string is valid using the property setter
            Debug.Assert(_id != null);
        }
        
        /// <summary>
        /// Creates a new <see cref="QModManifest"/> with a deep clone.
        /// </summary>
        /// <returns>A deep clone of this manifest</returns>
        public QModManifest DeepClone()
        {
            return new QModManifest(Id, Name, Version, PackageId, PackageVersion, Author)
            {
                IsLibrary = IsLibrary,
                Description = Description,
                CoverImagePath = CoverImagePath,
                Porter = Porter,
                SchemaVersion = SchemaVersion,
                FileCopies = FileCopies.Select(copy => new FileCopy(copy.Name, copy.Destination)).ToList(),
                Dependencies = Dependencies.Select(dep => new Dependency(dep.Id, dep.VersionRangeString, dep.DownloadUrlString)).ToList(),
                CopyExtensions = CopyExtensions.Select(ext => new CopyExtension(ext.Extension, ext.Destination)).ToList(),
                ModFileNames = ModFileNames.ToList(),
                LateModFileNames = LateModFileNames.ToList(),
                LibraryFileNames = LibraryFileNames.ToList(),
                ModLoader = ModLoader
            };
        }

        /// <summary>
        /// Creates a new <see cref="QModManifest"/> with a shallow copy
        /// </summary>
        /// <returns>A shallow clone of this manifest</returns>
        public QModManifest ShallowClone()
        {
            return (QModManifest) MemberwiseClone();
        }

        /// <summary>
        /// Creates a new manifest.
        /// </summary>
        /// <param name="id">ID of the mod, cannot contain whitespace</param>
        /// <param name="name">Human readable name of the mod</param>
        /// <param name="versionString">Version of the mod, must comply with semver</param>
        /// <param name="packageId">ID of the Android app this mod is intended for</param>
        /// <param name="packageVersion">Version of the Android ap this mod is intended for</param>
        /// <param name="author">Author of the mod</param>
        [JsonConstructor]
        public QModManifest(string id, string name, string versionString, string packageId, string packageVersion, string author) : this(id, name, new Version(versionString), packageId, packageVersion, author) { }

        /// <summary>
        /// Creates a new manifest.
        /// Sets the name of the mod to the same as the ID.
        /// </summary>
        /// <param name="id">ID of the mod, cannot contain whitespace</param>
        /// <param name="version">Version of the mod, must comply with semver</param>
        /// <param name="packageId">ID of the Android app this mod is intended for</param>
        /// <param name="packageVersion">Version of the Android ap this mod is intended for</param>
        /// <param name="author">Author of the mod</param>
        public QModManifest(string id, string version, string packageId, string packageVersion, string author) : this(id, id, version, packageId, packageVersion, author) { }

        static QModManifest()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? schemaStream = assembly.GetManifestResourceStream("QuestPatcher.QMod.Resources.qmod.schema.json");
            if (schemaStream == null)
            {
                throw new FileNotFoundException("Could not find qmod schema in assembly resources");
            }

            // TODO: It might be better to lazily (and asynchronously) load the schema to avoid the .Result
            Schema = JsonSchema.FromStream(schemaStream).Result;
        }

        /// <summary>
        /// Parses a <see cref="QModManifest"/> from the given stream.
        /// Validates it against the QMOD schema.
        /// </summary>
        /// <param name="stream">The stream to load from</param>
        /// <exception cref="InvalidModException">If the JSON does not adhere to the QMOD schema, or there is invalid JSON</exception>
        /// <exception cref="UnsupportedSchemaVersionException">If the given QMOD's schema version isn't supported by this library</exception>
        /// <returns>The loaded manifest</returns>
        public static async Task<QModManifest> ParseAsync(Stream stream)
        {
            TextReader reader = new StreamReader(stream);
            
            // TODO: Find a way to validate the schema while reading directly from the stream instead of reading as a string first and loading as a JsonDocument, which is inefficient.
            string manifestString = await reader.ReadToEndAsync();

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(manifestString);
            }
            catch (JsonException ex)
            {
                throw new InvalidModException("Invalid JSON in manifest", ex);
            }
            
            // Validate that the document matches the QMOD schema
            EvaluationResults results = Schema.Evaluate(document.RootElement);
            
            if (!results.IsValid)
            {
                
                // If the root element contains a _QPVersion element with an incorrect version, this may be why schema validation failed.
                // We want to provide a more descriptive error for this.
                if (document.RootElement.TryGetProperty("_QPVersion", out JsonElement versionElement))
                {
                    string? version = versionElement.GetString();
                    
                    // Check that the version is not null, and that it's actually the cause of the validation failure
                    if (version != null && !SupportedSchemaVersions.Contains(version))
                    {
                        throw new UnsupportedSchemaVersionException(version);
                    }
                }

                // TODO: results.Errors is always empty. Maybe we need to recursively search for errors?
                var errors = new StringBuilder();
                if(results.Errors != null)
                {
                    foreach(var pair in results.Errors)
                    {
                        errors.AppendLine($"{pair.Key}: {pair.Value}");
                    }
                }

                throw new InvalidModException($"QMOD schema validation failed: {errors}");
            }
            
            // Now we attempt to parse the QMOD
            QModManifest? result = JsonSerializer.Deserialize<QModManifest>(manifestString, SerializerOptions);
            
            // This should never happen, the schema should detect it above
            if (result == null)
            {
                throw new InvalidModException("No root object found in QMOD manifest");
            }

            return result;
        }
    }
}
