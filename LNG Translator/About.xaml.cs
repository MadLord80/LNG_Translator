using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LNG_Translator
{
    /// <summary>
    /// Логика взаимодействия для About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();

            string version = System.Windows.Forms.Application.ProductVersion;
            this.AboutText.Text = "LNG Translator v." + version.Remove(version.Length - 2) + "\n"
                + "https://github.com/MadLord80/LNG_Translator" + "\n"
                + "Donate: https://www.paypal.me/madlord80";
        }
    }
}
