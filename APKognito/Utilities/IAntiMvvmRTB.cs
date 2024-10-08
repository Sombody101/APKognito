using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wpf.Ui.Controls;

namespace APKognito.Utilities;

#pragma warning disable S101 // Types should be named in PascalCase
internal interface IAntiMvvmRTB
{
    public void AntiMvvm_SetRichTextbox(RichTextBox rtb);
}
