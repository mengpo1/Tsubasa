using System.Buffers.Binary;
using Arcade.Core.Content;
using Arcade.Core.Football;
using Arcade.Core.Football.Tactics;

namespace Arcade.Tests.Football;

public sealed class TacticTests
{
    [Theory]
    [InlineData(0.1f, 0.1f, 0)]
    [InlineData(0.1f, 0.9f, 3)]
    [InlineData(0.5f, 0.3f, 5)]
    [InlineData(0.9f, 0.6f, 10)]
    [InlineData(1.2f, -0.5f, 8)]
    public void Ball_zones_are_numbered_by_column_then_by_row_from_the_own_goal(float across, float up, int zone)
    {
        Assert.Equal(zone, TacticDefinition.ZoneOf(across, up));
    }

    [Fact]
    public void Original_tac_files_are_read_with_the_documented_layout()
    {
        // Synthetic file: player p, position i -> (p * 100 + i, 1000 + i), plus trailing bytes of unknown meaning.
        byte[] data = new byte[980];
        for (int p = 0; p < 10; p++)
        {
            for (int i = 0; i < 20; i++)
            {
                int offset = ((p * 20) + i) * 4;
                BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(offset), (short)((p * 40) + i));
                BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(offset + 2), (short)(1000 + i));
            }
        }

        TacticDefinition tactic = KickOff2TacticFile.Read(data, "custom");
        var problems = new List<string>();
        tactic.Validate(problems);
        Assert.Empty(problems);
        Assert.Equal(912f, tactic.Units.Width);
        Assert.Equal(10, tactic.Players.Count);
        TacticPlayer third = tactic.Players[2];
        Assert.Equal([80f, 1000f], third.Defend[0]);
        Assert.Equal([91f, 1011f], third.Defend[11]);
        Assert.Equal(1012f, third.KickOffOwn[1]);
        Assert.Equal(1013f, third.KickOffOpponent[1]);
        Assert.Equal(1014f, third.GoalKickOpponent[1]);
        Assert.Equal(1015f, third.GoalKickOwn[1]);
        Assert.Equal(1016f, third.CornerOwnLeft[1]);
        Assert.Equal(1017f, third.CornerOpponentLeft[1]);
        Assert.Equal(1018f, third.CornerOwnRight[1]);
        Assert.Equal(1019f, third.CornerOpponentRight[1]);
        Assert.Null(third.Attack);
    }

    [Fact]
    public void A_truncated_tac_file_is_rejected()
    {
        Assert.Throws<ContentException>(() => KickOff2TacticFile.Read(new byte[100], "broken"));
    }

    [Fact]
    public void A_mod_can_provide_an_original_tac_file()
    {
        using var root = new TempGameRoot();
        CopyBaseContent(root.Root);
        root.WriteMod("tacmod");
        byte[] data = new byte[800];
        for (int i = 0; i < data.Length; i += 2)
        {
            BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(i), 300);
        }

        string path = Path.Combine(root.Root, "mods", "tacmod", "tactics", "zbreak.tac");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);

        FootballContent content = FootballContent.Load(root.Load("tacmod").Repository);
        Assert.Contains("zbreak", content.Tactics.Keys);
        Assert.Equal(300f, content.Tactics["zbreak"].Players[4].Defend[7][0]);
    }

    [Fact]
    public void Shipped_tactics_keep_kick_off_positions_in_the_own_half()
    {
        foreach (TacticDefinition tactic in TestPaths.BaseContent.Tactics.Values)
        {
            Assert.All(tactic.Players, p => Assert.True(p.KickOffOwn[1] <= tactic.Units.Length / 2f && p.KickOffOpponent[1] <= tactic.Units.Length / 2f));
        }
    }

    private static void CopyBaseContent(string root)
    {
        string source = Path.Combine(TestPaths.RepositoryRoot, "content");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(root, "content", Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
