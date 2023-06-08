using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace SecureArchive.Utils;

public class FolderPickerBuilder {
    //private PickerLocationId _pickerLocationId = PickerLocationId.Unspecified;
    //private string? _commitButtonText = null;
    //private string? _identifier = null;
    private FolderPicker _picker;

    private FolderPickerBuilder(Window parentWindow) {
        _picker = new FolderPicker();
        // Retrieve the window handle (HWND) of the current WinUI 3 window.
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(parentWindow);
        // Initialize the folder picker with the window handle (HWND).
        WinRT.Interop.InitializeWithWindow.Initialize(_picker, hWnd);

        // Set options for your folder picker
        _picker.FileTypeFilter.Add("*");
    }

    public static FolderPickerBuilder Create(Window parentWindow) => new FolderPickerBuilder(parentWindow);

    public FolderPickerBuilder SetSuggestedStartLocation(PickerLocationId locationId) {
        _picker.SuggestedStartLocation = locationId;
        return this;
    }
    public FolderPickerBuilder SetIdentifier(string identifier) {
        _picker.SettingsIdentifier = identifier;
        return this;
    }
    public FolderPickerBuilder SetCommitButtonText(string commitButtonText) {
        _picker.CommitButtonText = commitButtonText;
        return this;
    }

    public FolderPickerBuilder SetViewMode(PickerViewMode mode) {
        _picker.ViewMode = mode;
        return this;
    }

    public FolderPicker Build() {
        return _picker;
    }

    public async Task<StorageFolder?> PickAsync() {
        return await _picker.PickSingleFolderAsync();
    }
}

public class FileOpenPickerBuilder {
    private FileOpenPicker _picker;
    public FileOpenPickerBuilder(Window parentWindow) {
        _picker = new FileOpenPicker();
        // Retrieve the window handle (HWND) of the current WinUI 3 window.
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(parentWindow);
        // Initialize the folder picker with the window handle (HWND).
        WinRT.Interop.InitializeWithWindow.Initialize(_picker, hWnd);

        // Set options for your folder picker
        _picker.SuggestedStartLocation = PickerLocationId.Unspecified;
        //_picker.FileTypeFilter.Add("*");
    }
    public static FileOpenPickerBuilder Create(Window parentWindow) => new FileOpenPickerBuilder(parentWindow);

    public FileOpenPickerBuilder SetSuggestedStartLocation(PickerLocationId locationId) {
        _picker.SuggestedStartLocation = locationId;
        return this;
    }
    public FileOpenPickerBuilder SetIdentifier(string identifier) {
        _picker.SettingsIdentifier = identifier;
        return this;
    }
    public FileOpenPickerBuilder SetCommitButtonText(string commitButtonText) {
        _picker.CommitButtonText = commitButtonText;
        return this;
    }

    public FileOpenPickerBuilder SetViewMode(PickerViewMode mode) {
        _picker.ViewMode = mode;
        return this;
    }

    public FileOpenPickerBuilder AddExtension(string ext) {
        if(!ext.StartsWith(".")) {
            ext = "." + ext;
        }
        _picker.FileTypeFilter.Add(ext);
        return this;
    }
    public FileOpenPickerBuilder AddExtensionAny() {
        _picker.FileTypeFilter.Add("*");
        return this;
    }


    public FileOpenPicker Build() {
        if(_picker.FileTypeFilter.Count==0) {
            AddExtensionAny();
        }
        return _picker;
    }

    public async Task<StorageFile?> PickAsync() {
        return await _picker.PickSingleFileAsync();
    }
    public async Task<IReadOnlyList<StorageFile>?> PickMultiAsync() {
        return await _picker.PickMultipleFilesAsync();
    }
}

public class FileSavePickerBuilder {
    private FileSavePicker _picker;
    public FileSavePickerBuilder(Window parentWindow) {
        _picker = new FileSavePicker();
        // Retrieve the window handle (HWND) of the current WinUI 3 window.
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(parentWindow);
        // Initialize the folder picker with the window handle (HWND).
        WinRT.Interop.InitializeWithWindow.Initialize(_picker, hWnd);

        // Set options for your folder picker
        _picker.SuggestedStartLocation = PickerLocationId.Unspecified;
        //_picker.FileTypeFilter.Add("*");
    }
    public static FileSavePickerBuilder Create(Window parentWindow) => new FileSavePickerBuilder(parentWindow);

    public FileSavePickerBuilder SetSuggestedStartLocation(PickerLocationId locationId) {
        _picker.SuggestedStartLocation = locationId;
        return this;
    }
    public FileSavePickerBuilder SetIdentifier(string identifier) {
        _picker.SettingsIdentifier = identifier;
        return this;
    }
    public FileSavePickerBuilder SetCommitButtonText(string commitButtonText) {
        _picker.CommitButtonText = commitButtonText;
        return this;
    }
    public FileSavePickerBuilder SetSuggestedSaveFile(StorageFile file) {
        _picker.SuggestedSaveFile = file;
        return this;
    }
    public FileSavePickerBuilder SetSuggestedFileName(string name) {
        _picker.SuggestedFileName = name;
        return this;
    }
    public FileSavePickerBuilder AddFileType(string typeDescription, string ext, params string[] otherExts) {
        var extensions = new List<string>() { ext };
        if (otherExts.Length > 0) {
            extensions.AddRange(otherExts);
        }
        _picker.FileTypeChoices.Add(typeDescription, extensions);
        return this;
    }

    public FileSavePicker Build() {
        return _picker;
    }

    public async Task<StorageFile?> PickAsync() {
        return await _picker.PickSaveFileAsync();
    }
}