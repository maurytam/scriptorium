using FluentAssertions;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class EntityReplyParserTests
{
    [Fact]
    public void Parse_OneLinePerKind_GivesFiveCandidatesOfTheRightTypes()
    {
        var reply = EntityReplyParser.Parse(
            "person: Mario Rossi\norganization: Alfa Srl\ndate: 12 March 2026\namount: 15,000 euro\nplace: Turin");

        reply.Kind.Should().Be(EntityReplyKind.Entities);
        reply.Candidates.Should().Equal(
            new EntityCandidate(EntityType.Person, "Mario Rossi"),
            new EntityCandidate(EntityType.Organization, "Alfa Srl"),
            new EntityCandidate(EntityType.Date, "12 March 2026"),
            new EntityCandidate(EntityType.Amount, "15,000 euro"),
            new EntityCandidate(EntityType.Place, "Turin"));
    }

    [Theory]
    [InlineData("PERSON: Mario", EntityType.Person)]
    [InlineData("People: Mario", EntityType.Person)]
    [InlineData("persons: Mario", EntityType.Person)]
    [InlineData("Organizations: Alfa", EntityType.Organization)]
    [InlineData("ORGANISATION: Alfa", EntityType.Organization)]
    [InlineData("Dates: 5 May", EntityType.Date)]
    [InlineData("Amounts: 10 euro", EntityType.Amount)]
    [InlineData("PLACES: Turin", EntityType.Place)]
    public void Parse_TheKindIsReadInAnyLetterCaseSingularOrPlural(string line, EntityType expected)
    {
        var reply = EntityReplyParser.Parse(line);

        reply.Kind.Should().Be(EntityReplyKind.Entities);
        reply.Candidates.Should().ContainSingle().Which.Type.Should().Be(expected);
    }

    [Theory]
    [InlineData("- person: Mario Rossi")]
    [InlineData("* person: Mario Rossi")]
    [InlineData("1. person: Mario Rossi")]
    [InlineData("2) person: Mario Rossi")]
    [InlineData("   person:    Mario Rossi   ")]
    public void Parse_ABulletANumberAndSpacesAroundAreDropped(string line)
    {
        var reply = EntityReplyParser.Parse(line);

        reply.Candidates.Should().Equal(new EntityCandidate(EntityType.Person, "Mario Rossi"));
    }

    [Fact]
    public void Parse_ALineIsSplitAtTheFirstColonOnly()
    {
        var reply = EntityReplyParser.Parse("date: 5 May, 10:30\namount: 15,000 euro: total");

        reply.Candidates.Should().Equal(
            new EntityCandidate(EntityType.Date, "5 May, 10:30"),
            new EntityCandidate(EntityType.Amount, "15,000 euro: total"));
    }

    [Fact]
    public void Parse_BlankLinesLinesWithoutAColonUnknownKindsAndEmptyTextsAreIgnored()
    {
        var reply = EntityReplyParser.Parse(
            "\nperson: Mario Rossi\n\njust some words\nemail: mario@example.com\nplace:\nplace:    \norganization: Alfa Srl");

        reply.Kind.Should().Be(EntityReplyKind.Entities);
        reply.Candidates.Should().Equal(
            new EntityCandidate(EntityType.Person, "Mario Rossi"),
            new EntityCandidate(EntityType.Organization, "Alfa Srl"));
    }

    [Fact]
    public void Parse_AFriendlySentenceAndMarkdownFencesAroundTheLinesDoNotMatter()
    {
        var reply = EntityReplyParser.Parse(
            "Sure! Here are the entities:\n```\nperson: Mario Rossi\nplace: Turin\n```\nHope this helps.");

        reply.Kind.Should().Be(EntityReplyKind.Entities);
        reply.Candidates.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_WindowsLineBreaksWork()
    {
        var reply = EntityReplyParser.Parse("person: Mario Rossi\r\nplace: Turin\r\n");

        reply.Candidates.Should().Equal(
            new EntityCandidate(EntityType.Person, "Mario Rossi"),
            new EntityCandidate(EntityType.Place, "Turin"));
    }

    [Theory]
    [InlineData("NONE")]
    [InlineData("none")]
    [InlineData("None.")]
    [InlineData("  \n NONE \n ")]
    public void Parse_AReplyThatIsOnlyNone_IsNone(string text)
    {
        var reply = EntityReplyParser.Parse(text);

        reply.Kind.Should().Be(EntityReplyKind.None);
        reply.Candidates.Should().BeEmpty();
    }

    [Theory]
    [InlineData("I could not find anything in this document, sorry.")]
    [InlineData("{\"people\": [\"Mario Rossi\"]}")]
    [InlineData("There are no entities of the requested kinds.")]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_AReplyWithNoReadableLineThatIsNotNone_IsUnreadable(string text)
    {
        var reply = EntityReplyParser.Parse(text);

        reply.Kind.Should().Be(EntityReplyKind.Unreadable);
        reply.Candidates.Should().BeEmpty();
    }

    [Fact]
    public void Parse_AMixOfReadableAndUnreadableLines_KeepsTheReadableOnes()
    {
        var reply = EntityReplyParser.Parse("person: Mario Rossi\nthis line is nonsense\nplace: Turin");

        reply.Kind.Should().Be(EntityReplyKind.Entities);
        reply.Candidates.Should().HaveCount(2);
    }
}
