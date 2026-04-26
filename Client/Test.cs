using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.IO;
using System.Linq;

namespace AwesomeChatClient;

// ================= М О Д Е Л И =================
public class Request {
    public string command { get; set; }
    public string sender_id { get; set; }
    public Dictionary<string, string> args { get; set; } = new();
}

public class User {
    public int user_id { get; set; }
    public string user_name { get; set; }
}

public class Messsage { 
    public int message_id { get; set; }
    public int sender_id { get; set; }
    public string body { get; set; }
    public string send_time { get; set; } 
}

// ================= К Л И Е Н Т =================
class Program {
    static TcpClient client;
    static NetworkStream stream;
    
    static string currentUserId = "-1";
    static string currentUserName = "Гость";
    static int activeChatId = -1; 
    static bool isRunning = true;

    static AutoResetEvent authEvent = new AutoResetEvent(false);
    static bool lastAuthSuccess = false;

    static readonly object consoleLock = new object();
    // Накопитель для приема сообщений
    static StringBuilder accumBuffer = new StringBuilder();

    static void Main(string[] args) {
        Console.Title = "TCP Messenger (Delimiter Mode)";
        Console.OutputEncoding = Encoding.UTF8;

        ConnectToServer("127.0.0.1", 5000);

        while (isRunning) {
            if (currentUserId == "-1") {
                ShowAuthMenu();
            } else {
                ShowMainMenu();
            }
        }
    }

    static void ConnectToServer(string ip, int port) {
        try {
            client = new TcpClient(ip, port);
            stream = client.GetStream();
            
            Thread receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();
            PrintSuccess("Подключено к серверу!");
        } catch (Exception ex) {
            PrintError($"Ошибка подключения: {ex.Message}");
            Environment.Exit(1);
        }
    }

