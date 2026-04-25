using System;
using System.Collections.Generic;

class Visual
{
    static List<string> messages = new List<string>()
    {
        "Джон: Привет, бро",
        "Джон: Как проект?"
    };

    static int W => Console.WindowWidth;
    static int H => Console.WindowHeight;

    static void Main1()
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

            if (choice == "1" || choice == "2")
                MainMenu();
            else if (choice == "0")
                return;
        }
    }

    // ================= MENU =================
    static void MainMenu()
    {
        while (true)
        {
            Console.Clear();

            DrawCentered("Пользователь: Алекс | Статус: ОНЛАЙН", 1);

            Console.SetCursorPosition(2, 4);
            Console.WriteLine("СПИСОК ЧАТОВ:");
            Console.WriteLine("[1] Джон");
            Console.WriteLine("[2] Анна");
            Console.WriteLine("[3] Групповой чат");
            Console.WriteLine();
            Console.WriteLine("[4] Выход");

            Console.SetCursorPosition(2, H - 2);
            Console.Write("Выберите чат: ");

            string choice = Console.ReadLine();

            if (choice == "1")
                ChatScreen();

            else if (choice == "3")
                GroupChatScreen();

            else if (choice == "4")
                return;
        }
    }

    // ================= CHAT =================
    static void ChatScreen()
    {
        while (true)
        {
            Console.Clear();
            DrawFrame();

            DrawCenteredTop("ЧАТ С ДЖОНОМ");

            int y = 3;

            foreach (string msg in messages)
            {
                if (msg.StartsWith("Джон"))
                {
                    Console.SetCursorPosition(2, y);
                    Console.Write(msg);
                }
                else
                {
                    string text = msg;
                    int x = W - text.Length - 3;
                    if (x < 2) x = 2;

                    Console.SetCursorPosition(x, y);
                    Console.Write(text);
                }

                y++;
            }

            DrawInputBar();

            string input = Console.ReadLine();

            if (input == "/back")
                return;

            messages.Add("Вы: " + input);
            messages.Add("Джон: Ок 👍");
        }
    }

    // ================= GROUP CHAT =================
    static void GroupChatScreen()
    {
        while (true)
        {
            Console.Clear();
            DrawFrame();

            DrawCenteredTop("ГРУППОВОЙ ЧАТ");

            Console.SetCursorPosition(2, 3);
            Console.WriteLine("Анна: Всем привет!");
            Console.SetCursorPosition(2, 4);
            Console.WriteLine("Майк: Готовы к проекту?");
            Console.SetCursorPosition(2, 5);
            Console.WriteLine("Макс: Да!");

            DrawInputBar();

            string input = Console.ReadLine();

            if (input == "/back")
                return;
        }
    }

    // ================= UI HELPERS =================

    static void DrawFrame()
    {
        for (int i = 0; i < H; i++)
        {
            for (int j = 0; j < W; j++)
            {
                if (i == 0 || i == H - 1)
                    Console.Write("─");
                else if (j == 0 || j == W - 1)
                    Console.Write("│");
                else
                    Console.Write(" ");
            }

            if (i < H - 1)
                Console.WriteLine();
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
