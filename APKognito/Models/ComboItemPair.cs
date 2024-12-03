using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APKognito.Models;

public class ComboItemPair<T>
{
    public string DisplayName { get; set; }
    public T Value { get; set; }

    public ComboItemPair(string displayName, T value)
    {
        DisplayName = displayName;
        Value = value;
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
