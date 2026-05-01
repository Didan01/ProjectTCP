using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Client.Visual
{
    public static class Visual
    {
        static int currentUserId;
        static string currentUserName;

        static readonly Dictionary<int, string> chats = new();
        static readonly Dictionary<int, List<(int senderId, string text)>> messages = new();

        static readonly Dictionary<int, Dictionary<int, string>> chatMembers = new();
        static readonly Dictionary<int, string> allUsers = new();

        static readonly object _lock = new();
        enum Screen { None, Login, UsersList, Chats, ChatRoom }
        static volatile Screen currentScreen = Screen.None;
        static int currentChatId = -1;

        static int chatScroll = 0;

        static volatile bool dirty = false;

        static int usersScroll = 0;
        const int USERS_VISIBLE = 7;

        static volatile bool usersLoading = false;

        static string notificationText = "";
        static DateTime notificationUntil = DateTime.MinValue;
        const int NOTIFICATION_SECONDS = 4;

        static int W => Math.Max(40, Console.WindowWidth);
        static int H => Math.Max(15, Console.WindowHeight);

        public static void Start()
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Console.CursorVisible = false;
            LoginScreen();
        }

        public static void SetUser(int id, string name)
        {
            currentUserId = id;
            currentUserName = name;
        }

        static void LoginScreen()
        {
            currentScreen = Screen.Login;
            while (true)
            {
                SafeClear();
                DrawHeader("TCP ЧАТ");

                int y = 5;
                DrawCentered("[1] Вход", y++);
                DrawCentered("[2] Регистрация", y++);

                DrawFooter("Выберите цифру   Esc - выход");

                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Escape)
                    return;

                if (key.KeyChar == '1')
                {
                    SafeClear();
                    DrawHeader("ВХОД");
                    DrawFooter("");
                    Console.CursorVisible = true;
                    Console.SetCursorPosition(2, 4);
                    Console.Write("Логин: ");
                    string login = SafeReadLine();
                    Console.SetCursorPosition(2, 5);
                    Console.Write("Пароль: ");
                    string pass = ReadPassword();
                    Console.CursorVisible = false;

                    DrawCentered("Вход...", H / 2);
                    Client.Program.OnLogin(login, pass);
                }
                else if (key.KeyChar == '2')
                {
                    SafeClear();
                    DrawHeader("РЕГИСТРАЦИЯ");
                    DrawFooter("");
                    Console.CursorVisible = true;
                    Console.SetCursorPosition(2, 4);
                    Console.Write("Логин: ");
                    string login = SafeReadLine();
                    Console.SetCursorPosition(2, 5);
                    Console.Write("Пароль: ");
                    string pass = ReadPassword();
                    Console.CursorVisible = false;

                    Client.Program.OnRegister(login, pass);
                }
            }
        }

        public static void NotifyLogin(bool success)
        {
            if (success)
            {
                UsersListScreen();
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

        static void UsersListScreen()
        {
            currentScreen = Screen.UsersList;
            usersScroll = 0;

            DrawUsersList();

            while (currentScreen == Screen.UsersList)
            {
                if (!Console.KeyAvailable)
                {
                    if (NotificationTick()) dirty = true;
                    if (dirty) { DrawUsersList(); dirty = false; }
                    Thread.Sleep(50);
                    continue;
                }

                var key = Console.ReadKey(true);

                List<(int id, string name)> users;
                lock (_lock) users = SnapshotUsers();

                int total = users.Count;
                int maxScroll = Math.Max(0, total - USERS_VISIBLE);

                if (key.Key == ConsoleKey.UpArrow)
                {
                    if (usersScroll > 0) { usersScroll--; DrawUsersList(); }
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    if (usersScroll < maxScroll) { usersScroll++; DrawUsersList(); }
                }
                else if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.RightArrow)
                {
                    currentScreen = Screen.Chats;
                    ChatsScreen();
                    if (currentScreen == Screen.UsersList) DrawUsersList();
                }
                else if (key.Key == ConsoleKey.R)
                {
                    lock (_lock) allUsers.Clear();
                    usersScroll = 0;
                    DrawUsersList();
                    Client.Program.LoadAllUsers();
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    return;
                }
            }
        }

        static List<(int id, string name)> SnapshotUsers()
        {
            var list = new List<(int id, string name)>();
            foreach (var kv in allUsers) list.Add((kv.Key, kv.Value));
            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }

        static void DrawUsersList()
        {
            SafeClear();
            DrawHeader($"ПОЛЬЗОВАТЕЛИ В СИСТЕМЕ");

            List<(int id, string name)> users;
            lock (_lock) users = SnapshotUsers();

            int total = users.Count;
            int maxScroll = Math.Max(0, total - USERS_VISIBLE);
            if (usersScroll > maxScroll) usersScroll = maxScroll;
            if (usersScroll < 0) usersScroll = 0;

            int contentLeft = 3;

            int statusY = 2;
            Console.SetCursorPosition(contentLeft, statusY);
            string loadStatus = usersLoading ? "  [загрузка...]" : "";
            Console.Write(Truncate(
                $"Всего: {total}    Прокрутка: {usersScroll}/{maxScroll}{loadStatus}",
                W - 6));

            int listTop = statusY + 2;

            if (usersScroll > 0)
            {
                Console.SetCursorPosition(contentLeft, listTop - 1);
                Console.Write("▲");
            }

            int shown = 0;
            for (int i = usersScroll; i < users.Count && shown < USERS_VISIBLE; i++, shown++)
            {
                var u = users[i];
                Console.SetCursorPosition(contentLeft, listTop + shown);
                string marker = u.id == currentUserId ? " (вы)" : "";
                string line = $"{u.name}[{u.id}]{marker}";
                Console.Write(Truncate(line, W - 6));
            }

            if (usersScroll < maxScroll)
            {
                Console.SetCursorPosition(contentLeft, listTop + USERS_VISIBLE);
                Console.Write("▼");
            }

            DrawFooter("↑/↓ — листать   Enter — к чатам   R — обновить   Esc — выйти");
        }

        static void ChatsScreen()
        {
            currentScreen = Screen.Chats;
            var numBuffer = new StringBuilder();
            DrawChats(numBuffer.ToString());

            while (currentScreen == Screen.Chats)
            {
                if (!Console.KeyAvailable)
                {
                    if (NotificationTick()) dirty = true;
                    if (dirty) { DrawChats(numBuffer.ToString()); dirty = false; }
                    Thread.Sleep(40);
                    continue;
                }

                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Escape)
                {
                    currentScreen = Screen.UsersList;
                    return;
                }

                if (key.Key == ConsoleKey.C || key.KeyChar == 'c' || key.KeyChar == 'C'
                    || key.KeyChar == 'с' || key.KeyChar == 'С')
                {
                    SafeClear();
                    DrawHeader("СОЗДАНИЕ ЧАТА");
                    DrawFooter("");
                    Console.SetCursorPosition(2, 4);
                    Console.CursorVisible = true;
                    Console.Write("Название чата: ");
                    string name = SafeReadLine();
                    Console.CursorVisible = false;
                    if (!string.IsNullOrWhiteSpace(name))
                        Client.Program.OnCreateChat(name);
                    numBuffer.Clear();
                    DrawChats(numBuffer.ToString());
                    continue;
                }

                if (char.IsDigit(key.KeyChar))
                {
                    numBuffer.Append(key.KeyChar);
                    DrawChats(numBuffer.ToString());
                    continue;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (numBuffer.Length > 0) numBuffer.Length--;
                    DrawChats(numBuffer.ToString());
                    continue;
                }

                if (key.Key == ConsoleKey.Enter && numBuffer.Length > 0
                    && int.TryParse(numBuffer.ToString(), out int idx))
                {
                    numBuffer.Clear();

                    List<(int id, string name)> snap;
                    lock (_lock)
                    {
                        snap = new List<(int id, string name)>();
                        foreach (var kv in chats) snap.Add((kv.Key, kv.Value));
                    }

                    if (idx > 0 && idx <= snap.Count)
                    {
                        currentChatId = snap[idx - 1].id;
                        Client.Program.OnGetMessages(currentChatId);
                        Client.Program.OnGetMembers(currentChatId);
                        ChatRoomScreen();
                        if (currentScreen == Screen.Chats) DrawChats(numBuffer.ToString());
                    }
                    else
                    {
                        DrawChats(numBuffer.ToString());
                    }
                }
            }
        }

        static void DrawChats(string numBuffer = "")
        {
            SafeClear();
            DrawHeader($"ВАШИ ЧАТЫ");

            List<(int id, string name)> snap;
            lock (_lock)
            {
                snap = new List<(int id, string name)>();
                foreach (var kv in chats) snap.Add((kv.Key, kv.Value));
            }

            int contentLeft = 3;
            int y = 2;

            if (snap.Count == 0)
            {
                Console.SetCursorPosition(contentLeft, y);
                Console.Write(Truncate("(пока нет чатов - нажмите C для создания)", W - 6));
            }
            else
            {
                int i = 1;
                foreach (var c in snap)
                {
                    if (y >= H - 4) break;
                    Console.SetCursorPosition(contentLeft, y++);
                    string line = $"[{i++}] {c.name} ({c.id})";
                    Console.Write(Truncate(line, W - 6));
                }
            }
            if (!string.IsNullOrEmpty(numBuffer))
            {
                int promptY = H - 3;
                ClearInside(promptY);
                Console.SetCursorPosition(contentLeft, promptY);
                Console.Write(Truncate($"Открыть чат: {numBuffer} - Enter", W - 6));
            }

            DrawFooter("Введите номер чата   C - создать чат   Esc — назад");
        }

        static readonly StringBuilder inputBuffer = new();

        static void ChatRoomScreen()
        {
            currentScreen = Screen.ChatRoom;
            inputBuffer.Clear();
            chatScroll = 0;
            DrawChatRoom();

            while (currentScreen == Screen.ChatRoom)
            {
                if (!Console.KeyAvailable)
                {
                    if (NotificationTick()) dirty = true;
                    if (dirty) { DrawChatRoom(); dirty = false; }
                    Thread.Sleep(40);
                    continue;
                }

                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    string text = inputBuffer.ToString();
                    inputBuffer.Clear();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        DrawInputLine();
                        continue;
                    }

                    if (HandleChatCommand(text))
                    {
                        if (currentScreen != Screen.ChatRoom) return;
                        DrawChatRoom();
                        continue;
                    }

                    Client.Program.OnSendMessage(currentChatId, text);
                    chatScroll = 0;
                    DrawChatRoom();
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (inputBuffer.Length > 0)
                        inputBuffer.Length--;
                    DrawInputLine();
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    currentScreen = Screen.Chats;
                    return;
                }
                else if (key.Key == ConsoleKey.UpArrow)
                {
                    ScrollChat(+1);
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    ScrollChat(-1);
                }
                else if (key.Key == ConsoleKey.PageUp)
                {
                    ScrollChat(+ChatAreaHeight() / 2);
                }
                else if (key.Key == ConsoleKey.PageDown)
                {
                    ScrollChat(-ChatAreaHeight() / 2);
                }
                else if (key.Key == ConsoleKey.Home)
                {
                    ScrollChat(int.MaxValue / 2);
                }
                else if (key.Key == ConsoleKey.End)
                {
                    chatScroll = 0;
                    DrawChatRoom();
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    inputBuffer.Append(key.KeyChar);
                    DrawInputLine();
                }
            }
        }

        static int ChatAreaHeight()
        {
            int top = 3;
            int bottom = H - 4;
            return Math.Max(1, bottom - top);
        }
        static void ScrollChat(int delta)
        {
            int total = RenderChatLines().Count;
            int area = ChatAreaHeight();
            int maxScroll = Math.Max(0, total - area);

            chatScroll += delta;
            if (chatScroll < 0) chatScroll = 0;
            if (chatScroll > maxScroll) chatScroll = maxScroll;

            DrawChatRoom();
        }
        static bool HandleChatCommand(string text)
        {
            string t = text.Trim();
            string lower = t.ToLowerInvariant();

            if (lower == "/members" || lower == "members")
            {
                ShowMembersScreen();
                return true;
            }

            if (lower.StartsWith("/add ") || lower.StartsWith("add "))
            {
                string arg = t.Substring(t.IndexOf(' ') + 1).Trim();
                if (int.TryParse(arg, out int uid))
                    Client.Program.OnAddUser(uid, currentChatId);
                return true;
            }

            if (lower.StartsWith("/kick ") || lower.StartsWith("kick "))
            {
                string arg = t.Substring(t.IndexOf(' ') + 1).Trim();
                if (int.TryParse(arg, out int uid))
                    Client.Program.OnKickUser(uid, currentChatId);
                return true;
            }

            if (lower == "/help" || lower == "help" || lower == "?")
                return true;

            return false;
        }

        static void ShowMembersScreen()
        {
            SafeClear();
            string chatName;
            lock (_lock) chats.TryGetValue(currentChatId, out chatName);
            DrawHeader($"УЧАСТНИКИ ЧАТА: {chatName}");

            Client.Program.OnGetMembers(currentChatId);

            Dictionary<int, string> members;
            lock (_lock)
            {
                if (!chatMembers.TryGetValue(currentChatId, out members))
                    members = new Dictionary<int, string>();
                else
                    members = new Dictionary<int, string>(members);
            }

            int contentLeft = 3;
            int y = 2;
            if (members.Count == 0)
            {
                Console.SetCursorPosition(contentLeft, y);
                Console.Write("(нет участников)");
            }
            else
            {
                foreach (var kv in members)
                {
                    if (y >= H - 3) break;
                    Console.SetCursorPosition(contentLeft, y++);
                    string marker = kv.Key == currentUserId ? " (вы)" : "";
                    Console.Write(Truncate($"{kv.Value}[{kv.Key}]{marker}", W - 6));
                }
            }

            DrawFooter("Нажмите любую клавишу для возврата...");
            try { Console.ReadKey(true); } catch { }
        }
        static List<(bool mine, string line)> RenderChatLines()
        {
            int maxWidth = Math.Max(10, W - 6);

            List<(int senderId, string text)> snap;
            lock (_lock)
            {
                if (!messages.TryGetValue(currentChatId, out var list))
                    snap = new List<(int senderId, string text)>();
                else
                    snap = new List<(int senderId, string text)>(list);
            }

            var rendered = new List<(bool mine, string line)>();
            foreach (var msg in snap)
            {
                bool mine = msg.senderId == currentUserId;
                string prefix = mine ? "Вы: " : $"{ResolveName(msg.senderId)}[{msg.senderId}]: ";
                string full = prefix + (msg.text ?? "");
                foreach (var seg in WrapText(full, maxWidth))
                    rendered.Add((mine, seg));
            }
            return rendered;
        }

        static void DrawChatRoom()
        {
            SafeClear();

            string chatName;
            lock (_lock) chats.TryGetValue(currentChatId, out chatName);
            DrawHeader($"ЧАТ: {chatName ?? ""} ({currentChatId})");

            int top = 2;
            int bottom = H - 5;
            int areaHeight = Math.Max(1, bottom - top + 1);

            var rendered = RenderChatLines();

            int total = rendered.Count;
            int maxScroll = Math.Max(0, total - areaHeight);
            if (chatScroll > maxScroll) chatScroll = maxScroll;
            if (chatScroll < 0) chatScroll = 0;

            int endIndex = total - chatScroll;
            int startIndex = Math.Max(0, endIndex - areaHeight);

            int contentLeft = 2;
            int contentRight = W - 3;
            int contentWidth = contentRight - contentLeft + 1;

            for (int i = 0; i < areaHeight && startIndex + i < endIndex; i++)
            {
                var item = rendered[startIndex + i];
                int y = top + i;
                ClearInside(y);
                if (item.mine)
                {
                    int x = contentRight - item.line.Length + 1;
                    if (x < contentLeft) x = contentLeft;
                    Console.SetCursorPosition(x, y);
                }
                else
                {
                    Console.SetCursorPosition(contentLeft, y);
                }
                Console.Write(Truncate(item.line, contentWidth));
            }

            Console.SetCursorPosition(contentLeft, H - 3);
            Console.Write(new string('─', contentWidth));

            DrawFooter("↑/↓ PgUp/PgDn - навигация   add <id>   kick <id>   /members   Esc — назад");
            DrawInputLine();
        }

        static void DrawInputLine()
        {
            int y = H - 4;
            ClearInside(y);
            Console.SetCursorPosition(2, y);
            string prompt = "> ";
            Console.Write(prompt);
            string text = inputBuffer.ToString();
            int avail = W - 4 - prompt.Length;
            if (avail < 1) avail = 1;
            if (text.Length > avail) text = text.Substring(text.Length - avail);
            Console.Write(text);
        }

        public static void AddMessage(int chatId, int senderId, string text)
        {
            int extraLines = 0;
            lock (_lock)
            {
                if (!messages.ContainsKey(chatId))
                    messages[chatId] = new List<(int senderId, string text)>();
                messages[chatId].Add((senderId, text));
            }

            if (currentScreen == Screen.ChatRoom && currentChatId == chatId && chatScroll > 0)
            {
                int maxWidth = Math.Max(10, W - 6);
                bool mine = senderId == currentUserId;
                string prefix = mine ? "Вы: " : $"{ResolveName(senderId)}[{senderId}]: ";
                string full = prefix + (text ?? "");
                foreach (var _ in WrapText(full, maxWidth)) extraLines++;
                chatScroll += extraLines;
            }

            if (currentScreen == Screen.ChatRoom && currentChatId == chatId)
                dirty = true;
        }

        public static void SetChatHistory(int chatId, List<(int, string)> msgs)
        {
            lock (_lock) messages[chatId] = msgs ?? new List<(int senderId, string text)>();
            if (currentScreen == Screen.ChatRoom && currentChatId == chatId)
                dirty = true;
        }

        public static void SetChats(List<(int id, string name)> list)
        {
            lock (_lock)
            {
                chats.Clear();
                if (list != null)
                    foreach (var c in list) chats[c.id] = c.name;
            }
            if (currentScreen == Screen.Chats) dirty = true;
        }

        public static void AddChat(int id, string name)
        {
            lock (_lock) chats[id] = name;
            ShowNotification($"Вы добавлены в чат «{name}»");
            if (currentScreen == Screen.Chats) dirty = true;
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
            if (currentScreen == Screen.ChatRoom && currentChatId == id)
            {
                currentScreen = Screen.Chats;
            }
            else if (currentScreen == Screen.Chats)
            {
                dirty = true;
            }
        }

        public static void NotifyCreateChat(bool success, int id, string name, string err)
        {
            if (success)
            {
                lock (_lock) chats[id] = name;
                if (currentScreen == Screen.Chats) dirty = true;
            }
            else
            {
                ShowToast("Ошибка создания чата: " + err);
            }
        }

        public static void NotifyAddUser(bool success, string msg) { ShowToast(msg); }
        public static void NotifyKickUser(bool success, string msg) { ShowToast(msg); }

        public static void SetChatMembers(int chatId, List<(int, string)> users)
        {
            lock (_lock)
            {
                if (!chatMembers.ContainsKey(chatId))
                    chatMembers[chatId] = new Dictionary<int, string>();
                else
                    chatMembers[chatId].Clear();

                if (users != null)
                    foreach (var u in users)
                    {
                        chatMembers[chatId][u.Item1] = u.Item2;
                        if (!allUsers.ContainsKey(u.Item1))
                            allUsers[u.Item1] = u.Item2;
                    }
            }
        }

        public static void SetAllUsers(List<(int id, string name)> users)
        {
            lock (_lock)
            {
                allUsers.Clear();
                if (users != null)
                    foreach (var u in users) allUsers[u.id] = u.name;
            }
            if (currentScreen == Screen.UsersList) dirty = true;
        }
        public static void AddOrUpdateUser(int id, string name)
        {
            lock (_lock) allUsers[id] = name;
            if (currentScreen == Screen.UsersList) dirty = true;
        }

        public static void ShowNotification(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            notificationText = text;
            notificationUntil = DateTime.UtcNow.AddSeconds(NOTIFICATION_SECONDS);
            dirty = true;
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
            usersLoading = loading;
            if (currentScreen == Screen.UsersList) dirty = true;
        }

        static string ResolveName(int userId)
        {
            lock (_lock)
            {
                if (allUsers.TryGetValue(userId, out var n)) return n;
            }
            return "user";
        }

        static IEnumerable<string> WrapText(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) { yield return ""; yield break; }
            for (int i = 0; i < text.Length; i += width)
                yield return text.Substring(i, Math.Min(width, text.Length - i));
        }

        static string Truncate(string s, int max)
        {
            if (s == null) return "";
            if (s.Length <= max) return s;
            if (max <= 0) return "";
            return s.Substring(0, max);
        }

        static void DrawFrame(string title, string footer)
        {
            int w = W;
            int h = H;

            string topLeft = "─[ ";
            string topRight = " ]";
            string topMid = title ?? "";
            int topInner = w - 2;
            string topContent = topLeft + topMid + topRight;
            if (topContent.Length > topInner) topContent = topContent.Substring(0, topInner);
            string topPad = new string('─', Math.Max(0, topInner - topContent.Length));
            Console.SetCursorPosition(0, 0);
            Console.Write("┌" + topContent + topPad + "┐");

            for (int y = 1; y < h - 1; y++)
            {
                try
                {
                    Console.SetCursorPosition(0, y);
                    Console.Write("│");
                    Console.SetCursorPosition(w - 1, y);
                    Console.Write("│");
                }
                catch { }
            }

            string botLeft = "─[ ";
            string botRight = " ]";
            string botMid = footer ?? "";
            int botInner = w - 2;
            string botContent = botLeft + botMid + botRight;
            if (botContent.Length > botInner) botContent = botContent.Substring(0, botInner);
            string botPad = new string('─', Math.Max(0, botInner - botContent.Length));
            Console.SetCursorPosition(0, h - 1);
            Console.Write("└" + botContent + botPad + "┘");
        }

        static string pendingTitle = "";
        static string pendingFooter = "";
        static void DrawHeader(string title)
        {
            pendingTitle = title;
            DrawFrame(pendingTitle, pendingFooter);
        }
        static void DrawFooter(string text)
        {
            pendingFooter = text;
            DrawFrame(pendingTitle, pendingFooter);

            DrawNotificationIfAny();
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