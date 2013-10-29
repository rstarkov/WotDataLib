using System;

namespace WotDataLib
{
    public class WotDataException : Exception
    {
        public WotDataException() { }
        public WotDataException(string message) : base(message) { }
    }

    public class WotDataUserError : WotDataException
    {
        public WotDataUserError() { }
        public WotDataUserError(string message) : base(message) { }
    }
}
