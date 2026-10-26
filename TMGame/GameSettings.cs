namespace TMGame;

public sealed record GameSettings(string Name, int Players, int Mafia, int Police,
    int Doctors, int Detectives, int Villagers)
{
    public static GameSettings Default(int players = 5) => new("Terminal Mafia", players, 1, 0, 0, 0, players - 1);

    public GameSettings Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 64 || Name.Any(char.IsControl))
            throw new ArgumentException("Server name must be 1–64 characters without control characters.");
        if (Players is < 3 or > 16) throw new ArgumentException("Players must be 3–16.");
        int[] counts = [Mafia, Police, Doctors, Detectives, Villagers];
        if (counts.Any(n => n < 0 || n > Players)) throw new ArgumentException("Role counts must be between zero and the player count.");
        if (counts.Sum() != Players) throw new ArgumentException("Role counts must add up to the player count.");
        if (Mafia < 1 || Mafia * 2 >= Players) throw new ArgumentException("Mafia must be at least one and fewer than half of all players.");
        return this;
    }
}
