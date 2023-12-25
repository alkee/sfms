using sfms;

namespace sfms_test;

[TestClass]
public class ContinerTest
{
    const string SAMPLE_SOURCE = "test_sample_structure.json";
    const string TEST_DIR_PATH = "/aa/bb";
    const string TEST_FILE_PATH = "/aa/bb/a.1";
    const string NOT_EXIST_DIR_PATH = "/xx/yy/zz";

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



    // [TestMethod]
    // public async Task TouchAsync()
    // {
    //     const string INVALID_FILE_PATH = "invalid.ext";
    //     const string TEST_FILE_PATH = $"{TEST_DIR_PATH}/sample.ext";

    //     await Assert.ThrowsExceptionAsync<ArgumentInvalidAbsolutePathException>(() =>
    //         c.TouchAsync(INVALID_FILE_PATH)
    //     );

    //     var touched = await c.TouchAsync(TEST_FILE_PATH);
    //     Assert.AreNotEqual(null, touched);
    //     Assert.AreEqual(0, touched!.originalFileSize);
    //     var interval = (touched.createDateTime - touched.modifiedDateTime).Duration();
    //     Assert.IsTrue(interval < TimeSpan.FromSeconds(1));

    //     // touch again
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //     touched = await c.TouchAsync(TEST_FILE_PATH);

    //     Assert.AreNotEqual(touched!.createDateTime, touched.modifiedDateTime);
    //     interval = (touched.createDateTime - touched.modifiedDateTime).Duration();
    //     Assert.IsTrue(interval >= TimeSpan.FromSeconds(2));
    // }
}