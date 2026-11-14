namespace Arcade.Importer.Amiga;

/// <summary>A named file read from a disk image, an archive or a folder.</summary>
/// <param name="Origin">Where it comes from, for the report (e.g. <c>KickOff2.adf</c> or <c>KickOff2.adf/sprites.dat</c>).</param>
internal sealed record DiskFile(string Name, byte[] Data, string Origin);
