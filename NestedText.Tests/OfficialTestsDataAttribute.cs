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
            return Directory.GetDirectories(TestPath).Select(x => new object[] { Path.GetFileName(x) });
        }
    }
}
