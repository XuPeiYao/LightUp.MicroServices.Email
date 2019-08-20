using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Text;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LightUp.MicroServices.Email {
    class Program {
        static HttpClient http { get; set; } = new HttpClient();
        static void Main(string[] args) {
            var setting = ReadFromAppSettings();

            var factory = new ConnectionFactory() {
                HostName = setting.GetValue<string>("RabbitMQ:Hostname"),
                VirtualHost = setting.GetValue<string>("RabbitMQ:VirtualHost"),
                Port = setting.GetValue<int>("RabbitMQ:Port"),
                UserName = setting.GetValue<string>("RabbitMQ:Username"),
                Password = setting.GetValue<string>("RabbitMQ:Password")
            };
            var connection = factory.CreateConnection();
            var model = connection.CreateModel();

            model.QueueDeclare(
                queue: "email",
                durable: true, // 持久性，確保RMQ出問題可以繼續存在
                exclusive: false, // 不獨佔
                autoDelete: false, // 不自動清除
                arguments: null);
            model.QueueDeclare(
                queue: "email-success",
                durable: true, // 持久性，確保RMQ出問題可以繼續存在
                exclusive: false, // 不獨佔
                autoDelete: false, // 不自動清除
                arguments: null);
            model.QueueDeclare(
                queue: "email-fail",
                durable: true, // 持久性，確保RMQ出問題可以繼續存在
                exclusive: false, // 不獨佔
                autoDelete: false, // 不自動清除
                arguments: null);

            var consumer = new EventingBasicConsumer(model);

            consumer.Received += (m, ea) => {
                Models.Email email = null;

                try {
                    email = JObject.Parse(Encoding.UTF8.GetString(ea.Body)).ToObject<Models.Email>(); // 取得傳輸訊息(轉檔路徑)

                    SendEmail(email).GetAwaiter().GetResult();
                    model.BasicPublish(string.Empty, "email-success", null, ea.Body);
                    model.BasicAck(ea.DeliveryTag, false);
                } catch (Exception e) {
                    if (email == null) {
                        email = new Models.Email() { };
                    }
                    email.Exception = e.ToString();
                    model.BasicPublish(string.Empty, "email-fail", null, Encoding.UTF8.GetBytes(JObject.FromObject(email).ToString()));
                    model.BasicAck(ea.DeliveryTag, false);
                }
            };

            model.BasicConsume( // 綁定Consumer
                queue: "email",
                autoAck: false,
                consumer: consumer);

            while (true) {
                Console.ReadKey();
            }
        }

        static IConfigurationRoot ReadFromAppSettings() {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        static async Task SendEmail(Models.Email email) {
            var message = new MimeMessage();
            message.From.AddRange(email.From.Select(x => new MailboxAddress(x.Name, x.Address)));
            message.To.AddRange(email.To.Select(x => new MailboxAddress(x.Name, x.Address)));
            message.Cc.AddRange(email.Cc.Select(x => new MailboxAddress(x.Name, x.Address)));
            message.Bcc.AddRange(email.Bcc.Select(x => new MailboxAddress(x.Name, x.Address)));
            message.Subject = email.Subject;

            var builder = new BodyBuilder();

            foreach (var item in email.Attachments) {
                builder.Attachments.Add(item.Name, await http.GetStreamAsync(item.Url));
            }
            if (email.BodyType == Models.EmailBodyType.Text) {
                builder.TextBody = email.Body;
            } else if (email.BodyType == Models.EmailBodyType.HTML) {
                builder.HtmlBody = email.Body;
            }

            message.Body = builder.ToMessageBody();


            using (var client = new SmtpClient()) {
                if (email.SmtpConfig.IgnoreCertificatValidation) {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                }

                client.Connect(email.SmtpConfig.Host, email.SmtpConfig.Port, email.SmtpConfig.Ssl);
                client.Authenticate(email.SmtpConfig.Username, email.SmtpConfig.Password);
                client.Send(message);
                client.Disconnect(true);
            }
        }
    }
}
