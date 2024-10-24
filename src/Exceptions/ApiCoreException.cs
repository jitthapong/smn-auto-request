using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VtecInventory.Exceptions
{
    public class ApiCoreException : Exception
    {
        public ApiCoreException(string message) : base(message) { }
    }
}
