using BattleshipBlazor.Models;
using System.Text.Json;

namespace BattleshipBlazor.Services
{
    public class GameService : IGameService
    {
        private readonly Dictionary<string, GameSession> _games = new();
        private readonly Dictionary<string, User> _usersByName = new();
        private readonly Dictionary<string, User> _usersByConnectionId = new();
        private readonly object _statsLock = new();
        private readonly string _statsFilePath;

        public GameService()
        {
            try
            {
                string appFolder = Path.Combine(Path.GetTempPath(), "BattleshipBlazor");
                Directory.CreateDirectory(appFolder);
                _statsFilePath = Path.Combine(appFolder, "stats.json");
                LoadStats();
            }
            catch
            {
                _statsFilePath = Path.Combine(AppContext.BaseDirectory, "stats.json");
            }
        }

        public User RegisterUser(string name, string connectionId)
        {
            string baseName = name;
            int counter = 1;
            while (_usersByName.ContainsKey(name))
                name = $"{baseName} {counter++}";
            var user = new User { Name = name, ConnectionId = connectionId };
            _usersByName[name] = user;
            _usersByConnectionId[connectionId] = user;
            SaveStats();
            return user;
        }

        public User? GetUserByName(string name) => _usersByName.GetValueOrDefault(name);
        public User? GetUserByConnectionId(string connectionId) => _usersByConnectionId.GetValueOrDefault(connectionId);

        public void UpdateConnectionId(string name, string connectionId)
        {
            if (_usersByName.TryGetValue(name, out var user))
            {
                if (!string.IsNullOrEmpty(user.ConnectionId))
                    _usersByConnectionId.Remove(user.ConnectionId);
                user.ConnectionId = connectionId;
                _usersByConnectionId[connectionId] = user;
            }
        }

        public (bool Success, string? GameId, string? ErrorMessage) CreateGame(
            string creatorName, int gridSize, List<int> shipLengths, bool isAI = false)
        {
            var creator = GetUserByName(creatorName);
            if (creator == null)
                return (false, null, "Creator not found.");
            if (creator.IsInGame)
                return (false, null, "You are already in a game.");

            var game = new GameSession
            {
                GridSize = gridSize,
                ShipLengths = shipLengths.ToList(),
                Creator = creator,
                IsAgainstAI = isAI,
                CreatorShips = new List<Ship>(),
                OpponentShips = new List<Ship>(),
                CreatorReady = false,
                OpponentReady = false
            };

            game.BoardCreator = new List<List<Cell>>(gridSize);
            game.BoardOpponent = new List<List<Cell>>(gridSize);
            for (int i = 0; i < gridSize; i++)
            {
                var row1 = new List<Cell>(gridSize);
                var row2 = new List<Cell>(gridSize);
                for (int j = 0; j < gridSize; j++)
                {
                    row1.Add(new Cell());
                    row2.Add(new Cell());
                }
                game.BoardCreator.Add(row1);
                game.BoardOpponent.Add(row2);
            }

            game.Id = Guid.NewGuid().ToString();
            _games[game.Id] = game;
            creator.IsInGame = true;
            SaveStats();
            return (true, game.Id, null);
        }
        public (bool Success, GameSession? GameState, string? ErrorMessage) JoinGame(
            string gameId, string playerName, string connectionId)
        {
            if (!_games.TryGetValue(gameId, out var game))
                return (false, null, "Game not found.");
            if (game.IsGameOver)
                return (false, null, "Game already over.");
            var player = GetUserByName(playerName);
            if (player == null)
                return (false, null, "User not found.");
            if (player.IsInGame)
                return (false, null, "You are already in a game.");

            if (game.IsAgainstAI)
            {
                if (game.Creator?.Name == playerName)
                    return (false, null, "You are already the creator.");
                return (false, null, "This is an AI game; no additional players allowed.");
            }

            if (game.Opponent != null)
                return (false, null, "Game already has an opponent.");

            game.Opponent = player;
            player.IsInGame = true;
            game.CurrentTurn = game.Creator; 
            SaveStats();
            return (true, game, null);
        }

