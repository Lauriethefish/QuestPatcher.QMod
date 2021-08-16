using System;

namespace QuestPatcher.QMod
{
    /// <summary>
    /// Root class for QMOD format errors
    /// </summary>
    public class InvalidModException : FormatException
    {
        public InvalidModException(string message, Exception cause) : base(message, cause) {}
        
        public InvalidModException(string message) : base(message) {}
    }
}