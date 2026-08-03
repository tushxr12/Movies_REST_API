using Movies.Application.Models;
using Dapper;

namespace Movies.Application.Repositories;

public class MovieRepository : IMovieRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public MovieRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(Movie movie, CancellationToken token = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        var res = await connection.ExecuteAsync(new CommandDefinition("""
            insert into movies(id, slug, title, yearofrelease)
            values(@Id, @Slug, @Title, @YearOfRelease)
        """, movie, cancellationToken: token));

        if (res > 0)
        {
            foreach (var genre in movie.Genres)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    insert into genres(movieId, name)
                    values(@MovieId, @Name)
                """, new {MovieId = movie.Id, Name = genre}, cancellationToken: token));
            }
        }
        transaction.Commit();
        return res > 0;
    }

    public async Task<Movie?> GetByIdAsync(Guid id, CancellationToken token = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var movie = await connection.QuerySingleOrDefaultAsync<Movie>(
            new CommandDefinition("""
            select * from movies where id = @id
            """, new {id}));


        if (movie is null)
            return null;

        var genres = await connection.QueryAsync<string>(
            new CommandDefinition("""
            select name from genres where movieid = @id
            """, new {id},cancellationToken: token));

        foreach (var genre in genres)
        {
            movie.Genres.Add(genre);
        }
        return movie;
    }

    public async Task<Movie?> GetBySlugAsync(string slug, CancellationToken token = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var movie = await connection.QuerySingleOrDefaultAsync<Movie>(
            new CommandDefinition("""
            select * from movies where slug = @slug
            """, new {slug}, cancellationToken: token));


        if (movie is null)
            return null;

        var genres = await connection.QueryAsync<string>(
            new CommandDefinition("""
            select name from genres where movieid = @id
            """, new {id = movie.Id}, cancellationToken: token));

        foreach (var genre in genres)
        {
            movie.Genres.Add(genre);
        }
        return movie;
    }

    public async Task<IEnumerable<Movie>> GetAllAsync(CancellationToken token = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var res = await connection.QueryAsync(new CommandDefinition("""
        select m.*, string_agg(g.name, ',') as genres
        from movies m left
        join genres g on m.id = g.movieid
        group by m.id
        """, cancellationToken: token));

        return res.Select(x => new Movie
        {
            Id = x.id,
            Title = x.title,
            YearOfRelease = x.yearofrelease,
            Genres = x.genres is null
                        ? new List<string>()
                        : ((string)x.genres).Split(',').ToList()
        });
    }

    public async Task<bool> UpdateAsync(Movie movie, CancellationToken token = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
        delete from genres where movieid = @id
        """, new {id = movie.Id}));

        foreach (var genre in movie.Genres)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
            insert into genres(movieId, name)
            values(@MovieId, @Name)
            """, new {MovieId = movie.Id, Name = genre}, cancellationToken: token));
        }

        var res = await connection.ExecuteAsync(new CommandDefinition("""
        update movies set slug = @slug, title = @Title, yearofrelease = @YearOfRelease
        where id = @Id
        """, movie, cancellationToken: token));

        transaction.Commit();
        return res > 0;
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken token = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
        delete from genres where movieid = @id
        """, new {id}, cancellationToken: token));

        var res = await connection.ExecuteAsync(new CommandDefinition("""
        delete from movies where id = @id
        """, new {id}, cancellationToken: token));

        transaction.Commit();
        return res > 0;
    }

    public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken token = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
        select count(1) from movies where id = @id
        """, new {id}, cancellationToken: token));
    }
}
