using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Client.NetworkLogic
{
    public class Network
    {
        private TcpClient client;
        private NetworkStream stream;
        private byte[] buffer = new byte[1024];

        public void Connect(string ip, int port)
        {
            client = new TcpClient();
            client.Connect(ip, port);

            Console.WriteLine("Подключено к серверу!");

            stream = client.GetStream();

            StartReceiving();
            SendLoop();
        }

        private void StartReceiving()
        {
            Thread receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();
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
                    Console.WriteLine("Сервер: " + message);
                }
                catch
                {
                    Console.WriteLine("Соединение разорвано");
                    break;
                }
            }
        }

        private void SendLoop()
        {
            while (true)
            {
                string message = Console.ReadLine();
                Send(message);
            }
        }

        private void Send(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            stream.Write(data, 0, data.Length);
        }
    }
}