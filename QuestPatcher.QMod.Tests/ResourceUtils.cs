using System.IO;
using System.Reflection;

namespace QuestPatcher.QMod.Tests
{
    public static class ResourceUtils
    {
        private const string ResourcePrefix = "QuestPatcher.QMod.Tests.Resources.";
        private static Assembly _executingAssembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// Opens the resource in the tests assembly with the given name.
        /// </summary>
        /// <param name="name">The name of the resource to open</param>
        /// <returns>A stream to read from the resource</returns>
        public static Stream OpenResource(string name)
        {
            return _executingAssembly.GetManifestResourceStream(ResourcePrefix + name);
        }
    }
}