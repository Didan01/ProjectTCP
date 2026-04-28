using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Client.NetworkLogic;

public class Request
{
    public string command { get; set; }
    public string sender_id { get; set; }
    public Dictionary<string, string> args { get; set; }
}

public static class Network
{
    private static Socket socket;
    private static Thread receiveThread;
    private static volatile bool running;

    private static int currentUserId = -1;

    private static readonly BlockingCollection<Request> responses = new();

    public static Action<Request> OnPush;

    private static readonly HashSet<string> responseCommands = new()
    {
        "loged", "registered", "new_chat", "chat_messsages", "user_chats"
    };

    public static void Connect(string host, int port)
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(host, port);

        running = true;
        receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();
    }

    public static void SetUserId(int id) => currentUserId = id;

    private static void ReceiveLoop()
    {
        var buffer = new byte[4096];
        var SB = new StringBuilder();

        try
        {
            while (running)
            {
                int n = socket.Receive(buffer);
                if (n == 0) break;

                SB.Append(Encoding.UTF8.GetString(buffer, 0, n));

                while (true)
                {
                    string current = SB.ToString();
                    int sep = current.IndexOf('\n');
                    if (sep < 0) break;

                    string raw = current.Substring(0, sep);
                    SB.Remove(0, sep + 1);

                    Request request;
                    try { request = JsonSerializer.Deserialize<Request>(raw); }
                    catch { continue; }

                    if (request == null || request.command == null) continue;

                    try
                    {
                        if (responseCommands.Contains(request.command))
                            responses.Add(request);
                        else
                            OnPush?.Invoke(request);
                    }
                    catch { }
                }
            }
        }
        catch { }
        finally
        {
            running = false;
        }
    }

    private static void Send(Request request)
    {
        if (request.sender_id == null && currentUserId != -1)
            request.sender_id = currentUserId.ToString();

        string json = JsonSerializer.Serialize(request);
        byte[] json_str = Encoding.UTF8.GetBytes(json + "\n");
        socket.Send(json_str);
    }

    private static Request WaitResponse()
    {
        if (responses.TryTake(out var response, TimeSpan.FromSeconds(10)))
            return response;
        throw new Exception("Сервер не ответил вовремя");
    }

    public static Request Login(string userName, string password)
    {
        Send(new Request
        {
            command = "login",
            args = new() { ["user_name"] = userName, ["password"] = password }
        });
        return WaitResponse();
    }

    public static Request Register(string userName, string password)
    {
        Send(new Request
        {
            command = "register",
            args = new() { ["user_name"] = userName, ["password"] = password }
        });
        return WaitResponse();
    }

    public static Request CreateChat(string chatName)
    {
        Send(new Request
        {
            command = "create_chat",
            args = new() { ["chat_name"] = chatName }
        });
        return WaitResponse();
    }

    public static void SendMessage(int chatId, string body)
    {
        Send(new Request
        {
            command = "send_message",
            args = new()
            {
                ["chat_id"] = chatId.ToString(),
                ["body"] = body
            }
        });
    }

    public static Request GetUserChats()
    {
        Send(new Request
        {
            command = "get_user_chats",
            args = new() { ["user_id"] = currentUserId.ToString() }
        });
        return WaitResponse();
    }

    public static Request GetChatMessages(int chatId)
    {
        Send(new Request
        {
            command = "get_chat_messages",
            args = new() { ["chat_id"] = chatId.ToString() }
        });
        return WaitResponse();
    }

    public static Request GetChatMembers(int chatId)
    {
        //Дописать
        return null;
    }

    public static Request GetUser(int userId)
    {
        //Дописать
        return null;
    }

    public static void AddMember(int userId, int chatId)
    {
        //Дописать
    }

    public static void KickMember(int userId, int chatId)
    {
        //Дописать
    }

    public static void Disconnect()
    {
        running = false;
        try { socket?.Shutdown(SocketShutdown.Both); } catch { }
        socket?.Close();
    }
}