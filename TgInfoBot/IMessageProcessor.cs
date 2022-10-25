namespace TgInfoBot
{
    public interface IMessageProcessor
    {
        bool Accept(string s);
    }
}
