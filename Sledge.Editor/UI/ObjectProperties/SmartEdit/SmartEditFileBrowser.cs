using System;
using System.Linq;
using System.Windows.Forms;
using Sledge.DataStructures.GameData;
using Sledge.Editor.UI.FileSystem;
using Sledge.FileSystem;

namespace Sledge.Editor.UI.ObjectProperties.SmartEdit
{
    [SmartEdit(VariableType.Studio)]
    [SmartEdit(VariableType.Sound)]
    internal class SmartEditFileBrowser : SmartEditControl
    {
        private readonly TextBox _textBox;
        public SmartEditFileBrowser()
        {
            _textBox = new TextBox { Width = 180 };
            _textBox.TextChanged += (sender, e) => OnValueChanged();
            Controls.Add(_textBox);

            var btn = new Button { Text = "Browse...", Margin = new Padding(1), Height = 24 };
            btn.Click += OpenModelBrowser;
            Controls.Add(btn);
        }

        private FileSystemBrowserDialog CreateDialog()
        {
            var fs = new FileSystemBrowserDialog(Document.Environment.Root);
            switch (Property.VariableType)
            {
                case VariableType.Studio:
                    fs.Filter = "*.bam,*.egg";
                    fs.FilterText = "Models (*.bam, *.egg)";
                    break;
                case VariableType.Sprite:
                    fs.Filter = "*.bam,*.egg";
                    fs.FilterText = "Sprites (*.bam, *.egg)";
                    break;
                case VariableType.Sound:
                    fs.Filter = "*.wav,*.mp3,*.ogg";
                    fs.FilterText = "Audio (*.wav, *.mp3, *.ogg)";
                    break;
            }
            return fs;
        }

        private void OpenModelBrowser(object sender, EventArgs e)
        {
            var rt = Document.Environment.Root;
            using (var fb = CreateDialog())
            {
                if (fb.ShowDialog() == DialogResult.OK && fb.SelectedFiles.Any())
                {
                    var f = fb.SelectedFiles.First();
                    _textBox.Text = GetPath(f);
                }
            }
        }

        private string GetPath(IFile file)
        {
            var path = "";
            while (file != null && !(file is RootFile))
            {
                path = "/" + file.Name + path;
                file = file.Parent;
            }
            return path.TrimStart('/');
        }

        protected override string GetName()
        {
            return OriginalName;
        }

        protected override string GetValue()
        {
            return _textBox.Text;
        }

        protected override void OnSetProperty()
        {
            _textBox.Text = PropertyValue;
        }
    }
}