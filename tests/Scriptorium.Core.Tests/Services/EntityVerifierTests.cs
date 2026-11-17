using FluentAssertions;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class EntityVerifierTests
{
    private const string Text = "Mario Rossi signed for Alfa Srl in Turin on 12 March 2026. The price is 15,000 euro.";

    [Fact]
    public void Verify_AnEntityFoundInTheTextIsKept()
    {
        var groups = EntityVerifier.Verify([Candidate(EntityType.Person, "Mario Rossi")], Text);

        groups.Should().ContainSingle();
        groups[0].Type.Should().Be(EntityType.Person);
        groups[0].Entities.Should().Equal("Mario Rossi");
    }

    [Fact]
    public void Verify_AnEntityThatIsNotInTheTextIsDropped_PersonDateAndAmount()
    {
        var groups = EntityVerifier.Verify(
            [
                Candidate(EntityType.Person, "Luca Verdi"),
                Candidate(EntityType.Date, "1 January 1999"),
                Candidate(EntityType.Amount, "999 euro"),
                Candidate(EntityType.Place, "Turin")
            ],
            Text);

        groups.Should().ContainSingle();
        groups[0].Entities.Should().Equal("Turin");
    }

    [Fact]
    public void Verify_TheComparisonIgnoresLetterCaseAndTheEntityIsShownAsTheTextWritesIt()
    {
        var groups = EntityVerifier.Verify([Candidate(EntityType.Person, "mario ROSSI")], Text);

        groups[0].Entities.Should().Equal("Mario Rossi");
    }

    [Fact]
    public void Verify_ARunOfSpacesOrLineBreaksCountsAsOneSpace_AndIsShownAsOneSpace()
    {
        var text = "The partner is Alfa\n   Srl, from Turin.";

        var groups = EntityVerifier.Verify(
            [Candidate(EntityType.Organization, "Alfa Srl"), Candidate(EntityType.Place, "Turin")], text);

        groups.Single(g => g.Type == EntityType.Organization).Entities.Should().Equal("Alfa Srl");
    }

    [Fact]
    public void Verify_ARunOfSpacesInTheCandidateIsFoundInTheTextWithOne()
    {
        var groups = EntityVerifier.Verify([Candidate(EntityType.Person, "Mario    Rossi")], Text);

        groups[0].Entities.Should().Equal("Mario Rossi");
    }

    [Fact]
    public void Verify_TheSameEntityWrittenSeveralWaysAppearsOncePerType()
    {
        var groups = EntityVerifier.Verify(
            [
                Candidate(EntityType.Person, "Mario Rossi"),
                Candidate(EntityType.Person, "mario rossi"),
                Candidate(EntityType.Person, "Mario  Rossi")
            ],
            Text);

        groups.Should().ContainSingle();
        groups[0].Entities.Should().Equal("Mario Rossi");
    }

    [Fact]
    public void Verify_TheSameTextInTwoTypesIsKeptInBoth()
    {
        var text = "Torino has a company called Torino near Torino.";

        var groups = EntityVerifier.Verify(
            [Candidate(EntityType.Place, "Torino"), Candidate(EntityType.Organization, "Torino")], text);

        groups.Should().HaveCount(2);
        groups.Select(g => g.Type).Should().Equal(EntityType.Organization, EntityType.Place);
    }

    [Fact]
    public void Verify_TheGroupsComeInTheFixedOrderAndOnlyTheNonEmptyOnes()
    {
        var groups = EntityVerifier.Verify(
            [
                Candidate(EntityType.Place, "Turin"),
                Candidate(EntityType.Amount, "15,000 euro"),
                Candidate(EntityType.Date, "12 March 2026"),
                Candidate(EntityType.Organization, "Alfa Srl"),
                Candidate(EntityType.Person, "Mario Rossi")
            ],
            Text);

        groups.Select(g => g.Type).Should().Equal(
            EntityType.Person, EntityType.Organization, EntityType.Date, EntityType.Amount, EntityType.Place);

        var onlyTwo = EntityVerifier.Verify(
            [Candidate(EntityType.Place, "Turin"), Candidate(EntityType.Person, "Mario Rossi")], Text);
        onlyTwo.Select(g => g.Type).Should().Equal(EntityType.Person, EntityType.Place);
    }

    [Fact]
    public void Verify_InsideAGroupTheEntitiesAreOrderedByWhereTheyFirstAppearInTheText()
    {
        var text = "Anna met Mario, then Carla joined Mario and Anna.";

        var groups = EntityVerifier.Verify(
            [
                Candidate(EntityType.Person, "Carla"),
                Candidate(EntityType.Person, "Mario"),
                Candidate(EntityType.Person, "Anna")
            ],
            text);

        groups[0].Entities.Should().Equal("Anna", "Mario", "Carla");
    }

    [Fact]
    public void Verify_NoCandidatesOrAllDroppedGivesAnEmptyList()
    {
        EntityVerifier.Verify([], Text).Should().BeEmpty();
        EntityVerifier.Verify([Candidate(EntityType.Person, "Nobody Here")], Text).Should().BeEmpty();
    }

    [Fact]
    public void Verify_ACandidateOfOnlySpacesIsIgnored()
    {
        EntityVerifier.Verify([Candidate(EntityType.Person, "   ")], Text).Should().BeEmpty();
    }

    [Fact]
    public void Verify_AccentedLettersAreComparedLikeTheRest()
    {
        var text = "Il Perù e la Città di Milano.";

        var groups = EntityVerifier.Verify(
            [Candidate(EntityType.Place, "perù"), Candidate(EntityType.Place, "CITTÀ DI MILANO")], text);

        groups[0].Entities.Should().Equal("Perù", "Città di Milano");
    }

    [Fact]
    public void Verify_ANameThatIsOnlyPartOfALongerWordIsAccepted()
    {
        // A plain search, not a word search (research R3): a known and accepted limit.
        var groups = EntityVerifier.Verify([Candidate(EntityType.Person, "Rossi")], "The chairman Rossini spoke.");

        groups[0].Entities.Should().Equal("Rossi");
    }

    private static EntityCandidate Candidate(EntityType type, string text) => new(type, text);
}
