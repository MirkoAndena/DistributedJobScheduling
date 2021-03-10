namespace DistributedJobScheduling.Communication.Basic
{
    public partial class Node
    {
        public class NodeRegistryService : INodeRegistry
        {
            public Node GetNode(int ID)
            {
                throw new System.NotImplementedException();
            }

            public Node GetNode(string IP)
            {
                throw new System.NotImplementedException();
            }

            public Node GetOrCreate(string ip, int? id = null)
            {
                throw new System.NotImplementedException();
            }

            public Node GetOrCreate(Node node)
            {
                throw new System.NotImplementedException();
            }

            public void UpdateNodeID(Node node, int newID)
            {
                throw new System.NotImplementedException();
            }

            public void UpdateNodeIP(Node node, string newIP)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}