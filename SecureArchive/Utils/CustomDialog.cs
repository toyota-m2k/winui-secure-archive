using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils; 

public interface ICustomDialogPage<R> {
    event Action<R>? Complete;
    ContentDialog Dialog { get; set; }
}

public class CustomDialogBuilder<T,R> where T: Page, ICustomDialogPage<R> {
    public static CustomDialogBuilder<T,R> Create(XamlRoot root, T page) {
        return new CustomDialogBuilder<T,R>() { 
            XamlRoot = root,
            Page = page,
        };
    }

    ContentDialog Dialog = new() {
        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
    };

    private T Page { get; set; } = null!;

    private XamlRoot XamlRoot {
        get => Dialog.XamlRoot;
        set => Dialog.XamlRoot = value;
    }
    private object? Content {
        get => Dialog.Content;
        set => Dialog.Content = value;
    }

    public CustomDialogBuilder<T,R> SetTitle(string title) {
        Dialog.Title = title;
        return this;
    }

    private R? primaryValue = default;
    public CustomDialogBuilder<T,R> SetPrimaryButton(string text, R? value=default, bool asDefault=false) {
        Dialog.PrimaryButtonText = text;
        if(asDefault) {
            Dialog.DefaultButton = ContentDialogButton.Primary;
        }
        primaryValue = value;
        return this;
    }

    private R? secondaryValue = default;
    public CustomDialogBuilder<T,R> SetSecondaryButton(string text, R? value = default, bool asDefault=false) {
        Dialog.SecondaryButtonText = text;
        if(asDefault) {
            Dialog.DefaultButton = ContentDialogButton.Secondary;
        }
        secondaryValue = value;
        return this;
    }

    public CustomDialogBuilder<T,R> SetCloseButton(string text, bool asDefault=false) {
        Dialog.CloseButtonText = text;
        if(asDefault) {
            Dialog.DefaultButton = ContentDialogButton.Close;
        }
        return this;
    }

    private R? defaultValue = default;
    public CustomDialogBuilder<T,R> SetCancelValue(R? value=default) {
        defaultValue = value;
        return this;
    }

    public CustomDialogBuilder<T,R> SetDefaultButton(ContentDialogButton button) {
        Dialog.DefaultButton = button;
        return this;
    }



    public async Task<R?> ShowAsync() {
        Dialog.Content = Page;
        R? completionResult = default;
        bool completedByHandler = false;
        Page.Complete += (r) => {
            completedByHandler = true;
            completionResult = r;
            Dialog.Hide();
        };
        var result = await Dialog.ShowAsync();
        if(completedByHandler) {
            return completionResult;
        } else if(result == ContentDialogResult.Primary) {
            return primaryValue;
        } else if(result == ContentDialogResult.Secondary) {
            return secondaryValue;
        } else { 
            return defaultValue;
        }
    }

}
