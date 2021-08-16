using System;
using System.Linq;

namespace QuestPatcher.QMod
{
    internal static class StringExtensions
    {
        internal static bool ContainsWhitespace(this string str) => str.Any(Char.IsWhiteSpace);
    }
}