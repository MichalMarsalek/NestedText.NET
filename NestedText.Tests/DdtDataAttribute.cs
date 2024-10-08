﻿using System.Reflection;
using Xunit.Sdk;

namespace NestedText.Tests
{
    public class DdtDataAttribute : DataAttribute
    {
        public string TestPath { get; set; }
        public DdtDataAttribute(string path)
        {
            TestPath = path;
        }
        public override IEnumerable<object[]>? GetData(MethodInfo testMethod)
        {
            string[] kinds = ["load", "dump", "format", "parsemit"];
            return Directory.GetDirectories(TestPath)
                .SelectMany(directory =>
                {
                    var files = Directory.GetFiles(directory);
                    return kinds.Where(kind => files.Any(file => kind == "parsemit" ? file.Contains("load") || file.Contains("format") : file.Contains(kind)))
                        .Select(kind => new object[] { Path.GetFileName(directory), kind });
                });
        }
    }
}
