using DBAccess;
using Inetlab.SMPP.Common;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Data;
using System.Threading.Tasks;
using Channels_Api.Models;
using static Channels_Api.Models.SmsModel;
using Inetlab.SMPP;



namespace Channels_Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [EnableCors("AllowSpecificOrigin")]
    public class MessageController : ControllerBase
    {
        private readonly SmppClientService _smppClientService;
        private readonly ILogger<MessageController> _logger;

        // Constructor with dependency injection
        public MessageController(IDbHandler dbHandler, SmppClientService smppClientService, ILogger<MessageController> logger)
        {
            _smppClientService = smppClientService ?? throw new ArgumentNullException(nameof(smppClientService));
            _logger = logger;
        }





        [HttpGet("isserveralive")]
        public async Task<IActionResult> IsServerAlive()
        {
            return Ok(new
            {
                Status = "Success",
                Status_Description = "The server is alive",
                isAlive=true
            });
        }


        /// <summary>
        /// Connects to the SMPP server for a specific Channel ID.
        /// </summary>
        [HttpPost("connect")]
        public async Task<IActionResult> Connect([FromBody] SmppConnection request)
        {
            if (request == null || request.ChannelId == 0)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Status_Description = "❌ Invalid request. Please provide a valid ChannelId and connection details.",
                });
            }

            try
            {
                Console.WriteLine($"🔗 Attempting to connect to SMPP server {request.Host}:{request.Port} for Channel ID {request.ChannelId}...");

                bool response = await _smppClientService.ConnectAsync(request.ChannelId, request.Host, request.Port, request.SystemId, request.Password);
                if (response)
                {
                    await _smppClientService.StartReceivingAsync(request.ChannelId);
                    return Ok(new
                    {
                        Status = "Success",
                        Status_Description = $"✅ Successfully connected to SMPP server for Channel ID {request.ChannelId}.",
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Status = "Error",
                        Status_Description = $"❌ Connection failed. Either incorrect credentials or Channel ID {request.ChannelId} is already connected.",
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Error",
                    Status_Description = $"❌ Failed to connect: {ex.Message}",
                });
            }
        }

        /// <summary>
        /// Checks if a specific Channel ID is connected to the SMPP server.
        /// </summary>
        [HttpGet("isAlive")]
        public async Task<IActionResult> IsSMPPConnected([FromQuery] int channelId)
        {
            try
            {
                bool isAlive = await _smppClientService.IsConnected(channelId);

                if (isAlive)
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Status_Description = $"✅ SMPP Connection (Channel ID: {channelId}) is Alive.",
                    });
                }
                else
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Status_Description = $"❌ SMPP Connection (Channel ID: {channelId}) is Not Alive.",
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Error",
                    Status_Description = $"❌ Error checking SMPP service: {ex.Message}",
                });
            }
        }

        /// <summary>
        /// Sends an SMS using the correct SMPP connection based on the provided Channel ID.
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendSms([FromBody] SendSmsRequest request)
        {
            if (request == null || request.ChannelId == 0)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Status_Description = "❌ Invalid request. Please provide a valid ChannelId and SMS details.",
                });
            }

            try
            {
                if (!await _smppClientService.IsConnected(request.ChannelId))
                {
                    return StatusCode(503, new
                    {
                        Status = "Error",
                        Status_Description = $"❌ SMPP connection lost for Channel ID {request.ChannelId}. Please reconnect before sending SMS.",
                    });
                }

                Console.WriteLine($"📤 Sending SMS to {request.Receiver} via Channel ID {request.ChannelId}...");

                CommandStatus status = await _smppClientService.SendSmsAsync(request.ChannelId, request.Sender, request.Receiver, request.Message);

                if (status == CommandStatus.ESME_ROK)
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Status_Description = "✅ SMS sent successfully.",
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Status = "Error",
                        Status_Description = "❌ Failed to send SMS.",
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Error",
                    Status_Description = $"❌ Error sending SMS: {ex.Message}",
                });
            }
        }


        [HttpPost("sendBulk")]
        public async Task<IActionResult> SendBulkSms([FromBody] SendBulkSmsRequest request)
        {
            if (request == null || request.ChannelId == 0 || request.Recipients == null || request.Recipients.Count == 0)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Status_Description = "❌ Invalid request. Please provide a valid ChannelId and list of recipients.",
                });
            }

            try
            {
                if (!await _smppClientService.IsConnected(request.ChannelId))
                {
                    return StatusCode(503, new
                    {
                        Status = "Error",
                        Status_Description = $"❌ SMPP connection lost for Channel ID {request.ChannelId}. Please reconnect before sending SMS.",
                    });
                }

                Console.WriteLine($"📤 Sending Bulk SMS via Channel ID {request.ChannelId}...");

                var results = await _smppClientService.SendBulkSmsAsync(request.ChannelId, request.Sender, request.Recipients, request.Message);

                if (results.Count == 0)
                {
                    return BadRequest(new
                    {
                        Status = "Error",
                        Status_Description = "❌ Failed to send bulk SMS.",
                    });
                }

                return Ok(new
                {
                    Status = "Success",
                    Status_Description = "✅ Bulk SMS sent.",
                    Results = results.Select(r => new
                    {
                        Recipient = r.Key,
                        Status = r.Value.ToString()
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Error",
                    Status_Description = $"❌ Error sending bulk SMS: {ex.Message}",
                });
            }
        }



        /// <summary>
        /// Disconnects a specific Channel ID from the SMPP server.
        /// </summary>
        [HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect([FromQuery] int channelId)
        {
            if (channelId == 0)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Status_Description = "❌ Invalid request. Please provide a valid ChannelId.",
                });
            }

            try
            {
                Console.WriteLine($"🚫 Disconnecting Channel ID {channelId} from SMPP server...");

                bool response = await _smppClientService.DisconnectAsync(channelId);
                if (response)
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Status_Description = $"✅ Successfully disconnected Channel ID {channelId} from SMPP server.",
                    });
                }
                else
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Status_Description = $"❌ No active connection found for Channel ID {channelId} to disconnect.",
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Error",
                    Status_Description = $"❌ Failed to disconnect: {ex.Message}",
                });
            }
        }
    }
}
