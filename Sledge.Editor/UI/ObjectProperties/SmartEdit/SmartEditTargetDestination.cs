using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Sledge.DataStructures.GameData;

namespace Sledge.Editor.UI.ObjectProperties.SmartEdit
{
    [SmartEdit(VariableType.TargetDestination)]
    public class SmartEditTargetDestination : SmartEditControl
    {
        private readonly Button _editBtn;
        public string _targetcode;

        public SmartEditTargetDestination()
        {
            _editBtn = new Button();
            _editBtn.Text = "Edit";
            _editBtn.Click += DoEdit;

            Controls.Add(_editBtn);
        }

        private void DoEdit(object sender, EventArgs e)
        {
            TargetDestinationsEdit edit = new TargetDestinationsEdit(this);
            edit.ShowDialog();
        }

        protected override string GetName()
        {
            return OriginalName;
        }

        protected override string GetValue()
        {
            return _targetcode;
        }

        protected override void OnSetProperty()
        {
        }
    }
}