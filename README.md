# D-Bus Source Generator Issue

Minimal repro for https://github.com/affederaffe/Tmds.DBus.SourceGenerator/issues/6. Requires .NET 7.0 to run.

The issue seems to be the last field in structs being missed out, e.g. these methods in `ReaderExtensions`:

```csharp
// Should be (ObjectPath, byte[], byte[], string)
public static (ObjectPath, byte[], byte[]) ReadStruct_roayaysz(this ref Reader reader)
{
    reader.AlignStruct();
    return ValueTuple.Create(reader.ReadObjectPath(), reader.ReadArray_ay(), reader.ReadArray_ay());
}

// Should be (ObjectPath, byte[], byte[])
public static (ObjectPath, byte[]) ReadStruct_roayayz(this ref Reader reader)
{
    reader.AlignStruct();
    return ValueTuple.Create(reader.ReadObjectPath(), reader.ReadArray_ay());
}
```
