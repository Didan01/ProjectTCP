using Client.NetworkLogic;

class Program
{
    static void Main()
    {
        Network network = new Network();
        network.Connect("ip", 5000);
    }
}