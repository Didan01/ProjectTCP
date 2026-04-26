using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Client.NetworkLogic;

public class Request
{
    public string command { get; set; }
    public string sender_id { get; set; }
    public Dictionary<string, string> args { get; set; }
}

public class Response
{
    public string command { get; set; }
    public bool success { get; set; }
    public Dictionary<string, string> args { get; set; }
}

public class Network
{
    private TcpClient client;
    private NetworkStream stream;
    public bool connected = false;
    public Action<Response> OnResponse;

    public bool Connect(string ip, int port)
    {
        try
        {
            client = new TcpClient();
            client.Connect(ip, port);
            stream = client.GetStream();
            connected = true;
            new Thread(ReceiveLoop) { IsBackground = true }.Start();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ReceiveLoop()
    {
        List<byte> accumulator = new();
        byte[] buffer = new byte[4096];

        while (true)
        {
            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                for (int i = 0; i < bytesRead; i++)
                    accumulator.Add(buffer[i]);

                while (accumulator.Count >= 4)
                {
                    int length = BitConverter.ToInt32(accumulator.GetRange(0, 4).ToArray(), 0);
                    if (accumulator.Count < 4 + length) break;

                    byte[] body = accumulator.GetRange(4, length).ToArray();
                    accumulator.RemoveRange(0, 4 + length);

                    try
                    {
                        Response response = JsonSerializer.Deserialize<Response>(Encoding.UTF8.GetString(body));
                        OnResponse?.Invoke(response);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Parse: {ex.Message}");
                    }
                }
            }
            catch
            {
                connected = false;
                OnResponse?.Invoke(new Response
                {
                    command = "disconnected",
                    success = false,
                    args = new() { ["message"] = "Соединение разорвано" }
                });
                break;
            }
        }
    }

    public void SendRequest(Request request)
    {
        if (!connected) return;
        try
        {
            string json = JsonSerializer.Serialize(request);
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] lenPrefix = BitConverter.GetBytes(body.Length);

            stream.Write(lenPrefix, 0, 4);
            stream.Write(body, 0, body.Length);
        }
        catch
        {
            connected = false;
        }
    }

    public Response SendAndWait(string command, Dictionary<string, string> args, int timeoutMs = 5000)
    {
        Response result = null;
        ManualResetEventSlim ev = new(false);

        void handler(Response r)
        {
            if (r.command == command)
            {
                result = r;
                ev.Set();
            }
        }

        OnResponse += handler;
        SendRequest(new Request { command = command, args = args });
        ev.Wait(timeoutMs);
        OnResponse -= handler;

        return result;
    }
}