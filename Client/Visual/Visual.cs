using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Visual
{
    public static class Visual
    {

        public static Action<string, string> OnLogin;
        public static Action<string, string> OnRegister;
        public static Action<int, string> OnSendMessage;
        public static Action<string> OnCreateChat;
        public static Action<int, string> OnAddUser;
        public static Action<int, string> OnKickUser;
        public static Action<int> OnGetChatMembers;
        public static Action<int> OnLeaveChat;
        public static Action<int> OnGetMessages;


        static int currentUserId;
        static string currentUserName;

        static Dictionary<int, string> chats = new();
        static Dictionary<int, List<(int senderId, string text)>> messages = new();
        static readonly Dictionary<int, Dictionary<int, string>> chatMembers = new();
        static readonly Dictionary<int, string> allUsers = new();


        static readonly object _lock = new();

        static int currentChatId = -1;

        static string notificationText = "";
        static DateTime notificationUntil = DateTime.MinValue;
        const int NOTIFICATION_SECONDS = 4;

        static int W => Math.Max(40, Console.WindowWidth);
        static int H => Math.Max(15, Console.WindowHeight);


        public static void Start()
        {
            Console.CursorVisible = false;
            LoginScreen();
        }

        // ================= LOGIN =================

        static void LoginScreen()
        {
            while (true)
            {
                Console.Clear();

                DrawCentered("TCP ЧАТ СИСТЕМА", 2);
                DrawCentered("[1] Вход", 5);
                DrawCentered("[2] Регистрация", 6);
                DrawCentered("[0] Выход", 7);

                Console.SetCursorPosition(2, H - 2);
                Console.Write("Выберите: ");

                string choice = Console.ReadLine();

                if (choice == "1")
                {
                    Console.Clear();
                    DrawCenteredTop("ВХОД");

                    Console.SetCursorPosition(2, 4);
                    Console.Write("Логин: ");
                    string login = Console.ReadLine();

                    Console.Write("Пароль: ");
                    string pass = Console.ReadLine();

                    OnLogin?.Invoke(login, pass);
                }
                else if (choice == "2")
                {
                    Console.Clear();
                    DrawCenteredTop("РЕГИСТРАЦИЯ");

                    Console.SetCursorPosition(2, 4);
                    Console.Write("Логин: ");
                    string login = Console.ReadLine();

                    Console.Write("Пароль: ");
                    string pass = Console.ReadLine();

                    OnRegister?.Invoke(login, pass);
                }
                else if (choice == "0")
                    return;
            }
        }

        public static void NotifyLogin(bool success)
        {
            if (success)
            {
                MainMenu();
            }
            else
            {
                ShowToast("Ошибка входа: неверный логин или пароль");
            }
        }

        public static void NotifyRegister(bool success)
        {
            ShowToast(success ? "Регистрация успешна!" : "Имя пользователя уже занято!");
        }

        public static void SetUser(int id, string name)
        {
            currentUserId = id;
            currentUserName = name;
        }

        // ================= MENU =================

        static void MainMenu()
        {
            while (true)
            {
                Console.Clear();

                DrawCentered($"Пользователь: {currentUserName}", 1);

                Console.SetCursorPosition(2, 4);
                Console.WriteLine("ЧАТЫ:");

                int i = 1;
                var chatList = new List<int>();

                foreach (var c in chats)
                {
                    Console.WriteLine($"[{i}] {c.Value}");
                    chatList.Add(c.Key);
                    i++;
                }

                Console.WriteLine();
                Console.WriteLine("[C] Создать чат");
                Console.WriteLine("[0] Выход");

                Console.SetCursorPosition(2, H - 2);
                Console.Write("Выберите: ");
                string input = Console.ReadLine();

                if (input == "0") return;

                if (input?.ToLower() == "c")
                {
                    Console.Write("Название: ");
                    string name = Console.ReadLine();
                    OnCreateChat?.Invoke(name);
                    continue;
                }

                if (int.TryParse(input, out int index) &&
                    index > 0 && index <= chatList.Count)
                {
                    currentChatId = chatList[index - 1];
                    OnGetMessages?.Invoke(currentChatId);
                    ChatScreen();
                }
            }
        }

        // ================= CHAT =================

        static void ChatScreen()
        {
            while (true)
            {
                Console.Clear();

                DrawCenteredTop(chats[currentChatId]);

                if (messages.ContainsKey(currentChatId))
                    DrawMessages(messages[currentChatId]);

                DrawInputBar();

                string input = Console.ReadLine();

                if (input == "/back")
                    return;

                OnSendMessage?.Invoke(currentChatId, input);
            }
        }

        public static void AddMessage(int chatId, int senderId, string text)
        {
            if (!messages.ContainsKey(chatId))
                messages[chatId] = new List<(int, string)>();

            messages[chatId].Add((senderId, text));
        }

        public static void SetChatHistory(int chatId, List<(int, string)> msgs)
        {
            lock (_lock) messages[chatId] = msgs ?? new List<(int senderId, string text)>();
        }

        // ================= CHATS =================

        public static void SetChats(List<(int id, string name)> list)
        {
            lock ( _lock)
            {
                chats.Clear();
                if (list != null)
                    foreach (var c in list) chats[c.id] = c.name;
            }
        }

        public static void AddChat(int id, string name)
        {
            lock (_lock) chats[id] = name;
            ShowNotification($"Вы добавлены в чат «{name}»");
        }

        public static void RemoveChat(int id)
        {
            string removedName;
            lock (_lock)
            {
                chats.TryGetValue(id, out removedName);
                chats.Remove(id);
                messages.Remove(id);
                chatMembers.Remove(id);
            }
            ShowNotification($"Вы исключены из чата «{removedName ?? ""}»");
        }

        public static void NotifyCreateChat(bool success, int id, string name, string err)
        {
            if (success)
            {
                lock (_lock) chats[id] = name;
            }
            else
            {
                ShowToast("Ошибка создания чата: " + err);
            }
        }

        public static void NotifyAddUser(bool success, string msg)
        {
            Console.WriteLine(msg);
            Console.ReadLine();
        }

        public static void NotifyKickUser(bool success, string msg)
        {
            Console.WriteLine(msg);
            Console.ReadLine();
        }

        public static void SetChatMembers(int chatId, List<(int, string)> users)
        {
            Console.Clear();
            Console.WriteLine("Участники:");
            foreach (var u in users)
                Console.WriteLine(u.Item2);

            Console.ReadLine();
        }

        public static void AddOrUpdateUser(int id, string name)
        {
            lock (_lock) allUsers[id] = name;
        }

        public static void ShowNotification(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            notificationText = text;
            notificationUntil = DateTime.UtcNow.AddSeconds(NOTIFICATION_SECONDS);
        }

        static void DrawNotificationIfAny()
        {
            if (string.IsNullOrEmpty(notificationText)) return;
            if (DateTime.UtcNow >= notificationUntil)
            {
                notificationText = "";
                return;
            }

            int y = H - 2;
            ClearInside(y);
            string txt = "★ " + notificationText;
            string display = Truncate(txt, W - 4);
            int x = (W - display.Length) / 2;
            if (x < 2) x = 2;
            Console.SetCursorPosition(x, y);
            Console.Write(display);
        }

        static bool NotificationTick()
        {
            if (!string.IsNullOrEmpty(notificationText)
                && DateTime.UtcNow >= notificationUntil)
            {
                notificationText = "";
                return true;
            }
            return false;
        }

        public static void SetUsersLoading(bool loading)
        {

        }

        // ================= UI =================

        static void DrawMessages(List<(int senderId, string text)> list)
        {
            int y = 3;

            foreach (var msg in list)
            {
                string text = msg.senderId == currentUserId
                    ? $"Вы: {msg.text}"
                    : $"{msg.senderId}: {msg.text}";

                if (msg.senderId == currentUserId)
                {
                    int x = W - text.Length - 3;
                    if (x < 2) x = 2;
                    Console.SetCursorPosition(x, y);
                }
                else
                {
                    Console.SetCursorPosition(2, y);
                }

                Console.Write(text);
                y++;
            }
        }

        static void DrawInputBar()
        {
            Console.SetCursorPosition(2, H - 3);
            Console.Write(new string('-', W - 4));

            Console.SetCursorPosition(2, H - 2);
            Console.Write("Сообщение (/back): ");
        }

        static string Truncate(string s, int max)
        {
            if (s == null) return "";
            if (s.Length <= max) return s;
            if (max <= 0) return "";
            return s.Substring(0, max);
        }

        static void DrawCentered(string text, int y)
        {
            int x = (W - text.Length) / 2;
            if (x < 1) x = 1;
            ClearInside(y);
            Console.SetCursorPosition(x, y);
            Console.Write(Truncate(text, W - 2));
        }

        static void ClearInside(int y)
        {
            if (y <= 0 || y >= H - 1) return;
            try
            {
                Console.SetCursorPosition(1, y);
                Console.Write(new string(' ', Math.Max(0, W - 2)));
            }
            catch { }
        }

        static void DrawCenteredTop(string text)
        {
            Console.SetCursorPosition((W - text.Length) / 2, 1);
            Console.Write(text);
        }

        static void SafeClear()
        {
            try { Console.Clear(); }
            catch
            {
            }
        }

        static string SafeReadLine()
        {
            Console.CursorVisible = true;
            string s = Console.ReadLine();
            Console.CursorVisible = false;
            return s ?? "";
        }

        static string ReadPassword()
        {
            var sb = new StringBuilder();
            while (true)
            {
                var k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
                if (k.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0) { sb.Length--; Console.Write("\b \b"); }
                    continue;
                }
                if (char.IsControl(k.KeyChar)) continue;
                sb.Append(k.KeyChar);
                Console.Write('*');
            }
            return sb.ToString();
        }

        static void ShowToast(string text)
        {
            int y = H / 2;
            DrawCentered(text, y);
            DrawCentered("(нажмите Enter)", y + 1);
            try { Console.ReadLine(); } catch { }
        }
    }
}