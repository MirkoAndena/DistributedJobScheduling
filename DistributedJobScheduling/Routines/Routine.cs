using Communication;

namespace Routines
{
    public abstract class Routine
    {
        private ICommunicator _communicator;
        public ICommunicator Communicator { set { _communicator = value; } }

        public void Start()
        {
            this._communicator.ReceiveCallBack(message => OnMessageReceived(message));
            this.Build();
            this._communicator.Close();
        }

        protected bool Send(Message message) => _communicator.Send(message);

        public abstract void OnMessageReceived(Message message);
        public abstract void Build();
    }
}