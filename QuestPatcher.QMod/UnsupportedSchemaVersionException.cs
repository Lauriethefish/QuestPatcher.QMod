using System;

namespace QuestPatcher.QMod
{
    /// <summary>
    /// Exception thrown when a QMOD is loaded with an unsupported schema version
    /// </summary>
    public class UnsupportedSchemaVersionException : InvalidModException
    {
        /// <summary>
        /// The version of the QMOD schema that was unsupported.
        /// </summary>
        public string Version { get; }

        internal UnsupportedSchemaVersionException(string version) : base($"Unsupported QMOD schema version {version}. Upgrade your mod installer!")
        {
            Version = version;
        }
    }
}