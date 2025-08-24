
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
// Exception handling middleware (first)
app.Use(async (context, next) =>
{
	try
	{
		await next();
	}
	catch (Exception ex)
	{
		context.Response.StatusCode = 500;
		context.Response.ContentType = "application/json";
		var error = new { error = "Internal server error." };
		var json = System.Text.Json.JsonSerializer.Serialize(error);
		await context.Response.WriteAsync(json);
		// Optionally log the exception
		Console.WriteLine($"Exception: {ex.Message}");
	}
});

// Token validation middleware (second)
app.Use(async (context, next) =>
{
	// Example: Expect token in Authorization header as 'Bearer <token>'
	var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
	string? token = null;
	if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
		token = authHeader.Substring("Bearer ".Length).Trim();

	if (!IsValidToken(token))
	{
		context.Response.StatusCode = 401;
		context.Response.ContentType = "application/json";
		var error = new { error = "Unauthorized" };
		var json = System.Text.Json.JsonSerializer.Serialize(error);
		await context.Response.WriteAsync(json);
		return;
	}
	await next();
});

// Simple token validation (replace with real logic)
bool IsValidToken(string? token)
{
	// For demo: accept token "valid-token" only
	return token == "valid-token";
}

// Logging middleware (last)
app.Use(async (context, next) =>
{
	var method = context.Request.Method;
	var path = context.Request.Path;
	await next();
	var statusCode = context.Response.StatusCode;
	Console.WriteLine($"{method} {path} => {statusCode}");
});

// In-memory user store
var users = new ConcurrentDictionary<int, User>();
var nextId = 1;

// GET: Retrieve all users
app.MapGet("/users", () => users.Values.ToList());

// GET: Retrieve a specific user by ID
app.MapGet("/users/{id:int}", (int id) =>
	users.TryGetValue(id, out var user) ? Results.Ok(user) : Results.NotFound());

// POST: Add a new user
app.MapPost("/users", (User user) =>
{
	var errors = ValidateUser(user);
	if (errors.Count > 0)
		return Results.BadRequest(errors);
	user.Id = nextId++;
	users[user.Id] = user;
	return Results.Created($"/users/{user.Id}", user);
});

// PUT: Update an existing user's details
app.MapPut("/users/{id:int}", (int id, User updatedUser) =>
{
	if (!users.ContainsKey(id)) return Results.NotFound();
	var errors = ValidateUser(updatedUser);
	if (errors.Count > 0)
		return Results.BadRequest(errors);
	updatedUser.Id = id;
	users[id] = updatedUser;
	return Results.Ok(updatedUser);
});

// DELETE: Remove a user by ID
app.MapDelete("/users/{id:int}", (int id) =>
{
	return users.TryRemove(id, out var removed) ? Results.Ok(removed) : Results.NotFound();
});


// Simple user validation
List<string> ValidateUser(User user)
{
	var errors = new List<string>();
	if (string.IsNullOrWhiteSpace(user.Name))
		errors.Add("Name is required.");
	if (string.IsNullOrWhiteSpace(user.Email))
		errors.Add("Email is required.");
	else if (!user.Email.Contains("@"))
		errors.Add("Email must be valid.");
	return errors;
}

app.Run();

// User model
public class User
{
	public int Id { get; set; }
	public string? Name { get; set; }
	public string? Email { get; set; }
}
