using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Sledge.Editor.UI.ObjectProperties.SmartEdit
{
    public partial class TargetDestinationsEdit : Form
    {
        private SmartEditTargetDestination _smartEdit;
        public TargetDestinationsEdit(SmartEditTargetDestination smartEdit)
        {
            _smartEdit = smartEdit;
            InitializeComponent();
            UpdateList();
        }

        public void UpdateList()
        {
            listView1.Items.Clear();
            comboBox1.Items.Clear();

            if (_smartEdit.PropertyValue.Length > 0)
            {
                string[] targets = _smartEdit.PropertyValue.Split(';');
                foreach (string target in targets)
                {
                    listView1.Items.Add(target);
                }
            }

            UpdateAvailableTargets();
            listView1.Update();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            listView1.Items.Add(comboBox1.Text);
            listView1.Update();

            UpdateAvailableTargets();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                listView1.Items.Remove(item);
            }

            listView1.Update();

            UpdateAvailableTargets();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _smartEdit._targetcode = GetValue();
            _smartEdit.OnValueChanged();
            Close();
        }

        public string GetValue()
        {
            string full = "";
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                full += listView1.Items[i].Text;
                if (i < listView1.Items.Count - 1)
                {
                    full += ";";
                }
            }
            return full;
        }

        private IEnumerable<string> GetAvailableTargetNames()
        {
            IEnumerable<string> result = _smartEdit.Document.Map.WorldSpawn.Find(x => x.GetEntityData() != null)
                .Select(x => x.GetEntityData().GetPropertyValue("targetname"))
                .Where(x => !String.IsNullOrWhiteSpace(x))
                .Distinct()
                .Where(x => listView1.FindItemWithText(x) == null)
                .OrderBy(x => x.ToLowerInvariant());

            return result;
        }

        private void UpdateAvailableTargets()
        {
            comboBox1.Items.Clear();
            foreach (string target in GetAvailableTargetNames())
            {
                comboBox1.Items.Add(target);
            }
        }
    }
}
