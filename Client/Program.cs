using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TestClient;

// Класс должен совпадать со структурой на сервере
public class Request {
    public string command { get; set; }
    public Dictionary<string, string> args { get; set; }
}

class Program {
    static void Main(string[] args) {
        const string ip = "127.0.0.1";
        const int port = 5000; // Убедись, что на сервере такой же

        try {
            using TcpClient client = new TcpClient();
            Console.WriteLine($"Подключение к {ip}:{port}...");
            client.Connect(ip, port);
            Console.WriteLine("Подключено!");

            using NetworkStream stream = client.GetStream();

            // 1. Формируем запрос
            var requestObj = new Request {
                command = "send_message",
                args = new Dictionary<string, string> {
                    ["body"] = "Привет, сервер! Это тестовое сообщение."
                }
            };

            // 2. Сериализуем в JSON
            string json = JsonSerializer.Serialize(requestObj);
            byte[] data = Encoding.UTF8.GetBytes(json);

            // 3. Отправляем
            stream.Write(data, 0, data.Length);
            Console.WriteLine("Сообщение отправлено.");

            // 4. Ждем ответ от сервера
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            
            if (bytesRead > 0) {
                string responseRaw = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Ответ от сервера: {responseRaw}");
            }

            Console.WriteLine("Нажмите любую клавишу, чтобы закрыть соединение...");
            Console.ReadKey();

        } catch (Exception ex) {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }
}