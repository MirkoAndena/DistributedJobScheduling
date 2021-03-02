using System;
using Communication;

namespace Routines
{
    class DummyRoutine : Routine
    {
        public override void OnMessageReceived(Message message)
        {
            Console.WriteLine($"Received: {message.ToString()}");
        }

        public override void Build()
        {
            Message message = new DummyMessage("Hello World!");
            Send(message);
            Console.WriteLine($"Sent: {message.ToString()}");
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
}