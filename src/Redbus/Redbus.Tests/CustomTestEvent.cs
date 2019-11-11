using Redbus.Events;

namespace Redbus.Tests
{
    internal class CustomTestEvent : EventBase
    {
        public string Name { get; set; }
        public int Identifier { get; set; }
    }
}