using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Testing.Exceptions
{
    class InvalidFormatException : FormatException
    {
        public InvalidFormatException() : base()
        {
        }

        public InvalidFormatException(String message) : base(message)
        {
        }
    }
}
