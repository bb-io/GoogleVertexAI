using Apps.GoogleVertexAI.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;

namespace Apps.GoogleVertexAI.Connections;

public class ConnectionDefinition : IConnectionDefinition
{
    public IEnumerable<ConnectionPropertyGroup> ConnectionPropertyGroups => new List<ConnectionPropertyGroup>
    {
        new()
        {
            Name = "Service account",
            AuthenticationType = ConnectionAuthenticationType.Undefined,
            ConnectionUsage = ConnectionUsage.Actions,
            ConnectionProperties = new List<ConnectionProperty>
            {
                //new(CredNames.ProjectId) { DisplayName = "Project ID" },
                new(CredNames.ServiceAccountConfString) { DisplayName = "Service account configuration string" },
                new(CredNames.Region) { DisplayName = "Region" , DataItems= new ConnectionPropertyValue[]{

                            new("africa-south1",       "Johannesburg, South Africa, Africa (africa-south1)"),
                            new("asia-east1",          "Changhua County, Taiwan, Asia Pacific (asia-east1)"),
                            new("asia-east2",          "Hong Kong, Asia Pacific (asia-east2)"),
                            new("asia-northeast1",     "Tokyo, Japan, Asia Pacific (asia-northeast1)"),
                            new("asia-northeast2",     "Osaka, Japan, Asia Pacific (asia-northeast2)"),
                            new("asia-northeast3",     "Seoul, South Korea, Asia Pacific (asia-northeast3)"),
                            new("asia-south1",         "Mumbai, India, Asia Pacific (asia-south1)"),
                            new("asia-southeast1",     "Jurong West, Singapore, Asia Pacific (asia-southeast1)"),
                            new("asia-southeast2",     "Jakarta, Indonesia, Asia Pacific (asia-southeast2)"),
                            new("australia-southeast1","Sydney, Australia, Asia Pacific (australia-southeast1)"),
                            new("australia-southeast2","Melbourne, Australia, Asia Pacific (australia-southeast2)"),
                            new("europe-central2",     "Warsaw, Poland, Europe (europe-central2)"),
                            new("europe-north1",       "Hamina, Finland, Europe (europe-north1)"),
                            new("europe-southwest1",   "Madrid, Spain, Europe (europe-southwest1)"),
                            new("europe-west1",        "St. Ghislain, Belgium, Europe (europe-west1)"),
                            new("europe-west2",        "London, England, Europe (europe-west2)"),
                            new("europe-west3",        "Frankfurt, Germany, Europe (europe-west3)"),
                            new("europe-west4",        "Eemshaven, Netherlands, Europe (europe-west4)"),
                            new("europe-west6",        "Zürich, Switzerland, Europe (europe-west6)"),
                            new("europe-west8",        "Milan, Italy, Europe (europe-west8)"),
                            new("europe-west9",        "Paris, France, Europe (europe-west9)"),
                            new("europe-west12",       "Turin, Italy, Europe (europe-west12)"),
                            new("me-central1",         "Doha, Qatar, Middle East (me-central1)"),
                            new("me-central2",         "Damman, Saudi Arabia, Middle East (me-central2)"),
                            new("me-west1",            "Tel Aviv, Israel, Middle East (me-west1)"),
                            new("northamerica-northeast1","Montréal, Québec, North America (northamerica-northeast1)"),
                            new("northamerica-northeast2","Toronto, Ontario, North America (northamerica-northeast2)"),
                            new("southamerica-east1",  "Osasco, São Paulo, Brazil, South America (southamerica-east1)"),
                            new("southamerica-west1",  "Santiago, Chile, South America (southamerica-west1)"),
                            new("us-central1",         "Council Bluffs, Iowa, North America (us-central1)"),
                            new("us-east1",            "Moncks Corner, South Carolina, North America (us-east1)"),
                            new("us-east4",            "Ashburn, Virginia, North America (us-east4)"),
                            new("us-east5",            "Columbus, Ohio, North America (us-east5)"),
                            new("us-south1",           "Dallas, Texas, North America (us-south1)"),
                            new("us-west1",            "The Dalles, Oregon, North America (us-west1)"),
                            new("us-west2",            "Los Angeles, California, North America (us-west2)"),
                            new("us-west3",            "Salt Lake City, Utah, North America (us-west3)"),
                            new("us-west4",            "Las Vegas, Nevada, North America (us-west4)")
                }}
            }
        }
    };

    public IEnumerable<AuthenticationCredentialsProvider> CreateAuthorizationCredentialsProviders(
           Dictionary<string, string> values)
    {
        yield return new AuthenticationCredentialsProvider(
            AuthenticationCredentialsRequestLocation.None,
            CredNames.ServiceAccountConfString,
            values[CredNames.ServiceAccountConfString]);

        yield return new AuthenticationCredentialsProvider(
            AuthenticationCredentialsRequestLocation.None,
            CredNames.Region,
            values[CredNames.Region]);
    }
}
