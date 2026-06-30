namespace BattleshipBlazor.Models
{
    public class User
    {
        public string Name { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public int Wins { get; set; }
        public int Losses { get; set; }
        public bool IsInGame { get; set; }
    }
}
