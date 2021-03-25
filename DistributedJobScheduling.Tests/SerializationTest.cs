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

        [Fact]
        public void JsonSerializerTest()
        {
            JsonSerializer serializer = new JsonSerializer();
            KeepAliveRequestSerialization(serializer);
            InsertionRequestSerialization(serializer);
        }

        [Fact]
        public void ByteSerializerTest()
        {
            ByteSerializer serializer = new ByteSerializer();
            KeepAliveRequestSerialization(serializer);
            InsertionRequestSerialization(serializer);
        }

        public void KeepAliveRequestSerialization(ISerializer serializer)
        {
            Message message = new KeepAliveRequest();
            byte[] serialized = serializer.Serialize(message);
            Message deserialized = serializer.Deserialize<KeepAliveRequest>(serialized);
            Assert.True(deserialized is KeepAliveRequest);
        }

        public void InsertionRequestSerialization(ISerializer serializer)
        {
            int nodeId = 7262;
            int requestId = 15272;
            int jobId = 2432;
            Node node = new Node.NodeRegistryService().GetOrCreate("198.168.1.54", nodeId);
            Job job = new TimeoutJob(0);
            job.ID = jobId;
            job.Node = node.ID;
            Message message = new InsertionRequest(job, requestId);
            byte[] serialized = serializer.Serialize(message);
            InsertionRequest deserialized = serializer.Deserialize<InsertionRequest>(serialized);

            Assert.Equal(deserialized.RequestID, requestId);
            Assert.Equal(deserialized.Job.ID.Value, jobId);
            Assert.Equal(deserialized.Job.Node.Value, nodeId);
        }
    }
}