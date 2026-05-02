using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
    private static readonly object _lock = new object();

    public static void Main()
    {
        try
        {
            Network.OnPush = Dispatch;
            Network.Connect("192.168.1.80", 5000);
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

    public static int GetMyId() => myId;

    public static void OnLogin(string userName, string password)
    {
        try
        {
            var resp = Network.Login(userName, password);
            if (resp == null || resp.args == null || !resp.args.TryGetValue("user", out var userJson) || string.IsNullOrEmpty(userJson) || userJson == "null")
            {
                Visual.Visual.NotifyLogin(false);
                return;
            }

            var user = JsonSerializer.Deserialize<User>(userJson);
            if (user == null || user.user_id <= 0)
            {
                Visual.Visual.NotifyLogin(false);
                return;
            }
            myId = user.user_id;
            Network.SetUserId(user.user_id);
            Visual.Visual.SetUser(user.user_id, user.user_name);

            LoadUserChats();
            LoadAllUsers();
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
            bool success = resp.args.TryGetValue("success", out var s) && s == "True" && resp != null && resp.args != null;
            Visual.Visual.NotifyRegister(success);
        }
        catch (Exception)
        {
            Visual.Visual.NotifyRegister(false);
        }
    }

    private static void LoadUserChats()
    {
        try
        {
            var resp = Network.GetUserChats();
            if (resp == null || resp.args == null) return;

            var chats = JsonSerializer.Deserialize<List<Chat>>(resp.args["chats"]);
            var list = new List<(int, string)>();
            if (chats != null)
                foreach (var c in chats) list.Add((c.chat_id, c.chat_name));
            Visual.Visual.SetChats(list);
        }
        catch { }
    }
    private static int loadingUsers = 0;
    public static void LoadAllUsers()
    {
        lock (_lock)
        {
            if (loadingUsers != 0)
                return;

            loadingUsers = 1;
        }

        Task.Run(() =>
        {
            try
            {
                Visual.Visual.SetUsersLoading(true);

                int missesInRow = 0;
                for (int id = 1; id < 1000; id++)
                {
                    try
                    {
                        var resp = Network.GetUser(id);
                        if (resp == null || resp.args == null
                            || !resp.args.TryGetValue("user", out var uj)
                            || string.IsNullOrEmpty(uj) || uj == "null")
                        {
                            missesInRow++;
                            if (missesInRow >= 5) break;
                            continue;
                        }

                        var u = JsonSerializer.Deserialize<User>(uj);
                        if (u == null || u.user_id <= 0)
                        {
                            missesInRow++;
                            if (missesInRow >= 5) break;
                            continue;
                        }

                        missesInRow = 0;
                        Visual.Visual.AddOrUpdateUser(u.user_id, u.user_name);
                    }
                    catch
                    {
                        missesInRow++;
                        if (missesInRow >= 5) break;
                    }
                }
            }
            finally
            {
                Visual.Visual.SetUsersLoading(false);
                lock (_lock)
                {
                    loadingUsers = 0;
                }
            }
        });
    }

    public static void OnCreateChat(string name)
    {
        try
        {
            var resp = Network.CreateChat(name);
            if (resp == null || resp.args == null || !resp.args.TryGetValue("chat", out var cj))
            {
                Visual.Visual.NotifyCreateChat(false, 0, "", "Сервер не ответил");
                return;
            }

            var chat = JsonSerializer.Deserialize<Chat>(cj);
            if (chat == null)
            {
                Visual.Visual.NotifyCreateChat(false, 0, "", "Некорректный ответ сервера");
                return;
            }

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
            if (resp == null || resp.args == null) return;
            if (!resp.args.TryGetValue("messages", out var rawMsgs)) return;

            var msgs = JsonSerializer.Deserialize<List<Message>>(rawMsgs);
            var list = new List<(int, string)>();
            if (msgs != null)
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
            if (resp == null || resp.args == null) return;

            var users = JsonSerializer.Deserialize<List<User>>(resp.args["members"]);
            var list = new List<(int, string)>();
            if (users != null)
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
                        {
                            Visual.Visual.AddChat(chat.chat_id, chat.chat_name);
                        }
                        else
                        {
                            OnGetMembers(chat.chat_id);
                            Visual.Visual.ShowNotification(
                                $"В чат «{chat.chat_name}» добавлен {user.user_name}[{user.user_id}]");
                        }
                        break;
                    }
                case "kicked_from_chat":
                    {
                        var chat = JsonSerializer.Deserialize<Chat>(req.args["chat"]);
                        var user = JsonSerializer.Deserialize<User>(req.args["user"]);
                        if (user.user_id == myId)
                        {
                            Visual.Visual.RemoveChat(chat.chat_id);
                        }
                        else
                        {
                            OnGetMembers(chat.chat_id);
                            Visual.Visual.ShowNotification(
                                $"Из чата «{chat.chat_name}» исключён {user.user_name}[{user.user_id}]");
                        }
                        break;
                    }
            }
        }
        catch { }
    }
}
