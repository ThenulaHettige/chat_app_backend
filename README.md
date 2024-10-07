Adding SinnalR

dotnet add package Microsoft.AspNetCore.SignalR

Run the Application

dotnet run

MySQL Workbench Connection
Ensure you have a working connection to your MySQL database in MySQL Workbench

Program.cs: {

builder.Services.AddScoped<MySqlConnection>(_ => new MySqlConnection("Server=localhost;Database=chat_db;User=root;Password=Lalani@1970;"));
Repalce it with 
builder.Services.AddScoped<MySqlConnection>(_ => new MySqlConnection("Server=localhost;Database=chat_db;User=root;Password="YourPassword";"));

make sure to configure

builder.Services.AddCors(opt =>
{   
    opt.AddPolicy("reactApp", builder =>
    {
        builder.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


Mapping the SignalR Hub
app.MapHub<ChatHub>("/chat");

}
