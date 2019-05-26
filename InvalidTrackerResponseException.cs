using System;

namespace CourseWork
{
    class InvalidTrackerResponseException : Exception
    {
        public InvalidTrackerResponseException()
        {
        }

        public InvalidTrackerResponseException(string message) : base(message)
        {
        }

        public InvalidTrackerResponseException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
