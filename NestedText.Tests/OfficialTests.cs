using FluentAssertions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestedText.Tests;

public class OfficialTests
{
    const string PATH = "official_tests/test_cases";
    [Theory]
    [OfficialTestsData(PATH)]
    public void Official(string name)
    {
        string path = Path.Combine(PATH, name);
        string? Read(string file)
        {
            string filePath = Path.Combine(path, file);
            return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
        }
        string? loadIn = Read("load_in.nt");
        string? loadOut = Read("load_out.json");
        string? loadError = Read("load_err.json");
        string? dumpIn = Read("dump_in.json");
        string? dumpOut = Read("dump_out.nt");
        // We are not testing dump errors since we are statically typed.

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
                var exception = act.Should().Throw<NestedTextDeserializeException>().Which;
                exception.Line.Should().Be(expected.LineNo + 1);
                exception.Column.Should().Be(expected.ColNo + 1);
            }
        }
    }

    private class ErrorDescription
    {
        public int LineNo { get; set; }
        public int ColNo { get; set; }
        public string Message { get; set; }
    }
}
