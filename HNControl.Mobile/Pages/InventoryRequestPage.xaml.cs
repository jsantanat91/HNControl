using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class InventoryRequestPage : ContentPage
{
    private readonly ModulesService _modules;
    private readonly List<InventoryCatalogItemDto> _items = new();
    private readonly List<InventoryProjectDto> _projects = new();
    private readonly List<LineVm> _lines = new();
    private bool _isInMode;

    public InventoryRequestPage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    public void SetMode(bool isInMode)
    {
        _isInMode = isInMode;
        HeaderTitleLabel.Text = isInMode ? "Solicitud de entrada" : "Solicitud de salida";
        HeaderSubLabel.Text = isInMode
            ? "Registra ingreso de material para aprobacion."
            : "Registra salida de material para aprobacion.";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_items.Count == 0)
        {
            await LoadCatalogAsync();
        }
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            var data = await _modules.GetInventoryCatalogAsync();
            _items.Clear();
            _items.AddRange(data.Items ?? new List<InventoryCatalogItemDto>());
            ItemPicker.ItemsSource = _items.Select(i => new ItemPickerVm
            {
                Id = i.Id,
                Display = $"{i.Name} | Stock: {i.Stock:N2} {i.Unit}"
            }).ToList();

            _projects.Clear();
            _projects.Add(new InventoryProjectDto { Id = Guid.Empty, Title = "Sin proyecto" });
            if (data.Projects != null)
            {
                _projects.AddRange(data.Projects);
            }
            ProjectPicker.ItemsSource = _projects.Select(p => new ProjectPickerVm { Id = p.Id, Title = p.Title }).ToList();
            ProjectPicker.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Inventario", ex.Message, "OK");
        }
    }

    private async void OnAddLineClicked(object sender, EventArgs e)
    {
        if (ItemPicker.SelectedItem is not ItemPickerVm item)
        {
            await DisplayAlertAsync("Inventario", "Selecciona un item.", "OK");
            return;
        }

        if (!decimal.TryParse(QtyEntry.Text, out var qty) || qty <= 0m)
        {
            await DisplayAlertAsync("Inventario", "Cantidad invalida.", "OK");
            return;
        }

        var notes = (LineNotesEntry.Text ?? "").Trim();
        _lines.Add(new LineVm
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            ItemName = item.Display,
            Quantity = qty,
            Notes = notes,
            Meta = $"Cantidad: {qty:N2}" + (string.IsNullOrWhiteSpace(notes) ? "" : $" | Nota: {notes}")
        });

        LinesCollection.ItemsSource = null;
        LinesCollection.ItemsSource = _lines.ToList();
        QtyEntry.Text = "1";
        LineNotesEntry.Text = "";
    }

    private void OnRemoveLineClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Guid id)
        {
            return;
        }

        var row = _lines.FirstOrDefault(x => x.Id == id);
        if (row == null) return;
        _lines.Remove(row);
        LinesCollection.ItemsSource = null;
        LinesCollection.ItemsSource = _lines.ToList();
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_lines.Count == 0)
        {
            await DisplayAlertAsync("Inventario", "Agrega al menos una linea.", "OK");
            return;
        }

        try
        {
            var projectId = (ProjectPicker.SelectedItem as ProjectPickerVm)?.Id;
            if (projectId == Guid.Empty) projectId = null;

            var body = new InventoryCreateRequestDto
            {
                Type = _isInMode ? "In" : "Out",
                ProjectId = projectId,
                Notes = (GlobalNotesEditor.Text ?? "").Trim(),
                Lines = _lines.Select(l => new InventoryRequestLineDto
                {
                    ItemId = l.ItemId,
                    Quantity = l.Quantity,
                    Notes = l.Notes ?? "",
                    Reference = "",
                    SerialNumber = "",
                    AssignedClientId = null
                }).ToList()
            };

            var res = await _modules.CreateInventoryRequestAsync(body);
            await DisplayAlertAsync("Inventario", res.Message, "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Inventario", ex.Message, "OK");
        }
    }

    private sealed class ItemPickerVm
    {
        public Guid Id { get; set; }
        public string Display { get; set; } = "";
    }

    private sealed class ProjectPickerVm
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
    }

    private sealed class LineVm
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Notes { get; set; } = "";
        public string Meta { get; set; } = "";
    }
}
