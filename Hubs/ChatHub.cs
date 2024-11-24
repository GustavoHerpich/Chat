using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Chat.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> _connectedUsers = new();
    public async Task SendMessage(string message)
    {
        var user = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
        if (user == null)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "User not authenticated");
            return;
        }

        await Clients.All.SendAsync("ReceiveMessage", user, message);
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
        var user = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
        if (user == null)
        {
            await Clients.Caller.SendAsync("ReceivePrivateMessage", "System", "User not authenticated");
            return;
        }

        if (_connectedUsers.TryGetValue(receiver, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage", user, message);
            await SendNewConversationNotification(receiver);
        }
        else
        {
            await Clients.Caller.SendAsync("ReceivePrivateMessage", "System", "User is not connected");
        }
    }

    public async Task SendNewConversationNotification(string receiver)
    {
        if (_connectedUsers.TryGetValue(receiver, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("NewConversationNotification", receiver);
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
