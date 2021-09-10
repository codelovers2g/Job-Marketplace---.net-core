using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Text;
using _999Space.Common.ViewModels;
using System.Threading.Tasks;
using _999Space.DAL.DataModels;
using System.Linq;
using _999Space.DAL.Interfaces;
using _999Space.DAL.Repository;
using AutoMapper;
using _999Space.BAL.Session;
using Microsoft.Extensions.Logging;
using _999Space.Common.Enum;
using Newtonsoft.Json;
using _999Space.BAL.ServiceInterfaces;
using System.ComponentModel;
using System.Reflection;
using _999Space.Utility.Mail;
using Microsoft.Extensions.Options;
using _999Space.Common.ConfigurationModel;
using SendGrid.Helpers.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace _999Space.BAL.Hubs
{

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MessageHub : Hub
    {

        private _999SpaceContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISessionService _sessionService;
        private readonly ICommonService _commonService;
        private readonly IUserService _userService;
        private readonly IMessageService _messageServie;
        private readonly IOptions<SmtpConfig> _settings;
        private readonly IMapper _mapper;
        public static List<string> Ids = new List<string>();
        private readonly ILogger<MessageHub> _logger;

        OnlineUserDetailVM userDetailVM = new OnlineUserDetailVM();
        static List<OnlineUserDetailVM> ConnectedUsers = new List<OnlineUserDetailVM>();
        static List<MessageVM> CurrentMessage = new List<MessageVM>();




        public MessageHub(IMessageService messageServie, IOptions<SmtpConfig> settings, IUserService userService, IHttpContextAccessor httpContextAccessor, IMapper mapper, ISessionService sessionService, ICommonService commonService, ILogger<MessageHub> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _mapper = mapper;
            _sessionService = sessionService;
            _commonService = commonService;
            _userService = userService;
            _logger = logger;
            _settings = settings;
            _messageServie = messageServie;
        }


        #region Messages/Chat
        public async Task SendMessageToAll(string message)
        {

            await Clients.All.SendAsync("ReceiveMessage", "", message);
        }

        public async Task SendMessageToCaller(string message)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "", message);
        }

        public async Task SendMessageToUser(string Attachment, string sendTo, int roomId, string message, int messageType)
        {
            string strfromUserId = (ConnectedUsers.Where(u => u.ConnectionId == Context.ConnectionId).Select(u => u.UserID).FirstOrDefault()).ToString();
            int _fromUserId = 0;
            int.TryParse(strfromUserId, out _fromUserId);
            int _toUserId = 0;
            int.TryParse(sendTo, out _toUserId);
            List<OnlineUserDetailVM> FromUsers = ConnectedUsers.Where(u => u.UserID == _sessionService.CurrentUserSession.UserId).ToList();
            List<OnlineUserDetailVM> ToUsers = new List<OnlineUserDetailVM>();
            if (sendTo != "")
            {
                ToUsers = ConnectedUsers.Where(x => x.UserID == Convert.ToInt32(sendTo)).ToList();
            }

            try
            {
                ChatRoom chatRoom = new ChatRoom();
                MessageVM messageVM = new MessageVM();
                MessageVM messageReturnVM = new MessageVM();
                messageVM.SenderId = _fromUserId;
                messageVM.ChatRoomId = roomId;
                messageVM.CreatedDate = DateTime.UtcNow;
                var messageModel = _mapper.Map<Message>(messageVM);

                if (FromUsers.Count > 0)
                {

                    if (messageType == (int)MessageType.File)
                    {
                        List<MessageAttachmentVM> messageAttachmentVMs = JsonConvert.DeserializeObject<List<MessageAttachmentVM>>(Attachment);
                        _context = new _999SpaceContext();
                        using (IUnityOfWork unityOfWork = new UnityOfWork(_context))
                        {

                            for (int i = 0; i < messageAttachmentVMs.Count; i++)
                            {
                                messageModel.MessageId = 0;
                                messageModel.CreatedBy = _sessionService.CurrentUserSession.UserId;
                                messageModel.MessageType = (int)MessageType.File;
                                messageModel.MessageText = "";
                                messageModel.IsDeleted = false;
                                messageModel.IsActive = true;
                                unityOfWork.MessageRepository.SaveMessage(messageModel);
                                unityOfWork.Save();
								
                                List<MessageAttachment> messageAttachments = new List<MessageAttachment>();
                                MessageAttachment messageAttachment = new MessageAttachment();
                                messageAttachment.MessageId = messageModel.MessageId;
                                messageAttachment.OriginalName = messageAttachmentVMs[i].OriginalName;
                                messageAttachment.UniqueName = messageAttachmentVMs[i].UniqueName;
                                messageAttachments.Add(messageAttachment);
                                unityOfWork.MessageRepository.SaveMessageAttachments(messageAttachments);
                                unityOfWork.Save();
								
                                messageReturnVM.ChatRoomId = messageModel.ChatRoomId;
                                messageReturnVM.MessageText = messageModel.MessageText;
                                messageReturnVM.MessageType = messageModel.MessageType;
                                messageReturnVM.SenderId = messageModel.SenderId;
                                messageReturnVM.CreatedDate = messageModel.CreatedDate;
                                messageReturnVM.MessageId = messageModel.MessageId;
								
                                List<MessageAttachmentVM> messageAttachmentReturnList = new List<MessageAttachmentVM>();
                                MessageAttachmentVM messageAttachmentReturn = new MessageAttachmentVM();
                                messageAttachmentReturn.Url = messageAttachmentVMs[i].Url;
                                messageAttachmentReturn.MessageId = messageModel.MessageId;
                                messageAttachmentReturn.OriginalName = messageAttachmentVMs[i].OriginalName;
                                messageAttachmentReturn.UniqueName = messageAttachmentVMs[i].UniqueName;
                                messageAttachmentReturnList.Add(messageAttachmentReturn);
                                messageReturnVM.MessageAttachment = messageAttachmentReturnList;


                                if (ToUsers.Count() > 0)
                                {
                                    foreach (var ToUser in ToUsers)
                                    {
                                        await Clients.Client(ToUser.ConnectionId).SendAsync("ReceiveMessage", _fromUserId.ToString(), FromUsers[0].UserName, messageReturnVM, false);
                                    }
                                }

                                foreach (var FromUser in FromUsers)
                                {
                                    await Clients.Client(FromUser.ConnectionId).SendAsync("ReceiveMessage", _toUserId.ToString(), FromUsers[0].UserName, messageReturnVM, true);
                                }
                            }
                        }
                    }
                    else if (messageType == (int)MessageType.Text)
                    {
                        _context = new _999SpaceContext();
                        using (IUnityOfWork unityOfWork = new UnityOfWork(_context))
                        {
                            messageModel.CreatedBy = _sessionService.CurrentUserSession.UserId;
                            messageModel.MessageType = (int)MessageType.Text;
                            messageModel.MessageText = message;
                            messageModel.IsDeleted = false;
                            messageModel.IsActive = true;
                            unityOfWork.MessageRepository.SaveMessage(messageModel);
                            unityOfWork.Save();
                        }
                        messageReturnVM = _mapper.Map<MessageVM>(messageModel);

                        if (ToUsers.Count() > 0)
                        {
                            foreach (var ToUser in ToUsers)
                            {
                                await Clients.Client(ToUser.ConnectionId).SendAsync("ReceiveMessage", _fromUserId.ToString(), FromUsers[0].UserName, messageReturnVM, false);
                            }
                        }
                        foreach (var FromUser in FromUsers)
                        {
                            await Clients.Client(FromUser.ConnectionId).SendAsync("ReceiveMessage", _toUserId.ToString(), FromUsers[0].UserName, messageReturnVM, true);
                        }
                    }
                    else
                    {
                        List<MessageAttachmentVM> messageAttachmentVMs = JsonConvert.DeserializeObject<List<MessageAttachmentVM>>(Attachment);
                        _context = new _999SpaceContext();
                        using (IUnityOfWork unityOfWork = new UnityOfWork(_context))
                        {
                            messageModel.MessageId = 0;
                            messageModel.CreatedBy = _sessionService.CurrentUserSession.UserId;
                            messageModel.MessageType = (int)MessageType.TextAndFile;
                            messageModel.MessageText = message;
                            messageModel.IsDeleted = false;
                            messageModel.IsActive = true;
                            unityOfWork.MessageRepository.SaveMessage(messageModel);
                            unityOfWork.Save();
                            messageReturnVM.ChatRoomId = messageModel.ChatRoomId;
                            messageReturnVM.MessageText = messageModel.MessageText;
                            messageReturnVM.MessageType = messageModel.MessageType;
                            messageReturnVM.SenderId = messageModel.SenderId;
                            messageReturnVM.CreatedDate = messageModel.CreatedDate;
                            messageReturnVM.MessageId = messageModel.MessageId;
                            List<MessageAttachment> messageAttachments = new List<MessageAttachment>();
                            List<MessageAttachmentVM> messageAttachmentReturnList = new List<MessageAttachmentVM>();
                            for (int i = 0; i < messageAttachmentVMs.Count; i++)
                            {
                                MessageAttachment messageAttachment = new MessageAttachment();
                                messageAttachment.MessageId = messageModel.MessageId;
                                messageAttachment.OriginalName = messageAttachmentVMs[i].OriginalName;
                                messageAttachment.UniqueName = messageAttachmentVMs[i].UniqueName;
                                messageAttachments.Add(messageAttachment);
                                MessageAttachmentVM messageAttachmentReturn = new MessageAttachmentVM();
                                messageAttachmentReturn.Url = messageAttachmentVMs[i].Url;
                                messageAttachmentReturn.MessageId = messageModel.MessageId;
                                messageAttachmentReturn.OriginalName = messageAttachmentVMs[i].OriginalName;
                                messageAttachmentReturn.UniqueName = messageAttachmentVMs[i].UniqueName;
                                messageAttachmentReturnList.Add(messageAttachmentReturn);
                            }
                            unityOfWork.MessageRepository.SaveMessageAttachments(messageAttachments);
                            unityOfWork.Save();

                            messageReturnVM.MessageAttachment = messageAttachmentReturnList;
                            if (ToUsers.Count() > 0)
                            {
                                foreach (var ToUser in ToUsers)
                                {
                                    await Clients.Client(ToUser.ConnectionId).SendAsync("ReceiveMessage", _fromUserId.ToString(), FromUsers[0].UserName, messageReturnVM, false);
                                }
                            }

                            foreach (var FromUser in FromUsers)
                            {
                                await Clients.Client(FromUser.ConnectionId).SendAsync("ReceiveMessage", _toUserId.ToString(), FromUsers[0].UserName, messageReturnVM, true);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                foreach (var FromUser in FromUsers)
                {
                    await Clients.Client(FromUser.ConnectionId).SendAsync("ExceptionMessage", true);
                }
            }
        }

        public Task JoinGroup(string group)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        public Task SendMessageToGroup(string group, string message)
        {
            return Clients.Group(group).SendAsync("ReceiveMessage", "", message);
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                string id = Context.ConnectionId;

                if (ConnectedUsers.Where(x => x.ConnectionId == id).Count() == 0)
                {
                    ConnectedUsers.Add(new OnlineUserDetailVM { ConnectionId = id, Name = _sessionService.CurrentUserSession.FirstName + " " + _sessionService.CurrentUserSession.LastName, UserName = _sessionService.CurrentUserSession.Email, UserID = _sessionService.CurrentUserSession.UserId, listConnectionIds = new List<string>() });
                }
                OnlineUserDetailVM CurrentUser = new OnlineUserDetailVM();
                CurrentUser = ConnectedUsers.Where(u => u.UserID == _sessionService.CurrentUserSession.UserId).FirstOrDefault();
      
                await Clients.Caller.SendAsync("UserConnected", CurrentUser.UserID.ToString(), CurrentUser.UserName, CurrentUser.UserID, ConnectedUsers, CurrentMessage, CurrentUser.Name);

                await Clients.AllExcept(CurrentUser.ConnectionId).SendAsync("NewUserConnected", CurrentUser.ConnectionId, CurrentUser.UserID, CurrentUser.UserName, CurrentUser.Name);

                await base.OnConnectedAsync();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);

            }
        }

        public override async Task OnDisconnectedAsync(Exception ex)
        {
            try
            {
                if (ConnectedUsers != null && ConnectedUsers.Count > 0)
                {
                    if (ConnectedUsers[0] != null)
                    {
                        var item = ConnectedUsers.Where(x => x.ConnectionId == Context.ConnectionId).FirstOrDefault();
                        if (item != null)
                        {
                            ConnectedUsers.Remove(item);
                            if (ConnectedUsers != null)
                            {
                                if (ConnectedUsers.Where(u => u.UserID == item.UserID).Count() == 0)
                                {
                                    var id = item.UserID.ToString();
                                    await Clients.All.SendAsync("UserDisconnected", id, item.UserName);
                                }
                            }
                        }
                    }
                }

                await base.OnDisconnectedAsync(ex);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message, e);
            }
        }

        public async Task SendUserTypingRequest(string toUserId)
        {
            try
            {
                string strfromUserId = ConnectedUsers.Where(u => u.ConnectionId == Context.ConnectionId).Select(u => u.UserID).FirstOrDefault().ToString();

                int _toUserId = 0;
                int.TryParse(toUserId, out _toUserId);
                List<OnlineUserDetailVM> ToUsers = ConnectedUsers.Where(x => x.UserID == _toUserId).ToList();

                foreach (var ToUser in ToUsers)
                {                                                                                          
                    await Clients.Client(ToUser.ConnectionId).SendAsync("ReceiveTypingRequest", strfromUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        public async Task UpdateSeenMessagesRequest(int chatRoomId)
        {
            List<MessageRead> messageReadList = new List<MessageRead>();
            List<MessageVM> messageVMs = new List<MessageVM>();

            try
            {               
                _context = new _999SpaceContext();
                using (IUnityOfWork unityOfWork = new UnityOfWork(_context))
                {
                    var allMessagesIdInGroup = unityOfWork.MessageRepository.GetMessageByRoomId(chatRoomId, _sessionService.CurrentUserSession.UserId);
                    var allReadMessagesIdInGroup = unityOfWork.MessageRepository.GetMessageReadByRoomId(chatRoomId, _sessionService.CurrentUserSession.UserId);

                    var readMessageIds = allReadMessagesIdInGroup.Select(x => x.MessageId).ToList();
                    allMessagesIdInGroup = allMessagesIdInGroup.Where(x => !readMessageIds.Contains(x.MessageId)).ToList();
                    messageVMs = _mapper.Map<List<MessageVM>>(allMessagesIdInGroup);
                }
              
                for (int i = 0; i < messageVMs.Count; i++)
                {
                    var toUserId = messageVMs[i].SenderId;

                    MessageRead messageRead = new MessageRead();
                    messageRead.ChatRoomId = messageVMs[i].ChatRoomId;
                    messageRead.MessageId = messageVMs[i].MessageId;
                    messageRead.UserId = _sessionService.CurrentUserSession.UserId;
                    messageRead.CreatedDate = DateTime.UtcNow;
                    messageReadList.Add(messageRead);

                    List<OnlineUserDetailVM> ToUsers = new List<OnlineUserDetailVM>();
                    List<OnlineUserDetailVM> FromUsers = new List<OnlineUserDetailVM>();
                    ToUsers = ConnectedUsers.Where(x => x.UserID == Convert.ToInt32(toUserId)).ToList();
                    FromUsers = ConnectedUsers.Where(u => u.UserID == _sessionService.CurrentUserSession.UserId).ToList();

                    if (ToUsers.Count() > 0)
                    {
                        foreach (var ToUser in ToUsers)
                        {
                            await Clients.Client(ToUser.ConnectionId).SendAsync("ReceiveSeenMessageRequest", messageRead.MessageId, messageRead.ChatRoomId);
                        }

                    }
                    if (FromUsers.Count() > 0)
                    {
                        foreach (var FromUser in FromUsers)
                        {
                            await Clients.Client(FromUser.ConnectionId).SendAsync("ReceiveSeenMessageCountRequest", messageRead.ChatRoomId);
                        }
                    }
                }
				
                if (messageReadList.Count > 0)
                {
                    _context = new _999SpaceContext();
                    using (IUnityOfWork unityOfWork = new UnityOfWork(_context))
                    {
                        unityOfWork.MessageRepository.UpdateSeenMessages(messageReadList);
                        unityOfWork.Save();
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        public async Task CreateNewChatWindow(int sendTo, string message, int jobId)
        {
            try
            {
                string strfromUserId = (ConnectedUsers.Where(u => u.ConnectionId == Context.ConnectionId).Select(u => u.UserID).FirstOrDefault()).ToString();
                int _fromUserId = 0;
                int.TryParse(strfromUserId, out _fromUserId);
                int _toUserId = sendTo;

                List<OnlineUserDetailVM> FromUsers = ConnectedUsers.Where(u => u.UserID == _sessionService.CurrentUserSession.UserId).ToList();
                List<OnlineUserDetailVM> ToUsers = new List<OnlineUserDetailVM>();
                if (sendTo > 0)
                {
                    ToUsers = ConnectedUsers.Where(x => x.UserID == Convert.ToInt32(sendTo)).ToList();
                }

                InitiateChatRoomVM initiateChatRoomVM = new InitiateChatRoomVM();
                initiateChatRoomVM.ToUserId = _toUserId;
                initiateChatRoomVM.Message = message;
                initiateChatRoomVM.JobId = jobId;

                var mesageChatRoom = _messageServie.InitiateChatToProvider(initiateChatRoomVM);
                if (mesageChatRoom != null && mesageChatRoom.ChatRoom != null && mesageChatRoom.ChatRoom.ChatRoomId > 0)
                {
                    if (mesageChatRoom.ChatRoom.ChatRoomUser.Count > 0)
                    {
                        for (int i = 0; i < mesageChatRoom.ChatRoom.ChatRoomUser.Count; i++)
                        {
                            mesageChatRoom.ChatRoom.ChatRoomUser[i].User.ProfileImage = _commonService.GetUserProfileImageByUserId(mesageChatRoom.ChatRoom.ChatRoomUser[i].User.UserId);
                        }
                    }

                    foreach (var ToUser in ToUsers)
                    {                                                                                       
                        await Clients.Client(ToUser.ConnectionId).SendAsync("CreateNewChatWindowResponse", mesageChatRoom, _sessionService.CurrentUserSession.UserId, 0);
                    }


                    foreach (var FromUser in FromUsers)
                    {
                        await Clients.Client(FromUser.ConnectionId).SendAsync("CreateNewChatWindowResponse", mesageChatRoom, _sessionService.CurrentUserSession.UserId, 1);
                    }               
					}
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }


        public async Task CreateNewExistingChatWindow(int sendTo, string message, int jobId)
        {
            try
            {
                string strfromUserId = (ConnectedUsers.Where(u => u.ConnectionId == Context.ConnectionId).Select(u => u.UserID).FirstOrDefault()).ToString();
                int _fromUserId = 0;
                int.TryParse(strfromUserId, out _fromUserId);
                int _toUserId = sendTo;

                List<OnlineUserDetailVM> FromUsers = ConnectedUsers.Where(u => u.UserID == _sessionService.CurrentUserSession.UserId).ToList();
                List<OnlineUserDetailVM> ToUsers = new List<OnlineUserDetailVM>();
                if (sendTo > 0)
                {
                    ToUsers = ConnectedUsers.Where(x => x.UserID == Convert.ToInt32(sendTo)).ToList();
                }

                InitiateChatRoomVM initiateChatRoomVM = new InitiateChatRoomVM();
                initiateChatRoomVM.ToUserId = _toUserId;
                initiateChatRoomVM.Message = message;
                initiateChatRoomVM.JobId = jobId;

                var mesageChatRoom = _messageServie.InitiateChatToProvider(initiateChatRoomVM);
                    foreach (var ToUser in ToUsers)
                    {                                                                                           
                        await Clients.Client(ToUser.ConnectionId).SendAsync("CreateNewExistingChatWindowResponse", mesageChatRoom, 0);
                    }

                    foreach (var FromUser in FromUsers)
                    {
                        await Clients.Client(FromUser.ConnectionId).SendAsync("CreateNewExistingChatWindowResponse", mesageChatRoom, 1);

                    }               
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        #endregion



        #region Notifications

        public async Task SendNotification(int notificationId, int sendTo, int notificationType, int referenceId)
        {
            try
            {
                List<OnlineUserDetailVM> FromUsers = ConnectedUsers.Where(u => u.UserID == _sessionService.CurrentUserSession.UserId).ToList();
                List<OnlineUserDetailVM> ToUsers = new List<OnlineUserDetailVM>();
                if (sendTo != 0)
                {
                    ToUsers = ConnectedUsers.Where(x => x.UserID == sendTo).ToList();
                }
                if (notificationType != 0 && sendTo != 0 && notificationId != 0)
                {
                    _context = new _999SpaceContext();
                    using (IUnityOfWork unityOfWork = new UnityOfWork(_context))
                    {
                        NotificationVM notificationVM = new NotificationVM();                         

                        if (ToUsers.Count() > 0)
                        {
                            foreach (var ToUser in ToUsers)
                            {
                                await Clients.Client(ToUser.ConnectionId).SendAsync("ReceiveNotification", notificationVM);
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }
        #endregion
    }
}
