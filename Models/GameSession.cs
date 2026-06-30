namespace BattleshipBlazor.Models
{
    public class GameSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int GridSize { get; set; } = 10;
        public List<int> ShipLengths { get; set; } = new() { 5, 4, 3, 3, 2 };
        public User? Creator { get; set; }
        public User? Opponent { get; set; }
        public User? CurrentTurn { get; set; }
        public List<List<Cell>> BoardCreator { get; set; } = new();
        public List<List<Cell>> BoardOpponent { get; set; } = new();
        public bool IsGameOver { get; set; }
        public User? Winner { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsAgainstAI { get; set; }
        public List<Ship> CreatorShips { get; set; } = new();
        public List<Ship> OpponentShips { get; set; } = new();
        public bool CreatorReady { get; set; }
        public bool OpponentReady { get; set; }
        public string? CurrentPlayerName => CurrentTurn?.Name;

        public List<List<Cell>> GetBoard(User player)
        {
            if (player == Creator) return BoardCreator;
            if (player == Opponent) return BoardOpponent;
            throw new ArgumentException("Player not in game");
        }

        public List<Ship> GetShips(User player)
        {
            if (player == Creator) return CreatorShips;
            if (player == Opponent) return OpponentShips;
            throw new ArgumentException("Player not in game");
        }
    }
}