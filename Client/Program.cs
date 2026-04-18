using Client.NetworkLogic;

class Program
{
    static void Main()
    {
        Network network = new Network();
        network.Connect("127.0.0.1", 5000);
    }
}