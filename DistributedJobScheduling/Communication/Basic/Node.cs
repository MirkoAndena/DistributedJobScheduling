namespace DistributedJobScheduling.Communication.Basic
{
    public class Node
    {
        public string IP;
        public int? ID;

        public Node(string ip, int? id = null)
        {
            IP = ip;
            ID = id;
        }

        public override string ToString()
        {
            if (ID.HasValue) return $"{ID} ({IP})";
            else return $"anonymous ({IP})";
        } 
    }
}