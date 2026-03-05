using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class KnowledgeModulePage : ContentPage
{
    private readonly ModulesService _modules;

    public KnowledgeModulePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var data = await _modules.GetKnowledgeAsync();
            ItemsCollection.ItemsSource = data.Select(x => new
            {
                x.Title,
                x.Category,
                x.Url,
                HasUrl = !string.IsNullOrWhiteSpace(x.Url),
                Meta = $"{x.DocType} | {x.Status} | {x.UpdatedAt:yyyy-MM-dd}"
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnOpenClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string url || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await DisplayAlertAsync("Documentos", "URL invalida.", "OK");
            return;
        }

        await Launcher.Default.OpenAsync(uri);
    }
}
