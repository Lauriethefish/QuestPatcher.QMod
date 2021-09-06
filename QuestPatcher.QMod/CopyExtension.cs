namespace QuestPatcher.QMod
{
    /// <summary>
    /// Represents a QMOD file copy extension.
    /// These allow a mod to register with the mod installer that certain file types should be copied to a particular directory when imported.
    /// For instance, a cosmetic mod may want its cosmetic files to be imported to its own ModData directory.
    /// </summary>
    public class CopyExtension
    {
        /// <summary>
        /// The file extension to register, without a period prefix!
        /// </summary>
        public string Extension { get; set; }

        /// <summary>
        /// The folder to copy the files with the extension to
        /// </summary>
        public string Destination { get; set; }

        /// <summary>
        /// Creates a new copy extension.
        /// </summary>
        /// <param name="extension">The file extension to register, without a period prefix!</param>
        /// <param name="destination">The folder to copy the files with the extension to</param>
        public CopyExtension(string extension, string destination)
        {
            Extension = extension;
            Destination = destination;
        }
    }
}
