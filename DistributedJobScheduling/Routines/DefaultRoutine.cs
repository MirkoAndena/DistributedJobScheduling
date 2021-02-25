using Communication;

namespace Routines
{
    public class DefaultRoutine : Routine
    {
        public override void OnMessageReceived(Message message)
        {
            // TODO: from message type select an opportune routine
        }

        public override void Start()
        {
            // In this case can be empty
        }
    }
}