using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExcelSticker.Wpf.Studio;

/// <summary>First mouse/keyboard focus selects the existing value. Later clicks can position the caret.</summary>
public static class InputEditing
{
    public static void Install(UIElement scope)
    {
        scope.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler((_,e)=>
        {
            if(e.OriginalSource is not DependencyObject source || Keyboard.Modifiers!=ModifierKeys.None)return;
            var box=Ancestor<TextBox>(source);
            if(box==null || box.IsKeyboardFocusWithin)return;
            e.Handled=true;box.Focus();box.SelectAll();
        }));
        scope.AddHandler(UIElement.GotKeyboardFocusEvent,new KeyboardFocusChangedEventHandler((_,e)=>
        {
            if(e.NewFocus is TextBox box)box.SelectAll();
        }));
    }
    internal static T? Ancestor<T>(DependencyObject? source) where T:DependencyObject
    {
        while(source!=null)
        {
            if(source is T found)return found;
            source=source is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }
}
