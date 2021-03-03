using System.Collections.Generic;

namespace Communication
{
    public class Interlocutors
    {
        private static Interlocutors _instance;
        private Dictionary<int, BaseSpeaker> _speakers;

        public static Interlocutors Instance => _instance == null ? new Interlocutors() : _instance;

        public Interlocutors()
        {
            _speakers = new Dictionary<int, BaseSpeaker>();
        }

        public void Add(Node node, BaseSpeaker speaker) => _speakers.Add(node.ID, speaker);
        public void Remove(Node node) => _speakers.Remove(node.ID);

        public BaseSpeaker GetSpeaker(Node node) => _speakers.ContainsKey(node.ID) ? _speakers[node.ID] : null;

        public void CloseAll() 
        {
            _speakers.ForEach((id, speaker) => 
            {
                speaker.Close();
                _speakers.Remove(id);
            });
        }
    }
}