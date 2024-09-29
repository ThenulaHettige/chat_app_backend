using System.Collections.Concurrent;
using FormulaOne.ChatService.Models;
using Microsoft.AspNet.SignalR.Infrastructure;

namespace FormulaOne.ChatService.DataService;

public class SharedDb
{
    public readonly ConcurrentDictionary<string, UserConnection> _Connections = new ();
    
    public ConcurrentDictionary<string , UserConnection> connections => _Connections;
}