using Reactive.Bindings;
using SecureArchive.DI;

namespace SecureArchive.Views.ViewModels {
    internal class MenuPageViewModel {
        IPageService _pageService;
        public ReactiveCommandSlim ListCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim SettingsCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim ClearAllCommand { get; } = new ReactiveCommandSlim();

        public MenuPageViewModel(IPageService pageSercice) {
            _pageService = pageSercice;
            SettingsCommand.Subscribe(pageSercice.ShowSettingsPage);
            ListCommand.Subscribe(pageSercice.ShowListPage);
        }
    }
}
