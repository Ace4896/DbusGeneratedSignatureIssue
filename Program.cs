using System.Text;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace GeneratedSignatureIssue;

public sealed class Program
{
    private const string ServiceName = "org.freedesktop.secrets";
    private const string ServicePath = "/org/freedesktop/secrets";

    public static async Task Main(string[] args)
    {
        // Setup a new connection
        Console.WriteLine("Opening D-Bus secret service session");
        var connection = Connection.Session;
        var serviceProxy = new OrgFreedesktopSecretService(connection, ServiceName, ServicePath);
        (_, ObjectPath sessionPath) = await serviceProxy.OpenSessionAsync("plain", new DBusVariantItem("s", new DBusStringItem(string.Empty)));
        Console.WriteLine($"Opened new session at path {sessionPath}");

        // Retrieve the default collection
        Console.WriteLine("Retrieving default collection");
        ObjectPath collectionPath = await serviceProxy.ReadAliasAsync("default");
        if (collectionPath == "/")
        {
            Console.WriteLine("Could not retrieve default collection");
            return;
        }

        Console.WriteLine($"Retrieved default collection at {collectionPath}");
        var collection = new OrgFreedesktopSecretCollection(connection, ServiceName, collectionPath);

        Console.WriteLine("Unlocking collection");
        await LockOrUnlockAsync(connection, false, collectionPath);

        // Create an item in the default collection
        // The secret struct should contain the following fields:
        // - ObjectPath of the session
        // - Byte array containing secret encoding parameters
        // - Byte array containing the encoded secret
        // - String containing the content type hint
        //
        // https://specifications.freedesktop.org/secret-service/latest/ch14.html#idm45741568876576
        //
        // 0.0.11 generates the correct code and this works correctly
        // 0.0.12 is missing the content type hint - from looking at the generated files, it seems to miss out the last field
        string secretString = "secret value";
        byte[] secretParameters = Array.Empty<byte>();
        byte[] secretValue = Encoding.UTF8.GetBytes(secretString);
        string contentType = "text/plain; charset=utf-8";

        string secretValueLabel = "label";
        Dictionary<string, DBusVariantItem> lookupAttributes = GetLookupAttributes(secretValueLabel, new Dictionary<string, string>() {
            { "test-lookup-attribute", "value" }
        });

        Console.WriteLine($"Creating new item with secret value '{secretString}'");
        (ObjectPath itemPath, ObjectPath promptPath) = await collection.CreateItemAsync(
            lookupAttributes,
            (sessionPath, secretParameters, secretValue, contentType),
            true
        );
        Console.WriteLine($"Created new item at path {itemPath}");

        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
    }

    private static Dictionary<string, DBusVariantItem> GetLookupAttributes(string label, Dictionary<string, string> lookupAttributes)
    {
        DBusArrayItem lookupAttributesArray = new(
            DBusType.DictEntry,
            lookupAttributes.Select(kvp => new DBusDictEntryItem(new DBusStringItem(kvp.Key), new DBusStringItem(kvp.Value))).ToArray()
        );

        return new()
        {
            { "org.freedesktop.Secret.Item.Label", new DBusVariantItem("s", new DBusStringItem(label)) },
            { "org.freedesktop.Secret.Item.Attributes", new DBusVariantItem("a{ss}", lookupAttributesArray) }
        };
    }

    /// <summary>
    /// Locks or unlocks the specified object paths, prompting the user where necessary.
    /// </summary>
    /// <param name="connection">The current <see cref="Connection"/>.</param>
    /// <param name="newLockedValue">Whether the items should be locked or unlocked.</param>
    /// <param name="objectPaths">The <see cref="ObjectPath"/>s to lock or unlock.</param>
    private static async Task LockOrUnlockAsync(Connection connection, bool newLockedValue, params ObjectPath[] objectPaths)
    {
        OrgFreedesktopSecretService serviceProxy = new(connection, ServiceName, ServicePath);

        (_, ObjectPath promptPath) = newLockedValue switch
        {
            false => await serviceProxy.UnlockAsync(objectPaths),
            true => await serviceProxy.LockAsync(objectPaths),
        };

        if (promptPath != "/")
        {
            await PromptAsync(connection, promptPath);
        }
    }

    /// <summary>
    /// Displays a prompt required by the secret service using the specified window handle.
    /// </summary>
    /// <param name="connection">The current <see cref="Connection"/> in use.</param>
    /// <param name="promptPath">The <see cref="ObjectPath"/> of the prompt.</param>
    /// <param name="windowId">The platform-specific window handle for displaying the prompt. Defaults to an empty string.</param>
    /// <returns>The result of the prompt.</returns>
    private static async Task<(bool dismissed, DBusVariantItem result)> PromptAsync(Connection connection, ObjectPath promptPath, string windowId = "")
    {
        TaskCompletionSource<(bool, DBusVariantItem)> tcs = new();
        OrgFreedesktopSecretPrompt promptProxy = new(connection, ServiceName, promptPath);

        await promptProxy.WatchCompletedAsync(
            (exception, result) =>
            {
                if (exception != null)
                {
                    tcs.TrySetException(exception);
                }
                else
                {
                    tcs.TrySetResult(result);
                }
            }
        );

        await promptProxy.PromptAsync(windowId);

        return await tcs.Task;
    }
}
