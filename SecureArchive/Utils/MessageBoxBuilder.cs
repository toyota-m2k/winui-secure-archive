using Microsoft.UI.Xaml;
using Windows.UI.Popups;

namespace SecureArchive.Utils {
    internal class MessageBoxBuilder {
        private MessageDialog _dialog;
        
        public static MessageBoxBuilder Create(Window window) { return new MessageBoxBuilder(window); }
        
        public MessageBoxBuilder(Window window) { 
            _dialog = new MessageDialog("");
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            // Initialize the folder picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(_dialog, hWnd);
        }

        public MessageBoxBuilder SetTitle(string title) {
            _dialog.Title = title;
            return this;
        }

        public MessageBoxBuilder SetMessage(string message) {
            _dialog.Content = message;
            return this;
        }


        public MessageBoxBuilder AddButton(string text, Action? fn=null, object? id=null) {
            if (fn != null) {
                _dialog.Commands.Add(new UICommand(text, (c) => fn(), null));
            } else {
                _dialog.Commands.Add(new UICommand(text) { Id = id });
            }
            return this;
        }
        public MessageBoxBuilder AddButton(string text, object id) {
            _dialog.Commands.Add(new UICommand(text) { Id = id });
            return this;
        }

        public async Task<object?> ShowAsync() {
            return (await _dialog.ShowAsync()).Id;
        }
    }
}
