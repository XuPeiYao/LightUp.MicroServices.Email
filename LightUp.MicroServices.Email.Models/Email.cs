using System;

namespace LightUp.MicroServices.Email.Models {
    public class Email {
        public Guid Id { get; set; }
        public string Exception { get; set; }

        public string Subject { get; set; }

        public EmailAddress[] From { get; set; }
        public EmailAddress[] To { get; set; }
        public EmailAddress[] Cc { get; set; }
        public EmailAddress[] Bcc { get; set; }
        public AttachmentItem[] Attachments { get; set; }
        public EmailBodyType BodyType { get; set; }
        public string Body { get; set; }


        public SmtpConfig SmtpConfig { get; set; }
    }
}
