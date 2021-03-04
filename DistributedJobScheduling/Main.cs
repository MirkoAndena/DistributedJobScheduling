using System;
using Communication;

public class Program
{
    const int ID = 2;

    static void Main(string[] args)
    {
        Workers group = Workers.Build("group.json", ID);
        Console.WriteLine($"Me: {group.Me}, Coordinator: {group.Coordinator}");

        // Start the server
        Listener listener = Listener.CreateAndStart();

        Console.ReadKey();

        listener.Close();
    }
}