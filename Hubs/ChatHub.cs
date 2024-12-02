using Chat.Entities;
using Chat.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Chat.Hubs
{
    [Authorize]
    public class ChatHub(IMessageRepository _messageRepository, IUserRepository _userRepository) : Hub
    {
        private static readonly ConcurrentDictionary<string, string> _connectedUsers = new();

        private static readonly ConcurrentDictionary<string, int> _unreadMessages = new();

        public async Task CreateGroup(string groupName, List<string> participants)
        {
            var sender = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (sender == null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "User not authenticated");
                return;
            }

            var chatSession = new ChatSession
            {
                ChatId = GenerateGroupChatId(sender, participants),
                GroupName = groupName
            };

            foreach (var participant in participants)
            {
                var user = await _userRepository.FindByUsernameAsync(participant);
                if (user != null)
                {
                    chatSession.AddParticipant(user);
                }
            }

            await _messageRepository.AddChatSessionAsync(chatSession);

            foreach (var participant in participants)
            {
                var connectionId = _connectedUsers.GetValueOrDefault(participant);
                if (connectionId != null)
                {
                    await Clients.Client(connectionId).SendAsync("GroupCreated", groupName, participants);
                }
            }
        }

        public async Task SendMessageToGroup(string message, List<string> recipients, string groupName)
        {
            var sender = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (sender == null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "User not authenticated");
                return;
            }

            string chatId = GenerateGroupChatId(sender, recipients);

            var chatSession = await _messageRepository.GetChatSessionAsync(chatId);
            if (chatSession == null)
            {
                chatSession = new ChatSession
                {
                    ChatId = chatId,
                    GroupName = groupName
                };

                foreach (var recipient in recipients)
                {
                    var user = await _userRepository.FindByUsernameAsync(recipient);
                    if (user != null)
                    {
                        chatSession.AddParticipant(user);
                    }
                }
                await _messageRepository.AddChatSessionAsync(chatSession);
            }

            var messageToSave = new Message
            {
                Sender = sender,
                Content = message,
                ChatId = chatId
            };

            await _messageRepository.AddMessageAsync(messageToSave);

            await Clients.Group(groupName).SendAsync("ReceiveMessage", sender, message);
        }

        public async Task<List<Message>> GetMessagesForChat(string groupName)
        {
            var sender = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (sender == null)
            {
                return [];
            }

            var chatId = GenerateGroupChatId(sender, [groupName]);
            var messages = await _messageRepository.GetMessagesByChatIdAsync(chatId);

            if (_unreadMessages.ContainsKey(sender))
            {
                _unreadMessages[sender] = 0;
            }

            return messages;
        }

        public static Task<int> GetUnreadMessagesCount(string userName)
        {
            return Task.FromResult(_unreadMessages.ContainsKey(userName) ? _unreadMessages[userName] : 0);
        }

        private static string GenerateGroupChatId(string sender, List<string> recipients)
        {
            var users = new List<string> { sender };
            users.AddRange(recipients);
            users.Sort();
            return string.Join("-", users);
        }


        public override async Task OnConnectedAsync()
        {
            var user = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (user != null)
            {
                if (_connectedUsers.ContainsKey(user))
                {
                    _connectedUsers[user] = Context.ConnectionId;
                }
                else
                {
                    _connectedUsers.TryAdd(user, Context.ConnectionId);
                }
                await Clients.All.SendAsync("OnlineUsers", _connectedUsers.Keys.ToList());
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var user = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (user != null)
            {
                _connectedUsers.TryRemove(user, out _);
                await Clients.All.SendAsync("UserDisconnected", $"{user} left the chat");

                await Clients.All.SendAsync("OnlineUsers", _connectedUsers.Keys.ToList());
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendPrivateMessage(string receiver, string message)
        {
            var sender = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (sender == null)
            {
                await Clients.Caller.SendAsync("ReceivePrivateMessage", "System", "User not authenticated");
                return;
            }

            string chatId = GetChatId(sender, receiver);
            var chatSession = await _messageRepository.GetChatSessionAsync(chatId); 

            if (chatSession == null)
            {
                chatSession = new ChatSession
                {
                    ChatId = chatId,
                    GroupName = receiver
                };
                await _messageRepository.AddChatSessionAsync(chatSession); 
            }

            var messageToSave = new Message
            {
                Sender = sender,
                Content = message,
                ChatId = chatId
            };

            await _messageRepository.AddMessageAsync(messageToSave);

            if (_connectedUsers.TryGetValue(receiver, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage", sender, message);

                await Clients.Caller.SendAsync("ReceivePrivateMessage", sender, message);

                await SendNewConversationNotification(receiver, sender);
            }
            else
            {
                await Clients.Caller.SendAsync("ReceivePrivateMessage", "System", "User is not connected");
            }
        }

        public async Task SendNewGroupMessageNotification(string groupName)
        {
            foreach (var recipient in _connectedUsers.Keys)
            {
                if (_connectedUsers.TryGetValue(recipient, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("NewGroupMessageNotification", groupName);
                }
            }
        }
        public async Task<List<string>> GetUserGroups()
        {
            var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userName))
            {
                return new List<string>(); 
            }

            var userGroups = await _messageRepository.GetUserGroupsAsync(userName);

            return userGroups.Select(g => g.GroupName).ToList();
        }

        private static string GetChatId(string sender, string receiver)
        {
            var users = new[] { sender, receiver };
            Array.Sort(users);
            return string.Join("-", users);
        }

        public async Task SendNewConversationNotification(string receiver, string sender)
        {
            if (_connectedUsers.TryGetValue(receiver, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("NewConversationNotification", sender);
            }
        }

        public async Task<List<string>> GetOnlineUsers()
        {
            var currentUser = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            var users = _connectedUsers.Keys.Where(u => u != currentUser).ToList();
            await Clients.Caller.SendAsync("OnlineUsers", users);
            return users;
        }
    }
}
