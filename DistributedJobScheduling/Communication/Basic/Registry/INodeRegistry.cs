namespace DistributedJobScheduling.Communication.Basic
{
    public partial class Node
    {
        public interface INodeRegistry
        {
            /// <summary>
            /// Gets a node by its ID
            /// </summary>
            Node GetNode(int ID);

            /// <summary>
            /// Gets a node by its IP
            /// </summary>
            Node GetNode(string IP);

            /// <summary>
            /// Gets or creates a node
            /// </summary>
            Node GetOrCreate(string ip = null, int? id = null);
            
            /// <summary>
            /// Gets or creates a node from an unreference node
            /// </summary>
            Node GetOrCreate(Node node);

            /// <summary>
            /// Updates a node with a new IP
            /// </summary>
            void UpdateNodeIP(Node node, string newIP);

            /// <summary>
            /// Updates a node with a new ID
            /// </summary>
            void UpdateNodeID(Node node, int newID);
        }
    }
}