        public (bool Success, GameSession? GameState, string? ErrorMessage) PlaceShip(
    string gameId, string playerName, int shipIndex, int row, int col, bool isHorizontal)
        {
            if (!_games.TryGetValue(gameId, out var game))
                return (false, null, "Game not found.");

            var player = GetUserByName(playerName);
            if (player == null)
                return (false, null, "User not found.");

            bool isCreator = game.Creator?.Name == playerName;
            var board = isCreator ? game.BoardCreator : game.BoardOpponent;
            var ships = isCreator ? game.CreatorShips : game.OpponentShips;

            if (ships.Count == game.ShipLengths.Count)
                return (false, null, "All ships already placed.");
            if (shipIndex < 0 || shipIndex >= game.ShipLengths.Count)
                return (false, null, "Invalid ship index.");

            // Must place ships in order (next unplaced ship)
            if (shipIndex != ships.Count)
                return (false, null, $"You must place ship #{ships.Count + 1} next.");

            int length = game.ShipLengths[shipIndex];
            if (isHorizontal && col + length > game.GridSize)
                return (false, null, "Ship doesn't fit horizontally.");
            if (!isHorizontal && row + length > game.GridSize)
                return (false, null, "Ship doesn't fit vertically.");

            // Check overlap
            var coords = new List<ShipCoordinate>();
            for (int i = 0; i < length; i++)
            {
                int r = isHorizontal ? row : row + i;
                int c = isHorizontal ? col + i : col;
                if (board[r][c].HasShip)
                    return (false, null, "Ship overlaps with another.");
                coords.Add(new ShipCoordinate { Row = r, Col = c, IsHit = false });
            }

            var ship = new Ship { Length = length, Coordinates = coords };
            ships.Add(ship);
            foreach (var coord in coords)
                board[coord.Row][coord.Col].HasShip = true;

            if (isCreator && ships.Count == game.ShipLengths.Count)
                game.CreatorReady = true;
            else if (!isCreator && ships.Count == game.ShipLengths.Count)
                game.OpponentReady = true;

            if (game.CreatorReady && game.OpponentReady)
                game.CurrentTurn = game.Creator;
            else if (game.IsAgainstAI && game.CreatorReady)
            {
                game.OpponentReady = true;
                game.CurrentTurn = game.Creator;
            }

            SaveStats();
            return (true, game, null);
        }

        // ---- Auto Place Ships ----
        public (bool Success, GameSession? GameState, string? ErrorMessage) AutoPlaceShips(
    string gameId, string playerName)
        {
            if (!_games.TryGetValue(gameId, out var game))
                return (false, null, "Game not found.");

            bool isCreator = game.Creator?.Name == playerName;
            bool isOpponent = game.Opponent?.Name == playerName;
            if (!isCreator && !isOpponent)
                return (false, null, "Player not in game.");

            var board = isCreator ? game.BoardCreator : game.BoardOpponent;
            var ships = isCreator ? game.CreatorShips : game.OpponentShips;

            // Clear old ships
            foreach (var s in ships)
                foreach (var c in s.Coordinates)
                    board[c.Row][c.Col].HasShip = false;
            ships.Clear();

            Random rnd = new();
            foreach (int length in game.ShipLengths)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 1000; attempt++)
                {
                    bool horiz = rnd.Next(2) == 0;
                    int row = rnd.Next(game.GridSize);
                    int col = rnd.Next(game.GridSize);

                    if (horiz && col + length <= game.GridSize)
                    {
                        bool overlap = false;
                        for (int i = 0; i < length; i++)
                            if (board[row][col + i].HasShip) { overlap = true; break; }
                        if (!overlap)
                        {
                            var coords = new List<ShipCoordinate>();
                            for (int i = 0; i < length; i++)
                            {
                                coords.Add(new ShipCoordinate { Row = row, Col = col + i });
                                board[row][col + i].HasShip = true;
                            }
                            ships.Add(new Ship { Length = length, Coordinates = coords });
                            placed = true;
                            break;
                        }
                    }
                    else if (!horiz && row + length <= game.GridSize)
                    {
                        bool overlap = false;
                        for (int i = 0; i < length; i++)
                            if (board[row + i][col].HasShip) { overlap = true; break; }
                        if (!overlap)
                        {
                            var coords = new List<ShipCoordinate>();
                            for (int i = 0; i < length; i++)
                            {
                                coords.Add(new ShipCoordinate { Row = row + i, Col = col });
                                board[row + i][col].HasShip = true;
                            }
                            ships.Add(new Ship { Length = length, Coordinates = coords });
                            placed = true;
                            break;
                        }
                    }
                }
                if (!placed) return (false, null, "Auto-placement failed.");
            }

            if (isCreator) game.CreatorReady = true;
            else game.OpponentReady = true;

            if (game.CreatorReady && game.OpponentReady)
                game.CurrentTurn = game.Creator;

