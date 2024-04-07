using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Host.UseSerilog((_, services, config) => config
        .ReadFrom.Services(services)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    );
    
    // configure the cors policy for development
    // builder.Services.AddCors(cfg => {
    //     cfg.AddDefaultPolicy(plc => plc
    //         .WithOrigins(builder.Configuration.GetValue<string>("AllowedHosts")!.Split("|"))
    //         .AllowAnyHeader()
    //         .AllowAnyMethod()
    //     );
    // });
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("customPolicy", b =>
        {
            b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        });
    });
    
    // configure our authentication with clerk
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(cfg => {
            // Authority is the URL of your clerk instance
            cfg.Authority = builder.Configuration["Clerk:Authority"];
            cfg.TokenValidationParameters = new TokenValidationParameters {
                // Disable audience validation as we aren't using it
                ValidateAudience = false,
                NameClaimType = ClaimTypes.NameIdentifier 
            };
            cfg.Events = new JwtBearerEvents() {
                OnTokenValidated = context => {
                    var azp = context.Principal?.FindFirstValue("azp");
                    // AuthorizedParty is the base URL of your frontend.
                    if (string.IsNullOrEmpty(azp) || !azp.Equals(builder.Configuration["Clerk:AuthorizedParty"])) {
                        context.Fail("AZP Claim is invalid or missing");
                    }
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(JwtBearerDefaults.AuthenticationScheme, p => p.RequireAuthenticatedUser())
        .SetFallbackPolicy(null);
    
    // configure the reverse proxy for our gateway to our microservices
    builder.Services
        .AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
}

var app = builder.Build();
{
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.MapReverseProxy();
}

app.Run();