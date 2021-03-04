using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Communication;

public class NetworkManager : ICommunicationManager
{
    private Dictionary<int, Speaker> _speakers;
    private Listener _listener;

    public event Action<Node, Message> OnMessageReceived;

    public NetworkManager()
    {
        _speakers = new Dictionary<int, Speaker>();
        _listener = new Listener();
        _listener.OnSpeakerCreated += OnSpeakerCreated;
        _listener.Start();
    }

    private void OnSpeakerCreated(Node node, Speaker speaker)
    {
        _speakers.Add(node.ID, speaker);
        speaker.OnMessageReceived += _OnMessageReceived;
    }

    private void _OnMessageReceived(Node node, Message message)
    {
        OnMessageReceived?.Invoke(node, message);
    }

    public ITopicPublisher GetPublisher(Type topicType)
    {
        throw new NotImplementedException();
    }

    public async Task Send(Node node, Message message, int timeout = 30)
    {
        Speaker speaker = await GetSpeakerTo(node, timeout);
        await speaker.Send(message);
    }

    public async Task<T> SendAndWait<T>(Node node, Message message, int timeout = 30) where T: Message
    {
        Speaker speaker = await GetSpeakerTo(node, timeout);
        await speaker.Send(message);
        return await speaker.Receive<T>();
    }

    private async Task<Speaker> GetSpeakerTo(Node node, int timeout)
    {
        // Retrieve an already connected speaker
        if (_speakers.ContainsKey(node.ID))
            return _speakers[node.ID];

        // Create a new speaker and connect to remote
        BoldSpeaker speaker = new BoldSpeaker(node);
        await speaker.Connect(timeout);
        OnSpeakerCreated(node, speaker);
        return speaker;
    }

    public Task SendMulticast(Message message)
    {
        throw new NotImplementedException();
    }

    public void Close() 
    {
        _listener.Close();
        _speakers.ForEach((id, speaker) => 
        {
            speaker.Close();
            speaker.OnMessageReceived -= _OnMessageReceived;
            _speakers.Remove(id);
        });
    }
}