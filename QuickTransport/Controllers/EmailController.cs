using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Http;
using log4net;
using log4net.Config;

namespace QuickTransport.Controllers
{
    public class EmailController : ApiController
    {
        private static readonly ILog log =
LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public EmailController()
        {
            XmlConfigurator.Configure();
        }

        [HttpGet]
        [Route("api/inet/currentdatetime")]
        public DateTime SayHello()
        {
            return (DateTime.Now);
        }


        [HttpPost]
        [Route("api/inet/sendemail")]
        public IHttpActionResult SendEmail()
        {
            try
            {
                string To = System.Web.HttpContext.Current.Request.Form["To"];
                string Cc = System.Web.HttpContext.Current.Request.Form["Cc"];
                string Bcc = System.Web.HttpContext.Current.Request.Form["Bcc"];
                string Subject = System.Web.HttpContext.Current.Request.Form["Subject"];
                string Message = System.Web.HttpContext.Current.Request.Form["Message"];
                string Attachments = System.Web.HttpContext.Current.Request.Form["Attachments"];
                string AppKey = System.Web.HttpContext.Current.Request.Form["AppKey"];
                string strApplicationIdentifier = ConfigurationManager.AppSettings["ApplicationIdentifier"];

                int intAttachmentCount = Convert.ToInt32(ConfigurationManager.AppSettings["MaxAttachmentsPerMessage"]);
                long lngAttachmentSize = Convert.ToInt64(ConfigurationManager.AppSettings["MaxSizePerAttachment"]);
                long lngMailBodyCumulativeMaxSize = Convert.ToInt64(ConfigurationManager.AppSettings["MaxBodySizeOfAttachment"]);
                long intMaxNumberOfRecipients = Convert.ToInt64(ConfigurationManager.AppSettings["MaxNumberOfRcptsPerMessage"]); //Office365 limit is 1000

                SmtpClient smtp = new SmtpClient();
                smtp.Host = ConfigurationManager.AppSettings["SmtpServer"];
                smtp.Port = Convert.ToInt32(ConfigurationManager.AppSettings["SmtpPort"]);
                smtp.EnableSsl = Convert.ToBoolean(ConfigurationManager.AppSettings["EnableSSL"]);
                smtp.UseDefaultCredentials = Convert.ToBoolean(ConfigurationManager.AppSettings["UseDefaultCredentials"]);


                string strUserName = ConfigurationManager.AppSettings["SmtpUserName"];
                string strPassword = ConfigurationManager.AppSettings["SmtpPassword"];

                if (strUserName.Length > 0)
                {
                    smtp.Credentials = new NetworkCredential(strUserName, strPassword);

                    if (!string.IsNullOrEmpty(strPassword))
                    {
                        try
                        {
                            //strPassword = QuickUtils.GetPassword(strPassword, strApplicationIdentifier);
                        }
                        catch (Exception GetDecryptedPasswordException)
                        {
                            log.Error(GetDecryptedPasswordException.Message, GetDecryptedPasswordException);
                            return (InternalServerError(new ApplicationException("Potential configuration issue. The supplied SMTP password failed to  validate.")));
                        }
                    }
                }

                if (string.IsNullOrEmpty(AppKey) || !AppKey.Equals(strApplicationIdentifier))
                {
                    return (Unauthorized());
                }
                else
                {

                    MailMessage mail = new MailMessage();

                    mail.From = new MailAddress(ConfigurationManager.AppSettings["SmtpFrom"]);
                    int intRcptCount = 0;

                    if (string.IsNullOrEmpty(To))
                    {
                        return (InternalServerError(new ApplicationException("At least one recipient is necessary")));
                    }
                    else
                    {
                        var strTo = To.Split(',');

                        intRcptCount += strTo.Length;

                        foreach (var i in strTo)
                            mail.To.Add(i);

                        if (!string.IsNullOrEmpty(Cc))
                        {
                            var strCc = Cc.Split(',');
                            intRcptCount += strCc.Length;

                            foreach (var i in strCc)
                                mail.CC.Add(i);
                        }

                        if (!string.IsNullOrEmpty(Bcc))
                        {
                            var strBcc = Bcc.Split(',');
                            intRcptCount += strBcc.Length;

                            foreach (var i in strBcc)
                                mail.Bcc.Add(i);
                        }

                        if (intRcptCount > intMaxNumberOfRecipients)
                        {
                            return (InternalServerError(new ApplicationException(string.Format("Only {0} number of recipients across To/Cc/Bcc per message are allowed to be sent.", intMaxNumberOfRecipients))));
                        }

                        mail.Subject = Subject;
                        mail.Body = Message;

                        mail.IsBodyHtml = Convert.ToBoolean(ConfigurationManager.AppSettings["IsBodyHTML"]);

                        mail.Headers.Add("X-Originating-IP", HttpContext.Current.Request.UserHostAddress);
                        //mail.Headers.Add("X-LoggedOn-User", AppIdentity.AppUser.FullName);

                        if (!string.IsNullOrEmpty(Attachments))
                        {
                            string[] arrAttachments = Attachments.Split(',');

                            if (arrAttachments.Length > intAttachmentCount)
                            {
                                return (InternalServerError(new ApplicationException(string.Format("Only {0} number of attachments per message are allowed to be sent.", intAttachmentCount))));
                            }
                            else
                            {
                                long lngCumulativeSize = 0;

                                foreach (var i in arrAttachments)
                                {
                                    var eachAttachment = i.ToString().Split('=');
                                    if (eachAttachment.Length == 2)
                                    {
                                        byte[] bytFile = null;
                                        try
                                        {
                                            bytFile = Convert.FromBase64String(eachAttachment[1].ToString());
                                        }
                                        catch (Exception InvalidAttachmentException)
                                        {
                                            log.Error(InvalidAttachmentException.Message, InvalidAttachmentException);
                                            return (InternalServerError(new ApplicationException(string.Format("{0} was received corrupted", eachAttachment[0]))));
                                        }

                                        //Check for Size of the File from String
                                        float mb = (bytFile.Length / 1024f) / 1024f;
                                        if (mb > lngAttachmentSize)
                                        {
                                            return (InternalServerError(new ApplicationException(string.Format("{0} was too big. Allowed size is {1}", arrAttachments[0], lngAttachmentSize))));
                                        }
                                        else
                                        {
                                            lngCumulativeSize += Convert.ToInt64(mb);
                                            mail.Attachments.Add(new Attachment(new MemoryStream(bytFile, 0, bytFile.Length), eachAttachment[0].ToString()));
                                        }
                                    }
                                }

                                if (lngCumulativeSize > lngMailBodyCumulativeMaxSize)
                                {
                                    return (InternalServerError(new ApplicationException(string.Format("Email too big. Maximum size with all attachments is {0}", lngMailBodyCumulativeMaxSize))));
                                }
                            }
                        }
                        smtp.Send(mail);

                        return (Ok());
                    }
                }
            }
            catch (Exception SendEmailException)
            {
                log.Error(SendEmailException.Message);
                return (InternalServerError(SendEmailException));
            }
        }
    }
}
