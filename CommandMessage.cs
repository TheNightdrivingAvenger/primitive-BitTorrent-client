namespace CourseWork
{
    public enum ControlMessageType { CloseConnection, SendInner };
    public class CommandMessage : Message
    {
        public ControlMessageType messageType;

        public PeerMessage messageToSend;

        public CommandMessage(ControlMessageType messageType, PeerMessage messageToSend)
        {
            this.messageType = messageType;
            this.messageToSend = messageToSend;
        }

        public CommandMessage(ControlMessageType messageType,
            DownloadingFile targetFile, PeerConnection targetConnection)
        {
            base.targetConnection = targetConnection;
            base.targetFile = targetFile;
        }
    }
}
