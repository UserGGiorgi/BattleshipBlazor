using BattleshipBlazor.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace BattleshipBlazor.Hubs;

public class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> _connectionUserMap = new();

    private readonly IGameService _gameService;
    private readonly IUserService _userService;

    public GameHub(IGameService gameService, IUserService userService)
    {
        _gameService = gameService;
        _userService = userService;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionUserMap.TryRemove(Context.ConnectionId, out var userName))
        {
            var user = _gameService.GetUserByName(userName);
            if (user != null)
                await _gameService.PlayerDisconnected(user);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task RegisterUser(string name)
    {
        var user = _gameService.RegisterUser(name, Context.ConnectionId);
        _connectionUserMap[Context.ConnectionId] = user.Name;
        _userService.CurrentUser = user;
        await Clients.Caller.SendAsync("UserRegistered", user);
    }

    public async Task CreateGame(int gridSize, List<int> shipLengths, bool isAI)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userName))
        {
            await Clients.Caller.SendAsync("Error", "Not registered.");
            return;
        }

        var user = _gameService.GetUserByName(userName);
        if (user == null)
        {
            await Clients.Caller.SendAsync("Error", "User not found.");
            return;
        }

        _userService.CurrentUser = user;

        var result = _gameService.CreateGame(user.Name, gridSize, shipLengths, isAI);
        if (result.Success)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, result.GameId);

            await Clients.Caller.SendAsync("GameCreated", result.GameId);

            if (isAI)
            {
                var game = _gameService.GetGame(result.GameId);
                if (game != null)
                {
                    var aiUser = new Models.User { Name = "AI", ConnectionId = "AI" };
                    game.Opponent = aiUser;
                    game.CurrentTurn = game.Creator;
                    var autoResult = _gameService.AutoPlaceShips(result.GameId, "AI");
                    if (autoResult.Success)
                    {
                        await Clients.Caller.SendAsync("GameStateUpdated", autoResult.GameState);
                    }
                }
            }
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.ErrorMessage);
        }
    }

    public async Task JoinGame(string gameId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userName))
        {
            await Clients.Caller.SendAsync("Error", "Not registered.");
            return;
        }

        var user = _gameService.GetUserByName(userName);
        if (user == null)
        {
            await Clients.Caller.SendAsync("Error", "User not found.");
            return;
        }

        _userService.CurrentUser = user;

        var game = _gameService.GetGame(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        if (game.Creator?.Name == user.Name || game.Opponent?.Name == user.Name)
        {
            _gameService.UpdateConnectionId(user.Name, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            await Clients.Caller.SendAsync("GameStateUpdated", game);
            return;
        }

        var result = _gameService.JoinGame(gameId, user.Name, Context.ConnectionId);
        if (result.Success)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            await Clients.Group(gameId).SendAsync("GameStateUpdated", result.GameState);
            await Clients.Caller.SendAsync("GameStateUpdated", result.GameState);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.ErrorMessage);
        }
    }

    public async Task PlaceShip(string gameId, int shipIndex, int row, int col, bool isHorizontal)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userName))
        {
            await Clients.Caller.SendAsync("Error", "Not registered.");
            return;
        }

        var user = _gameService.GetUserByName(userName);
        if (user == null)
        {
            await Clients.Caller.SendAsync("Error", "User not found.");
            return;
        }

        var result = _gameService.PlaceShip(gameId, user.Name, shipIndex, row, col, isHorizontal);
        if (result.Success)
        {
            await Clients.Group(gameId).SendAsync("GameStateUpdated", result.GameState);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.ErrorMessage);
        }
    }

    public async Task AutoPlaceShips(string gameId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userName))
        {
            await Clients.Caller.SendAsync("Error", "Not registered.");
            return;
        }

        var user = _gameService.GetUserByName(userName);
        if (user == null)
        {
            await Clients.Caller.SendAsync("Error", "User not found.");
            return;
        }

        var result = _gameService.AutoPlaceShips(gameId, user.Name);
        if (result.Success)
        {
            await Clients.Group(gameId).SendAsync("GameStateUpdated", result.GameState);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.ErrorMessage);
        }
    }

    public async Task MakeMove(string gameId, int row, int col)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userName))
        {
            await Clients.Caller.SendAsync("Error", "Not registered.");
            return;
        }

        var user = _gameService.GetUserByName(userName);
        if (user == null)
        {
            await Clients.Caller.SendAsync("Error", "User not found.");
            return;
        }

        var result = _gameService.MakeMove(gameId, user.Name, row, col);
        if (result.Success)
        {
            await Clients.Group(gameId).SendAsync("GameStateUpdated", result.GameState);
            if (result.GameOver)
            {
                await Clients.Group(gameId).SendAsync("GameOver", result.GameState);
            }
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.ErrorMessage);
        }
    }

    public async Task RequestAIMove(string gameId)
    {
        var result = _gameService.MakeAIMove(gameId);
        if (result.Success)
        {
            await Clients.Group(gameId).SendAsync("GameStateUpdated", result.GameState);
            if (result.GameOver)
            {
                await Clients.Group(gameId).SendAsync("GameOver", result.GameState);
            }
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.ErrorMessage);
        }
    }
}