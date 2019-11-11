using System;
using System.Reflection;

namespace Redbus
{
    public class InvalidEventHandlerException : Exception
    {
        public MethodInfo Method { get; }

        public InvalidEventHandlerException(MethodInfo method)
        {
            Method = method;
        }
    }
}