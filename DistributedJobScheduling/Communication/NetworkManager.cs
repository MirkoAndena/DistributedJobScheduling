using System;
using System.Threading.Tasks;
using Communication;

public class NetworkManager : ICommunicationManager
{
    public event Action<Node, Message> OnMessageReceived;

    public ITopicPublisher GetPublisher(Type topicType)
    {
        throw new NotImplementedException();
    }

    public async Task Send(Node node, Message message, int timeout = 30)
    {
        BaseSpeaker speaker = await GetSpeakerTo(node, timeout);
        await speaker.Send(message);
    }

    public async Task<T> SendAndWait<T>(Node node, Message message, int timeout = 30) where T: Message
    {
        BaseSpeaker speaker = await GetSpeakerTo(node, timeout);
        await speaker.Send(message);
        return await speaker.Receive<T>();
    }

    private BaseSpeaker SearchConnectedSpeakerTo(Node node) 
    {
        return Interlocutors.Instance.GetSpeaker(node);
    }

    private async Task<BaseSpeaker> GetSpeakerTo(Node node, int timeout)
    {
        // Retrieve an already connected speaker
        BaseSpeaker speaker = SearchConnectedSpeakerTo(node);
        if (speaker != null) return speaker;

        // Create a new speaker and connect to remote
        speaker = new Speaker();
        await ((Speaker)speaker).Connect(node, timeout); // todo prendere il timeout da un altra parte
        return speaker;
    }
}