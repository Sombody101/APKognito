namespace APKognito.Utilities.MVVM;

/// <summary>
/// Anti MVVM RichTextBox. Used when a rich text box is only appended to via one paragraph element, which requires a
/// direct reference to the object without using a converter or runtime generated XAML.
/// </summary>
internal interface IAntiMvvmRtb
{
    public void AntiMvvm_SetRichTextbox(RichTextBox rtb);
}