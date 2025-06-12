using Apps.GoogleVertexAI.Connections;
using Blackbird.Applications.Sdk.Common.Authentication;
using GoogleVertexAI.Base;

namespace Tests.GoogleVertexAI;

[TestClass]
public class ConnectionValidatorTests : TestBase
{
    [TestMethod]
    public async Task ValidateConnection_ValidCredentials_ValidConnection()
    {
        var connectionValidator = new ConnectionValidator();

        var result = await connectionValidator.ValidateConnection(Creds, CancellationToken.None);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public async Task ValidateConnection_InvalidCredentials_InvalidConnection()
    {
        var connectionValidator = new ConnectionValidator();
        var invalidCredentials = Creds.Select(x => new AuthenticationCredentialsProvider(x.KeyName, x.Value + "_incorrect"));
        var result = await connectionValidator.ValidateConnection(invalidCredentials, CancellationToken.None);
        Assert.IsFalse(result.IsValid);
    }
}