            SaveStats();
            return (true, game, null);
        }

        public (bool Success, GameSession? GameState, bool GameOver, string? ErrorMessage) MakeMove(
     string gameId, string playerName, int row, int col)
        {
            if (!_games.TryGetValue(gameId, out var game))
                return (false, null, false, "Game not found.");
            if (game.IsGameOver)
                return (false, null, true, "Game already over.");

            var player = GetUserByName(playerName);
            if (player == null)
                return (false, null, false, "User not found.");

            if (game.CurrentTurn?.Name != playerName)
                return (false, null, false, "Not your turn.");

            bool isCreator = game.Creator?.Name == playerName;
            var targetBoard = isCreator ? game.BoardOpponent : game.BoardCreator;
            var targetShips = isCreator ? game.OpponentShips : game.CreatorShips;

            if (row < 0 || row >= game.GridSize || col < 0 || col >= game.GridSize)
                return (false, null, false, "Invalid coordinates.");
            if (targetBoard[row][col].IsHit)
                return (false, null, false, "Cell already attacked.");

            targetBoard[row][col].IsHit = true;
            bool hit = targetBoard[row][col].HasShip;

            if (hit)
            {
                foreach (var ship in targetShips)
                {
                    var coord = ship.Coordinates.FirstOrDefault(c => c.Row == row && c.Col == col);
                    if (coord != null)
                    {
                        coord.IsHit = true;

                        // If the ship is fully sunk, mark all its cells as sunk
                        if (ship.IsSunk)
                        {
                            foreach (var sc in ship.Coordinates)
                                targetBoard[sc.Row][sc.Col].IsSunk = true;
                        }
                        break;
                    }
                }
            }

            bool allSunk = targetShips.All(s => s.IsSunk);
            if (allSunk)
            {
                game.IsGameOver = true;
                game.Winner = player;
                player.Wins++;
                var loser = isCreator ? game.Opponent : game.Creator;
                if (loser != null) loser.Losses++;
                SaveStats();
                return (true, game, true, null);
            }

            // Switch turn
            game.CurrentTurn = isCreator ? game.Opponent : game.Creator;
            return (true, game, false, null);
        }
        public (bool Success, GameSession? GameState, bool GameOver, string? ErrorMessage) MakeAIMove(string gameId)
        {
            if (!_games.TryGetValue(gameId, out var game))
                return (false, null, false, "Game not found.");
            if (game.IsGameOver)
                return (false, null, true, "Game already over.");
            if (!game.IsAgainstAI)
                return (false, null, false, "Not an AI game.");

            var human = game.Creator;
            var ai = game.Opponent;
            if (game.CurrentTurn?.Name != ai?.Name)
                return (false, null, false, "Not AI's turn.");

            var targetBoard = game.BoardCreator; // AI attacks human's board
            var targetShips = game.CreatorShips;

            List<(int r, int c)> available = new();
            for (int i = 0; i < game.GridSize; i++)
                for (int j = 0; j < game.GridSize; j++)
                    if (!targetBoard[i][j].IsHit)
                        available.Add((i, j));

            if (available.Count == 0)
                return (false, null, false, "No moves available.");

            Random rnd = new Random();
            var (row, col) = available[rnd.Next(available.Count)];

            targetBoard[row][col].IsHit = true;
            bool hit = targetBoard[row][col].HasShip;

            if (hit)
            {
                foreach (var ship in targetShips)
                {
                    var coord = ship.Coordinates.FirstOrDefault(c => c.Row == row && c.Col == col);
                    if (coord != null)
                    {
                        coord.IsHit = true;

                        if (ship.IsSunk)
                        {
                            foreach (var sc in ship.Coordinates)
                                targetBoard[sc.Row][sc.Col].IsSunk = true;
                        }
                        break;
                    }
                }
            }

            bool allSunk = targetShips.All(s => s.IsSunk);
            if (allSunk)
            {
                game.IsGameOver = true;
                game.Winner = ai;
                human.Losses++;
                SaveStats();
                return (true, game, true, null);
            }

            game.CurrentTurn = human;
            return (true, game, false, null);
        }

        public List<GameSession> GetAvailableGames()
        {
            return _games.Values
                .Where(g => !g.IsGameOver && g.Opponent == null && !g.IsAgainstAI)
                .ToList();
        }

        public GameSession? GetGame(string gameId) => _games.GetValueOrDefault(gameId);

        public async Task PlayerDisconnected(User user)
        {
            if (user == null) return;
            user.IsInGame = false;
            foreach (var game in _games.Values)
            {
                if (game.Creator?.Name == user.Name)
                {
                    if (!game.IsGameOver)
                    {
                        game.IsGameOver = true;
                        game.Winner = game.Opponent;
                        if (game.Opponent != null) game.Opponent.Wins++;
                        user.Losses++;
                        SaveStats();
                    }
                }
                else if (game.Opponent?.Name == user.Name)
                {
                    if (!game.IsGameOver)
                    {
                        game.IsGameOver = true;
                        game.Winner = game.Creator;
                        if (game.Creator != null) game.Creator.Wins++;
                        user.Losses++;
                        SaveStats();
                    }
                }
            }
            _usersByConnectionId.Remove(user.ConnectionId);
            SaveStats();
        }

        public void SaveStats()
        {
            lock (_statsLock)
            {
                var list = _usersByName.Values.ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_statsFilePath, json);
            }
        }

        public void LoadStats()
        {
            if (!File.Exists(_statsFilePath)) return;
            lock (_statsLock)
            {
                try
                {
                    var json = File.ReadAllText(_statsFilePath);
                    var list = JsonSerializer.Deserialize<List<User>>(json);
                    if (list != null)
                    {
                        foreach (var user in list)
                        {
                            _usersByName[user.Name] = user;
                            if (!string.IsNullOrEmpty(user.ConnectionId))
                                _usersByConnectionId[user.ConnectionId] = user;
                        }
                    }
                }
                catch { /* ignore */ }
            }
        }
    }
}