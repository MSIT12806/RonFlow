namespace RonFlow.Application;

public sealed record OwnedResourceQueryResult<T>(T? Resource, bool NotFound, bool AccessDenied) where T : class
{
    public static OwnedResourceQueryResult<T> Success(T resource)
    {
        return new(resource, false, false);
    }

    public static OwnedResourceQueryResult<T> Missing()
    {
        return new(null, true, false);
    }

    public static OwnedResourceQueryResult<T> Denied()
    {
        return new(null, false, true);
    }
}