using Server.NetworkLogic;

class Program
{
    static void Main()
    {
        Network network = new Network();
        network.Start(5000);
    }
}