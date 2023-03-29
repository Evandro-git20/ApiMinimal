using ApiMinimal.Data;
using ApiMinimal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MiniValidation;
using NetDevPack.Identity;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;

var builder = WebApplication.CreateBuilder(args);

#region Configure Services

var myConnection = builder.Configuration.GetConnectionString("DevConnection");
builder.Services.AddDbContextPool<EmployeeContext>(options =>
    options.UseMySql(myConnection, ServerVersion.AutoDetect(myConnection)));

builder.Services.AddIdentityEntityFrameworkContextConfiguration(options =>
    options.UseMySql(myConnection, ServerVersion.AutoDetect(myConnection),
    b => b.MigrationsAssembly("ApiMinimal")));

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppJwtSettings");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ExcluirPedido", policy => policy.RequireClaim("ExcluirPedido"));
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Minimal API Sample",
        Description = "Developed by Evandro O Ribeiro",
        Contact = new OpenApiContact { Name = "Evandro Ribeiro", Email = "evandroamado@gmail.com" },
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o token JWT desta maneira: Bearer {seu token}",
        Name = "Authorization",
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

#endregion

#region Configure Pipeline

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();
app.UseHttpsRedirection();

MapActions(app);

app.Run();

#endregion

#region Actions

void MapActions(WebApplication app)
{
    app.MapGet("/", () => "Hello World!").ExcludeFromDescription();

    app.MapPost("/registro", [AllowAnonymous] async (SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager,
    IOptions<AppJwtSettings> appJwtSettings, RegisterUser registerUser) =>
    {
        if (registerUser == null)
            return Results.BadRequest("Usuário não informado.");

        if (!MiniValidator.TryValidate(registerUser, out var errors))
            return Results.ValidationProblem(errors);

        var user = new IdentityUser
        {
            UserName = registerUser.Email,
            Email = registerUser.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, registerUser.Password);
        if (!result.Succeeded)
            return Results.BadRequest(result.Errors);

        var jwt = new JwtBuilder()
            .WithUserManager(userManager)
            .WithJwtSettings(appJwtSettings.Value)
            .WithEmail(user.Email)
            .WithJwtClaims()
            .WithUserClaims()
            .WithUserRoles()
            .BuildUserResponse();
        return Results.Ok(jwt);
    })
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("PostUsuario")
    .WithTags("Usuario");

    app.MapPost("/login", [AllowAnonymous] async (SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager,
        IOptions<AppJwtSettings> appJwtSettings, LoginUser loginUser) =>
    {
        if (loginUser == null)
            return Results.BadRequest("Usuário não informado.");

        if (!MiniValidator.TryValidate(loginUser, out var errors))
            return Results.ValidationProblem(errors);

        var result = await signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password, false, true);
        if (result.IsLockedOut)
            return Results.BadRequest("Usuário bloqueado.");
        if (!result.Succeeded)
            return Results.BadRequest("Usuário ou senha incorretos.");

        var jwt = new JwtBuilder()
            .WithUserManager(userManager)
            .WithJwtSettings(appJwtSettings.Value)
            .WithEmail(loginUser.Email)
            .WithJwtClaims()
            .WithUserClaims()
            .WithUserRoles()
            .BuildUserResponse();
        return Results.Ok(jwt);
    })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PostLogin")
        .WithTags("Usuario");

    app.MapGet("/cliente", [AllowAnonymous] async (EmployeeContext context) =>
    await context.Clientes.ToListAsync())
        .WithName("GetCliente")
        .WithTags("Cliente");

    app.MapGet("/cliente/{id}", [AllowAnonymous] async (Guid id, EmployeeContext context) =>
    await context.Clientes.FindAsync(id)
        is ClienteEntity cliente ? Results.Ok(cliente) : Results.NotFound())
        .Produces<ClienteEntity>(StatusCodes.Status200OK)
        .Produces<ClienteEntity>(StatusCodes.Status404NotFound)
        .WithName("GetClienteId")
        .WithTags("Cliente");

    app.MapPost("/cliente", [Authorize] async (EmployeeContext context, ClienteEntity cliente) =>
    {
        if (!MiniValidator.TryValidate(cliente, out var errors))
            return Results.ValidationProblem(errors);
        context.Clientes.Add(cliente);
        var result = await context.SaveChangesAsync();

        return result > 0
        ? Results.Created($"/cliente/{cliente.Id}", cliente)
        //? Results.CreatedAtRoute("GetClienteId", new {id = cliente.Id}) 
        : Results.BadRequest("Houve um erro ao salvar cliente");
    })
        .ProducesValidationProblem()
        .Produces<ClienteEntity>(StatusCodes.Status201Created)
        .Produces<ClienteEntity>(StatusCodes.Status400BadRequest)
        .WithDisplayName("PostCliente")
        .WithTags("Cliente");

    app.MapPut("/cliente/{id}", [Authorize] async (Guid id, EmployeeContext context, ClienteEntity cliente) =>
    {
        var clienteBase = await context.Clientes.AsNoTracking<ClienteEntity>().FirstOrDefaultAsync(fornec => fornec.Id == id);
        if (clienteBase == null)
            return Results.NotFound();
        if (!MiniValidator.TryValidate(cliente, out var errors))
            return Results.ValidationProblem(errors);
        context.Clientes.Update(cliente);
        var result = await context.SaveChangesAsync();
        return result > 0
        ? Results.NoContent() : Results.BadRequest("Houve um erro ao atualizar cliente.");
    })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PutCliente")
        .WithTags("Cliente");

    app.MapDelete("/cliente/{id}", [Authorize] async (Guid id, EmployeeContext context) =>
    {
        var contextBase = await context.Clientes.FindAsync(id);
        if (contextBase == null)
            return Results.NotFound();
        context.Clientes.Remove(contextBase);
        var result = await context.SaveChangesAsync();
        return result > 0
        ? Results.NoContent() : Results.BadRequest("Houve um erro, não foi possível remover cliente.");
    })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireAuthorization("ExcluirPedido")
        .WithName("DeleteCliente")
        .WithTags("Cliente");
}
#endregion