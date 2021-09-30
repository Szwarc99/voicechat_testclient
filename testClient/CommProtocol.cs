using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace testClient
{
    class CommProtocol
    {
        static NetworkStream stream;
        public static AesManaged aes;
        static RSACryptoServiceProvider rsa;

        

        public static void init(NetworkStream _stream)
        {
            stream = _stream;
            aes = new AesManaged();
            rsa = new RSACryptoServiceProvider();
            string keyPath = Directory.GetCurrentDirectory() + "\\keys\\pub.txt";
            byte[] publicKey = File.ReadAllBytes(keyPath);
            rsa.ImportCspBlob(publicKey);
        }


        static byte[] Encrypt(string plainText, byte[] Key, byte[] IV)
        {
            byte[] encrypted;
            // Create a new AesManaged.    
            using (AesManaged aes = new AesManaged())
            {
                aes.Padding = PaddingMode.PKCS7;
                // Create encryptor    
                ICryptoTransform encryptor = aes.CreateEncryptor(Key, IV);
                // Create MemoryStream                    
                using (MemoryStream ms = new MemoryStream())
                {
                    // Create crypto stream using the CryptoStream class. This class is the key to encryption    
                    // and encrypts and decrypts data from any given stream. In this case, we will pass a memory stream    
                    // to encrypt    
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        // Create StreamWriter and write data to a stream    
                        using (StreamWriter sw = new StreamWriter(cs))
                            sw.Write(plainText);
                        encrypted = ms.ToArray();
                    }
                }
            }
            // Return encrypted data    
            return encrypted;
        }

        public static byte[] EncryptUDP(byte[] plainAudio, byte[] Key, byte[] IV)
        {
            byte[] encrypted;
            // Create a new AesManaged.    
            using (AesManaged aes = new AesManaged())
            {
                aes.Padding = PaddingMode.PKCS7;
                // Create encryptor    
                ICryptoTransform encryptor = aes.CreateEncryptor(Key, IV);
                // Create MemoryStream                    
                using (MemoryStream ms = new MemoryStream())
                {
                    // Create crypto stream using the CryptoStream class. This class is the key to encryption    
                    // and encrypts and decrypts data from any given stream. In this case, we will pass a memory stream    
                    // to encrypt    
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plainAudio, 0, plainAudio.Length);
                        cs.FlushFinalBlock();
                        encrypted = ms.ToArray();
                    }
                }
            }
            // Return encrypted data    
            return encrypted;
        }
        public static byte[] DecryptUDP(byte[] cipherAudio, byte[] Key, byte[] IV)
        {
            byte[] plainAudio;
            // Create AesManaged    
            using (AesManaged aes = new AesManaged())
            {
                aes.Padding = PaddingMode.PKCS7;
                // Create a decryptor    
                ICryptoTransform decryptor = aes.CreateDecryptor(Key, IV);
                // Create the streams used for decryption.    
                using (MemoryStream ms = new MemoryStream(cipherAudio))
                {
                    // Create crypto stream    
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(cipherAudio, 0, cipherAudio.Length);
                        cs.FlushFinalBlock();
                        plainAudio = ms.ToArray();
                    }
                }
            }
            return plainAudio;
        }
        static string Decrypt(byte[] cipherText, byte[] Key, byte[] IV)
        {
            string plaintext = null;
            // Create AesManaged    
            using (AesManaged aes = new AesManaged())
            {
                aes.Padding = PaddingMode.PKCS7;
                // Create a decryptor    
                ICryptoTransform decryptor = aes.CreateDecryptor(Key, IV);
                // Create the streams used for decryption.    
                using (MemoryStream ms = new MemoryStream(cipherText))
                {
                    // Create crypto stream    
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        // Read crypto stream    
                        using (StreamReader reader = new StreamReader(cs))
                            plaintext = reader.ReadToEnd();
                    }
                }
            }
            return plaintext;
        }

        public static void sendKey(Aes aes)
        {
            //string str = keyToSend + " " + ivToSend;

            byte[] encrypted = rsa.Encrypt(aes.Key, false);
            string msg = Convert.ToBase64String(encrypted);            


            encrypted = rsa.Encrypt(aes.IV, false);
            string msg2 = Convert.ToBase64String(encrypted);            

            if (stream == null) return;
            using (StreamWriter sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
            {
                sw.WriteLine(msg);
                sw.WriteLine(msg2);
            }            
        }
        public static string read()
        {
            if (stream == null)
            {
                return "";
            }

            using (StreamReader sr = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
            {
                try
                {
                    string str = sr.ReadLine();

                    byte[] bytes = Convert.FromBase64String(str);

                    string command = Decrypt(bytes, aes.Key, aes.IV);

                    return command;
                }
                catch (Exception e)
                {
                    return "";
                }
            }
        }

        public static void write(string msg)
        {
            if (stream == null) return;
            try
            {
                using (StreamWriter sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                {
                    byte[] encrytped = Encrypt(msg, aes.Key, aes.IV);

                    string command = Convert.ToBase64String(encrytped);


                    sw.WriteLine(command);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }        
        public static string[] CheckMessage(string sData)
        {
            return sData.Split(' ');
        }
    }
}
