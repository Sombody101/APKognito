using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APKognito.Configurations;

public interface IKognitoConfiguration
{
    /// <summary>
    /// The name of the file for a config. (e.g. <see langword="specific-config.json"/>). 
    /// All configs will be stored in <see langword="%APPDATA%"/> under <see langword="APKognito"/>.
    /// </summary>
    public string FileName { get; }

    public ConfigType ConfigType { get; }
}
