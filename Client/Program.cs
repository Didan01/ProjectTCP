using Client;
using Client.NetworkLogic;
using Client.Visual;
using System;
using System.Collections.Generic;
using System.Text.Json;

class Program
{
    static Network network = new Network();

    public static void Main()
    {
        Visual.OnLogin = Login;
        Visual.OnRegister = Register;
        Visual.OnSendMessage = SendMessage;
        Visual.OnCreateChat = CreateChat;
        Visual.OnAddUser = AddUser;
        Visual.OnKickUser = KickUser;
        Visual.OnGetChatMembers = GetChatMembers;
        Visual.OnLeaveChat = LeaveChat;
        Visual.OnGetMessages = GetMessages;

        network.OnResponse = HandleResponse;

        Visual.Start();
    }

    static void Login(string name, string pass)
    {
        if (!network.connected)
            if (!network.Connect("127.0.0.1", 5000))
            { Visual.NotifyLogin(false); return; }

        var resp = network.SendAndWait("login", new Dictionary<string, string>
        {
            ["user_name"] = name,
            ["password"] = pass
        });

        if (resp != null && resp.success)
        {
            Visual.SetUser(int.Parse(resp.args["user_id"]), resp.args["user_name"]);
            network.SendRequest(new Request { command = "get_chats" });
        }

        Visual.NotifyLogin(resp != null && resp.success);
    }

    static void Register(string name, string pass)
    {
        if (!network.connected)
            if (!network.Connect("127.0.0.1", 5000))
            { Visual.NotifyRegister(false); return; }

        var resp = network.SendAndWait("register", new Dictionary<string, string>
        {
            ["user_name"] = name,
            ["password"] = pass
        });

        Visual.NotifyRegister(resp != null && resp.success);
    }

    static void SendMessage(int chatId, string body)
    {
        network.SendRequest(new Request
        {
            command = "send_message",
            args = new() { ["chat_id"] = chatId.ToString(), ["body"] = body }
        });
    }

    static void CreateChat(string name)
    {
        var resp = network.SendAndWait("create_chat", new() { ["chat_name"] = name });
        if (resp != null && resp.success)
            Visual.NotifyCreateChat(true, int.Parse(resp.args["chat_id"]), name, "");
        else
            Visual.NotifyCreateChat(false, -1, "", resp?.args.GetValueOrDefault("message", "") ?? "нет ответа");
    }

    static void AddUser(int chatId, string userName)
    {
        var resp = network.SendAndWait("add_user", new()
        {
            ["chat_id"] = chatId.ToString(),
            ["user_name"] = userName
        });
        Visual.NotifyAddUser(resp != null && resp.success, resp?.args.GetValueOrDefault("message", "") ?? "");
    }

    static void KickUser(int chatId, string userName)
    {
        var resp = network.SendAndWait("kick_user", new()
        {
            ["chat_id"] = chatId.ToString(),
            ["user_name"] = userName
        });
        Visual.NotifyKickUser(resp != null && resp.success, resp?.args.GetValueOrDefault("message", "") ?? "");
    }

    static void GetChatMembers(int chatId)
    {
        network.SendRequest(new Request
        {
            command = "get_chat_members",
            args = new() { ["chat_id"] = chatId.ToString() }
        });
    }

    static void LeaveChat(int chatId)
    {
        var resp = network.SendAndWait("leave_chat", new() { ["chat_id"] = chatId.ToString() });
        Visual.NotifyLeave(resp != null && resp.success);
    }

    static void GetMessages(int chatId)
    {
        network.SendRequest(new Request
        {
            command = "get_messages",
            args = new() { ["chat_id"] = chatId.ToString() }
        });
    }

    static void HandleResponse(Response resp)
    {
        switch (resp.command)
        {
            case "new_message":
                Visual.AddMessage(
                    int.Parse(resp.args["chat_id"]),
                    int.Parse(resp.args["sender_id"]),
                    resp.args["body"]
                );
                break;

            case "get_chats":
                var chats = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(resp.args["chats"]);
                var list = new List<(int, string)>();
                foreach (var c in chats)
                    list.Add((c["chat_id"].GetInt32(), c["chat_name"].GetString()));
                Visual.SetChats(list);
                break;

            case "get_chat_members":
                int chatId = int.Parse(resp.args["chat_id"]);
                var users = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(resp.args["members"]);
                var memberList = new List<(int, string)>();
                foreach (var u in users)
                    memberList.Add((u["user_id"].GetInt32(), u["user_name"].GetString()));
                Visual.SetChatMembers(chatId, memberList);
                break;

            case "get_messages":
                int cid = int.Parse(resp.args["chat_id"]);
                var msgs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(resp.args["messages"]);
                var msgList = new List<(int, string)>();
                foreach (var m in msgs)
                    msgList.Add((m["sender_id"].GetInt32(), m["body"].GetString()));
                Visual.SetChatHistory(cid, msgList);
                break;

            case "added_to_chat":
                Visual.AddChat(
                    int.Parse(resp.args["chat_id"]),
                    resp.args["chat_name"]
                );
                break;

            case "removed_from_chat":
                Visual.RemoveChat(int.Parse(resp.args["chat_id"]));
                break;
        }
    }
}