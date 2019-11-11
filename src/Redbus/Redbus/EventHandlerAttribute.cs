using System;

namespace Redbus
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public class EventHandlerAttribute : Attribute
    {
        
    }
}