using System.ComponentModel;
using System.Reflection;
using SQLite;
namespace sfms;

public class Container
{
    // TODO: Container(string dbFilePath, string userid, Group group)
    public Container(string dbFilePath, bool inMemory = false)
    {
        // 테스트를 위한 in-memory-db 사용에 문제가 있어 URI 지원하도록
        var sqliteOpenFlags =
            (SQLiteOpenFlags)0x00000040 |  // SQLITE_OPEN_URI
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.SharedCache;
        var prefix = "file:";
        var suffix = inMemory ? "?mode=memory" : "";
        conn = new SQLiteAsyncConnection($"{prefix}{dbFilePath}{suffix}", sqliteOpenFlags);

        var c = conn.GetConnection();
        Init(c);
        UpdateSchema(c);
    }

    public async Task<List<File>> GetFilesAsync(string absoluteDirPath)
    {
        ArgumentInvalidAbsolutePathException.Validate(absoluteDirPath, nameof(absoluteDirPath));

        absoluteDirPath = MakeSureDirTail(absoluteDirPath);
        return await conn.QueryAsync<File>(
            "SELECT * FROM File WHERE filePath LIKE ?", absoluteDirPath + "%"
        );
    }

    public List<File> GetFiles(string absoluteDirPath)
    {
        // https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html
        // https://devblogs.microsoft.com/pfxteam/should-i-expose-synchronous-wrappers-for-asynchronous-methods/?WT.mc_id=DT-MVP-5000058
        //  단순히 GetFilesAsync.Result 와 같은 동기함수 사용은 위험하므로 애초에 비동기 함수만으로 구현
        ArgumentInvalidAbsolutePathException.Validate(absoluteDirPath, nameof(absoluteDirPath));

        absoluteDirPath = MakeSureDirTail(absoluteDirPath);
        var c = conn.GetConnection();
        return c.Query<File>(
            "SELECT * FROM File WHERE filePath LIKE ?", absoluteDirPath + "%"
        );
    }


    public async Task<File?> GetFileAsync(string absoluteFilePath)
    {
        ArgumentInvalidAbsolutePathException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        return await conn.FindAsync<File>(x => x.filePath == absoluteFilePath);
    }

    public File? GetFile(string absoluteFilePath)
    {
        ArgumentInvalidAbsolutePathException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        return conn.GetConnection().Find<File>(x => x.filePath == absoluteFilePath);
    }

    public async Task<FileContent> GetFileContentAsync(File file)
    {
        return await conn.FindAsync<FileContent>(x => x.fileId == file.id)
            ?? throw new NotFoundException($"file not found({file.id}) : {file.filePath}");
    }

    public FileContent GetFileContent(File file)
    {
        return conn.GetConnection().Find<FileContent>(x => x.fileId == file.id)
            ?? throw new NotFoundException($"file not found({file.id}) : {file.filePath}");
    }

    // public async Task<File> ReplaceContentAsync(File file, FileContent content)
    // {
    // }

    public async Task<File> TouchAsync(string absoluteFilePath)
    {
        ArgumentInvalidAbsolutePathException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        var file = await GetFileAsync(absoluteFilePath);
        if (file is null)
        {
            file = await CreateEmptyFileAsync(absoluteFilePath);
            return file;
        }
        file.modifiedDateTime = DateTime.UtcNow;
        await conn.UpdateAsync(file);
        return file;
    }

    public File Touch(string absoluteFilePath)
    {
        ArgumentInvalidAbsolutePathException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        var file = GetFile(absoluteFilePath);
        if (file is null)
        {
            file = CreateEmptyFile(absoluteFilePath);
            return file;
        }
        file.modifiedDateTime = DateTime.UtcNow;
        if (conn.GetConnection().Update(file) != 1)
            throw new DatabaseFailedException("failed to update for modified date");
        return file;
    }



    private async Task<File> CreateEmptyFileAsync(string absoluteFilePath)
    {
        var file = new File
        {
            filePath = absoluteFilePath,
        };
        await conn.RunInTransactionAsync(c =>
        {
            if (c.Insert(file) != 1)
                throw new AlreadyExistsException(absoluteFilePath);
            var fileContent = new FileContent
            {
                fileId = file.id
            };
            if (c.Insert(fileContent) != 1)
                throw new DatabaseFailedException("failed to insert FileContent");
        });
        if (file.id == 0)
            throw new DatabaseFailedException("failed to create empty file");
        return file;
    }

    private File CreateEmptyFile(string absoluteFilePath)
    {
        var file = new File
        {
            filePath = absoluteFilePath,
        };
        var c = conn.GetConnection();
        c.RunInTransaction(() =>
        {
            if (c.Insert(file) != 1)
                throw new AlreadyExistsException(absoluteFilePath);
            var fileContent = new FileContent
            {
                fileId = file.id
            };
            if (c.Insert(fileContent) != 1)
                throw new DatabaseFailedException("failed to insert FileContent");
        });
        if (file.id == 0)
            throw new DatabaseFailedException("failed to create empty file");
        return file;
    }


    private static string MakeSureDirTail(string dirPath)
    {
        return dirPath.EndsWith(File.DIRECTORY_SEPARATOR)
            ? dirPath : dirPath + File.DIRECTORY_SEPARATOR;
    }

    private static void Init(SQLiteConnection conn)
    {
        // TODO: configuration parameter 등으로 더 자세한 설정

        // TODO: 각 결과의 응답(오류) 처리

        // https://www.sqlite.org/wal.html
        conn.ExecuteScalar<string>($"PRAGMA journal_mode = WAL");
        // https://www.sqlite.org/atomiccommit.html#_incomplete_disk_flushes
        conn.ExecuteScalar<int>($"PRAGMA fullfsync = ON");
        // https://www.sqlite.org/pragma.html#pragma_synchronous
        conn.ExecuteScalar<int>($"PRAGMA synchronous = EXTRA");
    }

    private static void UpdateSchema(SQLiteConnection conn)
    {
        var tables = from t in Assembly.GetExecutingAssembly().GetTypes()
                     where t.IsDefined(typeof(SqliteTableAttribute))
                     && t.IsClass
                     select t;
        conn.CreateTables(CreateFlags.None, tables.ToArray());
        // TODO: 결과 처리(logging / exception)
    }

    private readonly SQLiteAsyncConnection conn;
}
