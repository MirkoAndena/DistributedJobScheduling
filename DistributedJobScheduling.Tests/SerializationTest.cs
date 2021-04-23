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
            DistributedStorageUpdateRequestSerialization(serializer);
        }

        [Fact]
        public void ByteSerializerTest()
        {
            ByteBase64Serializer serializer = new ByteBase64Serializer();
            KeepAliveRequestSerialization(serializer);
            DistributedStorageUpdateRequestSerialization(serializer);
        }

        public void KeepAliveRequestSerialization(ISerializer serializer)
        {
            Message message = new KeepAliveRequest();
            byte[] serialized = serializer.Serialize(message);
            Message deserialized = serializer.Deserialize<KeepAliveRequest>(serialized);
            Assert.True(deserialized is KeepAliveRequest);
        }

        public void DistributedStorageUpdateRequestSerialization(ISerializer serializer)
        {
            int nodeId = 7262;
            int jobId = 2432;
            Node node = new Node.NodeRegistryService().GetOrCreate("198.168.1.54", nodeId);
            Job job = new Job(jobId, node.ID.Value, new TimeoutJobWork(1));
            Message message = new DistributedStorageUpdateRequest(job);
            byte[] serialized = serializer.Serialize(message);
            DistributedStorageUpdateRequest deserialized = serializer.Deserialize<DistributedStorageUpdateRequest>(serialized);

            Assert.Equal(jobId, deserialized.Job.ID);
            Assert.Equal(nodeId, deserialized.Job.Node);
        }
    }
}