using System.Net;
using System.Collections.Generic;
using Communication;
using System.IO;
using Newtonsoft.Json;

using static OthersEnumerator = System.Collections.Generic.Dictionary<int, Node>.ValueCollection.Enumerator;

public class Node
{
    public string IP;
    public int ID;
    public bool Coordinator;

    public Node(string ip, int id, bool coordinator)
    {
        this.IP = ip;
        this.ID = id;
        this.Coordinator = coordinator;
    }

    public override string ToString() => $"{ID} ({IP})";
}

class StoredGroup
{ 
    public List<Node> Nodes; 

    public StoredGroup(List<Node> nodes)
    {
        this.Nodes = nodes;
    }
}

class WorkerGroup
{
    public static WorkerGroup Instance { get; private set; }

    private Dictionary<int, Node> _others;
    private Node _me;
    private Node _coordinator;

    private WorkerGroup(List<Node> nodes, int myID) 
    {
        _others = new Dictionary<int, Node>();
        nodes.ForEach(node => 
        {
            if (node.ID == myID) _me = node;
            else if (node.Coordinator) _coordinator = node;
            else _others.Add(node.ID, node);
        });
    }

    private static List<Node> ReadFromJson(string jsonPath)
    {
        string json = File.ReadAllText(jsonPath);
        StoredGroup stored = JsonConvert.DeserializeObject<StoredGroup>(json);
        return stored.Nodes;
    }
    
    public static WorkerGroup Build(string groupJsonFile, int myID)
    {
        WorkerGroup.Instance = new WorkerGroup(ReadFromJson(groupJsonFile), myID);
        return WorkerGroup.Instance;
    }

    public Node Me => _me;
    public Node Coordinator => _coordinator;
    public Dictionary<int, Node> Others => _others;
}