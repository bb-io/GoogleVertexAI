using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Filters.Transformations;

namespace Apps.GoogleVertexAI.Utils;

public static class ErrorHandler
{
    public static async Task ExecuteWithErrorHandlingAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            throw new PluginApplicationException(ex.Message);
        }
    }

    public static async Task<T> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new PluginApplicationException(ex.Message);
        }
    }
    
    public static T ExecuteWithErrorHandling<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            throw new PluginApplicationException(ex.Message);
        }
    }
    
    public static async Task<Transformation> ParseTransformationWithErrorHandling(this Stream stream, string fileName)
    {
        try
        {
            return await Transformation.Parse(stream, fileName);
        }
        catch (Exception ex) when(ex.Message.Contains("Unsupported XLIFF version", StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginMisconfigurationException(ex.Message);
        }
    }
}