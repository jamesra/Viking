using Microsoft.AspNetCore.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OData setup (example, update with your actual EDM model)
builder.Services.AddControllers().AddOData(opt =>
    opt.AddRouteComponents("odata", GetEdmModel()).Select().Filter().OrderBy().Expand().Count().SetMaxTop(100));

// TODO: Add your DbContext and other services here

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();

IEdmModel GetEdmModel()
{
    var builder = new ODataConventionModelBuilder();
    // TODO: Add your entity sets, e.g.:
    // builder.EntitySet<YourEntity>("YourEntities");
    return builder.GetEdmModel();
} 