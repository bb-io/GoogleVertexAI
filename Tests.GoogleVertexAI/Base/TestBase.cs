using Apps.GoogleVertexAI.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Microsoft.Extensions.Configuration;

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

            var serviceAccountPath = config.GetSection("ConnectionDefinition:ServiceAccountConfString").Value;

            if (File.Exists(serviceAccountPath))
            {
                var serviceAccountJson = File.ReadAllText(serviceAccountPath);

                Creds = new List<AuthenticationCredentialsProvider>
        {
            new(AuthenticationCredentialsRequestLocation.None, CredNames.ServiceAccountConfString, serviceAccountJson)
        };
            }
            else
            {
                Creds = config.GetSection("ConnectionDefinition").GetChildren()
                    .Select(x => new AuthenticationCredentialsProvider(x.Key, x.Value))
                    .ToList();
            }

            var relativePath = config.GetSection("TestFolder").Value;
            var projectDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName;
            var folderLocation = Path.Combine(projectDirectory, relativePath);

            InvocationContext = new InvocationContext
            {
                AuthenticationCredentialsProviders = Creds,
            };

            FileManager = new FileManager();
        }
    }
}
