using Microsoft.AspNetCore.SignalR;

namespace EPSignalRControl.Hubs;

public class ControlHub : Hub
{
    private static Queue<ControlRequest> controlQueue = new Queue<ControlRequest>();
    private static ControlRequest? currentControl;
    private static Timer? controlTimer;
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

        // Clear the timer if a client explicitly releases control
        ClearControlTimer();

        // Add the control request to the queue
        controlQueue.Enqueue(controlRequest);

        // Grant control if the queue is empty or it's the first request
        if (controlQueue.Count == 1 || controlQueue.Peek() == controlRequest)
        {
            currentControl = controlRequest;

            // Set a timer to release control after 5 minutes
            controlTimer = new Timer(ReleaseControl, null, 5 * 60 * 1000, Timeout.Infinite);
            await Clients.Client(controlRequest.ConnectionId).SendAsync("ControlGranted");
        }
    }

    private void ReleaseControl(object? state)
    {
        _ = ReleaseControl();
    }

    public async Task ReleaseControl()
    {
        if (currentControl != null && currentControl.ConnectionId == Context.ConnectionId)
        {
            // Release control
            currentControl = null;

            // Remove the first request from the queue
            if (controlQueue.Count > 0)
                controlQueue.Dequeue();

            // Grant control to the next in the queue
            if (controlQueue.Count > 0)
            {
                currentControl = controlQueue.Peek();
                await Clients.Client(currentControl.ConnectionId).SendAsync("ControlGranted");
            }
        }
        // Clear the timer when control is explicitly released
        ClearControlTimer();
    }

    private void ClearControlTimer()
    {
        if (controlTimer != null)
        {
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