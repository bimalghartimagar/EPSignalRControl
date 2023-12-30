using System.Timers;
using Microsoft.AspNetCore.SignalR;

namespace EPSignalRControl.Hubs;

public class ControlHub : Hub
{
    private static Queue<ControlRequest> controlQueue = new Queue<ControlRequest>();
    private static ControlRequest? currentControl;
    private ControlTimer controlTimer;
    private const int CONTROL_TIME = 120; //seconds

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
        ControlTimer.ControlTimers.GetOrAdd(controlRequest.ConnectionId, controlTimer);

        // Grant control if the queue is empty or it's the first request
        if (controlQueue.Count == 1 || controlQueue.Peek() == controlRequest)
        {
            currentControl = controlRequest;

            SetupTimerForRelease();

            await Clients.Client(controlRequest.ConnectionId).SendAsync("ControlGranted");
        }else{
            await Clients.Client(controlRequest.ConnectionId).SendAsync("ControlQueued");
        }
    }

    public void ReleaseControlMiddleware(object source, ElapsedEventArgs e)
    {
        _ = AutoReleaseControl(source, e);
    }

    public void SetupTimerForRelease()
    {
        if(controlTimer == null)
        {
            controlTimer = ControlTimer.ControlTimers.GetOrAdd(currentControl.ConnectionId, new ControlTimer(500));
        }
        controlTimer.Elapsed += new ElapsedEventHandler(ReleaseControlMiddleware);
        controlTimer.Enabled = true;
    }

    public async Task AutoReleaseControl(object source, ElapsedEventArgs e)
    {
        var controlTimer = (ControlTimer)source;
        HubCallerContext hcallerContext = controlTimer.hubContext;
        IHubCallerClients hubClients = controlTimer.hubCallerClients;

        if (currentControl != null)//&& currentControl.ConnectionId == Context.ConnectionId)
        {
            var elapsedSeconds = DateTime.Now.Subtract(currentControl.RequestTime).Seconds;
            await hubClients.Client(currentControl.ConnectionId).SendAsync("ControlRemaining", CONTROL_TIME - elapsedSeconds);
            if (elapsedSeconds >= CONTROL_TIME)
            {
                await hubClients.Client(hcallerContext.ConnectionId).SendAsync("ControlReleased");
                await controlTimer.hubCallerClients.All.SendAsync("ReceiveData", new CameraData());
                // Release control
                currentControl = null;
                // Clear the timer when control is explicitly released
                ClearControlTimer(hcallerContext.ConnectionId);

                // Remove the first request from the queue
                if (controlQueue.Count > 0)
                    controlQueue.Dequeue();

                // Grant control to the next in the queue
                if (controlQueue.Count > 0)
                {
                    currentControl = controlQueue.Peek();
                    currentControl.RequestTime = DateTime.Now;
                    SetupTimerForRelease();
                    await hubClients.Client(currentControl.ConnectionId).SendAsync("ControlGranted", currentControl.RequestTime.ToString());
                }
            }
        }
    }
    public async Task ReleaseControl()
    {

        if (currentControl != null && currentControl.ConnectionId == Context.ConnectionId)
        {
            controlTimer = ControlTimer.ControlTimers.GetOrAdd(Context.ConnectionId, controlTimer);

            await Clients.Client(Context.ConnectionId).SendAsync("ControlReleased");
            await Clients.All.SendAsync("ReceiveData", new CameraData());
            // Release control
            currentControl = null;
            // Clear the timer when control is explicitly released
            ClearControlTimer(Context.ConnectionId);

            // Remove the first request from the queue
            if (controlQueue.Count > 0)
                controlQueue.Dequeue();

            // Grant control to the next in the queue
            if (controlQueue.Count > 0)
            {
                currentControl = controlQueue.Peek();
                currentControl.RequestTime = DateTime.Now;
                SetupTimerForRelease();
                await Clients.Client(currentControl.ConnectionId).SendAsync("ControlGranted", currentControl.RequestTime.ToString());
            }
        }
    }

    private void ClearControlTimer(string connectionId)
    {
        controlTimer = ControlTimer.ControlTimers.GetOrAdd(connectionId, controlTimer);
        if (controlTimer != null)
        {
            controlTimer.Elapsed -= new ElapsedEventHandler(ReleaseControlMiddleware);
            controlTimer.Enabled = false;
            controlTimer.Dispose();
            controlTimer = null;
        }
    }

    public async Task SendData(CameraData cameraData)
    {
        controlTimer = ControlTimer.ControlTimers.GetOrAdd(Context.ConnectionId, controlTimer);
        if (controlTimer != null && controlTimer.hubContext!= null && Context.ConnectionId == controlTimer.hubContext.ConnectionId)
        {
            // Broadcast the received sensor data to all clients
            await controlTimer.hubCallerClients.All.SendAsync("ReceiveData", cameraData);
        }
    }
}