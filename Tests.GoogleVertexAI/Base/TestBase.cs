using Apps.GoogleVertexAI.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace GoogleVertexAI.Base
{
    public class TestBase
    {
        public IEnumerable<AuthenticationCredentialsProvider> Creds { get; set; }

        public InvocationContext InvocationContext { get; set; }

        public FileManager FileManager { get; set; }

        public TestBase()
        {
            var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();

            var connectionValues = config.GetSection("ConnectionDefinition").GetChildren()
                .ToDictionary(x => x.Key, x => x.Value ?? string.Empty);

            if (!connectionValues.ContainsKey(CredNames.ConnectionType))
            {
                connectionValues[CredNames.ConnectionType] = !string.IsNullOrWhiteSpace(connectionValues.GetValueOrDefault(CredNames.ApiKey))
                    ? Apps.GoogleVertexAI.Constants.ConnectionTypes.GeminiApiKey
                    : Apps.GoogleVertexAI.Constants.ConnectionTypes.ServiceAccount;
            }

            var serviceAccountPath = connectionValues.GetValueOrDefault(CredNames.ServiceAccountConfString);
            if (!string.IsNullOrWhiteSpace(serviceAccountPath) && File.Exists(serviceAccountPath))
            {
                connectionValues[CredNames.ServiceAccountConfString] = File.ReadAllText(serviceAccountPath);
            }

            Creds = connectionValues
                .Select(x => new AuthenticationCredentialsProvider(x.Key, x.Value))
                .ToList();

            var relativePath = config.GetSection("TestFolder").Value;
            var projectDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName;
            var folderLocation = Path.Combine(projectDirectory, relativePath);

            InvocationContext = new InvocationContext
            {
                AuthenticationCredentialsProviders = Creds,
            };

            FileManager = new FileManager();
        }

        private static JsonSerializerOptions PrintResultOptions => new() { WriteIndented = true };

        public static void PrintResult(object obj)
        {
            Console.WriteLine(JsonSerializer.Serialize(obj, PrintResultOptions));
        }
    }
}
