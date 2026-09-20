using System.Net;
using System.Net.Mail;

namespace RotiseriaAPI.Services;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string resetLink, string businessName);
    Task SendWelcomeAsync(string toEmail, string fullName, string businessName);
    Task SendStaffInviteAsync(string toEmail, string fullName, string role, string temporaryPassword, string businessName);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetLink, string businessName)
    {
        var subject = $"Restablecer contraseña - {businessName}";
        var body = $"""
            Hola,

            Recibimos una solicitud para restablecer tu contraseña en {businessName}.
            Haz clic en el siguiente enlace para continuar:
            {resetLink}

            Este enlace expirará en 1 hora. Si no solicitaste el restablecimiento, ignora este mensaje.

            Saludos,
            Equipo de {businessName}
            """;

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendWelcomeAsync(string toEmail, string fullName, string businessName)
    {
        var subject = $"¡Bienvenido a {businessName}!";
        var body = $"""
            Hola {fullName},

            Tu cuenta para {businessName} ha sido creada exitosamente. Ya puedes ingresar al sistema y comenzar a gestionar pedidos, mesas y catálogo.

            Saludos cordiales,
            Equipo Gastronómico SaaS
            """;

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendStaffInviteAsync(string toEmail, string fullName, string role, string temporaryPassword, string businessName)
    {
        var subject = $"Invitación a colaborar en {businessName}";
        var body = $"""
            Hola {fullName},

            Has sido dado de alta como {role} en {businessName}.
            Tus credenciales de acceso son:
            Email: {toEmail}
            Contraseña inicial: {temporaryPassword}

            Te recomendamos cambiar tu contraseña una vez que inicies sesión.

            Saludos,
            {businessName}
            """;

        await SendEmailAsync(toEmail, subject, body);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var smtpHost = _config["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation("================== [DEV TRANSACTIONAL EMAIL] ==================");
            _logger.LogInformation("To: {ToEmail}", toEmail);
            _logger.LogInformation("Subject: {Subject}", subject);
            _logger.LogInformation("Body:\n{Body}", body);
            _logger.LogInformation("===============================================================");
            return;
        }

        try
        {
            var port = int.TryParse(_config["Email:Smtp:Port"], out var p) ? p : 587;
            var user = _config["Email:Smtp:User"];
            var pass = _config["Email:Smtp:Password"];
            var from = _config["Email:Smtp:From"] ?? "no-reply@gastronomico.com";
            var enableSsl = bool.TryParse(_config["Email:Smtp:EnableSsl"], out var ssl) ? ssl : true;

            using var client = new SmtpClient(smtpHost, port)
            {
                EnableSsl = enableSsl,
                Credentials = string.IsNullOrWhiteSpace(user) ? null : new NetworkCredential(user, pass)
            };

            using var message = new MailMessage(from, toEmail, subject, body);
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando correo a {ToEmail}", toEmail);
        }
    }
}
