using Arcade.Core.Content;

namespace Arcade.Core.Football.Tactics;

/// <summary>
/// Reader for original Kick Off 2 / Player Manager tactic files (<c>.tac</c>), so that players who own them can
/// drop them in a mod. Format (documented by the Open Kick-Off and Tactical Editor projects, checked on real
/// files): 10 outfield players × 20 positions × (x, y) as big-endian 16-bit integers (800 bytes), followed by
/// data of unknown meaning (ignored). Units: pixels at 12 per yard on a 912 × 1392 pitch.
/// Position order per player: 12 ball zones (by column, then by row from the own goal), kick-off own,
/// kick-off opponent, goal kick opponent, goal kick own, corners own-left, opponent-left, own-right,
/// opponent-right (the order of the last eight was deduced from the data, see REFERENCE_ANALYSIS.md).
/// No original tactic file is shipped with the game.
/// </summary>
public static class KickOff2TacticFile
{
    public const string Extension = ".tac";
    public const float Width = 912f;
    public const float Length = 1392f;

    private const int PositionsPerPlayer = 20;
    private const int MinimumSize = TacticDefinition.OutfieldPlayers * PositionsPerPlayer * 4;

    public static TacticDefinition Read(ReadOnlySpan<byte> data, string id)
    {
        if (data.Length < MinimumSize)
        {
            throw new ContentException($"Tactic file '{id}{Extension}' is too short ({data.Length} bytes, at least {MinimumSize} expected).");
        }

        var players = new List<TacticPlayer>(TacticDefinition.OutfieldPlayers);
        for (int player = 0; player < TacticDefinition.OutfieldPlayers; player++)
        {
            var positions = new float[PositionsPerPlayer][];
            for (int i = 0; i < PositionsPerPlayer; i++)
            {
                int offset = ((player * PositionsPerPlayer) + i) * 4;
                positions[i] = [ReadInt16(data, offset), ReadInt16(data, offset + 2)];
            }

            players.Add(new TacticPlayer
            {
                Defend = positions[..TacticDefinition.Zones],
                KickOffOwn = positions[12],
                KickOffOpponent = positions[13],
                GoalKickOpponent = positions[14],
                GoalKickOwn = positions[15],
                CornerOwnLeft = positions[16],
                CornerOpponentLeft = positions[17],
                CornerOwnRight = positions[18],
                CornerOpponentRight = positions[19],
            });
        }

        return new TacticDefinition
        {
            Id = id,
            Units = new TacticUnits { Width = Width, Length = Length },
            Players = players,
        };
    }

    private static float ReadInt16(ReadOnlySpan<byte> data, int offset) =>
        (short)((data[offset] << 8) | data[offset + 1]);
}
