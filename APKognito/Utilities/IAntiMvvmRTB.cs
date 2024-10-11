using Wpf.Ui.Controls;

namespace APKognito.Utilities;

/// <summary>
/// Anti MVVM RichTextBox. Used when a rich text box is only appended to via one paragraph element, which requires a
/// direct reference to the object without using a converter or runtime generated XAML.
/// </summary>
internal interface IAntiMvvmRTB
{
    public void AntiMvvm_SetRichTextbox(RichTextBox rtb);
}