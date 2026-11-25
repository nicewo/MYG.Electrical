namespace MYG.Electrical.Infrastructure.Serialization;

/// <summary>Proje dosyası okunamadığında kullanıcıya gösterilebilir mesajla fırlatılır.</summary>
public sealed class ProjectFormatException(string message, Exception? inner = null) : Exception(message, inner);
