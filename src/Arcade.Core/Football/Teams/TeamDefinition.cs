using Arcade.Core.Content;

namespace Arcade.Core.Football.Teams;

/// <summary>A team and its squad (<c>teams/&lt;id&gt;.json</c>). Names are data, not translated.</summary>
public sealed record TeamDefinition : IContentData
{
    public const string Folder = "teams";

    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Three-letter abbreviation for the scoreboard.</summary>
    public required string ShortName { get; init; }

    public required KitDefinition Kit { get; init; }

    public required IReadOnlyList<PlayerProfile> Players { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NotEmpty(problems, "id", Id);
        DataChecks.NotEmpty(problems, "name", Name);
        DataChecks.NotEmpty(problems, "shortName", ShortName);
        DataChecks.Nested(problems, "kit", Kit);
        if (Players.Count == 0)
        {
            problems.Add("players must contain at least one player");
        }

        for (int i = 0; i < Players.Count; i++)
        {
            DataChecks.Nested(problems, $"players[{i}]", Players[i]);
        }

        var numbers = new HashSet<int>();
        foreach (PlayerProfile player in Players)
        {
            if (!numbers.Add(player.Number))
            {
                problems.Add($"shirt number {player.Number} is used twice");
            }
        }
    }
}

public sealed record KitDefinition : IContentData
{
    public required string Shirt { get; init; }

    public required string Shorts { get; init; }

    public required string Socks { get; init; }

    public ColorRgb ColorFor(string slot) => ColorRgb.Parse(slot switch
    {
        "shirt" => Shirt,
        "shorts" => Shorts,
        "socks" => Socks,
        _ => throw new ContentException($"Unknown kit slot '{slot}'."),
    });

    public void Validate(ICollection<string> problems)
    {
        foreach ((string name, string value) in new[] { ("shirt", Shirt), ("shorts", Shorts), ("socks", Socks) })
        {
            if (!ColorRgb.TryParse(value, out _))
            {
                problems.Add($"{name} '{value}' is not a #RRGGBB colour");
            }
        }
    }
}

/// <summary>Playing position family. Used by tactics and AI in later lots.</summary>
public enum PlayerRole
{
    Goalkeeper,
    Defender,
    Midfielder,
    Forward,
}

public sealed record PlayerProfile : IContentData
{
    public required int Number { get; init; }

    public required string Name { get; init; }

    public required PlayerRole Role { get; init; }

    public required PlayerAttributeValues Attributes { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "number", Number, 1, 99);
        DataChecks.NotEmpty(problems, "name", Name);
        DataChecks.Nested(problems, "attributes", Attributes);
    }
}

/// <summary>Attribute values on the scale declared in <c>config/players.json</c> (the Kick Off 2 / Final Whistle set).</summary>
public sealed record PlayerAttributeValues : IContentData
{
    public required float Pace { get; init; }

    public required float Stamina { get; init; }

    public required float Aggression { get; init; }

    public required float Resilience { get; init; }

    public required float Passing { get; init; }

    public required float Shooting { get; init; }

    public required float Tackling { get; init; }

    public required float Flair { get; init; }

    public void Validate(ICollection<string> problems)
    {
        foreach ((string name, float value) in new[]
        {
            ("pace", Pace), ("stamina", Stamina), ("aggression", Aggression), ("resilience", Resilience),
            ("passing", Passing), ("shooting", Shooting), ("tackling", Tackling), ("flair", Flair),
        })
        {
            if (!float.IsFinite(value))
            {
                problems.Add($"{name} must be a number");
            }
        }
    }
}
