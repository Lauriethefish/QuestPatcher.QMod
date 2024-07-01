using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Range = SemanticVersioning.Range;

namespace QuestPatcher.QMod
{
    /// <summary>
    /// Represents a dependency in the QMOD manifest.
    /// Installers need to verify that a dependency is installed when installing a QMOD.
    /// If the dependency is not installed, it can be downloaded from the URL in the <see cref="DownloadIfMissing"/>, if there is one.
    /// Otherwise, installation should fail, or the user should be warned.
    /// </summary>
    public class Dependency
    {
        
        /// <summary>
        /// Mod ID of the dependency
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
        /// The string representing the semver version range of this dependency.
        /// Simply converts/parses the <see cref="VersionRange"/> property to/from a string.
        /// </summary>
        [JsonPropertyName("version")]
        public string VersionRangeString
        {
            get => VersionRange.ToString();
            set => VersionRange = Range.Parse(value);
        }

        [JsonPropertyName("required")]
        public bool Required { get; set; }

        /// <summary>
        /// Supported version range of the dependency. Installers should check that if the dependency is installed, it is within this range.
        ///
        /// If not, it should attempt to upgrade the dependency from the URL in the <see cref="DownloadIfMissing"/> property.
        /// However, this should only be done if there aren't any mods installed that will not work with the newer version of the dependency. (or at least, the user should be warned before upgrading the dependency)
        /// </summary>
        [JsonIgnore]
        public Range VersionRange { get; set; }
        
        [JsonIgnore]
        public Uri? DownloadIfMissing { get; set; }
        
        
        [JsonPropertyName("downloadIfMissing")]
        public string? DownloadUrlString
        { 
            get => DownloadIfMissing?.ToString();
            set  {
                if (value == null)
                {
                    DownloadIfMissing = null;
                }
                else
                {
                    try
                    {
                        DownloadIfMissing = new Uri(value);
                    }
                    catch (UriFormatException ex)
                    {
                        throw new ArgumentException($"Could not parse dependency URL {value}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new dependency on the given mod ID.
        /// Defaults to wildcard version range (works with all versions - *)
        /// </summary>
        /// <param name="id">The ID of the mod depended on, must not contain whitespace</param>
        /// <param name="versionRangeString">The semver version range of supported versions of the mod</param>
        /// <param name="downloadUrlString">The URL to download the dependency from if it is not installed, null for none (default)</param>
        /// <param name="required">If true, then this dependency must be installed within the correct version range for the mod to be installed.
        /// If false, then:
        /// - If the dependency is not installed, this mod can install with no further checks.
        /// - If the dependency is installed, it MUST be within the specified version range and the installer should attempt to upgrade it if necessary.</param>
        public Dependency(string id, string versionRangeString = "*", string? downloadUrlString = null, bool required = true)
        {
            Id = id;
            VersionRangeString = versionRangeString;
            DownloadUrlString = downloadUrlString;
            Required = required;
            
            // _id has been set by assigning the property.
            // We don't just assign the field directly as we need to check that the ID string is valid using the property setter
            Debug.Assert(_id != null);
            Debug.Assert(VersionRange != null); // Assigning VersionRangeString assigns the version range
        }
    }
}
