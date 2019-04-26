using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
