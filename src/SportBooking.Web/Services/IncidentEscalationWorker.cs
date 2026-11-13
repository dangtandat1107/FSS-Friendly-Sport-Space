namespace SportBooking.Web.Services;

// Tiến trình nền chạy cùng ứng dụng: mỗi 30 giây quét các sự cố quá hạn tiếp nhận và leo thang lên Admin.
// Dùng BackgroundService có sẵn của .NET nên không cần thêm thư viện.
public class IncidentEscalationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<IncidentEscalationWorker> _log;

    public IncidentEscalationWorker(IServiceScopeFactory scopes, ILogger<IncidentEscalationWorker> log)
    { _scopes = scopes; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();   // IncidentService dùng DbContext theo từng request nên cần scope riêng
                    var count = await scope.ServiceProvider.GetRequiredService<IIncidentService>().EscalateOverdueAsync();
                    if (count > 0) _log.LogWarning("Đã leo thang {Count} sự cố quá hạn lên Admin", count);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Lỗi khi quét sự cố quá hạn");   // một lần lỗi không làm dừng tiến trình
                }
            }
        }
        catch (OperationCanceledException) { /* ứng dụng đang tắt */ }
    }
}
