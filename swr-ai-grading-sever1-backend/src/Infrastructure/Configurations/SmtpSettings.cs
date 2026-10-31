namespace Infrastructure.Configurations;

public sealed class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "SWR Grading System";
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}

