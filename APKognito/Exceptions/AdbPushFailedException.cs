using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APKognito.Exceptions;

public class AdbPushFailedException : Exception
{
    public AdbPushFailedException(string apkName, string reason)
        : base($"Failed to push to device: {reason}")
    { }
}
