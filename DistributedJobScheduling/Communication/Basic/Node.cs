using System;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Basic
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public partial class Node
    {
        [field: NonSerialized]
        public event Action<Node> Died;
        public string IP { get; private set; }
        public int? ID { get; private set; }

        [JsonConstructor]
        private Node(string ip, int? id = null)
        {
            IP = ip;
            ID = id;
        }

        public override string ToString()
        {
            if (ID.HasValue) return $"{ID} ({IP})";
            else return $"anonymous ({IP})";
        }

        public void NotifyDeath()
        {
            Died?.Invoke(this);
        }
    }
}