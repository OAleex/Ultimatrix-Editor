using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Ultimatrix_Editor
{
    public partial class Form1 : Form
    {
        private readonly Dictionary<byte, string> Replacements = new Dictionary<byte, string>
        {
            { 0x01, "[BUTTON_0x01]" }, { 0x02, "[BUTTON_0x02]" },
            { 0x03, "[BUTTON_0x03]" }, { 0x04, "[BUTTON_0x04]" },
            { 0x05, "[BUTTON_0x05]" }, { 0x06, "[BUTTON_0x06]" },
            { 0x07, "[BUTTON_0x07]" }, { 0x08, "[BUTTON_0x08]" },
            { 0x09, "[BUTTON_0x09]" }, { 0x0A, "<lw>" },
            { 0x0B, "[BUTTON_0x0B]" }, { 0x0C, "[BUTTON_0x0C]" },
            { 0x0D, "[BUTTON_0x0D]" }, { 0x0E, "[BUTTON_0x0E]" },
            { 0x0F, "[BUTTON_0x0F]" }, { 0x10, "[BUTTON_0x10]" },
            { 0x11, "[BUTTON_0x11]" }, { 0x12, "[BUTTON_0x12]" },
            { 0x13, "[BUTTON_0x13]" }, { 0x14, "[BUTTON_0x14]" },
            { 0x15, "[BUTTON_0x15]" }, { 0x16, "[BUTTON_0x16]" },
            { 0x17, "[BUTTON_0x17]" }, { 0x18, "[BUTTON_0x18]" },
            { 0x19, "[BUTTON_0x19]" }, { 0x1A, "[BUTTON_0x1A]" },
            { 0x1B, "[BUTTON_0x1B]" }, { 0x1C, "[BUTTON_0x1C]" },
            { 0x1D, "[BUTTON_0x1D]" }, { 0x1E, "[BUTTON_0x1E]" },
            { 0x1F, "[BUTTON_0x1F]" },
        };

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                Title = "Select Text Files",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                foreach (string filePath in openFileDialog.FileNames)
                {
                    ProcessTextFile(filePath);
                }

                MessageBox.Show("Texts extracted successfully!", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ProcessTextFile(string filePath)
        {
            string outputFilePath = Path.ChangeExtension(filePath, ".xml");

            byte[] fileBytes = File.ReadAllBytes(filePath);
            List<string> extractedTexts = new List<string>();

            int offset = 4; // Skip the first 4 bytes
            while (offset < fileBytes.Length)
            {
                offset += 8; // Move forward 8 bytes
                if (offset >= fileBytes.Length) break;

                int textStart = offset;
                int textEnd = Array.IndexOf(fileBytes, (byte)0x00, textStart);

                if (textEnd == -1) break;

                byte[] textBytes = fileBytes[textStart..textEnd];
                string text = Encoding.GetEncoding("ISO-8859-1").GetString(textBytes);

                foreach (var replacement in Replacements)
                {
                    text = text.Replace(((char)replacement.Key).ToString(), replacement.Value);
                }

                extractedTexts.Add(text);
                offset = textEnd + 1;
            }

            File.WriteAllLines(outputFilePath, extractedTexts, Encoding.GetEncoding("ISO-8859-1"));
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml",
                Title = "Select XML Files",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                foreach (string xmlFilePath in openFileDialog.FileNames)
                {
                    ProcessXmlFile(xmlFilePath);
                }

                MessageBox.Show("Texts processed successfully!", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ProcessXmlFile(string xmlFilePath)
        {
            string txtFilePath = FindTextFile(xmlFilePath);

            if (string.IsNullOrEmpty(txtFilePath))
            {
                MessageBox.Show($"Matching text file not found for {Path.GetFileName(xmlFilePath)}.",
                                "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string[] editedTexts = File.ReadAllLines(xmlFilePath, Encoding.GetEncoding("ISO-8859-1"));
                byte[] binaryData = File.ReadAllBytes(txtFilePath);

                Endianness endianness = DetectEndianness(binaryData);

                var modifiedData = new List<byte>(binaryData.Take(8)); // Preserves the first 8 bytes
                var tokenDictionary = Replacements.ToDictionary(kvp => kvp.Value, kvp => kvp.Key); // Invert the dictionary to table insert

                int offset = 8; // Ignore the first 8 bytes
                int textIndex = 0;

                while (offset < binaryData.Length)
                {
                    int textStart = offset + 4;
                    int textEnd = Array.IndexOf(binaryData, (byte)0x00, textStart);
                    if (textEnd == -1) break;

                    int extraBytesStart = textEnd + 1;
                    int extraBytesEnd = extraBytesStart + 4;
                    byte[] extraBytes = binaryData.Skip(extraBytesStart).Take(4).ToArray();

                    string originalText = Encoding.GetEncoding("ISO-8859-1")
                        .GetString(binaryData, textStart, textEnd - textStart);

                    string newText = textIndex < editedTexts.Length ? editedTexts[textIndex++] : originalText;

                    var encodedText = new List<byte>();
                    var stringBuilder = new StringBuilder(newText);
                    foreach (var token in tokenDictionary)
                    {
                        stringBuilder.Replace(token.Key, ((char)token.Value).ToString());
                    }

                    foreach (char c in stringBuilder.ToString())
                    {
                        encodedText.Add((byte)c);
                    }
                    encodedText.Add(0x00);

                    int newPointer = encodedText.Count;
                    byte[] pointerBytes = endianness == Endianness.Little
                        ? BitConverter.GetBytes(newPointer)
                        : BitConverter.GetBytes(newPointer).Reverse().ToArray();

                    modifiedData.AddRange(pointerBytes);
                    modifiedData.AddRange(encodedText);
                    modifiedData.AddRange(extraBytes);

                    offset = extraBytesEnd;
                }

                string outputFilePath = txtFilePath;
                File.WriteAllBytes(outputFilePath, modifiedData.ToArray());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing {Path.GetFileName(xmlFilePath)}: {ex.Message}", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Endianness DetectEndianness(byte[] fileBytes)
        {
            if (fileBytes.Length < 4) return Endianness.Little; // Default pointer and others magic pointers

            if (fileBytes[0] == 0xBE && fileBytes[1] == 0xBA && fileBytes[2] == 0xAD && fileBytes[3] == 0x1B)
            {
                return Endianness.Little;
            }
            if (fileBytes[0] == 0x1B && fileBytes[1] == 0xAD && fileBytes[2] == 0xBA && fileBytes[3] == 0xBE)
            {
                return Endianness.Big;
            }

            return Endianness.Little; // Default pointer
        }

        private string FindTextFile(string basePath)
        {
            string directory = Path.GetDirectoryName(basePath);
            string baseName = Path.GetFileNameWithoutExtension(basePath);

            if (!Directory.Exists(directory)) return null;

            string exactMatch = Path.Combine(directory, $"{baseName}.txt");
            if (File.Exists(exactMatch))
            {
                return exactMatch;
            }

            string[] matchingFiles = Directory.GetFiles(directory, $"{baseName}*.txt");
            return matchingFiles.FirstOrDefault();
        }

        private enum Endianness
        {
            Little,
            Big
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/OAleex") { UseShellExecute = true });
        }
    }
}
