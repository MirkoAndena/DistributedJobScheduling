using System;
using Communication;
using Xunit;

namespace DistributedJobScheduling.Tests
{
    public class CommunicationTest
    {
        [Fact]
        public void Test()
        {
            Listener listener = Listener.CreateAndStart();


        }
    }

    public class DummyRoutine : Routines.Routine
    {
        public override void Build()
        {
            SendTo(WorkerGroup.Instance.Coordinator, new DummyMessage() { Content = "ciao" }); 
            //DummyMessage message = ReceiveFrom(WorkerGroup.Instance.Coordinator);
        }
    }

    public class DummyMessage : Message
    {
        public string Content { get; set; }
    }
}
