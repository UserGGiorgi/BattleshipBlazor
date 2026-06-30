namespace BattleshipBlazor.Models
{
    public class Ship
    {
        public int Length { get; set; }
        public List<ShipCoordinate> Coordinates { get; set; } = new();
        public bool IsSunk => Coordinates.All(c => c.IsHit);
    }

    public class ShipCoordinate
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public bool IsHit { get; set; }
    }
}
