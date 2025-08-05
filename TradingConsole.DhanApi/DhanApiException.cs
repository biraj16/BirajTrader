using System;

namespace TradingConsole.DhanApi
{
    /// <summary>
    /// Represents errors that occur during calls to the Dhan API.
    /// </summary>
    public class DhanApiException : Exception
    {
        public DhanApiException()
        {
        }

        public DhanApiException(string message)
            : base(message)
        {
        }

        public DhanApiException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}