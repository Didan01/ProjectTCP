using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server.NetworkLogic
{
    public class Network
    {
        private TcpListener listener;
        private TcpClient client;
        private NetworkStream stream;
        private byte[] buffer = new byte[1024];

        public void Start(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            Console.WriteLine("Сервер запущен");
            AcceptClient();
        }

        private void AcceptClient()
        {
            client = listener.AcceptTcpClient();
            Console.WriteLine("Клиент подключен");

            stream = client.GetStream();

            ReceiveLoop();
        }

        private void ReceiveLoop()
        {
            while (true)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                        break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[{DateTime.Now}] " + message);

                    Send(message);
                }
                catch
                {
                    Console.WriteLine("Клиент отключился");
                    break;
                }
            }

            stream.Close();
            client.Close();
        }

        private void Send(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            stream.Write(data, 0, data.Length);
        }
    }
}