    // ================= ЛОГИКА ПРОТОКОЛА (\n) =================
    static void ReceiveLoop() {
        byte[] buffer = new byte[1024];
        try {
            while (isRunning) {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead <= 0) break;

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                accumBuffer.Append(chunk);

                string currentContent = accumBuffer.ToString();
                while (currentContent.Contains("\n")) {
                    int index = currentContent.IndexOf("\n");
                    string jsonObject = currentContent.Substring(0, index);
                    
                    accumBuffer.Remove(0, index + 1);
                    currentContent = accumBuffer.ToString();

                    if (!string.IsNullOrWhiteSpace(jsonObject)) {
                        try {
                            var response = JsonSerializer.Deserialize<Request>(jsonObject);
                            if (response != null) HandleServerResponse(response);
                        } catch { /* Ошибка парсинга отдельного пакета */ }
                    }
                }
            }
        } catch {
            PrintSystemMessage("Потеряно соединение.");
        }
    }

    static void Send(string command, string senderId, Dictionary<string, string> args) {
        try {
            var req = new Request { command = command, sender_id = senderId, args = args };
            // ВАЖНО: Добавляем \n при отправке
            string json = JsonSerializer.Serialize(req) + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);
        } catch (Exception ex) {
            PrintError($"Ошибка отправки: {ex.Message}");
        }
    }

    static void ShowAuthMenu() {
        Console.Clear();
        PrintHeader("ВХОД / РЕГИСТРАЦИЯ");
        Console.WriteLine("1. Войти\n2. Регистрация\n0. Выход");
        Console.Write("\nВыбор > ");
        string choice = Console.ReadLine();
        if (choice == "0") Environment.Exit(0);
        
        Console.Write("Логин: "); string login = Console.ReadLine();
        Console.Write("Пароль: "); string password = Console.ReadLine();
        
        if (choice == "1") {
            Send("login", "-1", new() { ["user_name"] = login, ["password"] = password });
            authEvent.WaitOne(3000); // Ждем ответа от сервера
        } else if (choice == "2") {
            Send("register", "-1", new() { ["user_name"] = login, ["password"] = password });
            authEvent.WaitOne(3000);
            PrintSystemMessage("Попробуйте войти.");
            Thread.Sleep(1000);
        }
    }

    static void ShowMainMenu() {
        Console.Clear();
        PrintHeader($"МЕНЮ | {currentUserName} (ID: {currentUserId})");
        Console.WriteLine("1. Создать чат");
        Console.WriteLine("2. Войти в чат");
        Console.WriteLine("3. Добавить пользователя в чат"); // НОВЫЙ ПУНКТ
        Console.WriteLine("0. Выход");
        Console.Write("\nВыбор > ");
        string choice = Console.ReadLine();
        
        switch (choice) {
            case "1":
                Console.Write("Название чата: ");
                Send("create_chat", currentUserId, new() { ["chat_name"] = Console.ReadLine() });
                Thread.Sleep(500); 
                break;
            case "2":
                Console.Write("ID чата: ");
                if (int.TryParse(Console.ReadLine(), out int id)) EnterChat(id);
                break;
            case "3":
                Console.Write("ID чата: ");
                string cId = Console.ReadLine();
                Console.Write("ID пользователя, которого нужно добавить: ");
                string uId = Console.ReadLine();
                Send("add_member", currentUserId, new() { ["chat_id"] = cId, ["user_id"] = uId });
                PrintSystemMessage("Запрос на добавление отправлен.");
                Thread.Sleep(1000);
                break;
            case "0": 
                currentUserId = "-1"; 
                break;
        }
    }

    static void EnterChat(int chatId) {
        activeChatId = chatId;
        Console.Clear();
        PrintHeader($"ЧАТ #{chatId} (Введите /exit для выхода)");
        Send("get_chat_messages", currentUserId, new() { ["chat_id"] = chatId.ToString() });
        
        while (activeChatId == chatId) {
            RestoreInputLine();
            string input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;
            if (input == "/exit") { activeChatId = -1; break; }
            
            Send("send_message", currentUserId, new() { ["chat_id"] = chatId.ToString(), ["body"] = input });
        }
    }

    static void HandleServerResponse(Request response) {
        switch (response.command) {
            case "loged":
                if (response.args.ContainsKey("user_id") && !string.IsNullOrEmpty(response.args["user_id"])) {
                    currentUserId = response.args["user_id"];
                    lastAuthSuccess = true;
                    PrintSystemMessage("Вы успешно вошли!");
                } else { 
                    lastAuthSuccess = false; 
                    PrintSystemMessage("Ошибка входа!");
                }
                authEvent.Set();
                break;
            
            case "registered": 
                authEvent.Set(); 
                break;

            case "message_recive":
                var msg = JsonSerializer.Deserialize<Messsage>(response.args["message"]);
                PrintLiveMessage($"ID {msg.sender_id}", msg.body);
                break;

            case "chat_messsages":
                var msgs = JsonSerializer.Deserialize<List<Messsage>>(response.args["messages"]);
                lock(consoleLock) {
                    Console.WriteLine("\n--- ИСТОРИЯ ЧАТА ---");
                    foreach(var m in msgs) Console.WriteLine($"[{m.send_time}] ID {m.sender_id}: {m.body}");
                    Console.WriteLine("--------------------\n");
                }
                break;

            case "added_to_chat":
                PrintSystemMessage($"Пользователь {response.args["user_id"]} добавлен в чат {response.args["chat_id"]}");
                break;
        }
    }

    // --- Вспомогательные методы для красивого вывода ---
    static void PrintLiveMessage(string sender, string msg) {
        lock (consoleLock) {
            Console.WriteLine($"\n{sender}: {msg}");
            RestoreInputLine();
        }
    }

    static void PrintSystemMessage(string text) {
        lock (consoleLock) {
            Console.WriteLine($"\n[СИСТЕМА] {text}");
            RestoreInputLine();
        }
    }

    static void RestoreInputLine() {
        if (activeChatId != -1) Console.Write("Вы: ");
    }

    static void PrintHeader(string t) => Console.WriteLine($"\n=== {t} ===");
    static void PrintSuccess(string t) => Console.WriteLine(t);
    static void PrintError(string t) => Console.WriteLine($"ОШИБКА: {t}");
}