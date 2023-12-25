using SQLite;
namespace sfms;

[AttributeUsage(AttributeTargets.Class)]
public class SqliteTableAttribute : Attribute { }


#pragma warning disable IDE1006 // Naming Styles

[SqliteTable]
public class File
{
    [PrimaryKey, AutoIncrement]
    public long id { get; set; } = 0;
    [Indexed]
    public string owner { get; set; } = "";
    public int accessGroup { get; set; } = 0;
    public DateTime createDateTime { get; set; } = DateTime.UtcNow;
    public DateTime modifiedDateTime { get; set; } = DateTime.UtcNow;

    // extension 등으로 검색을 위해서는
    //  functional index 가 필요할수도..
    //  https://atlasgo.io/guides/sqlite/functional-indexes
    [Indexed(Unique = true)]
    public string filePath { get; set; } = "";
    public string checksum { get; set; } = "";
    public long originalFileSize { get; set; } // 압축되어있는 경우 등 length(data) 와 다를 수 있음
    public string originalFileName { get; set; } = "";

    public string attributeJson { get; set; } = "";

    public const char DIRECTORY_SEPARATOR = '/';
}

[SqliteTable]
public class FileContent
{
    [PrimaryKey, AutoIncrement]
    public long id { get; set; } = 0;

    [Indexed(Unique = true)]
    public long fileId { get; set; } = 0;
    public byte[] data { get; set; } = Array.Empty<byte>();
}

#pragma warning restore IDE1006 // Naming Styles
