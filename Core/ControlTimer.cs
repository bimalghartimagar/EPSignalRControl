using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

public class ControlTimer : System.Timers.Timer{
    public static ConcurrentDictionary<string, ControlTimer> ControlTimers = new();
    public HubCallerContext hubContext {get;set;}
    public IHubCallerClients hubCallerClients {get;set;}
    public ControlTimer(double interval) : base(interval)
    {
    }
}