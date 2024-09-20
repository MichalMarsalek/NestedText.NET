using FluentAssertions;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NestedText.Tests;

public class DataDrivenTests
{
    const string OFFICIAL_TESTS_PATH = "official_tests/test_cases";
    const string CUSTOM_TESTS_PATH = "custom_tests/test_cases";

    [Theory]
    [DdtData(OFFICIAL_TESTS_PATH)]
    public void Official(string name, string kind) => DdtTheoryImplementation(OFFICIAL_TESTS_PATH, name, kind);

    [Theory]
    [DdtData(CUSTOM_TESTS_PATH)]
    public void Custom(string name, string kind) => DdtTheoryImplementation(CUSTOM_TESTS_PATH, name, kind);

    private void DdtTheoryImplementation(string path, string name, string kind)
    {
        path = Path.Combine(path, name);
        string? Read(string file)
        {
            string filePath = Path.Combine(path, file);
            string content = File.Exists(filePath) ? File.ReadAllText(filePath) : null;

            // On Windows the links are simply read as the name of the target file, so we follow the link manually
            return content == null ? null : Read(content) ?? content;
        }

        T? ReadJson<T>(string file)
        {
            var content = Read(file);
            if (content == null)
            {
                return default;
            }
            // hack to fix newlines inside of json strings
            content = content.Replace("\\n", Environment.NewLine.Replace("\n", "\\n").Replace("\r", "\\r"));
            return content == null ? default : JsonSerializer.Deserialize<T>(content);
        }

        if (kind == "load")
        {
            var loadIn = Read("load_in.nt");
            var loadOut = ReadJson<JsonNode>("load_out.json");
            var loadError = ReadJson<ErrorDescription>("load_err.json");

            if (loadIn != null)
            {
                var act = () => NestedTextSerializer.Deserialize<JsonNode>(loadIn, new() { ParseTaglessStringLines = false });
                if (loadOut != null)
                {
                    var expected = JsonSerializer.Serialize(loadOut);
                    var actual = JsonSerializer.Serialize(act());
                    actual.Should().BeEquivalentTo(expected);
                }
                else if (loadError != null)
                {
                    var actual = act.Should().Throw<NestedTextDeserializeException>().Which;
                    actual.Line.Should().Be(loadError.LineNo + 1);
                    actual.Column.Should().Be((loadError.ColNo ?? 0) + 1);
                }
            }
        }
        else if (kind == "dump")
        {
            var dumpIn = ReadJson<JsonNode>("dump_in.json");
            var dumpOut = Read("dump_out.nt")?.GetLines().JoinLines();
            var dumpError = ReadJson<ErrorDescription>("dump_err.json");
            var dumpOptions = ReadJson<NestedTextSerializerOptions>("dump_options.json") ?? new() { MaxDepthToInline = 0, Indentation = 4 };

            if (dumpIn != null)
            {
                var act = () => NestedTextSerializer.Serialize(dumpIn, dumpOptions);
                if (dumpOut != null)
                {
                    act().Should().BeEquivalentTo(dumpOut);
                }
            }
        }
        else if (kind == "format")
        {
            var formatIn = Read("format_in.nt");
            var formatOut = Read("format_out.nt")?.GetLines().JoinLines();
            var formatOptions = ReadJson<NestedTextSerializerOptions>("format_options.json") ?? new() { MaxDepthToInline = 0, Indentation = 4 };

            if (formatIn != null && formatOut != null)
            {
                NestedTextSerializer.Format(formatIn, formatOptions).Should().BeEquivalentTo(formatOut);
            }
        }
    }

    private class ErrorDescription
    {
        [JsonPropertyName("lineno")]
        public int LineNo { get; set; }
        [JsonPropertyName("colno")]
        public int? ColNo { get; set; }
        public string Message { get; set; }
    }
}
