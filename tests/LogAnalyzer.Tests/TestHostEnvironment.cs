using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace LogAnalyzer.Tests;

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public TestHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
    }

    public string ApplicationName { get; set; } = "LogAnalyzer.Tests";

    public IFileProvider ContentRootFileProvider { get; set; }

    public string ContentRootPath { get; set; }

    public string EnvironmentName { get; set; } = Environments.Development;
}
