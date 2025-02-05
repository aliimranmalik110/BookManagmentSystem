using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace BulkyBook.Utility
{
    public class EmailSender : IEmailSender
    {
        
        //public string SendGridSecret { get; set; }

        //public EmailSender(IConfiguration _config)
        //{
        //     //secret key is present is appconfiguration file 
        //    SendGridSecret = _config.GetValue<string>("SendGrid:SecretKey");
        //}

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {

            return Task.CompletedTask;

            ////logic here to send email
            //var client = new SendGridClient (SendEmailAsync);
            //var from = new EmailAddress("hello@doamin.com", "Email Title");
            //var to = new EmailAddress(email);
            //var message = MailHelper.CreateSingleEmail(from,to, subject, "",  htmlMessage);
            //return client.SendEmailAsync(message);
        }
    }
}
