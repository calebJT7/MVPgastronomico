using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RotiseriaAPI.Security;

namespace RotiseriaAPI.Hubs;

[Authorize]
public class OrderHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var businessId = Context.User?.GetBusinessId();
        if (businessId.HasValue && businessId.Value > 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"business_{businessId.Value}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var businessId = Context.User?.GetBusinessId();
        if (businessId.HasValue && businessId.Value > 0)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"business_{businessId.Value}");
        }
        await base.OnDisconnectedAsync(exception);
    }
}
