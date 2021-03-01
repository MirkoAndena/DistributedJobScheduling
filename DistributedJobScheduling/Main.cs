using System;
using Communication;
using Routines;

public class Program
{
    static void Main(string[] args)
    {
        WorkerGroup group = WorkerGroup.Build("group.json", 2);
        Console.WriteLine($"Me: {group.Me}, Coordinator: {group.Coordinator}");

        // Start the server
        Listener listener = new Listener();

        // Talk with someone
        Speaker.CreateAndRun("127.0.0.1", new DummyRoutine());    
    }
}

class DummyRoutine : Routine
{
    public override void OnMessageReceived(Message message)
    {
        Console.WriteLine($"Received: {message.ToString()}");
    }

    public override void Start()
    {
        Message message = new DummyMessage("Hello World!");
        Send(message);
        Console.WriteLine($"Sent: {message.ToString()}");
        Close();
    }
}

class DummyMessage : Message
{
    private string _text;

    public DummyMessage(string text)
    {
        this._text = text;
    }

    public override string ToString() => _text;
}