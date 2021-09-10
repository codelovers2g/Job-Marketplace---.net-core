using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace _999Space.BAL.Hubs
{
    public interface IMessageHub
    {
        public Task SendMessageToAll(string message);
        public Task SendMessageToUser(string Attachment, string sendTo, int roomId, string message, bool isAttachment);
    }
}
