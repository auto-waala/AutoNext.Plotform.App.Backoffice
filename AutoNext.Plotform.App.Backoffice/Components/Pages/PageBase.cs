using AutoNext.Plotform.App.Backoffice.Handlers;
using Microsoft.AspNetCore.Components;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class PageBase : ComponentBase
    {
        [Inject] protected SpinnerService Spinner { get; set; } = default!;

        protected async Task LoadData(Func<Task> dataLoader)
        {
            Spinner.Show();
            await dataLoader();
            Spinner.Hide();
        }
    }
}
