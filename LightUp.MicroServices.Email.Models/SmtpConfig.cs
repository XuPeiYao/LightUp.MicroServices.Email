namespace LightUp.MicroServices.Email.Models {
    public class SmtpConfig {
        public bool IgnoreCertificatValidation { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public bool Ssl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}