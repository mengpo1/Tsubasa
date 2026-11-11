using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Desktop;
using Arcade.Desktop.Hosting;

var log = new GameLog();
log.AddSink(entry =>
{
    if (entry.Level >= LogLevel.Info)
    {
        Console.Error.WriteLine(entry.Format());
    }
});

try
{
    LaunchOptions options = LaunchOptions.Parse(args);
    string root = GameRoot.Find(options.Root);
    if (options.ListMods)
    {
        foreach (string mod in ContentLoader.ListInstalledMods(root))
        {
            Console.WriteLine(mod);
        }

        return 0;
    }

    UserSettings settings = UserSettings.Load(root);
    using var game = new ArcadeGame(options, root, settings, log);
    game.Run();
    return game.ExitCode;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
catch (ContentException exception)
{
    Console.Error.WriteLine($"Content error: {exception.Message}");
    return 1;
}
