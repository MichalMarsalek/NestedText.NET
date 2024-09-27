using FluentAssertions;
using System.Text.Json;

namespace NestedText.Tests;

public class NestedTextSerializerTests
{
    [Fact]
    public void Deserialize_OnOfficers_ReturnsObject()
    {
        var NL = Environment.NewLine;
        const string input = """
            # Contact information for our officers

            Katheryn McDaniel:
                position: president
                address:
                    > 138 Almond Street
                    > Topeka, Kansas 20697
                phone:
                    cell: 1-210-555-5297
                    work: 1-210-555-3423
                    home: 1-210-555-8470
                        # Katheryn prefers that we always call her on her cell phone.
                email: KateMcD@aol.com
                kids:
                    - Joanie
                    - Terrance

            Margaret Hodge:
                position: vice president
                address:
                    > 2586 Marigold Lane
                    > Topeka, Kansas 20697
                phone:
                    {cell: 1-470-555-0398, home: 1-470-555-7570}
                email: margaret.hodge@ku.edu
                kids:
                    [Arnie, Zach, Maggie]

            """;
        Dictionary<string, Officer> expected = new()
        {
            ["Katheryn McDaniel"] = new Officer
            {
                Position = "president",
                Address = $"138 Almond Street{NL}Topeka, Kansas 20697",
                Phone = new()
                {
                    Cell = "1-210-555-5297",
                    Work = "1-210-555-3423",
                    Home = "1-210-555-8470",
                },
                Email = "KateMcD@aol.com",
                Kids = ["Joanie", "Terrance"]
            },
            ["Margaret Hodge"] = new Officer
            {
                Position = "vice president",
                Address = $"2586 Marigold Lane{NL}Topeka, Kansas 20697",
                Phone = new()
                {
                    Cell = "1-470-555-0398",
                    Home = "1-470-555-7570",
                },
                Email = "margaret.hodge@ku.edu",
                Kids = ["Arnie", "Zach", "Maggie"]
            }
        };
        var res = NestedTextSerializer.Deserialize<Dictionary<string, Officer>>(input, null, new(JsonSerializerDefaults.Web));
        res.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Serialize_IntWithDefaultOptions_Throws()
    {
        Func<string> act = () => NestedTextSerializer.Serialize(0);
        act.Should().Throw<NestedTextSerializeException>();
    }

    [Fact]
    public void Serialize_IntWithUseDefaultConverters_ReturnsAsString()
    {
        var actual = NestedTextSerializer.Serialize(0, new() { UseDefaultConverters = true });
        actual.Should().BeEquivalentTo("> 0\n");
    }

    [Fact]
    public void Deserialize_IntWithDefaultOptions_Throws()
    {
        Func<int> act = () => NestedTextSerializer.Deserialize<int>("> 0\n");
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_IntWithUseDefaultConverters_ReturnsAsString()
    {
        var actual = NestedTextSerializer.Deserialize<int>("> 0\n", new() { UseDefaultConverters = true });
        actual.Should().Be(0);
    }

    [Fact]
    public void Deserialize_UnterminatedWithDefaultOptions_Throws()
    {
        Func<string> act = () => NestedTextSerializer.Deserialize<string>("> x");
        act.Should().Throw<NestedTextDeserializeException>();
    }

    [Fact]
    public void Deserialize_UnterminatedWithoutThrowOnUnterminated_Throws()
    {
        var actual = NestedTextSerializer.Deserialize<string>("> x", new() { ThrowOnUnterminated = false });
        actual.Should().BeEquivalentTo("x");
    }
}

internal class Officer
{
    public required string Position { get; set; }
    public required string Address { get; set; }
    public PhoneNumbers Phone { get; set; } = new();
    public required string Email { get; set; }
    public List<string> Kids { get; set; } = [];
}
internal class PhoneNumbers
{
    public string? Cell { get; set; }
    public string? Work { get; set; }
    public string? Home { get; set; }
}
