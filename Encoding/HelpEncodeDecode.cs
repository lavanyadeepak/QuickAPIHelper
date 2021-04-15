using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickEncodeDecode
{
    public class HelpEncodeDecode
    {
        class Program
        {
            static void Main(string[] args)
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Insufficient arguments. Usage: InputString {To | From} Base64");
                }
                else
                {
                    string strInputString = args[0];
                    string strDirection = args[1];

                    if (strDirection.Contains("To"))
                    {
                        Console.WriteLine(Base64Encode(strInputString));
                    }
                    else if (strDirection.Contains("From"))
                    {
                        try
                        {
                            Console.WriteLine(Base64Decode(strInputString));
                        }
                        catch (Exception DecodeException)
                        {
                            Console.WriteLine(string.Format("Could not decode {0} because of error {1}", strInputString, DecodeException.Message));
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unsupported Invocation");
                    }
                }
            }

            public static string Base64Encode(string plainText)
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                return System.Convert.ToBase64String(plainTextBytes);
            }

            public static string Base64Decode(string base64EncodedData)
            {
                var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
                return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
            }
        }
    }
}
