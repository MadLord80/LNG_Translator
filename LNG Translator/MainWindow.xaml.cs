using System;
using System.Windows;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Windows.Controls;

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
        private readonly Dictionary<int, string> encodings = new Dictionary<int, string>
        { { 1200, "1200: UTF-16 LE" }, { 932, "932: Shift-JIS" }, {0,"" } };
        private int curEnc = 1200;

        public MainWindow()
        {
            InitializeComponent();

            foreach (EncodingInfo enc in Encoding.GetEncodings())
            {
                if (enc.CodePage != 1200 && enc.CodePage != 932)
                {
                    encodings.Add(enc.CodePage, enc.CodePage + ": " + enc.DisplayName);
                }
            }
            foreach (KeyValuePair<int, string> enc in encodings)
            {
                if (enc.Key == 0)
                {
                    Separator ms = new Separator();
                    resEncMenuItem.Items.Add(ms);
                    continue;
                }
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem
                {
                    Header = enc.Value,
                    Tag = enc.Key,
                    IsCheckable = true
                };
                mi.Click += resEncMenuItem_Select;
                if (enc.Key == this.curEnc) { mi.IsChecked = true; }
                resEncMenuItem.Items.Add(mi);
            }

            OpenFileDialog.Filter = "LNG files (*.LNG)|*.LNG|All files (*.*)|*.*";

            string version = System.Windows.Forms.Application.ProductVersion;
            this.windowTitle += " v." + version.Remove(version.Length - 2);
            MainWindowElement.Title = this.windowTitle;
        }

        private void resEncMenuItem_Select(object sender, RoutedEventArgs e)
        {
            int enc = Convert.ToInt32(((System.Windows.Controls.MenuItem)sender).Tag.ToString());
            foreach (var item in resEncMenuItem.Items)
            {
                if (item.GetType() == typeof(Separator)) { continue; }
                ((System.Windows.Controls.MenuItem)item).IsChecked =
                    (((System.Windows.Controls.MenuItem)item).Tag == ((System.Windows.Controls.MenuItem)sender).Tag)
                    ? true : false;
            }
            this.curEnc = enc;
            this.UpdateStringsEncoding();
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
                byte[] addr = new byte[4] { 0xff, 0xff, 0x00, 0x00 };
                byte[] endaddr = new byte[4] { 0x00, 0x00, 0x00, 0x00 };
                byte[] bChar = new byte[2] { 0xff, 0xff };
                byte[] bEndChar = new byte[2] { 0x00, 0x00 };
                List<byte> bText = new List<byte>();
                lngRows.Clear();

                int addrLength = 4;
                fs.Read(addr, 0, addrLength);
                // may be 4 bytes offset
                if (addr[2] != 0 || addr[3] != 0)
                {
                    addrLength = 2;
                    addr[2] = 0; addr[3] = 0;
                    fs.Position -= 2;
                }
                while (!addr.SequenceEqual(endaddr))
                {
                    bText.Clear();
                    long nextAddr = fs.Position;
                    fs.Position = 0x40 + BitConverter.ToUInt32(addr, 0) * 2;
                    fs.Read(bChar, 0, bChar.Length);
                    int maxTextLength = 512;
                    while (maxTextLength > 0 && fs.Position <= fs.Length - 1)
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

                    string sText = Encoding.GetEncoding(this.curEnc).GetString(bText.ToArray());
                    LNGRow lrow = new LNGRow(addr, sText);
                    lrow.Encoding = curEnc;
                    lrow.TransText = sText;
                    lngRows.Add(lrow);

                    if (fs.Position >= fs.Length - 1)
                    {
                        addr = endaddr;
                    }
                    fs.Position = nextAddr;
                    fs.Read(addr, 0, addrLength);
                }

                ((GridView)stringsView.View).Columns[0].Header = "Original: (found " + lngRows.Count + " strings)";
                stringsView.ItemsSource = lngRows.Where(row => row.OrigText != "");
                stringsView.Items.Refresh();
                AutoSizeColumns(stringsView.View as GridView);
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox tbox = (System.Windows.Controls.TextBox)sender;
            ContentPresenter cp = (ContentPresenter)tbox.TemplatedParent;
            LNGRow lrow = (LNGRow)cp.Content;
            lrow.TransText = tbox.Text;
        }

        private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox txtbox = (System.Windows.Controls.TextBox)sender;
            txtbox.IsReadOnly = false;
        }

        private void UpdateStringsEncoding()
        {
            lngRows.ForEach((lrow) => {
                lrow.OrigText = Encoding.GetEncoding(this.curEnc).GetString(Encoding.GetEncoding(lrow.Encoding).GetBytes(lrow.OrigText));
                lrow.TransText = Encoding.GetEncoding(this.curEnc).GetString(Encoding.GetEncoding(lrow.Encoding).GetBytes(lrow.TransText));
                lrow.Encoding = this.curEnc;
            });
            stringsView.ItemsSource = (this.skipEmptyStringsButton.IsChecked)
                ? lngRows.Where(row => row.OrigText != "")
                : lngRows;
            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private void UpdateStringsView()
        {
            stringsView.ItemsSource = (this.skipEmptyStringsButton.IsChecked)
                ? lngRows.Where(row => row.OrigText != "")
                : lngRows;

            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
               
        private void SkipEmptyStringsButton_Click(object sender, RoutedEventArgs e)
        {
            this.UpdateStringsView();
        }

        private void AutoSizeColumns(GridView gv)
        {
            if (gv != null)
            {
                foreach (var c in gv.Columns)
                {
                    // Code below was found in GridViewColumnHeader.OnGripperDoubleClicked() event handler (using Reflector)
                    // i.e. it is the almost same code that is executed when the gripper is double clicked
                    if (double.IsNaN(c.Width))
                    {
                        c.Width = c.ActualWidth;
                    }
                    else
                    {
                        continue;
                    }
                    c.Width = double.NaN;
                }
            }
        }
        
        private class LNGRow
        {
            private byte[] offset;
            private string origText;
            private string transText;
            private int encoding;

            public LNGRow(byte[] offset, string origText)
            {
                this.offset = offset;
                this.origText = origText;
            }

            public int Encoding
            {
                get { return this.encoding; }
                set { this.encoding = value; }
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
