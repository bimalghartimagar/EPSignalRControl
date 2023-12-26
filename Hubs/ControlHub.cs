using System.Timers;
using Microsoft.AspNetCore.SignalR;

namespace EPSignalRControl.Hubs;

public class ControlHub : Hub
{
    private static Queue<ControlRequest> controlQueue = new Queue<ControlRequest>();
    private static ControlRequest? currentControl;
    private ControlTimer controlTimer;
    private const int CONTROL_TIME = 30; //seconds
    public async Task SendData(string data)
    {
        await Clients.All.SendAsync(data);
    }

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
        ControlTimer.ControlTimers.GetOrAdd(controlRequest.ConnectionId, new ControlTimer(500){
            hubContext = Context,
            hubCallerClients = Clients
        });

        // Grant control if the queue is empty or it's the first request
        if (controlQueue.Count == 1 || controlQueue.Peek() == controlRequest)
        {
            // Clear the timer if a client explicitly releases control
            // ClearControlTimer();
            currentControl = controlRequest;
            // Set a timer to release control after 5 minutes

            SetupTimerForRelease();

            await Clients.Client(controlRequest.ConnectionId).SendAsync("ControlGranted");
        }else{
            await Clients.Client(controlRequest.ConnectionId).SendAsync("ControlQueued");
        }
    }

    public void ReleaseControlMiddleware(object source, ElapsedEventArgs e)
    {
        _ = ReleaseControl(source, e);
    }

    public void SetupTimerForRelease()
    {
        controlTimer = ControlTimer.ControlTimers.GetOrAdd(currentControl.ConnectionId, new ControlTimer(500));
        controlTimer.Elapsed += new ElapsedEventHandler(ReleaseControlMiddleware);
        controlTimer.Enabled = true;
    }

    public async Task ReleaseControl(object source, ElapsedEventArgs e)
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
                await hubClients.Client(currentControl.ConnectionId).SendAsync("ControlReleased");
                // Release control
                currentControl = null;
                // Clear the timer when control is explicitly released
                ClearControlTimer();

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

    private void ClearControlTimer()
    {
        controlTimer = ControlTimer.ControlTimers.GetOrAdd(controlTimer.hubContext.ConnectionId, controlTimer);
        if (controlTimer != null)
        {
            controlTimer.Elapsed -= new ElapsedEventHandler(ReleaseControlMiddleware);
            controlTimer.Enabled = false;
            controlTimer.Dispose();
            controlTimer = null;
        }
    }

    public async Task SendSensorData(SensorData sensorData)
    {
        if (currentControl != null && currentControl.ConnectionId == Context.ConnectionId)
        {
            // Broadcast the received sensor data to all clients
            await Clients.All.SendAsync("ReceiveSensorData", sensorData);
        }
    }
}