using System.Threading.Tasks;
using Communication;

namespace Routines
{
    public abstract class Routine
    {
        public void Start()
        {
            this.Build();
        }

        protected async Task SendTo(Node node, Message message)
        {
            BaseSpeaker speaker = await GetSpeakerTo(node);
            await speaker.Send(message);
        }

        protected async Task<T> ReceiveFrom<T>(Node node) where T: Message
        {
            BaseSpeaker speaker = await GetSpeakerTo(node);
            return await speaker.Receive<T>();
        }

        public abstract void Build();

        private BaseSpeaker SearchConnectedSpeakerTo(Node node) => Interlocutors.Instance.GetSpeaker(node);

        private async Task<BaseSpeaker> GetSpeakerTo(Node node)
        {
            // Retrieve an already connected speaker
            BaseSpeaker speaker = SearchConnectedSpeakerTo(node);
            if (speaker != null) return speaker;

            // Create a new speaker and connect to remote
            speaker = new Speaker();
            await ((Speaker)speaker).Connect(node);
            return speaker;
        }
    }
}