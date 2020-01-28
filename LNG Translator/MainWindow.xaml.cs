using System;
using System.Windows;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace LNG_Translator
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string windowTitle = "LNG Translator";
        private readonly OpenFileDialog OpenFileDialog = new OpenFileDialog();
        private List<LNGRow> lngRows = new List<LNGRow>();

        public MainWindow()
        {
            InitializeComponent();

            OpenFileDialog.Filter = "LNG files (*.LNG)|*.LNG|All files (*.*)|*.*";

            string version = System.Windows.Forms.Application.ProductVersion;
            this.windowTitle += " v." + version.Remove(version.Length - 2);
            MainWindowElement.Title = this.windowTitle;
        }

        private void OpenLNGFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MainWindowElement.Title = this.windowTitle + " - " + OpenFileDialog.FileName;
                this.ReadFile(OpenFileDialog.FileName);
            }
        }

        private void ReadFile(string fileName)
        {
            byte[] knownSignature = new byte[] { 0xA5, 0x5A, 0x5A, 0xA5, 0x01, 0x00, 0x00, 0x01 };
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                byte[] signature = new byte[8];
                fs.Read(signature, 0, signature.Length);
                if (!signature.SequenceEqual(knownSignature))
                {
                    System.Windows.MessageBox.Show("Unknown signature!");
                    return;
                }

                fs.Position = 0x40;
                //byte[] addr = new byte[4] { 0x00, 0x00, 0xff, 0xff };
                byte[] addr = new byte[4] { 0xff, 0xff, 0x00, 0x00 };
                byte[] endaddr = new byte[4] { 0x00, 0x00, 0x00, 0x00 };
                byte[] bChar = new byte[2] { 0xff, 0xff };
                byte[] bEndChar = new byte[2] { 0x00, 0x00 };
                List<byte> bText = new List<byte>();
                int maxTextLength = 512;
                while (!addr.SequenceEqual(endaddr))
                {
                    fs.Read(addr, 0, 2);
                    long nextAddr = fs.Position;
                    fs.Position = 0x40 + BitConverter.ToUInt32(addr, 0) * 2;
                    fs.Read(bChar, 0, bChar.Length);
                    while (maxTextLength > 0)
                    {
                        if (!bChar.SequenceEqual(bEndChar))
                        {
                            bText.Add(bChar[0]); bText.Add(bChar[1]);
                            fs.Read(bChar, 0, bChar.Length);
                            maxTextLength--;
                        }
                        else
                        {
                            break;
                        }
                    }
                    fs.Position = nextAddr;

                    string sText = Encoding.Unicode.GetString(bText.ToArray());
                    bText.Clear();
                    lngRows.Add(new LNGRow(addr, sText));
                }

                stringsView.ItemsSource = lngRows;
                stringsView.Items.Refresh();
            }
        }

        public class LNGRow
        {
            private byte[] offset;
            private string origText;
            private string transText;

            public LNGRow(byte[] offset, string origText)
            {
                this.offset = offset;
                this.origText = origText;
            }

            public byte[] Offset
            {
                get { return this.offset; }
                set { this.offset = value; }
            }

            public string OrigText
            {
                get { return this.origText; }
                set { this.origText = value; }
            }

            public string TransText
            {
                get { return this.transText; }
                set { this.transText = value; }
            }
        }
    }
}
