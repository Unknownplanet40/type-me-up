namespace TypeForge.Core.Models;

/// <summary>
/// Represents an embedded public-domain book split into sequential paragraphs for typing tests.
/// </summary>
public sealed class BookModel
{
    /// <summary>Gets the unique book identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the book title.</summary>
    public string Title { get; }

    /// <summary>Gets the book author name.</summary>
    public string Author { get; }

    /// <summary>Gets the chapter count.</summary>
    public int Chapters { get; }

    /// <summary>Gets the total word count across the book.</summary>
    public int AllWords { get; }

    /// <summary>Gets the distinct word count across the book.</summary>
    public int UniqueWords { get; }

    /// <summary>Gets the total character count across the book.</summary>
    public int Characters { get; }

    /// <summary>Gets the average word length in characters.</summary>
    public double AverageWordLength { get; }

    /// <summary>Gets the sequential list of readable paragraphs.</summary>
    public IReadOnlyList<string> Paragraphs { get; }

    /// <summary>
    /// Initializes a validated <see cref="BookModel"/> instance.
    /// </summary>
    public BookModel(
        string id,
        string title,
        string author,
        int chapters,
        int allWords,
        int uniqueWords,
        int characters,
        double averageWordLength,
        IReadOnlyList<string> paragraphs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentNullException.ThrowIfNull(paragraphs);

        if (paragraphs.Count == 0)
        {
            throw new ArgumentException("Book must contain at least one paragraph.", nameof(paragraphs));
        }

        Id = id;
        Title = title;
        Author = author;
        Chapters = chapters;
        AllWords = allWords;
        UniqueWords = uniqueWords;
        Characters = characters;
        AverageWordLength = averageWordLength;
        Paragraphs = paragraphs;
    }
}
