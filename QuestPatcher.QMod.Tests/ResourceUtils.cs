using System;
using System.IO;
using System.Reflection;

namespace QuestPatcher.QMod.Tests
{
    public static class ResourceUtils
    {
        private const string ResourcePrefix = "QuestPatcher.QMod.Tests.Resources.";
        private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// Opens the resource in the tests assembly with the given name.
        /// </summary>
        /// <param name="name">The name of the resource to open</param>
        /// <returns>A stream to read from the resource</returns>
        public static Stream OpenResource(string name)
        {
            Stream? result = ExecutingAssembly.GetManifestResourceStream(ResourcePrefix + name);
            if(result == null)
            {
                throw new NullReferenceException($"No resource exists with name {name}");
            }

            return result;
        }
    }
}
