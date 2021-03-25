using System.Threading;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;
using DistributedJobScheduling.Communication.Messaging.LeaderElection.KeepAlive;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Serialization;
using Xunit;

namespace DistributedJobScheduling.DistributedStorage
{
    public class SerializationTest
    {    
        JsonSerializer jsonSerializer;

        public SerializationTest()
        {
            jsonSerializer = new JsonSerializer();
        }

        [Fact]
        public void KeepAliveRequestSerialization()
        {
            Message message = new KeepAliveRequest();
            byte[] serialized = jsonSerializer.Serialize(message);
            Message deserialized = jsonSerializer.Deserialize<KeepAliveRequest>(serialized);
            Assert.True(deserialized is KeepAliveRequest);
        }

        [Fact]
        public void InsertionRequestSerialization()
        {
            string ip = "198.168.1.54";
            int id = 7262;
            int requestId = 15272;
            Node node = new Node.NodeRegistryService().GetOrCreate(ip, id);
            Job job = new TimeoutJob(0);
            job.ID = 2432;
            job.Node = node.ID;
            Message message = new InsertionRequest(job, requestId);
            byte[] serialized = jsonSerializer.Serialize(message);
            InsertionRequest deserialized = jsonSerializer.Deserialize<InsertionRequest>(serialized);

            Assert.Equal(deserialized.RequestID, requestId);
        }
    }
}