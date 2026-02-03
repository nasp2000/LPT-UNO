using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace LPTUnoApp
{
    public class EmailService
    {
        private readonly HttpClient _client;

        public EmailService()
        {
            _client = new HttpClient();
        }

        public async Task<int> CheckAndSendEmails(string data, string timestamp, string filePrefix, AppConfig config)
        {
            if (string.IsNullOrEmpty(config.GoogleAppsScriptUrl) || config.Recipients == null || config.Recipients.Count == 0)
            {
                return 0;
            }

            int sentCount = 0;
            var dataLower = data.ToLower();

            foreach (var recipient in config.Recipients)
            {
                if (!string.IsNullOrWhiteSpace(recipient.Name) && dataLower.Contains(recipient.Name.ToLower()))
                {
                    await SendEmailToRecipient(recipient, data, timestamp, filePrefix, config);
                    sentCount++;
                }
            }
            return sentCount;
        }

        private async Task SendEmailToRecipient(Recipient recipient, string data, string timestamp, string filePrefix, AppConfig config)
        {
            if (string.IsNullOrEmpty(config.GoogleAppsScriptUrl)) return;

            var sanitizedPrefix = filePrefix.Replace("<", "_").Replace(">", "_").Replace(":", "_")
                                            .Replace("\"", "_").Replace("/", "_").Replace("\\", "_")
                                            .Replace("|", "_").Replace("?", "_").Replace("*", "_");
            
            var fileName = $"{sanitizedPrefix} - {recipient.Name}";
            var attachmentName = $"{fileName}_{timestamp}.txt";

            var payload = new
            {
                to = recipient.Email,
                toName = recipient.Name,
                fromName = config.SenderName,
                subject = $"LPT-UNO {fileName}",
                body = $"Olá {recipient.Name},\n\nSegue em anexo os dados recebidos pela impressora LPT-UNO.\n\n---\nEnviado automaticamente em {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                attachmentData = data,
                attachmentName = attachmentName
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                // Trigger and forget logic as per original JS implementation (no-cors mode didn't read response)
                // But in C# we can read response.
                var response = await _client.PostAsync(config.GoogleAppsScriptUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    // Log error?
                    Console.WriteLine($"Failed to send email to {recipient.Email}. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
            }
        }
    }
}
