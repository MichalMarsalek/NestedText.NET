using System.Reflection;
using Xunit.Sdk;

namespace NestedText.Tests
{
    public class OfficialTestsDataAttribute : DataAttribute
    {
        public string TestPath { get; set; }
        public OfficialTestsDataAttribute(string path)
        {
            TestPath = path;
        }
        public override IEnumerable<object[]>? GetData(MethodInfo testMethod)
        {
            string[] kinds = ["load", "dump"];
            return Directory.GetDirectories(TestPath)
                .SelectMany(directory =>
                {
                    var files = Directory.GetFiles(directory);
                    return kinds.Where(kind => files.Any(file => file.Contains(kind)))
                        .Select(kind => new object[] { Path.GetFileName(directory), kind });
                });
        }
    }
}
