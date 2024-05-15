using MTConnect.Agents;
using MTConnect.Applications;
using MTConnect.Configurations;
using MTConnect.Formatters;
using Uns;

var app = new MTConnectAgentApplication();
app.Run(args, true);


public class ModuleConfiguration : DataSourceConfiguration
{
    public string MqttBrokerAddress { get; set; }
    public int MqttBrokerPort { get; set; }
}


public class SparkplugModule : MTConnectAgentModule
{
    public const string ConfigurationTypeId = "sparkplug"; // This must match the module section in the 'agent.config.yaml' file
    public const string DefaultId = "Sparkplug Module"; // The ID is mainly just used for logging.
    private readonly ModuleConfiguration _configuration;
    private readonly UnsClient _unsClient;
    private readonly UnsSparkplugConnection _sparkplugConnection;


    public SparkplugModule(IMTConnectAgentBroker agent, object configuration) : base(agent)
    {
        Id = DefaultId;

        _configuration = AgentApplicationConfiguration.GetConfiguration<ModuleConfiguration>(configuration);

        _sparkplugConnection = new UnsSparkplugConnection(_configuration.MqttBrokerAddress, _configuration.MqttBrokerPort);

        _unsClient = new UnsClient();
        _unsClient.AddMiddleware(new UnsReportByExceptionMiddleware());
    }

    protected override void OnStartAfterLoad(bool initializeDataItems)
    {
        Agent.ObservationAdded += AgentObservationAdded;

        foreach (var device in Agent.GetDevices())
        {
            _sparkplugConnection.AddDevice($"MTConnect/Devices/{device.Uuid}");
        }

        _unsClient.AddConnection(_sparkplugConnection, UnsConnectionType.Output);
        _unsClient.Start();
    }

    protected override void OnStop()
    {
        _unsClient.Stop();
    }


    private void AgentObservationAdded(object sender, MTConnect.Observations.IObservation observation)
    {
        var json = ToJson(observation);

        _unsClient.Publish($"MTConnect/Devices/{observation.DeviceUuid}/{observation.DataItemId}", json);
    }


    private static string ToJson(MTConnect.Observations.IObservation observation)
    {
        var formatResult = EntityFormatter.Format("json", observation);
        if (formatResult.Success)
        {
            formatResult.Content.Seek(0, SeekOrigin.Begin);

            var bytes = new byte[formatResult.Content.Length];
            using (var memoryStream = new MemoryStream())
            {
                formatResult.Content.CopyTo(memoryStream);
                bytes = memoryStream.ToArray();
            }

            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        return null;
    }

}
