using Inetlab.SMPP;
using Inetlab.SMPP.Common;
using Inetlab.SMPP.PDU;
using System;
using System.Collections.Concurrent;
using System.Net.Mail;
using System.Threading.Tasks;

public class SmppClientService
{
    private readonly ConcurrentDictionary<int, SmppClient> _activeConnections = new ConcurrentDictionary<int, SmppClient>();

    public async Task<bool> ConnectAsync(int channelId, string host, int port, string systemId, string password)
    {
        try
        {
            if (_activeConnections.ContainsKey(channelId))
            {
                Console.WriteLine($"⚠️ Connection for Channel ID {channelId} already exists.");
                return false; // Prevent duplicate connections
            }

            Console.WriteLine($"🔗 Connecting to SMPP Server {host}:{port} for Channel ID {channelId}...");
            var smppClient = new SmppClient
            {
                EnquireLinkInterval = TimeSpan.FromSeconds(30) // ✅ Auto health check
            };

            bool connected = await smppClient.ConnectAsync(host, port);
            if (!connected)
            {
                Console.WriteLine($"❌ Failed to connect to SMPP Server for Channel ID {channelId}.");
                return false;
            }

            Console.WriteLine($"✅ Connected. Sending Bind request for Channel ID {channelId}...");
            var bindResp = await smppClient.BindAsync(systemId, password, ConnectionMode.Transceiver);

            if (bindResp.Header.Status == CommandStatus.ESME_ROK)
            {
                Console.WriteLine($"✅ Successfully Bound to SMPP Server for Channel ID {channelId}.");
                _activeConnections[channelId] = smppClient; // Store active connection
                return true;
            }
            else
            {
                Console.WriteLine($"❌ Bind Failed for Channel ID {channelId}. Status: {bindResp.Header.Status}");
                await smppClient.DisconnectAsync();
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Connection Error for Channel ID {channelId}: {ex.Message}");
            return false;
        }
    }

    public async Task<CommandStatus> SendSmsAsync(int channelId, string sender, string receiver, string message)
    {
        try
        {
            if (!_activeConnections.TryGetValue(channelId, out var smppClient))
            {
                Console.WriteLine($"❌ No active connection found for Channel ID {channelId}.");
                return CommandStatus.ESME_RSYSERR; // No connection exists
            }

            var submitSm = new SubmitSm
            {
                SourceAddress = new SmeAddress(sender),
                DestinationAddress = new SmeAddress(receiver),
                DataCoding = DataCodings.UCS2,
            };

            submitSm.UserData.ShortMessage = smppClient.EncodingMapper.GetMessageBytes(message, submitSm.DataCoding);

            var response = await smppClient.SubmitAsync(submitSm);
            Console.WriteLine($"📤 Sending SMS to {receiver} via Channel ID {channelId}... Status: {response.Header.Status}");

            return response.Header.Status;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error sending SMS on Channel ID {channelId}: {ex.Message}");
            return CommandStatus.ESME_RSYSERR;
        }
    }

    public async Task<Dictionary<string, CommandStatus>> SendBulkSmsAsync(int channelId, string sender, List<string> recipients, string message)
    {
        var results = new Dictionary<string, CommandStatus>();

        try
        {
            if (!_activeConnections.TryGetValue(channelId, out var smppClient))
            {
                Console.WriteLine($"❌ No active connection found for Channel ID {channelId}.");
                return results; // Return empty if no connection
            }

            var tasks = new List<Task<(string, CommandStatus)>>();

            foreach (var receiver in recipients)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var submitSm = new SubmitSm
                        {
                            SourceAddress = new SmeAddress(sender),
                            DestinationAddress = new SmeAddress(receiver),
                            DataCoding = DataCodings.UCS2,
                        };

                        submitSm.UserData.ShortMessage = smppClient.EncodingMapper.GetMessageBytes(message, submitSm.DataCoding);

                        var response = await smppClient.SubmitAsync(submitSm);
                        Console.WriteLine($"📤 Sending SMS to {receiver} via Channel {channelId}. Status: {response.Header.Status}");

                        return (receiver, response.Header.Status);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error sending SMS to {receiver}: {ex.Message}");
                        return (receiver, CommandStatus.ESME_RSYSERR);
                    }
                }));
            }

            // Wait for all messages to be sent
            var responses = await Task.WhenAll(tasks);

            // Store results
            foreach (var (receiver, status) in responses)
            {
                results[receiver] = status;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Bulk SMS sending failed: {ex.Message}");
        }

        return results;
    }

    public async Task<bool> IsConnected(int channelId)
    {
        if (_activeConnections.TryGetValue(channelId, out var smppClient))
        {
            return smppClient.Status != ConnectionStatus.Closed;
            //return smppClient.Status == ConnectionStatus.Open;
        }
        return false;
    }

    public async Task<bool> DisconnectAsync(int channelId)
    {
        if (_activeConnections.TryRemove(channelId, out var smppClient))
        {
            Console.WriteLine($"🚫 Disconnecting from SMPP Server for Channel ID {channelId}...");
            await smppClient.DisconnectAsync();
            Console.WriteLine($"✅ Disconnected from SMPP Server for Channel ID {channelId}.");
            return true;
        }
        else
        {
            Console.WriteLine($"⚠️ No active connection found for Channel ID {channelId} to disconnect.");
            return false;
        }
    }

    public async Task StartReceivingAsync(int channelId)
    {
        if (_activeConnections.TryGetValue(channelId, out var smppClient))
        {
            Console.WriteLine($"📥 Listening for incoming SMS on Channel ID {channelId}...");
            smppClient.evDeliverSm += (sender, data) => OnDeliverSmReceived(channelId, data);
        }
    }

    private void OnDeliverSmReceived(int channelId, DeliverSm data)
    {
        try
        {
            if (_activeConnections.TryGetValue(channelId, out var smppClient))
            {
                string message = smppClient.EncodingMapper.GetMessageText(data.UserData.ShortMessage, data.DataCoding);
                Console.WriteLine($"📩 Received SMS from {data.SourceAddress.Address} on Channel ID {channelId}: {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing received SMS on Channel ID {channelId}: {ex.Message}");
        }
    }
}
