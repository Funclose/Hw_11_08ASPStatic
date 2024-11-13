
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var configurationService = app.Services.GetService<IConfiguration>();
string connectionString = configurationService["ConnectionStrings:DefaultConnection"];

app.Run(async (context) =>
{
    var response = context.Response;
    var request = context.Request;
    response.ContentType = "text/html; charset=utf-8";

    //При переходе на главную страницу, считываем всех пользователей
    if (request.Path == "/") //  "/" главная страница
    {
        List<User> users = new List<User>();
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            SqlCommand command = new SqlCommand("select Id, Name, Age from Users", connection);
            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                if (reader.HasRows)
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add(new User(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
                    }
                }
            }
        }
        await response.WriteAsync(GenerateHtmlPage(BuildHtmlTable(users), "All Users from DataBase"));
    }
    else if (request.Path == "/Html/addUsers.html")
    {
        await context.Response.SendFileAsync("Html/addUsers.html");
    }
    else if (request.Path == "/addUsers" && request.Method == "POST")
    {
        var name = request.Form["name"].ToString();
        var age = int.TryParse(request.Form["age"].FirstOrDefault(), out int ageForm) ? ageForm : 0;
        if (!string.IsNullOrEmpty(name) && age > 0)
        {
            using (SqlConnection connect = new SqlConnection(connectionString))
            {
                await connect.OpenAsync();
                SqlCommand sqlCommand = new SqlCommand("Insert into Users (Name, Age) Values (@name, @age) ", connect);
                sqlCommand.Parameters.AddWithValue("@name", name);
                sqlCommand.Parameters.AddWithValue("@age", age);
                await sqlCommand.ExecuteNonQueryAsync(); //после успешного запроса добавит данные, + нельзя добавлять коллекции данных

            }
            response.Redirect("/");
        }
        else
        {
            await response.WriteAsync("error on addUsers");
        }
    }

    else if (request.Path == "/delete" && request.Method == "GET")
    {
        var id = request.Query["id"];
        if (int.TryParse(id, out int userId))
        {
            using (SqlConnection connect = new SqlConnection(connectionString))
            {
                await connect.OpenAsync();
                SqlCommand cmd = new SqlCommand("Delete from Users Where Id = @id", connect);
                cmd.Parameters.AddWithValue("@id", userId);
                await cmd.ExecuteNonQueryAsync();
            }
            response.Redirect("/");
        }
        else
        {
            response.StatusCode = 400;
            await response.WriteAsync("bad request delete");
        }
    }
    //////////////////////
    else if(request.Path == "/edit" && request.Method == "GET")
    {
        await response.SendFileAsync("Html/updateUser.html");
    }
   
    else if (request.Path == "/updateUser" && request.Method == "POST")
    {
        var Id = request.Form["id"];
        //var Id = request.Query["id"];
        var newName = request.Form["name"].ToString();
        var newAgestr = request.Form["age"];

        if (int.TryParse(Id, out int userId) && int.TryParse(newAgestr, out int newAge) && !string.IsNullOrEmpty(newName))
        {
            using (SqlConnection connect = new SqlConnection(connectionString))
            {
                await connect.OpenAsync();
                SqlCommand cmd = new SqlCommand("Update Users Set Name = @name, Age = @age Where Id = @id");
                cmd.Parameters.AddWithValue("@id", userId);
                cmd.Parameters.AddWithValue("@name", newName);
                cmd.Parameters.AddWithValue("@age", newAge);
                await cmd.ExecuteNonQueryAsync();
            }
            response.Redirect("/");
        }
        else
        {
            response.StatusCode = 400;
            await response.WriteAsync("bad request");
        }
    }
    ///////////////////////



    else
    {
        response.StatusCode = 404;
        await response.WriteAsJsonAsync("Page Not Found");
    }
});
  

app.Run();


static string BuildHtmlTable<T>(IEnumerable<T> collection)
{
    StringBuilder tableHtml = new StringBuilder();
    tableHtml.Append("<table class=\"table\">");

    PropertyInfo[] properties = typeof(T).GetProperties();

    tableHtml.Append("<tr>");
    foreach (PropertyInfo property in properties)
    {
        tableHtml.Append($"<th>{property.Name}</th>");
    }
    tableHtml.Append("<th> Action </th>");

    tableHtml.Append("</tr>");
    foreach (T item in collection)
    {
        tableHtml.Append("<tr>");
        foreach (PropertyInfo property in properties)
        {
            object value = property.GetValue(item);
            tableHtml.Append($"<td>{value}</td>");
        }
        var userId = item.GetType().GetProperty("Id")?.GetValue(item);
        tableHtml.Append($"<td><a href=\"/edit?id={userId}\" class=\"btn btn-warning me-2\">Edit</a><a href=\"/delete?id={userId}\" class=\"btn btn-danger\">Delete</a></td>");





        //tableHtml.Append($" <td><a href=\"/edit?id={userId}\" class=\"btn btn-warning me-2\">Eddit</a><button href=\"/delete?id={userId}\"class=\"btn btn-danger\">Delete<button><td>");
        tableHtml.Append("</tr>");
    }

    tableHtml.Append("</table>");
    return tableHtml.ToString();
}
static string GenerateHtmlPage(string body, string header)
{
    string html = $"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0-alpha3/dist/css/bootstrap.min.css" rel="stylesheet" 
            integrity="sha384-KK94CHFLLe+nY2dmCWGMq91rCGa5gtU4mk92HdvYe+M/SXH301p5ILy+dN9+nJOZ" crossorigin="anonymous">
            <title>{header}</title>
        </head>
        <body>
        <div class="container">
        <h2 class="d-flex justify-content-center">{header}</h2>
        <div class="mt-5">
        <a href="/Html/addUsers.html" class="btn btn-primary">Add User<a>
        </div>
        {body}
            <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0-alpha3/dist/js/bootstrap.bundle.min.js" 
            integrity="sha384-ENjdO4Dr2bkBIFxQpeoTz1HIcje39Wm4jDKdf19U8gI4ddQ3GYNS7NTKfAdVQSZe" crossorigin="anonymous"></script>
        </div>
        </body>
        </html>
        """;
    return html;
}


record User(int Id, string Name, int age)
{
    public User(string Name, int age) : this(0, Name, age)
    {

    }
}

