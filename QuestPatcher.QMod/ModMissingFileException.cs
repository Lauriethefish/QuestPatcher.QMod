namespace QuestPatcher.QMod
{
    /// <summary>
    /// Thrown when a mod is missing a file, and loading is set to throw when a mod is missing a stated file.
    /// </summary>
    public class ModMissingFileException : InvalidModException
    {
        public ModMissingFileException(string message) : base(message) {}
    }
}