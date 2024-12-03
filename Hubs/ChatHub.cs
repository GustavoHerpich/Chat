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

            var existingGroupId = GenerateGroupChatId(participants);
            var existingChatSession = await _messageRepository.GetChatSessionAsync(existingGroupId);
            if (existingChatSession != null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Group already exists");
                return;
            }

            var chatSession = new ChatSession
            {
                ChatId = existingGroupId,
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

            var groupInfo = new
            {
                GroupName = groupName,
                Participants = participants
            };

            foreach (var participant in participants)
            {
                var connectionId = _connectedUsers.GetValueOrDefault(participant);
                if (connectionId != null)
                {
                    await Clients.Client(connectionId).SendAsync("GroupCreated", new List<object> { groupInfo });
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

            string chatId = GenerateGroupChatId(recipients);

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

            foreach (var recipient in recipients)
            {
                var connectionId = _connectedUsers.GetValueOrDefault(recipient);
                if (connectionId != null)
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", sender, message);
                }
            }

            await SendNewGroupMessageNotification(groupName);
        }

        public async Task<List<Message>> GetMessagesForChat(string groupName)
        {
            var sender = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (sender == null)
            {
                return [];
            }

            var chatId = GenerateGroupChatId([groupName], sender);
            var messages = await _messageRepository.GetMessagesByChatIdAsync(chatId);

            if (_unreadMessages.ContainsKey(sender))
            {
                _unreadMessages[sender] = 0;
            }

            return messages;
        }
        private static string GenerateGroupChatId(List<string> recipients, string? sender = null)
        {
            var users = new List<string>(recipients);

            if (sender != null)
            {
                users.Add(sender);
            }

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

        public async Task<List<string>> GetParticipantsForGroup(string groupName)
        {
            var chatId = await _messageRepository.GetChatIdByGroupNameAsync(groupName);

            if (string.IsNullOrEmpty(chatId))
            {
                return [];  
            }

            var chatSession = await _messageRepository.GetChatSessionAsync(chatId);

            if (chatSession == null)
            {
                return []; 
            }

            return chatSession.Participants.Select(p => p.Username).ToList();
        }

        public async Task<List<Message>> GetMessagesForGroup(string groupName)
        {
            var sender = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (sender == null)
            {
                return []; 
            }

            var chatId = await _messageRepository.GetChatIdByGroupNameAsync(groupName);

            if (string.IsNullOrEmpty(chatId))
            {
                return [];
            }
            var messages = await _messageRepository.GetMessagesByChatIdAsync(chatId);

            if (_unreadMessages.ContainsKey(sender))
            {
                _unreadMessages[sender] = 0;
            }

            return messages;
        }

        public async Task GetGroups()
        {
            var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userName)) return;

            var userGroups = await _messageRepository.GetUserGroupsAsync(userName);

            foreach (var group in userGroups)
            {
                var groupInfo = new
                {
                    GroupName = group.GroupName,
                    Participants = group.Participants.Select(p => p.Username)
                };

                await Clients.Caller.SendAsync("GroupCreated", new List<object> { groupInfo });
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
