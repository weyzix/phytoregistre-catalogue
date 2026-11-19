using System.Text;
using PhytoRegistre.CatalogBuilder;
using PhytoRegistre.CatalogBuilder.Sftp;

Console.OutputEncoding = Encoding.UTF8;
var app = new CatalogBuilderApp(
    new SshNetSftpSourceFactory(),
    TimeProvider.System,
    Environment.GetEnvironmentVariable,
    Console.Out,
    Console.Error);
return app.Run(args);
