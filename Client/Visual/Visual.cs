using System;
using System.Collections.Generic;

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

        static int currentChatId = -1;

        static int W => Console.WindowWidth;
        static int H => Console.WindowHeight;


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
                MainMenu();
            else
            {
                Console.WriteLine("Ошибка входа");
                Console.ReadLine();
            }
        }

        public static void NotifyRegister(bool success)
        {
            Console.WriteLine(success ? "Успешно!" : "Ошибка регистрации");
            Console.ReadLine();
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
            messages[chatId] = msgs;
        }

        // ================= CHATS =================

        public static void SetChats(List<(int id, string name)> list)
        {
            chats.Clear();
            foreach (var c in list)
                chats[c.id] = c.name;
        }

        public static void AddChat(int id, string name)
        {
            chats[id] = name;
        }

        public static void RemoveChat(int id)
        {
            chats.Remove(id);
        }

        public static void NotifyCreateChat(bool success, int id, string name, string err)
        {
            if (success)
                chats[id] = name;
            else
            {
                Console.WriteLine(err);
                Console.ReadLine();
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

        public static void NotifyLeave(bool success)
        {
            Console.WriteLine(success ? "Вы вышли из чата" : "Ошибка");
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

        static void DrawCentered(string text, int y)
        {
            Console.SetCursorPosition((W - text.Length) / 2, y);
            Console.Write(text);
        }

        static void DrawCenteredTop(string text)
        {
            Console.SetCursorPosition((W - text.Length) / 2, 1);
            Console.Write(text);
        }
    }
}