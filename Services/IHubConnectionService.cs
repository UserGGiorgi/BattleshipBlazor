using Microsoft.AspNetCore.SignalR.Client;

namespace BattleshipBlazor.Services
{
    public interface IHubConnectionService
    {
        HubConnection? Connection { get; set; }
    }

    public class HubConnectionService : IHubConnectionService
    {
        public HubConnection? Connection { get; set; }
    }
}
