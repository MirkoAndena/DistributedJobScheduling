using Communication;

namespace Routines
{
    public abstract class Routine
    {
        private ICommunicator _communicator;

        public ICommunicator Communicator
        {
            set
            {
                this._communicator = value;
                this._communicator.ReceiveCallBack(message => OnMessageReceived(message));
            }
        }

        protected void Close() => _communicator.Close();
        protected bool Send(Message message) => _communicator.Send(message);

        public abstract void OnMessageReceived(Message message);
        public abstract void Start();
    }
}