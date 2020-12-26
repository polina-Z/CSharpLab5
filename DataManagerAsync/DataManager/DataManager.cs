using ServiceLib;
using System.ServiceProcess;

namespace Laba4
{
    public partial class DataManager : ServiceBase
    {
        readonly DataIO appInsights;

        readonly DataOptions dataOptions;

        public DataManager(DataOptions dataOptions, DataIO appInsights)
        {
            InitializeComponent();

            this.dataOptions = dataOptions;

            this.appInsights = appInsights;
        }

        protected override async void OnStart(string[] args)
        {
            DataIO reader = new DataIO(dataOptions.ConnectionString);

            FileTransfer fileTransfer = new FileTransfer(dataOptions.TargetFolder, dataOptions.SourcePath);

            string personsFileName = "persons";

            await reader.GetCustomersAsync(dataOptions.TargetFolder, appInsights, personsFileName);

            await fileTransfer.SendFileToFtpAsync($"{personsFileName}.xml");
            await fileTransfer.SendFileToFtpAsync($"{personsFileName}.xsd");

            await appInsights.InsertInsightAsync("Files were sent to FTP successfully");

            await appInsights.InsertInsightAsync("Service was successfully stopped");

            await appInsights.WriteInsightsToXmlAsync(dataOptions.TargetFolder);
        }

        protected override void OnStop()
        {

        }
    }
}

