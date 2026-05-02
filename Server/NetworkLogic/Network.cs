using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Database.Repositories;
using Server.Database.Models;

namespace Server.NetworkLogic;

public class Connection {
    public string connectionId;
    public Socket socket;
    public byte[] buffer;
    public int user_id;
    public StringBuilder accumBuffer = new StringBuilder();
    public Connection(Socket s) {
        socket = s;
        buffer = new byte[1024];
        connectionId = Guid.NewGuid().ToString();
        user_id = -1;
    }
}

public class Request {
    public string command {get; set;}
    public string sender_id {get; set;}
    public Dictionary<string, string> args {get; set;}
}

public class Network {
    private static ConcurrentDictionary<string, Connection> connections = new();
    private static UserRepository userRepo = new();
    private static ChatRepository chatRepo = new();
    private static void SendCallback(IAsyncResult ar) {
        Connection connection = (Connection)ar.AsyncState;
        connection.socket.EndSend(ar);
    }
    private static void RecivecallBack(IAsyncResult ar) {
        Connection connection = (Connection)ar.AsyncState;
        try {
            int bytesRead = connection.socket.EndReceive(ar);
            Console.WriteLine(bytesRead);
            if (bytesRead == 0) {
                Disconnect(connection);
                return;
            }
            string chunk = Encoding.UTF8.GetString(connection.buffer, 0, bytesRead);
            connection.accumBuffer.Append(chunk);
            string current = connection.accumBuffer.ToString();
            while (current.Contains("\n")) {
                int i = current.IndexOf("\n");
                string requestRaw = current.Substring(0, i);
                connection.accumBuffer.Remove(0, i + 1);
                current = connection.accumBuffer.ToString();
                Request request = JsonSerializer.Deserialize<Request>(requestRaw);
                Console.WriteLine(requestRaw);
                if (request.command == "create_chat") {
                    int sender_id = int.Parse(request.sender_id);
                    int chat_id = userRepo.CreateChat(new Chat(){chat_name = request.args["chat_name"]});
                    Chat chat = new Chat(){chat_id = chat_id, chat_name = request.args["chat_name"]};
                    chatRepo.AddUser(sender_id, chat_id);
                    Request response = new Request() {
                        command = "new_chat",
                        args = new Dictionary<string, string>() {
                            ["chat"] = JsonSerializer.Serialize(chat),
                        }
                    };
                    string responseText = JsonSerializer.Serialize(response);
                    byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                    connection.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, connection);
                } else if (request.command == "send_message") {
                    int chat_id = int.Parse(request.args["chat_id"]);
                    int sender_id = int.Parse(request.sender_id);
                    Messsage message = new Messsage(){body = request.args["body"], sender_id = sender_id, send_time = DateTime.Now.ToString()};
                    userRepo.SendMessage(chat_id, message);
                    Request response = new Request() {
                        command = "message_recive",
                        args = new Dictionary<string, string>() {
                            ["chat_id"] = chat_id.ToString(),
                            ["message"] = JsonSerializer.Serialize(message),
                        }
                    };
                    string responseText = JsonSerializer.Serialize(response);
                    byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                    List<int> users = chatRepo.GetChatMembers(chat_id).Select(u => u.user_id).ToList();
                    Dictionary<string, Connection> members = connections.Where(p => users.Contains(p.Value.user_id) && p.Value.user_id != sender_id).ToDictionary();
                    foreach (var p in members) {
                        p.Value.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, p.Value);
                    }
                } else if (request.command == "login") {
                    User loged = userRepo.Login(new User(){user_name = request.args["user_name"], password = request.args["password"]});
                    if (loged != null) {connection.user_id = loged.user_id;}
                    Request response = new Request() {
                        command = "loged",
                        args = new Dictionary<string, string>() {
                            ["user"] = JsonSerializer.Serialize(loged)
                        }
                    };  
                    string responseText = JsonSerializer.Serialize(response);
                    byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                    connection.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, connection);
                } else if (request.command == "register") {
                    User regUser = new User(){user_name = request.args["user_name"], password = request.args["password"]};
                    bool registered = userRepo.Register(regUser );
                    Request response = new Request() {
                        command = "registered",
                        args = new Dictionary<string, string>() {
                            ["user"] = JsonSerializer.Serialize(regUser),
                            ["success"] = registered.ToString(),
                        }
                    };  
                    string responseText = JsonSerializer.Serialize(response);
                    byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                    connection.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, connection);
                } else if (request.command == "add_member") {
                    int user_id = int.Parse(request.args["user_id"]);
                    int chat_id = int.Parse(request.args["chat_id"]);
                    bool added = chatRepo.AddUser(user_id, chat_id);
                    if (added) {
                        User user = userRepo.GetUser(user_id);
                        Chat chat = chatRepo.GetChat(chat_id);
                        Request response = new Request() {
                            command = "added_to_chat",
                            args = new Dictionary<string, string>() {
                                ["user"] = JsonSerializer.Serialize(user),
                                ["chat"] = JsonSerializer.Serialize(chat),
                            }
                        };   
                        string responseText = JsonSerializer.Serialize(response);
                        byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                        List<int> users = chatRepo.GetChatMembers(chat_id).Select(u => u.user_id).ToList();
                        Dictionary<string, Connection> members = connections.Where(p => users.Contains(p.Value.user_id)).ToDictionary();
                        Connection reciver = connections.Where(p => p.Value.user_id == user_id).ToArray().FirstOrDefault().Value;
                        if (reciver != null) {
                            reciver.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, reciver);   
                        }
                        foreach (var p in members) {
                            p.Value.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, p.Value);
                        }
                    }
                } else if (request.command == "kick_member") {
                    int user_id = int.Parse(request.args["user_id"]);
                    int chat_id = int.Parse(request.args["chat_id"]);
                    bool added = chatRepo.KickUser(user_id, chat_id);
                    if (added) {
                        User user = userRepo.GetUser(user_id);
                        Chat chat = chatRepo.GetChat(chat_id);
                        Request response = new Request() {
                            command = "kicked_from_chat",
                            args = new Dictionary<string, string>() {
                                ["user"] = JsonSerializer.Serialize(user),
                                ["chat"] = JsonSerializer.Serialize(chat),
                            }
                        };
                        string responseText = JsonSerializer.Serialize(response);
                        byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                        List<int> users = chatRepo.GetChatMembers(chat_id).Select(u => u.user_id).ToList();
                        Dictionary<string, Connection> members = connections.Where(p => users.Contains(p.Value.user_id)).ToDictionary();
                        Connection reciver = connections.Where(p => p.Value.user_id == user_id).ToArray().FirstOrDefault().Value;
                        if (reciver != null) {
                            reciver.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, reciver);   
                        }
                        foreach (var p in members) {
                            p.Value.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, p.Value);
                        }   
                    }
                } else if (request.command == "get_chat_members") {
                    int chat_id = int.Parse(request.args["chat_id"]);
                    List<User> users = chatRepo.GetChatMembers(chat_id);
                    Chat chat = chatRepo.GetChat(chat_id);
                    Request response = new Request() {
                        command = "chat_members",
                        args = new Dictionary<string, string>() {
                            ["chat"] = JsonSerializer.Serialize(chat),
                            ["members"] = JsonSerializer.Serialize(users),
                        }
                    };
                    string responseText = JsonSerializer.Serialize(response);
                    byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                    connection.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, connection);
                } else if (request.command == "get_chat_messages") {
                    int chat_id = int.Parse(request.args["chat_id"]);
                    List<Messsage> messsages = chatRepo.GetChatMessages(chat_id);
                    Chat chat = chatRepo.GetChat(chat_id);
                    Request response = new Request() {
                        command = "chat_messsages",
                        args = new Dictionary<string, string>() {
                            ["chat"] = JsonSerializer.Serialize(chat),
                            ["messages"] = JsonSerializer.Serialize(messsages),
                        }
                    };
                    string responseText = JsonSerializer.Serialize(response);
                    byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                    connection.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, connection);
                } else if (request.command == "get_user") {
                    int user_id = int.Parse(request.args["user_id"]);
                    User user = userRepo.GetUser(user_id);
                    Request response = new Request() {
                        command = "user",
                        args = new Dictionary<string, string>() {
                            ["user"] = JsonSerializer.Serialize(user),
                        }
                    };
                    string responseText = JsonSerializer.Serialize(response);
                    byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                    connection.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, connection);
                } else if (request.command == "get_chat") {
                    int chat_id = int.Parse(request.args["chat_id"]);
                    Chat chat = chatRepo.GetChat(chat_id);
                    Request response = new Request() {
                        command = "chat",
                        args = new Dictionary<string, string>() {
                            ["chat"] = JsonSerializer.Serialize(chat),
                        }
                    };
                    string responseText = JsonSerializer.Serialize(response);
                    byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                    connection.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, connection);
                } else if (request.command == "get_user_chats") {
                    int user_id = int.Parse(request.args["user_id"]);
                    List<Chat> chats = userRepo.GetUserChats(user_id);
                    User user = userRepo.GetUser(user_id);
                    Request response = new Request() {
                        command = "user_chats",
                        args = new Dictionary<string, string>() {
                            ["user"] = JsonSerializer.Serialize(user),
                            ["chats"] = JsonSerializer.Serialize(chats),
                        }
                    };
                    string responseText = JsonSerializer.Serialize(response);
                    byte[] responseRaw = Encoding.UTF8.GetBytes(responseText+"\n");
                    connection.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, connection);
                }
            }
            connection.socket.BeginReceive(connection.buffer, 0, connection.buffer.Length, SocketFlags.None, RecivecallBack, connection);
        } catch (Exception e) {
            Console.Write($"error: {e.Message}");
            if (!connection.socket.Connected) Disconnect(connection);
        }
    }
    private static void Disconnect(Connection connection) {
        if (connections.TryRemove(connection.connectionId, out _)) {
            connection.socket.Close();
        }
    }
    private static void AcceptcallBack(IAsyncResult ar) {
        Socket server = (Socket)ar.AsyncState;
        Connection connection = new Connection(server.EndAccept(ar));
        connections.TryAdd(connection.connectionId, connection);
        connection.socket.BeginReceive(connection.buffer, 0, connection.buffer.Length, SocketFlags.None, RecivecallBack, connection);
        server.BeginAccept(AcceptcallBack, server);
    }
    public static void Serve(int port) {
        Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        server.Bind(new IPEndPoint(IPAddress.Any, port));
        server.Listen(10);
        server.BeginAccept(AcceptcallBack, server);
        Console.ReadLine();
    }
}