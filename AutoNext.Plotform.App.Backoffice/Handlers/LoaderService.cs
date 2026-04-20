namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public class LoaderService
    {
        private bool _isLoading = false;
        private string _message = "Loading...";

        public event Action? OnChange;

        public bool IsLoading => _isLoading;
        public string Message => _message;

        public void Show(string message = "Loading...")
        {
            // ✅ Set both fields first, then fire OnChange once
            _message = message;
            _isLoading = true;
            OnChange?.Invoke();
        }

        public void Hide()
        {
            _isLoading = false;
            OnChange?.Invoke();
        }
    }
}