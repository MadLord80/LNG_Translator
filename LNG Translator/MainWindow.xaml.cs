using System;
using System.Windows;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Windows.Controls;
using System.Net.Http;
using System.Web.Script.Serialization;
using System.Collections;
using System.Windows.Input;

namespace LNG_Translator
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string windowTitle = "LNG Translator";
        private readonly OpenFileDialog OpenFileDialog = new OpenFileDialog();
        private string FileName;
        private int addrLength = 4;
        private List<LNGRow> lngRows = new List<LNGRow>();
        private readonly Dictionary<int, string> encodings = new Dictionary<int, string> {
            { 932, "Shift-JIS" },
            { 1200, "UTF-16 LE" },
            { 1201, "UTF-16 BE" },
            { 65001, "UTF-8" },
            { 1251, "Windows - 1251" },
            { 1252, "Windows - 1252" }
        };
        private int curEnc = 932;

        private Dictionary<string, string> langs;
        private string curFromLang = "ja";
        private string curToLang = "en";

        public MainWindow()
        {
            InitializeComponent();
            
            this.FillLangs();

            foreach (KeyValuePair<int, string> enc in encodings)
            {
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

            foreach (KeyValuePair<string, string> lang in langs)
            {
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem
                {
                    Header = lang.Key,
                    Tag = lang.Value,
                    IsCheckable = true
                };
                mi.Click += langFrom_Select;
                if (lang.Value == curFromLang) { mi.IsChecked = true; }
                langFromMenuItem.Items.Add(mi);
                System.Windows.Controls.MenuItem mi2 = new System.Windows.Controls.MenuItem
                {
                    Header = lang.Key,
                    Tag = lang.Value,
                    IsCheckable = true
                };
                mi2.Click += langTo_Select;
                if (lang.Value == curToLang) { mi2.IsChecked = true; }
                langToMenuItem.Items.Add(mi2);
            }
            selectTransLangsButton.Header = curFromLang + " => " + curToLang;

            OpenFileDialog.Filter = "LNG files (*.LNG)|*.LNG|All files (*.*)|*.*";

            System.Windows.Controls.ContextMenu context = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem exportItem = new System.Windows.Controls.MenuItem() { Header = "Google Translate (or Ctrl+Q)" };
            exportItem.Click += TranslateContextButtonClick;
            context.Items.Add(exportItem);
            // System.Windows.Controls.MenuItem exportItem2 = new System.Windows.Controls.MenuItem() { Header = "Google Translate 2" };
            // exportItem2.Click += TranslateContextButtonClick2;
            // context.Items.Add(exportItem2);
            System.Windows.Controls.MenuItem copyOrigToTranslane = new System.Windows.Controls.MenuItem() { Header = "Copy original to translate" };
            copyOrigToTranslane.Click += CopyContextButtonClick2;
            context.Items.Add(copyOrigToTranslane);
            System.Windows.Controls.MenuItem copyOrigToClipboard = new System.Windows.Controls.MenuItem() { Header = "Copy original to clipboard" };
            copyOrigToClipboard.Click += CopyContextButtonClick;
            context.Items.Add(copyOrigToClipboard);
            stringsView.ContextMenu = context;

            stringsView.KeyDown += DefaultTranslateKeyDown;

            searchTextBox.GotFocus += RemovePlaceholder;
            searchTextBox.LostFocus += AddSearchText;

            string version = System.Windows.Forms.Application.ProductVersion;
            this.windowTitle += " v." + version.Remove(version.Length - 2);
            MainWindowElement.Title = this.windowTitle;
        }

        private void DefaultTranslateKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool modifier = (e.KeyboardDevice.Modifiers == ModifierKeys.Control || e.KeyboardDevice.Modifiers == ModifierKeys.Shift); 
            if (modifier && e.Key == Key.Q)
            {
                TranslateContext();
            }
        }

        private void RemovePlaceholder(object sender, RoutedEventArgs e)
        {
            if (searchTextBox.Text == "Search...")
            {
                searchTextBox.Text = "";
            }
        }
        private void AddSearchText(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                searchTextBox.Text = "Search...";
            }                
        }

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                string searchText = searchTextBox.Text;
                stringsView.ItemsSource = lngRows.Where(row => row.OrigText.Contains(searchText));
                stringsView.Items.Refresh();
                AutoSizeColumns(stringsView.View as GridView);
            }
        }

        private void TranslateContextButtonClick(object sender, RoutedEventArgs e)
        {
            TranslateContext();
        }

        public void TranslateContext()
        {
            LNGRow lrow = (LNGRow)stringsView.SelectedItem;
            if (lrow == null) { return; }
            byte[] trans = Encoding.UTF8.GetBytes(this.GoogleTranslate(lrow.OrigText));
            lrow.TransText = Encoding.GetEncoding(this.curEnc).GetString(Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding(this.curEnc), trans));
            if (lrow.TransText.Length > 1)
            {
                lrow.TransText = char.ToUpper(lrow.TransText[0]) + lrow.TransText.Substring(1);
            }
            stringsView.Items.Refresh();
            stringsView.UpdateLayout();
            stringsView.ScrollIntoView(lrow);
            ListBoxItem lbi = (ListBoxItem)stringsView.ItemContainerGenerator.ContainerFromItem(lrow);
            AutoSizeColumns(stringsView.View as GridView);
            lbi.Focus();
        }

        private void TranslateContextButtonClick2(object sender, RoutedEventArgs e)
        {
            LNGRow lrow = (LNGRow)stringsView.SelectedItem;
            if (lrow == null) { return; }
            byte[] trans = Encoding.UTF8.GetBytes(this.GoogleTranslate2(lrow.OrigText));
            lrow.TransText = Encoding.GetEncoding(this.curEnc).GetString(Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding(this.curEnc), trans));
            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private void CopyContextButtonClick(object sender, RoutedEventArgs e)
        {
            LNGRow lrow = (LNGRow)stringsView.SelectedItem;
            if (lrow == null) { return; }
            System.Windows.Clipboard.SetText(lrow.OrigText);
        }

        private void CopyContextButtonClick2(object sender, RoutedEventArgs e)
        {
            LNGRow lrow = (LNGRow)stringsView.SelectedItem;
            if (lrow == null) { return; }
            lrow.TransText = lrow.OrigText;
            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private void langFrom_Select(object sender, RoutedEventArgs e)
        {
            string lfrom = ((System.Windows.Controls.MenuItem)sender).Tag.ToString();
            curFromLang = lfrom;
            selectTransLangsButton.Header = curFromLang + " => " + curToLang;
        }
        private void langTo_Select(object sender, RoutedEventArgs e)
        {
            string lto = ((System.Windows.Controls.MenuItem)sender).Tag.ToString();
            curToLang = lto;
            selectTransLangsButton.Header = curFromLang + " => " + curToLang;
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
            this.ReadFile();
        }

        private void OpenLNGFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MainWindowElement.Title = this.windowTitle + " - " + OpenFileDialog.FileName;
                MainWindowElement.Title += " [" + Encoding.GetEncoding(this.curEnc).WebName + "]";
                this.FileName = OpenFileDialog.FileName;
                this.ReadFile();
            }
        }

        private void ReadFile()
        {
            MainWindowElement.Title = this.windowTitle + " - " + this.FileName;
            MainWindowElement.Title += " [" + Encoding.GetEncoding(this.curEnc).WebName + "]";

            byte[] knownSignature = new byte[] { 0xA5, 0x5A, 0x5A, 0xA5 };
            this.addrLength = 4;
            using (FileStream fs = new FileStream(this.FileName, FileMode.Open, FileAccess.Read))
            {
                byte[] signature = new byte[4];
                fs.Read(signature, 0, signature.Length);
                if (!signature.SequenceEqual(knownSignature))
                {
                    System.Windows.MessageBox.Show("Unknown signature!");
                    return;
                }

                int bytesPerSymbol = Encoding.GetEncoding(this.curEnc).GetByteCount(new char[] { 'A' });

                byte[] lastStringOffset = new byte[4];
                fs.Position = 0x20;
                fs.Read(lastStringOffset, 0, lastStringOffset.Length);

                fs.Position = 0x40;
                byte[] addr = new byte[4] { 0xff, 0xff, 0x00, 0x00 };
                byte[] endaddr = new byte[4] { 0x00, 0x00, 0x00, 0x00 };
                byte[] bChar = new byte[bytesPerSymbol];
                byte[] bEndChar = new byte[bytesPerSymbol];
                List<byte> bText = new List<byte>();
                lngRows.Clear();

                int addr_id = 1;
                fs.Read(addr, 0, this.addrLength);
                // may be 4 bytes offset
                if (addr[2] != 0 || addr[3] != 0)
                {
                    this.addrLength = 2;
                    addr[2] = 0; addr[3] = 0;
                    fs.Position -= 2;
                }
                while (!addr.SequenceEqual(endaddr))
                {
                    bText.Clear();
                    long nextAddr = fs.Position;
                    fs.Position = 0x40 + BitConverter.ToUInt32(addr, 0) * 2;
                    if (BitConverter.GetBytes(fs.Position).SequenceEqual(lastStringOffset)) { break; }
                    fs.Read(bChar, 0, bChar.Length);
                    while (fs.Position < fs.Length - 1)
                    {
                        if (bChar.SequenceEqual(bEndChar)) { break; }

                        foreach (byte mchar in bChar)
                        {
                            bText.Add(mchar);
                        }
                        fs.Read(bChar, 0, bChar.Length);
                    }

                    LNGRow lrow = new LNGRow(
                        addr_id, addr, Encoding.GetEncoding(this.curEnc).GetString(bText.ToArray()), this.curEnc);
                    lngRows.Add(lrow);
                    addr_id++;

                    fs.Position = nextAddr;
                    fs.Read(addr, 0, this.addrLength);
                    if (fs.Position >= fs.Length - 1)
                    {
                        addr = endaddr;
                    }
                }

                ((GridView)stringsView.View).Columns[1].Header = "Original: (found " + lngRows.Count + " strings)";
                stringsView.ItemsSource = lngRows.Where(row => row.OrigText != "");
                stringsView.Items.Refresh();
                AutoSizeColumns(stringsView.View as GridView);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            FileInfo newFile = new FileInfo(this.FileName);
            string newFileName = newFile.Name.Substring(0, newFile.Name.Length - newFile.Extension.Length) + ".new" + newFile.Extension;
            string newFullFileName = newFile.Directory + "\\" + newFileName;

            //0x00 - 0x3f - copy header
            //0x18(4 bytes) - length of file
            //0x20(4 bytes) - last string(end of strings)
            //
            //end of strings(just copy from last string to EOF):
            //1.RG_VOICE_DATA
            //2. 0x31(606 bytes)
            //+ checksum ??? (2 bytes, if no change - still working)
            
            byte[] origHeader = new byte[64];
            byte[] eofData = new byte[] { };
            byte[] lastOffset = new byte[4];
            using (FileStream ifs = new FileStream(this.FileName, FileMode.Open, FileAccess.Read))
            {
                ifs.Read(origHeader, 0, origHeader.Length);
                lastOffset = origHeader.Skip(32).Take(4).ToArray();
                uint eofDataOffset = BitConverter.ToUInt32(lastOffset, 0);
                ifs.Position = eofDataOffset;
                Array.Resize(ref eofData, Convert.ToInt32(ifs.Length) - (int)eofDataOffset);
                ifs.Read(eofData, 0, eofData.Length);
            }

            bool hasEofOffset = 
                (lastOffset.SequenceEqual(BitConverter.GetBytes(0x40 + BitConverter.ToUInt32(lngRows.Last().Offset, 0) * 2))) 
                ? true : false;

            int bytesPerSymbol = Encoding.GetEncoding(this.curEnc).GetByteCount(new char[] { 'A' });
            byte[] endString = new byte[] { 0x00, 0x00 };
            using (FileStream ofs = new FileStream(newFullFileName, FileMode.Create, FileAccess.ReadWrite))
            {
                ofs.Write(origHeader, 0, origHeader.Length);
                byte[] offsetData = new byte[(lngRows.Count + 2) * this.addrLength];
                ofs.Write(offsetData, 0, offsetData.Length);

                foreach (LNGRow lrow in lngRows)
                {
                    long curAbsPos = ofs.Position;
                    ofs.Position = 0x40 + this.addrLength * (lrow.Id - 1);
                    byte[] curRelPos = BitConverter.GetBytes((curAbsPos - 0x40) / 2);
                    ofs.Write(curRelPos, 0, this.addrLength);                    

                    ofs.Position = curAbsPos;
                    if (hasEofOffset && lrow == lngRows.Last()) { break; }

                    lrow.TransText = lrow.TransText.Replace("\x0D", "");
                    byte[] saveString = (lrow.TransTextBytes.Length > 0) ? lrow.TransTextBytes : lrow.OrigTextBytes;
                    ofs.Write(saveString, 0, saveString.Length);
                    //end of string
                    ofs.Write(endString, 0, (bytesPerSymbol == 1 && saveString.Length % 2 != 0) ? 1 : endString.Length);
                }
                // add 2 zero bytes in the end for HRZ-900
                ofs.Write(endString, 0, 2);
                //0x20(4 bytes) - last string(end of strings)
                long lastAbsPos = ofs.Position;
                ofs.Position = 0x20;
                byte[] lastPos = BitConverter.GetBytes(lastAbsPos);
                ofs.Write(lastPos, 0, 4);
                ofs.Position = lastAbsPos;

                ofs.Write(eofData, 0, eofData.Length);

                //0x18(4 bytes) - length of file
                long lengthAbsPos = ofs.Position;
                ofs.Position = 0x18;
                byte[] fileLength = BitConverter.GetBytes(lengthAbsPos);
                ofs.Write(fileLength, 0, 4);
            }

            System.Windows.MessageBox.Show(newFullFileName + " saved ok");
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox tbox = (System.Windows.Controls.TextBox)sender;
            ContentPresenter cp = (ContentPresenter)tbox.TemplatedParent;
            LNGRow lrow = (LNGRow)cp.Content;
            lrow.TransText = tbox.Text;
            AutoSizeColumns(stringsView.View as GridView);
        }

        private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox txtbox = (System.Windows.Controls.TextBox)sender;
            txtbox.IsReadOnly = false;
        }
        
        private void UpdateStringsView()
        {
            stringsView.ItemsSource = (this.skipEmptyStringsButton.IsChecked)
                ? lngRows.Where(row => row.OrigText != "")
                : lngRows;

            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private string GoogleTranslate(string text)
        {
            // code from https://www.codeproject.com/Tips/5247661/Google-Translate-API-Usage-in-Csharp
            // Set the language from/to in the url (or pass it into this function)
            string url = String.Format 
                ("https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}",
                    curFromLang, curToLang, Uri.EscapeUriString(text));
            HttpClient httpClient = new HttpClient();
            string result = null;
            try
            {
                result = httpClient.GetStringAsync(url).Result;
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.ToString());
                return text;
            }

            // Get all json data
            var jsonData = new JavaScriptSerializer().Deserialize<List<dynamic>>(result);

            // Extract just the first array element (This is the only data we are interested in)
            var translationItems = jsonData[0];
            if (translationItems == null) 
            {
                System.Windows.MessageBox.Show("Can`t translate!");
                return text; 
            }

            // Translation Data
            string translation = "";

            // Loop through the collection extracting the translated objects
            foreach (object item in translationItems)
            {
                // Convert the item array to IEnumerable
                IEnumerable translationLineObject = item as IEnumerable;

                // Convert the IEnumerable translationLineObject to a IEnumerator
                IEnumerator translationLineString = translationLineObject.GetEnumerator();

                // Get first object in IEnumerator
                translationLineString.MoveNext();

                // Save its value (translated text)
                translation += string.Format("{0}", Convert.ToString(translationLineString.Current));
            }

            // Remove first blank character
            //if (translation.Length > 1) { translation = translation.Substring(1); };

            // Return translation
            return translation;
        }

        private string GoogleTranslate2(string text)
        {
            // example from bellic
            string url = String.Format
                ("http://translate.google.ru/translate_a/t?client=x&hl=ru&tab=wT&sl={0}&tl={1}&text={2}",
                    curFromLang, curToLang, Uri.EscapeUriString(text));
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; rv:59.0) Gecko/20100101 Firefox/59.0");

            string result = null;
            try
            {
                result = httpClient.GetStringAsync(url).Result;
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.ToString());
                return text;
            }

            return result;
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

        private void FillLangs()
        {
            this.langs = new Dictionary<string, string> {
                {"Japanese","ja"},
                {"English","en"},
                {"Russian","ru"},
                { "Dutch","nl"},
                {"Ukrainian","uk"}
                //{"", "" },

                //{ "Afrikaans", "af" },
                //{"Albanian","sq"},
                //{"Amharic","am"},
                //{"Arabic","ar"},
                //{"Armenian","hy"},
                //{"Azerbaijani","az"},
                //{"Basque","eu"},
                //{"Belarusian","be"},
                //{"Bengali","bn"},
                //{"Bosnian","bs"},
                //{"Bulgarian","bg"},
                //{"Catalan","ca"},
                //{"Chinese(Traditional)","zh-TW"},
                //{"Corsican","co"},
                //{"Croatian","hr"},
                //{"Czech","cs"},
                //{"Danish","da"},
                //{"Esperanto","eo"},
                //{"Estonian","et"},
                //{"Finnish","fi"},
                //{"French","fr"},
                //{"Frisian","fy"},
                //{"Galician","gl"},
                //{"Georgian","ka"},
                //{"German","de"},
                //{"Greek","el"},
                //{"Gujarati","gu"},
                //{"HaitianCreole","ht"},
                //{"Hausa","ha"},
                //{"Hebrew","he"},
                //{"Hindi","hi"},
                //{"Hungarian","hu"},
                //{"Icelandic","is"},
                //{"Igbo","ig"},
                //{"Indonesian","id"},
                //{"Irish","ga"},
                //{"Italian","it"},
                //{"Javanese","jv"},
                //{"Kannada","kn"},
                //{"Kazakh","kk"},
                //{"Khmer","km"},
                //{"Korean","ko"},
                //{"Kurdish","ku"},
                //{"Kyrgyz","ky"},
                //{"Lao","lo"},
                //{"Latin","la"},
                //{"Latvian","lv"},
                //{"Lithuanian","lt"},
                //{"Luxembourgish","lb"},
                //{"Macedonian","mk"},
                //{"Malagasy","mg"},
                //{"Malay","ms"},
                //{"Malayalam","ml"},
                //{"Maltese","mt"},
                //{"Maori","mi"},
                //{"Marathi","mr"},
                //{"Mongolian","mn"},
                //{"Myanmar(Burmese)","my"},
                //{"Nepali","ne"},
                //{"Norwegian","no"},
                //{"Nyanja(Chichewa)","ny"},
                //{"Pashto","ps"},
                //{"Persian","fa"},
                //{"Polish","pl"},
                //{"Portuguese(Portugal,Brazil)","pt"},
                //{"Punjabi","pa"},
                //{"Romanian","ro"},
                //{"Samoan","sm"},
                //{"ScotsGaelic","gd"},
                //{"Serbian","sr"},
                //{"Sesotho","st"},
                //{"Shona","sn"},
                //{"Sindhi","sd"},
                //{"Sinhala(Sinhalese)","si"},
                //{"Slovak","sk"},
                //{"Slovenian","sl"},
                //{"Somali","so"},
                //{"Spanish","es"},
                //{"Sundanese","su"},
                //{"Swahili","sw"},
                //{"Swedish","sv"},
                //{"Tagalog(Filipino)","tl"},
                //{"Tajik","tg"},
                //{"Tamil","ta"},
                //{"Telugu","te"},
                //{"Thai","th"},
                //{"Turkish","tr"},
                //{"Urdu","ur"},
                //{"Uzbek","uz"},
                //{"Vietnamese","vi"},
                //{"Welsh","cy"},
                //{"Xhosa","xh"},
                //{"Yiddish","yi"},
                //{"Yoruba","yo"},
                //{"Zulu","zu"}
            };
        }
        
        private class LNGRow
        {
            private int id;
            private byte[] offset;
            private string origText = "";
            private string transText = "";
            private int encoding;

            public LNGRow(int id, byte[] offset, string origText, int enc)
            {
                this.id = id;
                this.offset = new byte[offset.Length];
                offset.CopyTo(this.offset, 0);

                this.origText = origText;
                this.encoding = enc;
            }

            public int Id
            {
                get { return this.id; }
            }

            public byte[] Offset
            {
                get { return this.offset; }
            }

            public string OrigText
            {
                get { return this.origText; }
            }

            public byte[] OrigTextBytes
            {
                get { return Encoding.GetEncoding(this.encoding).GetBytes(this.origText); }

            }

            public byte[] TransTextBytes
            {
                get { return Encoding.GetEncoding(this.encoding).GetBytes(this.transText); }
            }

            public string TransText
            {
                get { return this.transText; }
                set { this.transText = value; }
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            Window about = new About();
            about.ShowDialog();
        }

        private void clearSearch_Click(object sender, RoutedEventArgs e)
        {
            searchTextBox.Text = "";
            stringsView.ItemsSource = lngRows;
            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }
    }
}
