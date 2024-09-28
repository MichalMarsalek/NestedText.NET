using FluentAssertions;
using System.Text.Json;

namespace NestedText.Tests;

public class NestedTextSerializerTests
{
    private readonly string NL = Environment.NewLine;
    [Fact]
    public void Deserialize_OnOfficers_ReturnsObject()
    {
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
        var res = NestedTextSerializer.Deserialize<Dictionary<string, Officer>>(input, new() { UseDefaultConventions = true });
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
        var actual = NestedTextSerializer.Serialize(0, new() { UseDefaultConventions = true });
        actual.Should().BeEquivalentTo("> 0" + NL);
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
        var actual = NestedTextSerializer.Deserialize<int>("> 0\n", new() { UseDefaultConventions = true });
        actual.Should().Be(0);
    }

    [Fact]
    public void Deserialize_UnterminatedWithDefaultOptions_Throws()
    {
        Func<string> act = () => NestedTextSerializer.Deserialize<string>("> x");
        act.Should().Throw<NestedTextDeserializeException>();
    }

    [Fact]
    public void Deserialize_UnterminatedWithoutThrowOnUnterminated_Returns()
    {
        var actual = NestedTextSerializer.Deserialize<string>("> x", new() { ThrowOnUnterminated = false });
        actual.Should().BeEquivalentTo("x");
    }

    private readonly Model1 testObject1 = new()
    {
        TextProperty = "x",
        IntProperty = 3,
        FloatProperty = 3.14f,
        BoolProperty = true,
        EnumProperty = Enum1.FirstEnumMember
    };

    private readonly string testObjectNt1 = """
        text property: x
        int property: 3
        float property: 3.14
        bool property: true
        enum property: first enum member

        """;

    [Fact]
    public void Serialize_ObjectWithScalarPropsWithUseDefaultConverters_ReturnsObject()
    {
        var actual = NestedTextSerializer.Serialize(testObject1, new() { UseDefaultConventions = true });
        actual.Should().BeEquivalentTo(testObjectNt1.GetLines().JoinLines());
    }

    [Fact]
    public void Deserialize_ObjectWithScalarPropsWithUseDefaultConverters_ReturnsObject()
    {
        var actual = NestedTextSerializer.Deserialize<Model1>(testObjectNt1, new() { UseDefaultConventions = true });
        actual.Should().BeEquivalentTo(testObject1);
    }

    [Fact]
    public void Deserialize_EmptyDocumentAsObject_ReturnsEmptyObject()
    {
        var actual = NestedTextSerializer.Deserialize<Model2>(NL);
        actual.Should().BeEquivalentTo(new Model2());
    }

    [Fact]
    public void Deserialize_EmptyDocumentAsObjectWhichHasRequiredProperties_ReturnsNull()
    {
        var actual = NestedTextSerializer.Deserialize<Model1>(NL);
        actual.Should().BeEquivalentTo(null as Model1);
    }

    [Fact]
    public void Deserialize_EmptyDocumentAsString_ReturnsEmptyString()
    {
        var actual = NestedTextSerializer.Deserialize<string>(NL);
        actual.Should().BeEquivalentTo("");
    }

    [Fact]
    public void Deserialize_EmptyDocumentAsList_ReturnsEmptyList()
    {
        var actual = NestedTextSerializer.Deserialize<List<Model2>>(NL);
        actual.Should().BeEquivalentTo(new List<Model2>());
    }

    [Fact]
    public void Deserialize_EmptyDocumentAsDictionary_ReturnsEmptyDictionary()
    {
        var actual = NestedTextSerializer.Deserialize<Dictionary<string,string>>(NL);
        actual.Should().BeEquivalentTo(new Dictionary<string, string>());
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

internal class Model1
{
    public required string TextProperty { get; set; }
    public required int IntProperty { get; set; }
    public required float FloatProperty { get; set; }
    public required bool BoolProperty { get; set; }
    public required Enum1 EnumProperty { get; set; }
}

internal class Model2
{
    public string? Property { get; set; }
}

internal enum Enum1 { FirstEnumMember, SecondEnumMember };