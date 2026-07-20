namespace Ashenfall.Data;

public sealed class CharacterRecord
{
    public ulong SteamId { get; set; }
    public string Name { get; set; } = "";
    public string Class { get; set; } = "Marksman";
    public int Level { get; set; } = 1;
    public long Xp { get; set; }
    public long Gold { get; set; }
}
