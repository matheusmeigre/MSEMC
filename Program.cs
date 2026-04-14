using FluentValidation;
using MSEMC.Abstractions;
using MSEMC.Configuration;
using MSEMC.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration: Options Pattern with validation at startup ──
builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration(SmtpOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── Dependency Injection ──
builder.Services.AddScoped<IEmailSender, EmailService>();

// ── Validation: FluentValidation auto-discovery ──
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── API: Controllers + Swagger ──
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Pipeline ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
