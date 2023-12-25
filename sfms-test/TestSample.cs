using Newtonsoft.Json.Linq;
using sfms;

namespace sfms_test;


internal class TestSample
{
    public const string INVALID_DIR_PATH = "not absolute/path/";
    public TestSample(string jsonFilePath)
    {
        var json = System.IO.File.ReadAllText(jsonFilePath);
        // init from json
        var objs = JObject.Parse(json);
        files = objs["files"].ToObject<JObject>();
    }

    public Container CreateSampleContainer()
    {
        // volaile memory database to test
        var sample = new Container(dbPath, true);

        foreach (var f in files)
        { // TODO: content 추가할수 있을 때
            sample.Touch(f.Key);
        }
        return sample;
    }

    public int CountOriginalFiles(string startsWith = "")
    {
        int count = 0;
        foreach (var f in files)
        {
            count += f.Key.StartsWith(startsWith) ? 1 : 0;
        }
        return count;
    }

    private readonly JObject files;

    // 단순히 `:memory:` 나
    //    var dbPath = "file::memory:";
    // 와 같은 database open 으로는 독립적인 memory database 가 생성되지 않고 항상 공유된 db 가
    // 생성되어 비동기테스트함수가 실행될 때 다른 테스트의 간섭을 받는 문제를 피하기 위해
    // https://github.com/praeclarum/sqlite-net/issues/1077
    private readonly string dbPath = $"db_{counter++}";
    private static int counter = 0;
}
