using HNControl.Shared;
using HNControl.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace HNControl.Mobile.Pages;

public sealed class OrderDeliveryPage : ContentPage
{
    private readonly OrdersService _orders;
    private readonly Guid _id;
    private readonly Action _saved;
    private OrderDeliveryResponse? _response;
    private readonly VerticalStackLayout _root = new() { Spacing = 16, Padding = new Thickness(18, 18, 18, 30) };
    private readonly Label _error = new() { TextColor = Color.FromArgb("#AA253A"), IsVisible = false };
    public OrderDeliveryPage(OrdersService orders, Guid id, Action saved)
    {
        _orders = orders; _id = id; _saved = saved;
        Title = "Entrega y equipos"; BackgroundColor = Color.FromArgb("#F3F7FC");
        Content = new ScrollView { Content = _root };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_response != null) return;
        try { _response = await _orders.DeliveryAsync(_id); Build(); }
        catch (Exception ex) { _root.Clear(); _root.Add(new Label { Text = ex.Message }); }
    }
    private void Build()
    {
        _root.Clear();
        var response = _response!; var data = response.Data;
        _root.Add(new Border { BackgroundColor = Color.FromArgb("#173B5D"), StrokeThickness = 0, Padding = 20, StrokeShape = new RoundRectangle { CornerRadius = 18 }, Content = new VerticalStackLayout { Spacing = 8, Children = {
            new Label { Text = data.KindLabel.ToUpperInvariant(), FontSize = 11, TextColor = Color.FromArgb("#A2D6FF") },
            new Label { Text = response.Title, FontSize = 21, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
            new Label { Text = response.Client + (string.IsNullOrWhiteSpace(response.Address) ? "" : "\n" + response.Address), FontSize = 13, TextColor = Color.FromArgb("#D6E9FF") }
        } } });
        _root.Add(new Label { Text = response.CanEdit ? "Guarda los equipos antes de capturar las firmas. Todo se incluirá en el mismo PDF." : "Consulta de entrega. Solo se puede editar una orden tomada y sin firmas, antes de enviarla a revisión.", FontSize = 12, TextColor = Color.FromArgb("#627793") });
        _root.Add(_error);
        var scope = Section("01 · Alcance de la entrega");
        Select(scope, "¿Qué entregamos?", ["Servicio", "Entrega de hardware", "Servicio + hardware"], Array.IndexOf(OrderDeliveryData.Kinds, data.Kind), i => data.Kind = OrderDeliveryData.Kinds[i]);
        var router = Section("02 · Router");
        var toggle = new Switch { IsToggled = data.HasRouter };
        router.Add(new HorizontalStackLayout { Spacing = 10, Children = { toggle, new Label { Text = "Registrar router", VerticalOptions = LayoutOptions.Center } } });
        var routerFields = new VerticalStackLayout { Spacing = 12, IsVisible = data.HasRouter };
        toggle.Toggled += (_, e) => { data.HasRouter = e.Value; routerFields.IsVisible = e.Value; };
        Field(routerFields, "Marca del router *", data.RouterBrand, v => data.RouterBrand = v);
        Field(routerFields, "Modelo", data.RouterModel, v => data.RouterModel = v);
        Field(routerFields, "Serie del router *", data.RouterSerial, v => data.RouterSerial = v);
        Select(routerFields, "Modalidad", OrderDeliveryData.Modes, Array.IndexOf(OrderDeliveryData.Modes, data.RouterMode), i => data.RouterMode = OrderDeliveryData.Modes[i]);
        Select(routerFields, "Estado físico", OrderDeliveryData.Conditions, Array.IndexOf(OrderDeliveryData.Conditions, data.RouterCondition), i => data.RouterCondition = OrderDeliveryData.Conditions[i]);
        Field(routerFields, "Accesorios / observaciones", data.RouterAccessories, v => data.RouterAccessories = v, 400);
        router.Add(routerFields);
        var equipment = Section("03 · Otros equipos");
        var rows = new VerticalStackLayout { Spacing = 16 }; equipment.Add(rows);
        void AddRow(DeliveryEquipment item)
        {
            var row = new VerticalStackLayout { Spacing = 10 };
            var card = new Border { Content = row, Stroke = Color.FromArgb("#D9E6F5"), StrokeShape = new RoundRectangle { CornerRadius = 12 }, Padding = 12 };
            Field(row, "Equipo", item.Name, v => item.Name = v);
            var quantity = new Entry { Text = item.Quantity.ToString(), Keyboard = Keyboard.Numeric, MaxLength = 5 };
            quantity.TextChanged += (_, e) => item.Quantity = int.TryParse(e.NewTextValue, out var n) ? n : 0;
            row.Add(new Label { Text = "Cantidad", FontSize = 12 }); row.Add(quantity);
            Field(row, "Marca", item.Brand, v => item.Brand = v); Field(row, "Modelo", item.Model, v => item.Model = v);
            Field(row, "Número de serie", item.Serial, v => item.Serial = v);
            Select(row, "Modalidad", OrderDeliveryData.Modes, Array.IndexOf(OrderDeliveryData.Modes, item.Mode), i => item.Mode = OrderDeliveryData.Modes[i]);
            Select(row, "Estado físico", OrderDeliveryData.Conditions, Array.IndexOf(OrderDeliveryData.Conditions, item.Condition), i => item.Condition = OrderDeliveryData.Conditions[i]);
            Field(row, "Accesorios / observaciones", item.Accessories, v => item.Accessories = v, 400);
            var remove = new Button { Text = "Quitar equipo", BackgroundColor = Color.FromArgb("#FFF0F2"), TextColor = Color.FromArgb("#A5243A") };
            remove.Clicked += (_, _) => { data.Items.Remove(item); rows.Remove(card); };
            row.Add(remove); rows.Add(card);
        }
        foreach(var item in data.Items) AddRow(item);
        var add = new Button { Text = "+ Agregar equipo", BackgroundColor = Color.FromArgb("#E7F1FF"), TextColor = Color.FromArgb("#1764D9") };
        add.Clicked += async (_, _) => { if(data.Items.Count >= 20) { await DisplayAlertAsync("Equipos", "Máximo 20 equipos adicionales.", "Aceptar"); return; } var item = new DeliveryEquipment(); data.Items.Add(item); AddRow(item); };
        equipment.Add(add);
        var notes = Section("04 · Observaciones");
        var editor = new Editor { Text = data.Notes, MaxLength = 2000, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 90, Placeholder = "Condiciones e indicaciones para el cliente. Sin contraseñas." };
        editor.TextChanged += (_, e) => data.Notes = e.NewTextValue ?? ""; notes.Add(editor);
        if (!response.CanEdit) return;
        var save = new Button { Text = "Guardar entrega y equipos", BackgroundColor = Color.FromArgb("#1764D9"), TextColor = Colors.White, CornerRadius = 12 };
        save.Clicked += async (_, _) => {
            var error = data.Validate();
            if (error != null) { await DisplayAlertAsync("Revisa la entrega", error, "Aceptar"); return; }
            save.IsEnabled = false;
            try { await _orders.SaveDeliveryAsync(_id, data, response.Expected); _saved(); await DisplayAlertAsync("Entrega", "Datos guardados. Ya puedes capturar las firmas de la orden.", "Aceptar"); await Navigation.PopAsync(); }
            catch(Exception ex) { await DisplayAlertAsync("No se pudo guardar", ex.Message, "Aceptar"); }
            finally { save.IsEnabled = true; }
        };
        _root.Add(save);
    }
    private VerticalStackLayout Section(string title)
    {
        var stack = new VerticalStackLayout { Spacing = 12, IsEnabled = _response!.CanEdit };
        stack.Add(new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#173B5D") });
        _root.Add(new Border { Content = stack, Padding = 18, BackgroundColor = Colors.White, Stroke = Color.FromArgb("#D9E6F5"), StrokeShape = new RoundRectangle { CornerRadius = 18 } });
        return stack;
    }
    private static void Field(VerticalStackLayout parent, string label, string value, Action<string> changed, int length = 120)
    {
        parent.Add(new Label { Text = label, FontSize = 12, TextColor = Color.FromArgb("#627793") });
        var entry = new Entry { Text = value, MaxLength = length }; entry.TextChanged += (_, e) => changed(e.NewTextValue ?? ""); parent.Add(entry);
    }
    private static void Select(VerticalStackLayout parent, string label, string[] values, int index, Action<int> changed)
    {
        parent.Add(new Label { Text = label, FontSize = 12, TextColor = Color.FromArgb("#627793") });
        var picker = new Picker { ItemsSource = values, SelectedIndex = index }; picker.SelectedIndexChanged += (_, _) => { if(picker.SelectedIndex >= 0) changed(picker.SelectedIndex); }; parent.Add(picker);
    }
}
