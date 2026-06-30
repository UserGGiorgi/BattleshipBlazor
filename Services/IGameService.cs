using BattleshipBlazor.Models;

namespace BattleshipBlazor.Services
{
    public interface IGameService
    {
        User RegisterUser(string name, string connectionId);
        User? GetUserByName(string name);
        User? GetUserByConnectionId(string connectionId);
        void UpdateConnectionId(string name, string connectionId);

        (bool Success, string? GameId, string? ErrorMessage) CreateGame(
            string creatorName, int gridSize, List<int> shipLengths, bool isAI = false);

        (bool Success, GameSession? GameState, string? ErrorMessage) JoinGame(
            string gameId, string playerName, string connectionId);

        List<GameSession> GetAvailableGames();
        GameSession? GetGame(string gameId);

        (bool Success, GameSession? GameState, string? ErrorMessage) PlaceShip(
            string gameId, string playerName, int shipIndex, int row, int col, bool isHorizontal);

        (bool Success, GameSession? GameState, string? ErrorMessage) AutoPlaceShips(
            string gameId, string playerName);

        (bool Success, GameSession? GameState, bool GameOver, string? ErrorMessage) MakeMove(
            string gameId, string playerName, int row, int col);

        (bool Success, GameSession? GameState, bool GameOver, string? ErrorMessage) MakeAIMove(
            string gameId);

        Task PlayerDisconnected(User user);
        void SaveStats();
        void LoadStats();
    }
}
