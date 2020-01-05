using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EasyConverter.WebUI.Jobs
{
    [System.Serializable]
    public class JobFailedException : System.Exception
    {
        public JobFailedException() { }
        public JobFailedException(string message) : base(message) { }
        public JobFailedException(string message, System.Exception inner) : base(message, inner) { }
        protected JobFailedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
