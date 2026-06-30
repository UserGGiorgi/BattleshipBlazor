using BattleshipBlazor.Models;

namespace BattleshipBlazor.Services
{
    public interface IUserService
    {
        User? CurrentUser { get; set; }
    }

    public class UserService : IUserService
    {
        public User? CurrentUser { get; set; }
    }
}
