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

    static void Main(string[] args) {
        Console.Title = "TCP Messenger (No-Delimiter Mode)";
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
            
            Thread receiveThread = new Thread(RawReceiveLoop) { IsBackground = true };
            receiveThread.Start();
            PrintSuccess("Подключено к серверу!");
        } catch (Exception ex) {
            PrintError($"Ошибка подключения: {ex.Message}");
            Environment.Exit(1);
        }
    }

    // ================= ЛОГИКА РАЗДЕЛЕНИЯ ПАКЕТОВ БЕЗ \n =================
    static void RawReceiveLoop() {
        byte[] buffer = new byte[8192];
        string accumulatedData = "";

        try {
            while (isRunning) {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead <= 0) break;

                // 1. Добавляем новые данные в "накопитель"
                accumulatedData += Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // 2. Пытаемся вытащить из накопителя законченные JSON-объекты
                while (true) {
                    accumulatedData = accumulatedData.TrimStart();
                    if (string.IsNullOrEmpty(accumulatedData) || accumulatedData[0] != '{') {
                        // Если данных нет или это не начало JSON, чистим и выходим из внутреннего цикла
                        if (!accumulatedData.Contains("{")) accumulatedData = "";
                        break;
                    }

                    int bracketCount = 0;
                    int endPos = -1;
                    bool inString = false;

                    // Ищем парную закрывающую скобку, игнорируя те, что в кавычках
                    for (int i = 0; i < accumulatedData.Length; i++) {
                        if (accumulatedData[i] == '"' && (i == 0 || accumulatedData[i-1] != '\\')) 
                            inString = !inString;

                        if (!inString) {
                            if (accumulatedData[i] == '{') bracketCount++;
                            else if (accumulatedData[i] == '}') {
                                bracketCount--;
                                if (bracketCount == 0) {
                                    endPos = i;
                                    break;
                                }
                            }
                        }
                    }

                    if (endPos != -1) {
                        // Извлекаем один полный JSON
                        string jsonObject = accumulatedData.Substring(0, endPos + 1);
                        accumulatedData = accumulatedData.Substring(endPos + 1);

                        try {
                            var response = JsonSerializer.Deserialize<Request>(jsonObject);
                            if (response != null) HandleServerResponse(response);
                        } catch (Exception ex) {
                            // Логируем ошибку десериализации конкретного объекта
                        }
                    } else {
                        // Полный объект еще не пришел, ждем следующей пачки байтов
                        break;
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
            byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req));
            stream.Write(data, 0, data.Length);
        } catch (Exception ex) {
            PrintError($"Ошибка отправки: {ex.Message}");
        }
    }

    // [Остальные методы: ShowAuthMenu, ShowMainMenu, EnterChat, HandleServerResponse остаются такими же]
    // Копируем их из предыдущей версии для работы интерфейса...

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
            authEvent.WaitOne(); 
            if (lastAuthSuccess) currentUserName = login;
        } else if (choice == "2") {
            Send("register", "-1", new() { ["user_name"] = login, ["password"] = password });
            authEvent.WaitOne();
            PrintSystemMessage("Регистрация завершена.");
        }
    }

    static void ShowMainMenu() {
        Console.Clear();
        PrintHeader($"МЕНЮ | {currentUserName}");
        Console.WriteLine("1. Создать чат\n2. Войти в чат\n0. Выход");
        Console.Write("\nВыбор > ");
        string choice = Console.ReadLine();
        if (choice == "1") {
            Console.Write("Название: ");
            Send("create_chat", currentUserId, new() { ["chat_name"] = Console.ReadLine() });
        } else if (choice == "2") {
            Console.Write("ID чата: ");
            if (int.TryParse(Console.ReadLine(), out int id)) EnterChat(id);
        } else if (choice == "0") currentUserId = "-1";
    }

    static void EnterChat(int chatId) {
        activeChatId = chatId;
        Console.Clear();
        PrintHeader($"ЧАТ #{chatId}");
        Send("get_chat_messages", currentUserId, new() { ["chat_id"] = chatId.ToString() });
        while (activeChatId == chatId) {
            RestoreInputLine();
            string input = Console.ReadLine();
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
                } else { lastAuthSuccess = false; }
                authEvent.Set();
                break;
            case "registered": authEvent.Set(); break;
            case "message_recive":
                var msg = JsonSerializer.Deserialize<Messsage>(response.args["message"]);
                PrintLiveMessage($"ID {msg.sender_id}", msg.body);
                break;
            case "chat_messsages":
                var msgs = JsonSerializer.Deserialize<List<Messsage>>(response.args["messages"]);
                lock(consoleLock) {
                    Console.WriteLine("\n--- ИСТОРИЯ ---");
                    foreach(var m in msgs) Console.WriteLine($"ID {m.sender_id}: {m.body}");
                    Console.WriteLine("---------------\n");
                }
                break;
        }
    }

    static void PrintLiveMessage(string sender, string msg) {
        lock (consoleLock) {
            ClearCurrentConsoleLine();
            Console.WriteLine($"{sender}: {msg}");
            RestoreInputLine();
        }
    }

    static void PrintSystemMessage(string text) {
        lock (consoleLock) {
            ClearCurrentConsoleLine();
            Console.WriteLine($"[!] {text}");
            RestoreInputLine();
        }
    }

    static void RestoreInputLine() {
        if (activeChatId != -1) Console.Write("Вы: ");
    }

    static void ClearCurrentConsoleLine() {
        int currentLineCursor = Console.CursorTop;
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth)); 
        Console.SetCursorPosition(0, currentLineCursor);
    }

    static void PrintHeader(string t) => Console.WriteLine($"=== {t} ===");
    static void PrintSuccess(string t) => Console.WriteLine(t);
    static void PrintError(string t) => Console.WriteLine(t);
}