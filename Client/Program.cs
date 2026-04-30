using System;
using System.Collections.Generic;
using System.Text.Json;
using Client.NetworkLogic;

namespace Client;

class User
{
    public int user_id { get; set; }
    public string user_name { get; set; }
    public string password { get; set; }
}

class Chat
{
    public int chat_id { get; set; }
    public string chat_name { get; set; }
}

class Message
{
    public int message_id { get; set; }
    public string send_time { get; set; }
    public string body { get; set; }
    public int sender_id { get; set; }
}

public static class Program
{
    private static int myId = -1;

    public static void Main()
    {
        try
        {
            Network.OnPush = Dispatch;
            Network.Connect("192.168.1.117", 5000);
            Visual.Visual.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Ошибка: {e.Message}");
            Console.ReadLine();
        }
        finally
        {
            Network.Disconnect();
        }
    }

    public static void OnLogin(string userName, string password)
    {
        try
        {
            var resp = Network.Login(userName, password);
            var userJson = resp.args["user"];

            if (string.IsNullOrEmpty(userJson) || userJson == "null")
            {
                Visual.Visual.NotifyLogin(false);
                return;
            }

            var user = JsonSerializer.Deserialize<User>(userJson);
            myId = user.user_id;
            Network.SetUserId(user.user_id);
            Visual.Visual.SetUser(user.user_id, user.user_name);

            LoadUserChats();
            Visual.Visual.NotifyLogin(true);
        }
        catch (Exception)
        {
            Visual.Visual.NotifyLogin(false);
        }
    }

    public static void OnRegister(string userName, string password)
    {
        try
        {
            var resp = Network.Register(userName, password);
            bool success = resp.args.TryGetValue("success", out var s) && s == "True";
            Visual.Visual.NotifyRegister(success);
        }
        catch (Exception)
        {
            Visual.Visual.NotifyRegister(false);
        }
    }

    private static void LoadUserChats()
    {
        var resp = Network.GetUserChats();
        var chats = JsonSerializer.Deserialize<List<Chat>>(resp.args["chats"]);
        var list = new List<(int, string)>();
        foreach (var c in chats) list.Add((c.chat_id, c.chat_name));
        Visual.Visual.SetChats(list);
    }

    public static void OnCreateChat(string name)
    {
        try
        {
            var resp = Network.CreateChat(name);
            var chat = JsonSerializer.Deserialize<Chat>(resp.args["chat"]);
            Visual.Visual.NotifyCreateChat(true, chat.chat_id, chat.chat_name, "");
        }
        catch (Exception e)
        {
            Visual.Visual.NotifyCreateChat(false, 0, "", e.Message);
        }
    }

    public static void OnGetMessages(int chatId)
    {
        try
        {
            var resp = Network.GetChatMessages(chatId);
            var msgs = JsonSerializer.Deserialize<List<Message>>(resp.args["messages"]);
            var list = new List<(int, string)>();
            foreach (var m in msgs) list.Add((m.sender_id, m.body));
            Visual.Visual.SetChatHistory(chatId, list);
        }
        catch { }
    }

    public static void OnSendMessage(int chatId, string body)
    {
        try
        {
            Network.SendMessage(chatId, body);
            Visual.Visual.AddMessage(chatId, myId, body);
        }
        catch { }
    }

    public static void OnGetMembers(int chatId)
    {
        try
        {
            var resp = Network.GetChatMembers(chatId);
            var users = JsonSerializer.Deserialize<List<User>>(resp.args["members"]);
            var list = new List<(int, string)>();
            foreach (var u in users) list.Add((u.user_id, u.user_name));
            Visual.Visual.SetChatMembers(chatId, list);
        }
        catch { }
    }

    public static void OnAddUser(int userId, int chatId)
    {
        try { Network.AddMember(userId, chatId); } catch { }
    }

    public static void OnKickUser(int userId, int chatId)
    {
        try { Network.KickMember(userId, chatId); } catch { }
    }

    private static void Dispatch(Request req)
    {
        try
        {
            switch (req.command)
            {
                case "message_recive":
                    {
                        int chatId = int.Parse(req.args["chat_id"]);
                        var msg = JsonSerializer.Deserialize<Message>(req.args["message"]);
                        Visual.Visual.AddMessage(chatId, msg.sender_id, msg.body);
                        break;
                    }
                case "added_to_chat":
                    {
                        var chat = JsonSerializer.Deserialize<Chat>(req.args["chat"]);
                        var user = JsonSerializer.Deserialize<User>(req.args["user"]);
                        if (user.user_id == myId)
                            Visual.Visual.AddChat(chat.chat_id, chat.chat_name);
                        break;
                    }
                case "kicked_from_chat":
                    {
                        var chat = JsonSerializer.Deserialize<Chat>(req.args["chat"]);
                        var user = JsonSerializer.Deserialize<User>(req.args["user"]);
                        if (user.user_id == myId)
                            Visual.Visual.RemoveChat(chat.chat_id);
                        break;
                    }
            }
        }
        catch { }
    }
}
