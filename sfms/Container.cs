using System.ComponentModel.DataAnnotations;
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
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        return await conn.FindAsync<File>(x => x.filePath == absoluteFilePath);
    }

    public File? GetFile(string absoluteFilePath)
    {
        ArgumentInvalidAbsolutePathException.Validate(absoluteFilePath, nameof(absoluteFilePath));
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        return conn.GetConnection().Find<File>(x => x.filePath == absoluteFilePath);
    }

    public async Task<FileContent> ReadFileAsync(File file)
    {
        return await conn.FindAsync<FileContent>(x => x.fileId == file.id)
            ?? throw new NotFoundException($"file not found({file.id}) : {file.filePath}");
    }

    public FileContent ReadFile(File file)
    {
        return conn.GetConnection().Find<FileContent>(x => x.fileId == file.id)
            ?? throw new NotFoundException($"file not found({file.id}) : {file.filePath}");
    }

    public async Task<File> TouchAsync(string absoluteFilePath)
    {
        ArgumentInvalidAbsolutePathException.Validate(absoluteFilePath, nameof(absoluteFilePath));
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

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
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

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

    public async Task<File> WriteFileAsync(string absoluteFilePath, Stream content, bool preserveStreamPosition = false)
    {
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        var file = await TouchAsync(absoluteFilePath); // argumentexception 포함
        WriteFileContent(file, content, preserveStreamPosition);
        return file;
    }

    public File WriteFile(string absoluteFilePath, Stream content, bool preserveStreamPosition = false)
    {
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        var file = Touch(absoluteFilePath);
        WriteFileContent(file, content, preserveStreamPosition);
        return file;
    }

    public async Task<File> MoveAsync(string srcAbsolutePath, string dstAbsolutePath)
    {
        var src = GetFile(srcAbsolutePath)
            ?? throw new NotFoundException($"file not found({srcAbsolutePath})");
        if (GetFile(dstAbsolutePath) is not null)
            throw new AlreadyExistsException($"${dstAbsolutePath} already exists");
        src.filePath = dstAbsolutePath;
        if (await conn.UpdateAsync(src) != 1)
            throw new DatabaseFailedException("failed to update for file move");
        return src;
    }

    public File Move(string srcAbsolutePath, string dstAbsolutePath)
    {
        var src = GetFile(srcAbsolutePath)
            ?? throw new NotFoundException($"file not found({srcAbsolutePath})");
        if (GetFile(dstAbsolutePath) is not null)
            throw new AlreadyExistsException($"${dstAbsolutePath} already exists");
        src.filePath = dstAbsolutePath;
        if (conn.GetConnection().Update(src) != 1)
            throw new DatabaseFailedException("failed to update for file move");
        return src;
    }

    public async Task<File> DeleteAsync(string absoluteFilePath)
    {
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        var file = await GetFileAsync(absoluteFilePath)
            ?? throw new NotFoundException($"file not found : {absoluteFilePath}");
        await conn.RunInTransactionAsync(c =>
        {
            c.Delete(file);
            c.Table<FileContent>().Delete(x => x.fileId == file.id);
        });
        return file;
    }

    public File Delete(string absoluteFilePath)
    {
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        var file = GetFile(absoluteFilePath)
            ?? throw new NotFoundException($"file not found : {absoluteFilePath}");

        var c = conn.GetConnection();
        c.RunInTransaction(() =>
        {
            c.Delete(file);
            c.Table<FileContent>().Delete(x => x.fileId == file.id);
        });
        return file;
    }

    public async Task<File> SetMetaAsync(string absoluteFilePath, string meta = "")
    {
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        var file = await GetFileAsync(absoluteFilePath)
            ?? throw new NotFoundException($"file not found : {absoluteFilePath}");
        file.meta = meta;
        if (await conn.UpdateAsync(file) != 1)
            throw new DatabaseFailedException($"failed to update for meta : {absoluteFilePath}");
        return file;
    }

    public File SetMeta(string absoluteFilePath, string meta = "")
    {
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

        var file = GetFile(absoluteFilePath)
            ?? throw new NotFoundException($"file not found : {absoluteFilePath}");
        file.meta = meta;
        if (conn.GetConnection().Update(file) != 1)
            throw new DatabaseFailedException($"failed to update for meta : {absoluteFilePath}");
        return file;
    }

    #region internal helpers

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

    private FileContent WriteFileContent(File file, Stream content, bool preserveStreamPosition)
    {
        if (preserveStreamPosition && content.CanSeek == false)
            throw new ArgumentException("not supoprted stream", nameof(preserveStreamPosition));
        var originalPosition = preserveStreamPosition ? content.Position : 0;
        try
        {
            var fileContent = ReadFile(file);
            fileContent.data = ReadToEnd(content);
            file.modifiedDateTime = DateTime.UtcNow;
            file.originalFileSize = fileContent.data.Length;
            var c = conn.GetConnection();
            c.RunInTransaction(() =>
            {
                // sqlite 는 serialized mode(https://www.sqlite.org/threadsafe.html) 에서 동작하니 deadlock 문제는 없겠지만
                // 항상 trasaction 내에서 File -> FileContent 순서로 접근
                c.Update(file);
                c.Update(fileContent);
            });
            return fileContent;
        }
        finally
        {
            if (preserveStreamPosition)
                content.Position = originalPosition;
        }
    }

    private static byte[] ReadToEnd(Stream stream)
    { // https://stackoverflow.com/a/1080445
        var readBuffer = new byte[4096];
        int totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
        {
            totalBytesRead += bytesRead;
            if (totalBytesRead == readBuffer.Length)
            {
                int nextByte = stream.ReadByte();
                if (nextByte != -1)
                {
                    byte[] temp = new byte[readBuffer.Length * 2];
                    Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                    Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                    readBuffer = temp;
                    totalBytesRead++;
                }
            }
        }

        byte[] buffer = readBuffer;
        if (readBuffer.Length != totalBytesRead)
        {
            buffer = new byte[totalBytesRead];
            Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
        }
        return buffer;
    }

    private File CreateEmptyFile(string absoluteFilePath)
    {
        ArgumentInvalidFileNameException.Validate(absoluteFilePath, nameof(absoluteFilePath));

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
    #endregion

    private readonly SQLiteAsyncConnection conn;
}
