namespace Communication
{
    public interface ICommunicable
    {
        void Send(IMessage message);
        IMessage Receive();
    }
}