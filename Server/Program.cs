using Server.NetworkLogic;
using Server.Database;
using Server.Database.Repositories;
using Server.Database.Models;

namespace Server;

class Program
{
    public static void Main()
    {
        // // Network network = new Network();
        // // network.Start(5000);
        // using UserRepository userRepo = new UserRepository();
        // using ChatRepository chatRepo = new ChatRepository();
        // foreach (User user in userRepo.GetUsers()) {
        //     Console.WriteLine($"[{user.user_id}] {user.user_name} {user.password}");
        // }
        // // userRepo.Register(new User(){user_name = "timbundon", password = "12345678"});
        // User us = userRepo.Login(new User(){user_name = "timbundon", password = "12345678"});
        // Console.WriteLine(us != null);
        // foreach (Chat chat in userRepo.GetUserChats(us.user_id)) {
        //     Console.WriteLine($"[{chat.chat_id}] {chat.chat_name}");
        // }
        // foreach (User chat in chatRepo.GetChatMembers(1)) {
        //     Console.WriteLine($"[{chat.user_id}] {chat.user_name} {chat.password}");
        // }
        // //chatRepo.AddUser(us.user_id, 1);
        // //userRepo.SendMessage(1, new Messsage(){body = "hello", sender_id = 1, send_time = TimeOnly.FromDateTime(DateTime.Now).ToTimeSpan()});
        Network.Serve(5000);
    }
}