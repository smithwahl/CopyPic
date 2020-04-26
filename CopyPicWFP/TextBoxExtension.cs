using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CopyPicWFP
{
    public static class TextBoxExtension
    {
        public static void Content(this System.Windows.Controls.TextBox txtBox, string text)
        {
            if (txtBox.Name == "txtSource") Properties.Settings.Default.Source = text;
            if (txtBox.Name == "txtDestination") Properties.Settings.Default.Destination = text;
            Properties.Settings.Default.Save();
            txtBox.Text = text;
        }
    }
}
