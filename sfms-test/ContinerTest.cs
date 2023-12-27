using sfms;

namespace sfms_test;

[TestClass]
public class ContinerTest
{
    const string SAMPLE_SOURCE = "test_sample_structure.json";
    const string TEST_DIR_PATH = "/aa/bb";
    const string TEST_FILE_PATH = "/aa/bb/a.1";
    const long TEST_FILE_SIZE = 3;
    const string NOT_EXIST_DIR_PATH = "/xx/yy/zz";
    const string NOT_EXIST_FILE_PATH = $"{TEST_DIR_PATH}/a.2";
    const string NOT_EXIST_DIR_FILE_PATH = $"{NOT_EXIST_DIR_PATH}/a.2";

    [TestInitialize]
    public void TestInitialize()
    {
        sample = new(SAMPLE_SOURCE);
        c = sample.CreateSampleContainer();
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private TestSample sample;
    private Container c;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    [TestMethod]
    public async Task GetFilePathsAsync()
    {
        await TestArgumentInvalidAbsolutePathExceptionAsync(c.GetFilesAsync);

        var createdFiles = await c.GetFilesAsync(TEST_DIR_PATH);
        Assert.AreEqual(sample.CountOriginalFiles(TEST_DIR_PATH), createdFiles.Count);
    }

    [TestMethod]
    public void GetFilePaths()
    {
        TestArgumentInvalidAbsolutePathException(c.GetFiles);

        var createdFiles = c.GetFiles(TEST_DIR_PATH);
        Assert.AreEqual(sample.CountOriginalFiles(TEST_DIR_PATH), createdFiles.Count);
    }

    [TestMethod]
    public async Task GetFilesAsync()
    {
        await TestArgumentInvalidAbsolutePathExceptionAsync(c.GetFilesAsync);

        var files = await c.GetFilesAsync(TEST_DIR_PATH);
        Assert.AreEqual(sample.CountOriginalFiles(TEST_DIR_PATH), files.Count);
    }


    [TestMethod]
    public void GetFiles()
    {
        TestArgumentInvalidAbsolutePathException(c.GetFiles);

        var files = c.GetFiles(TEST_DIR_PATH);
        Assert.AreEqual(sample.CountOriginalFiles(TEST_DIR_PATH), files.Count);
    }

    // 일단 test 코드도 계속 바뀔 수 있으므로
    //  async 함수 위주로 테스트 작성. 추후에 안정화 되면 sync 테스트 추가

    [TestMethod]
    public async Task GetFileAsync()
    {
        await TestArgumentInvalidAbsolutePathExceptionAsync(c.GetFileAsync);
        await TestArgumentInvalidFileNameExceptionAsync(c.GetFileAsync);

        var notExist = await c.GetFileAsync(NOT_EXIST_FILE_PATH);
        Assert.IsNull(notExist);
        var file = await c.GetFileAsync(TEST_FILE_PATH);
        Assert.IsNotNull(file);
        Assert.AreEqual(TEST_FILE_SIZE, file.originalFileSize);
    }

    [TestMethod]
    public async Task ReadFileAsync()
    {
        var original = sample.GetOriginalFileContent(TEST_FILE_PATH)!;
        var file = await c.GetFileAsync(TEST_FILE_PATH);
        Assert.IsNotNull(file);
        var content = await c.ReadFileAsync(file);
        Assert.IsNotNull(file);
        Assert.IsTrue(original.SequenceEqual(content.data));
    }

    [TestMethod]
    public async Task TouchAsync_Create()
    {
        await TestArgumentInvalidAbsolutePathExceptionAsync(c.TouchAsync);
        await TestArgumentInvalidFileNameExceptionAsync(c.TouchAsync);

        AssertNotExist(NOT_EXIST_FILE_PATH);
        var touched = await c.TouchAsync(NOT_EXIST_FILE_PATH);
        AssertExist(NOT_EXIST_FILE_PATH);
        var interval = (touched.createDateTime - touched.modifiedDateTime).Duration();
        // 초단위 미만(ns)에서는 다를 수 있음.
        Assert.IsTrue(interval < TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task TouchAsync_ModifiedDateTime()
    {
        const double DELAY_SECONDS = 1;
        await TestArgumentInvalidAbsolutePathExceptionAsync(c.TouchAsync);

        await Task.Delay(TimeSpan.FromSeconds(DELAY_SECONDS));
        var touched = await c.TouchAsync(TEST_FILE_PATH);
        Assert.AreNotEqual(touched!.createDateTime, touched.modifiedDateTime);
        var interval = (touched.createDateTime - touched.modifiedDateTime).Duration();
        Assert.IsTrue(interval >= TimeSpan.FromSeconds(DELAY_SECONDS));
    }

    [TestMethod]
    public async Task WriteFileAsync()
    {
        var src = new byte[] {
            0x11, 0xcf, 0xd1, 0x04,
        };
        var stream = new MemoryStream(src);

        AssertNotExist(NOT_EXIST_FILE_PATH);
        var file = await c.WriteFileAsync(NOT_EXIST_FILE_PATH, stream, true);
        Assert.AreEqual(src.Length, file.originalFileSize);
        var content = c.ReadFile(file);
        Assert.IsTrue(src.SequenceEqual(content.data));

        // valdidate overwriting environment
        var otherFile = c.GetFile(TEST_FILE_PATH);
        Assert.IsNotNull(otherFile);
        var otherContent = c.ReadFile(otherFile);
        Assert.IsFalse(src.SequenceEqual(otherContent.data));

        // test overwriting
        file = await c.WriteFileAsync(NOT_EXIST_FILE_PATH, stream, true);
        Assert.AreEqual(src.Length, file.originalFileSize);
        content = c.ReadFile(file);
        Assert.IsTrue(src.SequenceEqual(content.data));
    }

    [TestMethod]
    public async Task MoveAsync()
    {
        AssertExist(TEST_FILE_PATH);
        AssertNotExist(NOT_EXIST_FILE_PATH);

        await Assert.ThrowsExceptionAsync<NotFoundException>(() =>
            c.MoveAsync(NOT_EXIST_FILE_PATH, TEST_FILE_PATH)
        );
        await Assert.ThrowsExceptionAsync<AlreadyExistsException>(() =>
            c.MoveAsync(TEST_FILE_PATH, TEST_FILE_PATH)
        );

        var file = await c.MoveAsync(TEST_FILE_PATH, NOT_EXIST_FILE_PATH);
        Assert.AreEqual(NOT_EXIST_FILE_PATH, file.filePath);
        AssertExist(NOT_EXIST_FILE_PATH);
        AssertNotExist(TEST_FILE_PATH);
    }

    [TestMethod]
    public async Task DeleteAsync()
    {
        await TestArgumentInvalidFileNameExceptionAsync(c.DeleteAsync);

        var src = AssertExist(TEST_FILE_PATH);
        var fileContent = c.ReadFile(src);
        var deletedFile = await c.DeleteAsync(TEST_FILE_PATH);
        Assert.AreEqual(src.filePath, deletedFile.filePath);
        Assert.AreEqual(src.id, deletedFile.id);
        AssertNotExist(TEST_FILE_PATH);
        // make sure no content
        Assert.ThrowsException<NotFoundException>(() =>
            c.ReadFile(deletedFile)
        );
    }

    [TestMethod]
    public async Task SetMetaAsync()
    {
        await Assert.ThrowsExceptionAsync<NotFoundException>(() =>
            c.SetMetaAsync(NOT_EXIST_FILE_PATH)
        );
        const string META = "blabla";
        var file = await c.GetFileAsync(TEST_FILE_PATH);
        Assert.IsNotNull(file);
        Assert.AreEqual(string.Empty, file.meta);

        file = await c.SetMetaAsync(TEST_FILE_PATH, META);
        Assert.IsNotNull(file);
        Assert.AreEqual(META, file.meta);

        file = await c.GetFileAsync(TEST_FILE_PATH);
        Assert.IsNotNull(file);
        Assert.AreEqual(META, file.meta);
    }

    #region test helpers

    private static async Task TestArgumentInvalidAbsolutePathExceptionAsync<T>(Func<string, Task<T>> func)
    {
        await Assert.ThrowsExceptionAsync<ArgumentInvalidAbsolutePathException>(() =>
            func(TestSample.INVALID_DIR_PATH)
        );
    }

    private static void TestArgumentInvalidAbsolutePathException<T>(Func<string, T> func)
    {
        Assert.ThrowsException<ArgumentInvalidAbsolutePathException>(() =>
            func(TestSample.INVALID_DIR_PATH)
        );
    }

    private static async Task TestArgumentInvalidFileNameExceptionAsync<T>(Func<string, Task<T>> func)
    {
        await Assert.ThrowsExceptionAsync<ArgumentInvalidFileNameException>(() =>
            func(TestSample.END_WITH_SLASH_PATH)
        );
    }

    private static void TestArgumentInvalidFileNameException<T>(Func<string, T> func)
    {
        Assert.ThrowsException<ArgumentInvalidFileNameException>(() =>
            func(TestSample.END_WITH_SLASH_PATH)
        );
    }


    private sfms.File AssertExist(string absoluteFilePath)
    {
        var file = c.GetFile(absoluteFilePath);
        Assert.IsNotNull(file);
        return file;
    }

    private void AssertNotExist(string absoluteFilePath)
    {
        Assert.IsNull(c.GetFile(absoluteFilePath));
    }
    #endregion
}
