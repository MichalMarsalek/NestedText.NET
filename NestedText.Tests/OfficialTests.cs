using FluentAssertions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NestedText.Tests;

public class OfficialTests
{
    const string PATH = "official_tests/test_cases";
    [Theory]
    [OfficialTestsData(PATH)]
    public void Official(string name, string kind)
    {
        string path = Path.Combine(PATH, name);
        string? Read(string file)
        {
            string filePath = Path.Combine(path, file);
            string content = File.Exists(filePath) ? File.ReadAllText(filePath) : null;

            // On Windows the links are simply read as the name of the target file, so we follow the link manually
            return content == null ? null : Read(content) ?? content;
        }

        if (kind == "load")
        {
            string? loadIn = Read("load_in.nt");
            string? loadOut = Read("load_out.json");
            string? loadError = Read("load_err.json");

            if (loadIn != null)
            {
                var act = () => NestedTextSerializer.Deserialize<JsonNode>(loadIn);
                if (loadOut != null)
                {
                    var expected = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonNode>(loadOut));
                    var actual = JsonSerializer.Serialize(act());
                    actual.Should().BeEquivalentTo(expected);
                }
                else if (loadError != null)
                {
                    var expected = JsonSerializer.Deserialize<ErrorDescription>(loadError);
                    var actual = act.Should().Throw<NestedTextDeserializeException>().Which;
                    actual.Line.Should().Be(expected.LineNo + 1);
                    actual.Column.Should().Be(expected.ColNo + 1);
                }
            }
        }
        else if (kind == "dump")
        {

            string? dumpIn = Read("dump_in.json");
            string? dumpOut = Read("dump_out.nt");
            string? dumpError = Read("dump_err.json");

            if (dumpIn != null)
            {
                var act = () => NestedTextSerializer.Serialize<JsonNode>(dumpIn);
                if (dumpOut != null)
                {
                    var actual = act();
                    actual.Should().BeEquivalentTo(dumpOut);
                }
            }
        }
    }

    private class ErrorDescription
    {
        [JsonPropertyName("lineno")]
        public int LineNo { get; set; }
        [JsonPropertyName("colno")]
        public int ColNo { get; set; }
        public string Message { get; set; }
    }
}
