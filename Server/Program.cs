using Server.NetworkLogic;
using Server.Database;
using Server.Database.Repositories;
using Server.Database.Models;

namespace Server;

class Program
{
    public static void Main()
    {
        // Network network = new Network();
        // network.Start(5000);
        using UserRepository userRepo = new UserRepository();
        foreach (User user in userRepo.GetUsers()) {
            Console.WriteLine($"[{user.user_id}] {user.user_name} {user.password}");
        }
    }
}