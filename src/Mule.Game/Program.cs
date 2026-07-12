// Headless self-check: `MULE_SIMULATE=1 dotnet run` plays a full all-AI game in
// the console and exits, without opening a window.
if (System.Environment.GetEnvironmentVariable("MULE_SIMULATE") != null)
{
    System.Console.Write(Mule.Core.Simulation.RunHeadless(seed: 1983));
    return;
}

using var game = new Mule.Game.Game1();
game.Run();
