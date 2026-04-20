namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public class GlobalSpinnerState : IDisposable
    {
        private int _counter = 0;
        private bool _isVisible = false;
        private string _title = "Loading...";
        private string _message = "Please wait";

        public bool IsVisible => _isVisible;
        public string Title => _title;
        public string Message => _message;

        public event Action? OnChange;

        public void Show(string title = "Loading...", string message = "Please wait")
        {
            _counter++;
            Console.WriteLine($"Spinner Show called - Counter: {_counter}");

            if (_counter == 1)
            {
                _title = title;
                _message = message;
                _isVisible = true;
                NotifyStateChanged();
            }
        }

        public void Hide()
        {
            if (_counter > 0)
            {
                _counter--;
            }

            Console.WriteLine($"Spinner Hide called - Counter: {_counter}");

            if (_counter == 0 && _isVisible)
            {
                _isVisible = false;
                NotifyStateChanged();
            }
        }

        public void Reset()
        {
            _counter = 0;
            _isVisible = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            Console.WriteLine($"Spinner State Changed - IsVisible: {_isVisible}");
            OnChange?.Invoke();
        }

        public void Dispose()
        {
            Reset();
        }
    }
}
