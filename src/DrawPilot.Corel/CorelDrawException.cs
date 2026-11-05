using System;

namespace DrawPilot.Corel
{
    public sealed class CorelDrawException : Exception
    {
        public CorelDrawException(string message)
            : base(message)
        {
        }

        public CorelDrawException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
