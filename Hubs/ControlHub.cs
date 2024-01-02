using System.Timers;
using Microsoft.AspNetCore.SignalR;

namespace EPSignalRControl.Hubs;

public class ControlHub : Hub
{
    private static Queue<ControlRequest> controlQueue = new Queue<ControlRequest>();
    private static ControlRequest? currentControl;
    private ControlTimer? controlTimer;
    private const int CONTROL_TIME = 60; //seconds

    public async Task RequestControl()
    {
        var controlRequest = new ControlRequest
        {
            ConnectionId = Context.ConnectionId,
            RequestTime = DateTime.Now
        };

        // Add the control request to the queue
        controlQueue.Enqueue(controlRequest);

        // Add timer to dictionary for request
        controlTimer = new ControlTimer(500)
        {
            hubContext = Context,
            hubCallerClients = Clients
        };
        controlTimer = ControlTimer.ControlTimers.GetOrAdd(controlRequest.ConnectionId, controlTimer);

        // Grant control if the queue is empty or it's the first request
        if (controlQueue.Count == 1 || controlQueue.Peek() == controlRequest)
        {
            currentControl = controlRequest;

            SetupTimerForRelease(controlRequest);

            await Clients.Client(controlRequest.ConnectionId).SendAsync("ControlGranted");
        }
        else
        {
            await Clients.Client(controlRequest.ConnectionId).SendAsync("ControlQueued");
        }
    }

    public void ReleaseControlMiddleware(object source, ElapsedEventArgs e)
    {
        _ = AutoReleaseControl(source, e);
    }

    public void SetupTimerForRelease(ControlRequest controlRequest)
    {
        if (controlRequest != null && controlTimer == null)
        {
            controlTimer = ControlTimer.ControlTimers.GetOrAdd(controlRequest.ConnectionId, controlTimer);
        }
        controlTimer.Elapsed += new ElapsedEventHandler(ReleaseControlMiddleware);
        controlTimer.Enabled = true;
    }
    
    // Check elapsed seconds and release control
    public async Task AutoReleaseControl(object source, ElapsedEventArgs e)
    {
        var controlTimer = (ControlTimer)source;
        HubCallerContext hcallerContext = controlTimer.hubContext;
        IHubCallerClients hubClients = controlTimer.hubCallerClients;

        if (currentControl != null)//&& currentControl.ConnectionId == Context.ConnectionId)
        {
            var elapsedSeconds = Math.Ceiling(DateTime.Now.Subtract(currentControl.RequestTime).TotalSeconds);
            await hubClients.Client(currentControl.ConnectionId).SendAsync("ControlRemaining", CONTROL_TIME - elapsedSeconds);
            if (elapsedSeconds >= CONTROL_TIME)
            {
                await ClearTimerAndControl(hubClients, hcallerContext);
            }
        }
    }
    public async Task ReleaseControl()
    {
        try
        {
            if (currentControl != null && currentControl.ConnectionId == Context.ConnectionId)
            {
                await ClearTimerAndControl(Clients, Context);
            }
        }
        catch (Exception ex)
        {
            await Clients.All.SendAsync(ex.ToString());
        }
    }

    private async Task ClearTimerAndControl(IHubCallerClients hubClients, HubCallerContext context)
    {
        try
        {
            // Clear the timer when control is explicitly released
            ClearControlTimer(context.ConnectionId);

            await hubClients.Client(context.ConnectionId).SendAsync("ControlReleased");
            await hubClients.All.SendAsync("ReceiveData", new CameraData());
            // Release control
            currentControl = null;

            // Remove the first request from the queue
            if (controlQueue.Count > 0)
                controlQueue.Dequeue();

            // Grant control to the next in the queue
            if (controlQueue.Count > 0)
            {
                currentControl = controlQueue.Peek();
                currentControl.RequestTime = DateTime.Now;
                SetupTimerForRelease(currentControl);
                await hubClients.Client(currentControl.ConnectionId).SendAsync("ControlGranted", currentControl.RequestTime.ToString());
            }
        }
        catch (Exception ex)
        {
            await Clients.All.SendAsync(ex.ToString());
        }
    }

    private void ClearControlTimer(string connectionId)
    {
        controlTimer = ControlTimer.ControlTimers.GetOrAdd(connectionId, new ControlTimer(500));
        if (controlTimer != null)
        {
            controlTimer.Elapsed -= new ElapsedEventHandler(ReleaseControlMiddleware);
            controlTimer.Enabled = false;
            controlTimer = null;
        }
    }

    public async Task SendData(CameraData cameraData)
    {
        controlTimer = ControlTimer.ControlTimers.GetOrAdd(Context.ConnectionId, new ControlTimer(500));
        if (controlTimer != null && controlTimer.hubContext != null && Context.ConnectionId == controlTimer.hubContext.ConnectionId)
        {
            // Broadcast the received sensor data to all clients
            await controlTimer.hubCallerClients.All.SendAsync("ReceiveData", cameraData);
        }
    }
}