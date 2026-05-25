namespace RonFlow.Observability;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ObservedOperationAttribute(string operationName) : Attribute
{
    public string OperationName { get; } = operationName;
}