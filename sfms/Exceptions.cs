namespace sfms;

public class NotFoundException : ApplicationException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public class AlreadyExistsException : ApplicationException
{
    public AlreadyExistsException(string message) : base(message)
    {
    }
}

public class DatabaseFailedException : ApplicationException
{
    public DatabaseFailedException(string message) : base(message)
    {
    }
}

public class ArgumentInvalidAbsolutePathException : ArgumentException
{
    public ArgumentInvalidAbsolutePathException(string inputPath, string paramName) : base($"absolute path required: {inputPath}", paramName)
    {
    }

    public static void Validate(string inputPath, string paramName)
    {
        if (!inputPath.StartsWith(File.DIRECTORY_SEPARATOR))
            throw new ArgumentInvalidAbsolutePathException(inputPath, paramName);
    }
}

public class ArgumentInvalidFileNameException : ArgumentException
{
    public ArgumentInvalidFileNameException(string inputName, string paramName) : base($"invalid file name : {inputName}", paramName)
    {
    }

    public static void Validate(string inputPath, string paramName)
    {
        if (inputPath.EndsWith(File.DIRECTORY_SEPARATOR))
            throw new ArgumentInvalidFileNameException(inputPath, paramName);
    }
}