using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

var factory = new ChuckNorrisJokeFactory();
var context = factory.CreateDbContext();

List<ChuckNorrisJoke> jokes = await ProcessCommandArgs(args, context);
if (jokes == null) return;

var dbJokes = await context.Jokes.AsNoTracking().ToListAsync();

var allJokesAfterInsert = await ValidateJokesAgainstDB(dbJokes, jokes);

context.Jokes.AddRange(jokes);
await context.SaveChangesAsync();

Console.WriteLine($"{jokes.Count} joke(s) inserted");

if (allJokesAfterInsert)
{
    Console.WriteLine("You got all jokes in your database");
}

async Task<List<ChuckNorrisJoke>> ProcessCommandArgs(string[] args, ChuckNorrisJokeContext context)
{
    if (args.Length > 0)
    {
        if (args[0].ToLower() is "clear")
        {
            await context.Database.ExecuteSqlRawAsync("DELETE from Jokes");
            Console.WriteLine("All jokes have been removed from the db.");
            return null;
        }

        return await RetrieveJokesAsync(int.Parse(args[0]));
    }
    else
    {
        return await RetrieveJokesAsync();
    }
}

async Task<bool> ValidateJokesAgainstDB(List<ChuckNorrisJoke> dbJokes, List<ChuckNorrisJoke> localJokes)
{
    var retryCount = 0;

    foreach(var joke in localJokes)
    {
        if (dbJokes.Contains(joke) && retryCount < 10)
        {
            localJokes.Remove(joke);
            localJokes.Add((await RetrieveJokesAsync(1))[0]);
            retryCount++;
        } else if(retryCount >= 10)
        {
            return true;
        }
    }

    return false;
}

async Task<List<ChuckNorrisJoke>> RetrieveJokesAsync(int count = 5)
{
    if (count > 10)
    { 
        Console.WriteLine("Requesting too many jokes at once"); 
        return null;
    }

    using HttpClient httpClient = new HttpClient();

    try
    {
        List<ChuckNorrisJoke> retrievedJokes = new List<ChuckNorrisJoke>();
        HttpResponseMessage response;
        string responseBody;
        ChuckNorrisJoke deserializedJoke;

        while (count-- != 0)
        {
            response = await httpClient.GetAsync("https://api.chucknorris.io/jokes/random?category!%3Dexplicit");
            response.EnsureSuccessStatusCode();
            responseBody = await response.Content.ReadAsStringAsync();

            deserializedJoke = JsonSerializer.Deserialize<ChuckNorrisJoke>(responseBody)!;

            if (!retrievedJokes.Contains(deserializedJoke))
            {
                retrievedJokes.Add(deserializedJoke);
            }
            else count++;
        }

        return retrievedJokes;
    }
    catch (HttpRequestException e)
    {
        Console.WriteLine(e.Message);
    }
    return null;
}


// DB & JsonSerializer Configurations
class ChuckNorrisJoke
{
    public int Id { get; set; }

    [JsonPropertyName("id")]
    [MaxLength(40)]
    public string ChuckNorrisId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    [MaxLength(1024)]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Joke { get; set; } = string.Empty;
}

class ChuckNorrisJokeFactory : IDesignTimeDbContextFactory<ChuckNorrisJokeContext>
{
    public ChuckNorrisJokeContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<ChuckNorrisJokeContext>();
        optionsBuilder.UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new ChuckNorrisJokeContext(optionsBuilder.Options);
    }
}

class ChuckNorrisJokeContext : DbContext
{
    public DbSet<ChuckNorrisJoke> Jokes { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public ChuckNorrisJokeContext(DbContextOptions<ChuckNorrisJokeContext> options) : base(options)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {

    }
}