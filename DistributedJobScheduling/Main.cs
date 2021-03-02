using System;
using Communication;
using Routines;

public class Program
{
    const int ID = 2;

    static void Main(string[] args)
    {
        WorkerGroup group = WorkerGroup.Build("group.json", ID);
        Console.WriteLine($"Me: {group.Me}, Coordinator: {group.Coordinator}");

        // Start the server
        Listener listener = Listener.CreateAndStart(new DummyRoutine());

        // Talk with someone
        //Speaker.CreateAndRun(group.Coordinator, new DummyRoutine());    
        Speaker.CreateAndRun(group.Others[3], new DummyRoutine());   

        Console.ReadKey();

        listener.Close();
    }
}