// Enables C# 9 `record` types and `init`-only setters when targeting
// netstandard2.1, which does not ship this type. Compiler-only marker;
// it is never referenced at runtime.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
