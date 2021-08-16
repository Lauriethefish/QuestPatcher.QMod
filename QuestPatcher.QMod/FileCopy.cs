namespace QuestPatcher.QMod
{
    /// <summary>
    /// Represents a QMOD file copy. (QMODs allow copying of files to arbitrary locations)
    /// </summary>
    public class FileCopy
    {
        /// <summary>
        /// The path of the file within the QMOD archive.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// The destination path to copy the file to.
        /// </summary>
        public string Destination { get; set; }

        /// <summary>
        /// Creates a new file copy.
        /// </summary>
        /// <param name="name">Name of the file within the QMOD archive</param>
        /// <param name="destination">Location to copy the file to</param>
        public FileCopy(string name, string destination)
        {
            Name = name;
            Destination = destination;
        }
    }
}