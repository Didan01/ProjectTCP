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
    public Connection(Socket s) {
        socket = s;
        buffer = new byte[1024];
        connectionId = Guid.NewGuid().ToString();
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
            string requestRaw = Encoding.UTF8.GetString(connection.buffer, 0, bytesRead);
            Request request = JsonSerializer.Deserialize<Request>(requestRaw);
            Console.WriteLine(requestRaw);
            if (request.command == "create_chat") {
                int sender_id = int.Parse(request.sender_id);
                int chat_id = userRepo.CreateChat(new Chat(){chat_name = request.args["chat_name"]});
                Request response = new Request() {
                    command = "new_chat",
                    args = new Dictionary<string, string>() {
                        ["body"] = request.args["body"]
                    }
                };
            } else if (request.command == "send_message") {
                Console.WriteLine(request.args["body"]);
                Request response = new Request() {
                    command = "message_recive",
                    args = new Dictionary<string, string>() {
                        ["body"] = request.args["body"]
                    }
                };
                string responseText = JsonSerializer.Serialize(response);
                byte[] responseRaw = Encoding.UTF8.GetBytes(responseText);
                foreach (KeyValuePair<string, Connection> pair in connections) {
                    pair.Value.socket.BeginSend(responseRaw, 0, responseRaw.Length, SocketFlags.None, SendCallback, pair.Value);
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