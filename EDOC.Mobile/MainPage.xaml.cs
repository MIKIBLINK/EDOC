using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;

namespace EDOC.Mobile;

public partial class MainPage : ContentPage
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _baseUrl = DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5245" : "http://localhost:5245";
    private string? _token;
    private int? _doctorId;
    private string _doctorName = "";
    private List<Slot> _slots = [];

    public MainPage()
    {
        InitializeComponent();
        AppointmentDate.MinimumDate = DateTime.Today;
        AppointmentDate.Date = DateTime.Today.AddDays(1);
        _ = LoadDoctorsAsync();
        _ = RestoreSessionAsync();
    }

    private async Task RestoreSessionAsync()
    {
        try { _token = await SecureStorage.Default.GetAsync("access_token"); if (_token is not null) { AuthStatus.Text = "Secure session restored."; await LoadAppointmentsAsync(); } }
        catch { AuthStatus.Text = "Please sign in to continue."; }
    }

    private async Task<HttpRequestMessage> Request(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, new Uri(new Uri(_baseUrl), path));
        if (!string.IsNullOrWhiteSpace(_token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return req;
    }
    private async Task<string> ApiError(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error)) return error.GetString() ?? response.ReasonPhrase ?? "Request failed";
            if (json.RootElement.TryGetProperty("title", out var title)) return title.GetString() ?? "Request failed";
        }
        catch { }
        return string.IsNullOrWhiteSpace(body) ? $"Request failed ({(int)response.StatusCode})" : body;
    }
    private async Task AuthenticateAsync(bool register)
    {
        if (string.IsNullOrWhiteSpace(EmailEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text)) { AuthStatus.Text = "Enter your email and password."; return; }
        try
        {
            var path = register ? "/api/auth/register" : "/api/auth/login";
            using var req = await Request(HttpMethod.Post, path);
            req.Content = JsonContent.Create(register ? (object)new { email = EmailEntry.Text, password = PasswordEntry.Text, displayName = NameEntry.Text } : new { email = EmailEntry.Text, password = PasswordEntry.Text }, options: JsonOptions);
            using var response = await Http.SendAsync(req);
            if (!response.IsSuccessStatusCode) { AuthStatus.Text = await ApiError(response); return; }
            var result = await response.Content.ReadFromJsonAsync<AuthResult>(JsonOptions);
            if (result is null) { AuthStatus.Text = "The API returned an empty response."; return; }
            _token = result.AccessToken; await SecureStorage.Default.SetAsync("access_token", _token);
            AuthStatus.Text = $"Signed in as {result.DisplayName}.";
            await LoadAppointmentsAsync();
        }
        catch (Exception ex) { AuthStatus.Text = $"Can't reach E-DOC API at {_baseUrl}: {ex.Message}"; }
    }
    private async void LoginClicked(object? sender, EventArgs e) => await AuthenticateAsync(false);
    private async void RegisterClicked(object? sender, EventArgs e) => await AuthenticateAsync(true);

    private async Task LoadDoctorsAsync()
    {
        try
        {
            using var req = await Request(HttpMethod.Get, "/api/doctors?page=1&pageSize=30");
            using var response = await Http.SendAsync(req);
            if (!response.IsSuccessStatusCode) { BookingStatus.Text = await ApiError(response); return; }
            var result = await response.Content.ReadFromJsonAsync<DoctorPage>(JsonOptions);
            DoctorsList.ItemsSource = result?.Items ?? [];
            if (result?.Items?.Count == 0) BookingStatus.Text = "No doctors have been added yet. Ask an administrator to configure doctor profiles and schedules.";
        }
        catch (Exception ex) { BookingStatus.Text = $"API unavailable: {ex.Message}"; }
    }
    private async void SearchClicked(object? sender, EventArgs e)
    {
        try
        {
            var query = Uri.EscapeDataString(SearchEntry.Text?.Trim() ?? "");
            using var req = await Request(HttpMethod.Get, $"/api/doctors?q={query}&page=1&pageSize=30");
            using var response = await Http.SendAsync(req);
            if (!response.IsSuccessStatusCode) { BookingStatus.Text = await ApiError(response); return; }
            var result = await response.Content.ReadFromJsonAsync<DoctorPage>(JsonOptions); DoctorsList.ItemsSource = result?.Items ?? [];
        }
        catch (Exception ex) { BookingStatus.Text = $"Search failed: {ex.Message}"; }
    }
    private void DoctorSelected(object? sender, SelectionChangedEventArgs e) { if (e.CurrentSelection.FirstOrDefault() is Doctor doctor) _ = LoadSlotsAsync(doctor); }
    private async void SlotsClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: int id } button && button.BindingContext is Doctor doctor) await LoadSlotsAsync(doctor);
    }
    private async Task LoadSlotsAsync(Doctor doctor)
    {
        _doctorId = doctor.Id; _doctorName = doctor.Name; DoctorChoice.Text = $"{doctor.Name} · {doctor.Specialty}";
        try
        {
            var date = AppointmentDate.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            using var req = await Request(HttpMethod.Get, $"/api/doctors/{doctor.Id}/slots?date={date}");
            using var response = await Http.SendAsync(req);
            if (!response.IsSuccessStatusCode) { BookingStatus.Text = await ApiError(response); return; }
            _slots = await response.Content.ReadFromJsonAsync<List<Slot>>(JsonOptions) ?? [];
            SlotsPicker.ItemsSource = _slots.Select(s => s.StartsAtUtc.ToLocalTime().ToString("HH:mm")).ToList();
            BookingStatus.Text = _slots.Count == 0 ? "No available slots for this date." : $"{_slots.Count} available time(s).";
        }
        catch (Exception ex) { BookingStatus.Text = $"Could not load availability: {ex.Message}"; }
    }
    private async void BookClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_token)) { BookingStatus.Text = "Sign in with a patient account to book."; return; }
        if (_doctorId is null || SlotsPicker.SelectedIndex < 0) { BookingStatus.Text = "Choose a doctor and an available time."; return; }
        if (string.IsNullOrWhiteSpace(ReasonEntry.Text)) { BookingStatus.Text = "Add a short reason for the visit."; return; }
        try
        {
            using var req = await Request(HttpMethod.Post, "/api/appointments");
            req.Content = JsonContent.Create(new { doctorProfileId = _doctorId, startsAtUtc = _slots[SlotsPicker.SelectedIndex].StartsAtUtc, visitReason = ReasonEntry.Text, idempotencyKey = Guid.NewGuid().ToString("N") }, options: JsonOptions);
            using var response = await Http.SendAsync(req);
            if (!response.IsSuccessStatusCode) { BookingStatus.Text = await ApiError(response); return; }
            var booked = await response.Content.ReadFromJsonAsync<Appointment>(JsonOptions);
            BookingStatus.Text = booked is null ? "Appointment booked." : $"Request sent. Reference: {booked.BookingReference}";
            await LoadAppointmentsAsync(); await LoadSlotsAsync(new Doctor(_doctorId.Value, _doctorName, "", "", 0, null, 30));
        }
        catch (Exception ex) { BookingStatus.Text = $"Booking failed: {ex.Message}"; }
    }
    private async Task LoadAppointmentsAsync()
    {
        if (string.IsNullOrWhiteSpace(_token)) return;
        try
        {
            using var req = await Request(HttpMethod.Get, "/api/appointments"); using var response = await Http.SendAsync(req);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) { SecureStorage.Default.Remove("access_token"); _token = null; AuthStatus.Text = "Your session ended. Please sign in again."; return; }
            if (response.IsSuccessStatusCode) AppointmentsList.ItemsSource = await response.Content.ReadFromJsonAsync<List<Appointment>>(JsonOptions) ?? [];
            else BookingStatus.Text = await ApiError(response);
        }
        catch (Exception ex) { BookingStatus.Text = $"Could not load appointments: {ex.Message}"; }
    }
    private async void RefreshAppointmentsClicked(object? sender, EventArgs e) => await LoadAppointmentsAsync();
    private async void EmergencyClicked(object? sender, EventArgs e) => await DisplayAlertAsync("Emergency", "For urgent help, use your local emergency service. Official emergency contacts are not configured in this development installation.", "OK");

    public sealed record AuthResult(string AccessToken, string UserId, string DisplayName, string[] Roles);
    public sealed record Doctor(int Id, string Name, string Specialty, string Qualifications, decimal Fee, string? Hospital, int AppointmentDurationMinutes);
    public sealed record Slot(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);
    public sealed record DoctorPage(List<Doctor> Items, int Page, int PageSize, int Total);
    public sealed record Appointment(long Id, Guid BookingReference, int DoctorProfileId, string DoctorName, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string VisitReason, string Status);
}
