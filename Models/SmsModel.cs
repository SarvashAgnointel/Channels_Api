namespace Channels_Api.Models
{
    public class SmsModel
    {
        public class SmppConnectionRequest
        {
            public string ChannelName { get; set; }
            public string Type { get; set; }

            public string Host { get; set; }
            public int Port { get; set; }
            public string SystemId { get; set; }
            public string Password { get; set; }

        }

        public class SendSmsRequest
        {
            public string Sender { get; set; }
            public string Receiver { get; set; }
            public string Message { get; set; }

            public int ChannelId { get; set; }
        }

        public class SendBulkSmsRequest
        {
            public int ChannelId { get; set; }
            public string Sender { get; set; }
            public List<string> Recipients { get; set; }
            public string Message { get; set; }
        }


        public class SmppConnection
        {
            public string Host { get; set; }
            public int Port { get; set; }
            public string SystemId { get; set; }
            public string Password { get; set; }

            public int ChannelId { get; set; }
        }
    }
}
