using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APKognito.Configurations;

public class InvalidConfigModelException : Exception
{
    public InvalidConfigModelException(Type configType)
        : base($"The config model {configType.Name} does not implement {nameof(ConfigFileAttribute)}")
    {
    }
}
