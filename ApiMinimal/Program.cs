using ApiMinimal.Data;
using ApiMinimal.Models;
using Microsoft.EntityFrameworkCore;
using MiniValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var myConnection = builder.Configuration.GetConnectionString("DevConnection");
builder.Services.AddDbContextPool<EmployeeContext>(options =>
    options.UseMySql(myConnection, ServerVersion.AutoDetect(myConnection)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/cliente", async (EmployeeContext context) => 
await context.Clientes.ToListAsync())
    .WithName("GetCliente")
    .WithTags("Cliente");

app.MapGet("/cliente/{id}", async (Guid id, EmployeeContext context) => 
await context.Clientes.FindAsync(id)
    is ClienteEntity cliente ? Results.Ok(cliente) : Results.NotFound())
    .Produces<ClienteEntity>(StatusCodes.Status200OK)
    .Produces<ClienteEntity>(StatusCodes.Status404NotFound)
    .WithName("GetClienteId")
    .WithTags("Cliente");

app.MapPost("/cliente", async (EmployeeContext context, ClienteEntity cliente) =>
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

app.Run();
